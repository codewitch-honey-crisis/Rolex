using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using F;
using Xunit;

namespace Rolex.Tests
{
    /// <summary>
    /// The load-bearing test for the determinization fix.
    ///
    /// TestData/fa-golden.tsv holds packed state tables captured from the engine as
    /// it was BEFORE the fix. The fix was purely about time and memory, so every one
    /// of these tables must still come out identical, element for element.
    ///
    /// A failure here means the DFA changed, which is a correctness regression -
    /// strictly worse than the memory problem the fix was for.
    ///
    /// The recording carries two later changes, both because they change what a correct
    /// table looks like: the unicode escape fix, without which the escape-bearing rules
    /// mean the wrong thing, and the _KeySet.Add hash fix, without which determinization
    /// emits duplicate states. Both are applied to the pre-determinization engine when the
    /// file is recorded, and both produce identical tables on either side of the
    /// determinization rewrite, so these tables remain independent evidence for it - see
    /// Rolex.Tests/README.md.
    /// </summary>
    public class StateTableRegressionTests
    {
        public static IEnumerable<object[]> Goldens()
        {
            foreach (var g in GoldenFile.Load())
                yield return new object[] { g.Name, g.Stage };
        }

        [Theory]
        [MemberData(nameof(Goldens))]
        public void Table_matches_pre_fix_golden(string name, string stage)
        {
            var golden = GoldenFile.Load().Single(g => g.Name == name && g.Stage == stage);

            var fa = FA.Parse(golden.Regex, 0, true);
            switch (stage)
            {
                case "nfa": break;
                case "dfa": fa = fa.ToDfa(); break;
                case "min": fa = fa.ToMinimized(); break;
                default: throw new ArgumentOutOfRangeException("stage", stage, null);
            }

            var actual = fa.ToArray();

            Assert.Equal(golden.Table.Length, actual.Length);
            for (var i = 0; i < golden.Table.Length; ++i)
            {
                if (golden.Table[i] != actual[i])
                {
                    Assert.Fail(string.Format(
                        "{0}/{1}: state table diverges at index {2} of {3} (expected {4}, got {5}).",
                        name, stage, i, golden.Table.Length, golden.Table[i], actual[i]));
                }
            }
        }

        [Fact]
        public void Golden_file_covers_every_rule_of_the_slow_grammar()
        {
            var goldens = GoldenFile.Load();
            var ruleNames = GoldenFile
                .ReadRules(Path.Combine(TestPaths.TestCases, "slow-6-rules.rl"))
                .Select(kv => kv.Key)
                .ToList();

            Assert.Equal(6, ruleNames.Count);
            foreach (var rule in ruleNames)
            {
                Assert.True(goldens.Any(g => g.Name == rule),
                    "No golden recorded for rule " + rule + ".");
            }
        }
    }

    internal sealed class Golden
    {
        public string Name;
        public string Stage;
        public string Regex;
        public int[] Table;
    }

    internal static class GoldenFile
    {
        private static List<Golden> _cache;

        public static IReadOnlyList<KeyValuePair<string, string>> ReadRules(string rlPath)
        {
            var result = new List<KeyValuePair<string, string>>();
            foreach (var raw in File.ReadAllLines(rlPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;
                var eq = line.IndexOf('=');
                if (eq < 0) continue;
                var name = line.Substring(0, eq).Trim();
                var expr = line.Substring(eq + 1).Trim();
                if (expr.Length >= 2 && expr[0] == '\'' && expr[expr.Length - 1] == '\'')
                    expr = expr.Substring(1, expr.Length - 2);
                result.Add(new KeyValuePair<string, string>(name, expr));
            }
            return result;
        }

        /// <summary>How many of <see cref="Cases"/> are the small hand-written regexes.</summary>
        public const int SmallCaseCount = 7;

        private static IReadOnlyList<KeyValuePair<string, string>> _cases;

        /// <summary>Regexes must be reconstructed the same way the golden generator did.</summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Cases()
        {
            if (_cases != null) return _cases;

            var cases = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("lit_abc", "abc"),
                new KeyValuePair<string, string>("alt_abc", "a|b|c"),
                new KeyValuePair<string, string>("star_group", "(ab)*c"),
                new KeyValuePair<string, string>("plus_digit", "[0-9]+"),
                new KeyValuePair<string, string>("opt_nested", "(a(b|c)?d)*"),
                new KeyValuePair<string, string>("classic_abb", "(a|b)*abb"),
                new KeyValuePair<string, string>("dead_branch", "(ab|ac)d"),
            };
            Assert.Equal(SmallCaseCount, cases.Count);
            cases.AddRange(ReadRules(Path.Combine(TestPaths.TestCases, "slow-6-rules.rl")));
            _cases = cases;
            return cases;
        }

        public static List<Golden> Load()
        {
            if (_cache != null) return _cache;

            var byName = Cases().ToDictionary(kv => kv.Key, kv => kv.Value);
            var path = TestPaths.TestDataFile("fa-golden.tsv");
            var result = new List<Golden>();

            foreach (var line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split('\t');
                if (parts.Length != 3) continue;

                var table = parts[2].Length == 0
                    ? new int[0]
                    : parts[2].Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();

                result.Add(new Golden
                {
                    Name = parts[0],
                    Stage = parts[1],
                    Regex = byName[parts[0]],
                    Table = table,
                });
            }

            _cache = result;
            return result;
        }
    }
}
