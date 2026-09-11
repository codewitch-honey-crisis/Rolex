using System.Collections.Generic;
using System.Linq;
using F;
using Xunit;

namespace Rolex.Tests
{
    /// <summary>
    /// Covers the two rewrites that carried a correctness obligation, by checking each
    /// against the behaviour it replaced rather than against a recorded value.
    /// </summary>
    public class EngineInvariantTests
    {
        /// <summary>The small hand-written regexes. Cheap enough for the O(n^2) oracle.</summary>
        public static IEnumerable<object[]> SmallRegexes()
        {
            foreach (var kv in GoldenFile.Cases().Take(GoldenFile.SmallCaseCount))
                yield return new object[] { kv.Key };
        }

        /// <summary>Every case, including the six real R1C1 rules that caused the blow-up.</summary>
        public static IEnumerable<object[]> AllRegexes()
        {
            foreach (var kv in GoldenFile.Cases())
                yield return new object[] { kv.Key };
        }

        private static string Regex(string name)
        {
            return GoldenFile.Cases().First(kv => kv.Key == name).Value;
        }

        // ---- FillEpsilonClosure -------------------------------------------------
        // Rewritten to use a HashSet for the seen check (and to drop a duplicated
        // `result.Contains(this)` line). It must still return each state exactly once,
        // in the same depth-first order, including the state it was called on.

        [Theory]
        [MemberData(nameof(AllRegexes))]
        public void Epsilon_closure_has_no_duplicates_and_contains_self(string name)
        {
            var nfa = FA.Parse(Regex(name), 0, true);
            foreach (var state in nfa.FillClosure())
            {
                var closure = state.FillEpsilonClosure();
                Assert.True(closure.Contains(state), name + ": closure omitted the state it was called on.");
                Assert.True(closure.Count == closure.Distinct().Count(),
                    name + ": closure returned the same state more than once.");
                Assert.Same(state, closure[0]);
            }
        }

        [Theory]
        [MemberData(nameof(AllRegexes))]
        public void Epsilon_closure_agrees_with_the_static_overload(string name)
        {
            var nfa = FA.Parse(Regex(name), 0, true);
            foreach (var state in nfa.FillClosure())
            {
                var instance = state.FillEpsilonClosure();
                var viaStatic = FA.FillEpsilonClosure(new[] { state }, null);
                Assert.True(instance.SequenceEqual(viaStatic),
                    name + ": the instance and static epsilon closures disagree.");
            }
        }

        /// <summary>
        /// Pins the pre-existing contract for a pre-populated result: if the list already
        /// contains the starting state, the call is a no-op and descendants are NOT
        /// explored. That is what the original did - it opened with
        /// `if (result.Contains(this)) return result;` before touching any transition -
        /// and every caller in the engine relies on it, since the recursion re-enters
        /// through this same check.
        ///
        /// Worth stating plainly because it looks like a bug: seeding a list with only the
        /// start state gives back just that state. Making it traverse instead would be a
        /// behaviour change, not a fix, and this whole change set is meant to leave the
        /// generated tables untouched. Verified to hold on the pre-fix engine too.
        /// </summary>
        [Fact]
        public void Epsilon_closure_seeded_with_only_the_start_state_is_a_no_op()
        {
            // compact:false keeps the epsilon transitions the compact parse would collapse.
            var nfa = FA.Parse("(a(b|c)?d)*", 0, false);
            var full = nfa.FillEpsilonClosure();
            Assert.True(full.Count > 1, "test needs a state with epsilon descendants");

            var seeded = new List<FA> { nfa };
            nfa.FillEpsilonClosure(seeded);

            Assert.Single(seeded);
            Assert.Same(nfa, seeded[0]);
        }

        [Fact]
        public void Epsilon_closure_appends_to_a_pre_filled_list_without_re_adding()
        {
            var nfa = FA.Parse("(a|b)*abb", 0, true);
            var first = nfa.FillEpsilonClosure();
            var seeded = new List<FA>(first);

            nfa.FillEpsilonClosure(seeded);

            Assert.Equal(first.Count, seeded.Count);
            Assert.Equal(first, seeded);
        }

        // ---- dead-state trimming ------------------------------------------------
        // The per-transition `trns.To.FillAcceptingStates()` scan was replaced with a
        // single reverse-reachability pass.

        /// <summary>
        /// Checks the trim against FillAcceptingStates, which the fix left untouched and
        /// which therefore still describes what the old trim did. That method walks the
        /// whole reachable graph per call - the very cost the fix removed - so this runs
        /// only on the small regexes. <see cref="Trim_is_complete_on_every_case"/> covers
        /// the large ones in linear time.
        /// </summary>
        [Theory]
        [MemberData(nameof(SmallRegexes))]
        public void Trim_agrees_with_the_original_oracle_on_small_machines(string name)
        {
            foreach (var machine in new[] { FA.Parse(Regex(name), 0, true).ToDfa(),
                                            FA.Parse(Regex(name), 0, true).ToMinimized() })
            {
                foreach (var state in machine.FillClosure())
                {
                    foreach (var t in state.Transitions)
                    {
                        Assert.True(t.To.FillAcceptingStates().Count != 0,
                            name + ": a transition survived the trim even though its target " +
                            "cannot reach an accepting state.");
                    }
                }
            }
        }

        /// <summary>
        /// Same postcondition, computed in linear time so it can run on the real R1C1
        /// rules. The reachability here is written independently of the engine's own.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllRegexes))]
        public void Trim_is_complete_on_every_case(string name)
        {
            foreach (var machine in new[] { FA.Parse(Regex(name), 0, true).ToDfa(),
                                            FA.Parse(Regex(name), 0, true).ToMinimized() })
            {
                var closure = machine.FillClosure();
                var live = LiveStates(closure);

                foreach (var state in closure)
                {
                    Assert.True(live.Contains(state),
                        name + ": a state that cannot reach an accepting state survived the trim.");
                    foreach (var t in state.Transitions)
                    {
                        Assert.True(live.Contains(t.To),
                            name + ": a transition points at a state that cannot reach an accepting state.");
                    }
                }
            }
        }

        /// <summary>States from which an accepting state is reachable, counting the state itself.</summary>
        private static HashSet<FA> LiveStates(IList<FA> closure)
        {
            var incoming = new Dictionary<FA, List<FA>>();
            foreach (var s in closure) incoming[s] = new List<FA>();
            foreach (var s in closure)
                foreach (var t in s.Transitions)
                    if (incoming.ContainsKey(t.To)) incoming[t.To].Add(s);

            var live = new HashSet<FA>();
            var pending = new Queue<FA>();
            foreach (var s in closure)
                if (s.IsAccepting && live.Add(s)) pending.Enqueue(s);

            while (pending.Count > 0)
                foreach (var pre in incoming[pending.Dequeue()])
                    if (live.Add(pre)) pending.Enqueue(pre);

            return live;
        }

        [Fact]
        public void Trimming_does_not_remove_transitions_that_are_still_needed()
        {
            // "(ab|ac)d" only accepts via a longer path, so a trim that was too eager
            // would silently delete transitions the language still depends on.
            var dfa = FA.Parse("(ab|ac)d", 0, true).ToDfa();

            Assert.True(Accepts(dfa, "abd"));
            Assert.True(Accepts(dfa, "acd"));
            Assert.False(Accepts(dfa, "ab"));
            Assert.False(Accepts(dfa, "add"));
        }

        // ---- language sampling --------------------------------------------------

        [Theory]
        [InlineData("abc", "abc", true)]
        [InlineData("abc", "abd", false)]
        [InlineData("a|b|c", "b", true)]
        [InlineData("a|b|c", "d", false)]
        [InlineData("(ab)*c", "c", true)]
        [InlineData("(ab)*c", "ababc", true)]
        [InlineData("(ab)*c", "aba", false)]
        [InlineData("[0-9]+", "40917", true)]
        [InlineData("[0-9]+", "", false)]
        [InlineData("(a|b)*abb", "aababb", true)]
        [InlineData("(a|b)*abb", "aabab", false)]
        public void Determinized_and_minimized_machines_accept_the_same_language(
            string regex, string input, bool expected)
        {
            Assert.Equal(expected, Accepts(FA.Parse(regex, 0, true).ToDfa(), input));
            Assert.Equal(expected, Accepts(FA.Parse(regex, 0, true).ToMinimized(), input));
        }

        [Theory]
        [InlineData("R1C1", true)]
        [InlineData("R[-1]C[2]", true)]
        [InlineData("RC", true)]
        [InlineData("R1048576C16384", true)]
        [InlineData("R1048577C1", false)]
        [InlineData("Q1C1", false)]
        public void R1C1_cell_rule_accepts_the_expected_references(string input, bool expected)
        {
            var dfa = FA.Parse(Regex("A1_CELL"), 0, true).ToMinimized();
            Assert.Equal(expected, Accepts(dfa, input));
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
