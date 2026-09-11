using System.Collections.Generic;
using System.Linq;
using System.Text;
using F;
using Xunit;

namespace Rolex.Tests
{
    /// <summary>
    /// Covers the hash bug in <c>_KeySet&lt;T&gt;.Add</c>.
    ///
    /// The set XORed the item's hash on every call, including calls where the item was
    /// already present and the inner HashSet rejected it. XOR is self-inverse, so adding
    /// the same member twice cancelled its contribution and the set's hash came to depend
    /// on insertion multiplicity rather than on membership alone.
    ///
    /// That matters because <c>_Determinize</c> keys its subset table on <c>_KeySet</c>,
    /// and <c>_KeySet.Equals</c> opens with a hash comparison. Two sets with identical
    /// members but different arrival counts hash differently, so Equals refuses them
    /// before SetEquals ever runs, the lookup misses, and a second DFA state is allocated
    /// for a subset that already had one. Duplicate arrivals are routine: the inner loop
    /// reaches the same epsilon closure through several transitions of the same state set.
    ///
    /// _KeySet is private, so this asserts the consequence through the public API instead:
    /// determinization must produce exactly one state per NFA subset. That is the defining
    /// property of the subset construction, so it is the right thing to pin regardless of
    /// how the set is implemented.
    /// </summary>
    public class SubsetIdentityTests
    {
        public static IEnumerable<object[]> AllRegexes()
        {
            foreach (var kv in GoldenFile.Cases())
                yield return new object[] { kv.Key };
        }

        private static string Regex(string name)
        {
            return GoldenFile.Cases().First(kv => kv.Key == name).Value;
        }

        /// <summary>
        /// The subset construction maps each reachable set of NFA states to one DFA state.
        /// FromStates records the set a DFA state was built from, so two states carrying
        /// the same set are the duplicate this bug produced.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllRegexes))]
        public void Determinization_produces_one_state_per_subset(string name)
        {
            var dfa = FA.Parse(Regex(name), 0, true).ToDfa();

            var ids = new Dictionary<FA, int>();
            var seen = new Dictionary<string, int>();
            var states = dfa.FillClosure();

            for (var i = 0; i < states.Count; ++i)
            {
                var from = states[i].FromStates;
                Assert.True(from != null, name + ": a determinized state has no FromStates.");

                var key = SubsetKey(from, ids);
                int prior;
                if (seen.TryGetValue(key, out prior))
                {
                    Assert.Fail(string.Format(
                        "{0}: states {1} and {2} of {3} were both built from the same subset of " +
                        "{4} NFA states. Determinization allocated a duplicate.",
                        name, prior, i, states.Count, from.Length));
                }
                seen.Add(key, i);
            }
        }

        /// <summary>
        /// The duplicates were reachable, not stranded: they had incoming transitions, which
        /// is why they inflated the table rather than being trimmed away. Stated separately
        /// so a failure says which of the two things went wrong.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllRegexes))]
        public void Determinized_states_are_never_more_than_the_subsets_reached(string name)
        {
            var dfa = FA.Parse(Regex(name), 0, true).ToDfa();

            var ids = new Dictionary<FA, int>();
            var subsets = new HashSet<string>();
            var states = dfa.FillClosure();
            foreach (var state in states)
                subsets.Add(SubsetKey(state.FromStates, ids));

            Assert.Equal(subsets.Count, states.Count);
        }

        /// <summary>
        /// Removing the duplicates must not change the language. Minimization already
        /// merged them, so the minimized machine is the reference that did not move -
        /// every string either machine accepts, the other must accept too.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllRegexes))]
        public void Determinized_and_minimized_machines_agree_on_every_sampled_string(string name)
        {
            var regex = Regex(name);
            var dfa = FA.Parse(regex, 0, true).ToDfa();
            var min = FA.Parse(regex, 0, true).ToMinimized();

            foreach (var sample in Samples(min))
            {
                Assert.True(Accepts(dfa, sample) == Accepts(min, sample),
                    name + ": the determinized and minimized machines disagree on " +
                    Describe(sample) + ".");
            }
        }

        /// <summary>
        /// Walks the machine to collect strings worth trying: for every state reached, the
        /// prefix that got there, plus that prefix extended by the low end of each outgoing
        /// range and by one codepoint just outside it. The near-misses are the point - they
        /// probe the boundaries where a wrongly split state would show up.
        /// </summary>
        private static IEnumerable<string> Samples(FA machine)
        {
            var samples = new List<string>();
            var prefixes = new Dictionary<FA, string> { { machine, "" } };
            var pending = new Queue<FA>();
            pending.Enqueue(machine);

            while (pending.Count > 0 && samples.Count < 400)
            {
                var state = pending.Dequeue();
                var prefix = prefixes[state];
                samples.Add(prefix);

                foreach (var t in state.Transitions)
                {
                    if (t.Min < 0 || t.Min > 0xffff) continue;
                    var ch = (char)t.Min;
                    samples.Add(prefix + ch);
                    if (t.Min > 0) samples.Add(prefix + (char)(t.Min - 1));
                    if (t.Max < 0xffff) samples.Add(prefix + (char)(t.Max + 1));

                    if (!prefixes.ContainsKey(t.To))
                    {
                        prefixes.Add(t.To, prefix + ch);
                        pending.Enqueue(t.To);
                    }
                }
            }
            return samples;
        }

        /// <summary>
        /// A stable, exact key for a set of NFA states. FA does not override Equals, so
        /// reference identity is the right notion; each state gets an index the first time
        /// it is seen and the sorted indices name the set.
        /// </summary>
        private static string SubsetKey(FA[] from, Dictionary<FA, int> ids)
        {
            var indices = new int[from.Length];
            for (var i = 0; i < from.Length; ++i)
            {
                int id;
                if (!ids.TryGetValue(from[i], out id))
                {
                    id = ids.Count;
                    ids.Add(from[i], id);
                }
                indices[i] = id;
            }
            System.Array.Sort(indices);

            var sb = new StringBuilder();
            for (var i = 0; i < indices.Length; ++i)
            {
                if (i != 0) sb.Append(',');
                sb.Append(indices[i]);
            }
            return sb.ToString();
        }

        private static string Describe(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var ch in s)
            {
                if (ch >= ' ' && ch < (char)0x7f) sb.Append(ch);
                else sb.Append("\\u").Append(((int)ch).ToString("x4"));
            }
            return sb.Append('"').ToString();
        }

        /// <summary>Walks a DFA over the input. Returns false if it falls off the machine.</summary>
        private static bool Accepts(FA dfa, string input)
        {
            var current = dfa;
            foreach (var ch in input)
            {
                FA next = null;
                foreach (var t in current.Transitions)
                {
                    if (t.Min <= ch && ch <= t.Max) { next = t.To; break; }
                }
                if (next == null) return false;
                current = next;
            }
            return current.IsAccepting;
        }
    }
}
