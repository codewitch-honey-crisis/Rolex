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
    ///
    /// The six grammars that use escapes were re-recorded for the unicode escape fix,
    /// from that same pre-determinization build with only the escape fix applied. They
    /// came out byte-identical to the fully fixed build, so these still assert exactly
    /// what they did before.
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
                await RunGenerator(exe, grammar, input, output);
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

            var sw = Stopwatch.StartNew();

            var combined = new FA();
            for (var i = 0; i < rules.Count; ++i)
                combined.AddEpsilon(FA.Parse(rules[i].Value, i, true).ToMinimized());

            var dfa = combined.ToDfa();
            sw.Stop();

            Assert.NotEmpty(dfa.FillClosure());
            Assert.True(sw.Elapsed.TotalSeconds < 30,
                string.Format("Combined build took {0:N1}s; before the fix this path was the 76s/OOM case.",
                              sw.Elapsed.TotalSeconds));

            // Time only. This test used to also assert on GC.GetTotalMemory, which reports
            // the managed heap at one instant, not the peak - the intermediate structures
            // that made this path need 3 GB are garbage by the time the method returns, so
            // it never actually measured the blow-up (the pre-fix run failed on the 78.6s
            // time budget, not on memory). Peak memory is asserted where it can be observed
            // honestly: Generated_tokenizer_stays_within_its_memory_budget, which reads
            // PeakWorkingSet64 from the rolex.exe child process.
        }

        /// <summary>
        /// Peak memory, measured the only way it can be measured honestly: the real
        /// generator in its own process, reading PeakWorkingSet64 after it exits.
        ///
        /// full-r1c1 is the worst case - 39 rules. Before the fix it could not be generated
        /// at all, dying with OutOfMemoryException at ~2 GB on the shipped 32-bit-preferred
        /// build and ~3.4 GB when allowed more. It now peaks around 100 MB, so 1 GB is a
        /// loose bound that still cannot be reached without the blow-up coming back.
        /// </summary>
        [Fact]
        public async Task Generated_tokenizer_stays_within_its_memory_budget()
        {
            var exe = TestPaths.RolexExe;
            Assert.True(exe != null,
                "rolex.exe was not found. Build the Rolex project before running these tests.");

            const string grammar = "full-r1c1.rl";
            var input = Path.Combine(TestPaths.TestCases, grammar);
            var output = Path.Combine(Path.GetTempPath(), "rolex-test-" + Guid.NewGuid().ToString("N") + ".cs");
            try
            {
                var peak = await RunGenerator(exe, grammar, input, output);
                // Guard against a vacuous pass: if sampling never caught the process, peak
                // is 0 and the budget below would hold no matter how much memory was used.
                Assert.True(peak > 8L * 1024 * 1024,
                    string.Format("Only sampled {0:N0} bytes of peak working set - the measurement did not run.", peak));
                Assert.True(peak < 1024L * 1024 * 1024,
                    string.Format("{0} peaked at {1:N0} MB; before the fix this grammar could not be generated at all.",
                                  grammar, peak / (1024 * 1024)));
            }
            finally
            {
                if (File.Exists(output)) File.Delete(output);
            }
        }

        /// <summary>Runs rolex.exe over one grammar and returns its peak working set in bytes.</summary>
        private static async Task<long> RunGenerator(string exe, string grammar, string input, string output)
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

                // Sample the peak while it runs: PeakWorkingSet64 throws
                // InvalidOperationException once the process has exited, so it cannot be
                // read afterwards.
                long peak = 0;
                var clock = Stopwatch.StartNew();
                bool exited;
                while (!(exited = p.WaitForExit(25)) && clock.ElapsedMilliseconds < 300000)
                {
                    try
                    {
                        p.Refresh();
                        if (p.PeakWorkingSet64 > peak) peak = p.PeakWorkingSet64;
                    }
                    catch (InvalidOperationException) { break; } // exited between the check and the read
                }
                if (!exited) exited = p.WaitForExit(0);

                // Kill before asserting, otherwise a timeout leaves rolex.exe running:
                // disposing the Process only releases the handle, it does not stop the child.
                if (!exited)
                {
                    try { p.Kill(); p.WaitForExit(); }
                    catch (InvalidOperationException) { } // already gone
                }
                Assert.True(exited, grammar + " did not finish within 5 minutes.");
                await Task.WhenAll(stdout, stderr);
                Assert.True(p.ExitCode == 0, grammar + " exited with " + p.ExitCode + ".");

                return peak;
            }
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
