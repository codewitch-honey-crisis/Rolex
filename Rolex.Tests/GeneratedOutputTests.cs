using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using F;
using Xunit;

namespace Rolex.Tests
{
    /// <summary>
    /// End-to-end cover: drives the real rolex.exe over the grammars in testcases/
    /// and checks the emitted file against the golden hashes in testcases/expected.txt.
    ///
    /// The hashes for rules-1..5 and slow-6-rules were recorded from the build
    /// immediately before the determinization fix, so they still assert "the tables
    /// did not change". oom-7-rules and full-r1c1 could not be generated at all
    /// before the fix - for those, passing at all is the point.
    /// </summary>
    public class GeneratedOutputTests
    {
        public static IEnumerable<object[]> Grammars()
        {
            yield return new object[] { @"subsets\rules-1.rl" };
            yield return new object[] { @"subsets\rules-2.rl" };
            yield return new object[] { @"subsets\rules-3.rl" };
            yield return new object[] { @"subsets\rules-4.rl" };
            yield return new object[] { @"subsets\rules-5.rl" };
            yield return new object[] { "slow-6-rules.rl" };
            yield return new object[] { "oom-7-rules.rl" };
            yield return new object[] { "full-r1c1.rl" };
        }

        [Theory]
        [MemberData(nameof(Grammars))]
        public async Task Generated_tokenizer_matches_the_golden_hash(string grammar)
        {
            var exe = TestPaths.RolexExe;
            Assert.True(exe != null,
                "rolex.exe was not found. Build the Rolex project before running these tests.");

            var expected = LoadExpected();
            Assert.True(expected.ContainsKey(grammar), "No golden hash recorded for " + grammar + ".");

            var input = Path.Combine(TestPaths.TestCases, grammar);
            var output = Path.Combine(Path.GetTempPath(), "rolex-test-" + Guid.NewGuid().ToString("N") + ".cs");
            try
            {
                // /class and /namespace are pinned: without /class the emitted class name is
                // derived from the output file name, which would make the hash path-dependent.
                var psi = new ProcessStartInfo(exe,
                    string.Format("\"{0}\" /output \"{1}\" /class T /namespace N /noshared", input, output))
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using (var p = Process.Start(psi))
                {
                    // Drain both pipes concurrently. rolex.exe writes a busy progress
                    // spinner to stderr, so reading one stream to the end before starting
                    // on the other fills the other's buffer and deadlocks the child.
                    var stdout = p.StandardOutput.ReadToEndAsync();
                    var stderr = p.StandardError.ReadToEndAsync();

                    Assert.True(p.WaitForExit(300000), grammar + " did not finish within 5 minutes.");
                    await Task.WhenAll(stdout, stderr);
                    Assert.True(p.ExitCode == 0, grammar + " exited with " + p.ExitCode + ".");
                }

                Assert.True(File.Exists(output), "rolex.exe produced no output for " + grammar + ".");
                Assert.Equal(expected[grammar], Sha256(output));
            }
            finally
            {
                if (File.Exists(output)) File.Delete(output);
            }
        }

        /// <summary>
        /// Guards the blow-up itself. Before the fix, combining these six rules the way
        /// the lexer builder does needed more than 3 GB and either took ~76 s or died
        /// with OutOfMemoryException. The budget here is deliberately loose - it is
        /// there to catch a return of super-linear growth, not to police milliseconds.
        /// </summary>
        [Fact]
        public void Combined_lexer_for_six_rules_builds_without_blowing_up()
        {
            var rules = GoldenFile.ReadRules(Path.Combine(TestPaths.TestCases, "slow-6-rules.rl"));
            Assert.Equal(6, rules.Count);

            var before = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();

            var combined = new FA();
            for (var i = 0; i < rules.Count; ++i)
                combined.AddEpsilon(FA.Parse(rules[i].Value, i, true).ToMinimized());

            var dfa = combined.ToDfa();
            sw.Stop();
            var allocated = GC.GetTotalMemory(false) - before;

            Assert.NotEmpty(dfa.FillClosure());
            Assert.True(sw.Elapsed.TotalSeconds < 30,
                string.Format("Combined build took {0:N1}s; before the fix this path was the 76s/OOM case.",
                              sw.Elapsed.TotalSeconds));
            Assert.True(allocated < 512L * 1024 * 1024,
                string.Format("Combined build held {0:N0} MB; before the fix it needed over 3 GB.",
                              allocated / (1024 * 1024)));
        }

        private static Dictionary<string, string> LoadExpected()
        {
            var path = Path.Combine(TestPaths.TestCases, "expected.txt");
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#') continue;
                var parts = trimmed.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2) result[parts[1].Trim()] = parts[0].Trim();
            }
            return result;
        }

        private static string Sha256(string file)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(file))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }

}
