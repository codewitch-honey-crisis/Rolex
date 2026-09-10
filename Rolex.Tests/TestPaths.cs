using System;
using System.IO;

namespace Rolex.Tests
{
    /// <summary>
    /// Locates repository files relative to the test assembly, so the tests work
    /// from `dotnet test`, the VS test runner and a CI checkout alike.
    /// </summary>
    internal static class TestPaths
    {
        private static readonly Lazy<string> _repoRoot = new Lazy<string>(FindRepoRoot);

        public static string RepoRoot { get { return _repoRoot.Value; } }

        public static string TestCases { get { return Path.Combine(RepoRoot, "testcases"); } }

        /// <summary>
        /// The generator built in the same configuration as these tests, falling back to
        /// the other configuration and then to the copy in the solution root.
        /// Matching the configuration first matters: preferring Release unconditionally
        /// means a Debug test run silently checks a stale Release exe.
        /// </summary>
        public static string RolexExe
        {
            get
            {
#if DEBUG
                const string own = "Debug", other = "Release";
#else
                const string own = "Release", other = "Debug";
#endif
                var candidates = new[]
                {
                    Path.Combine(RepoRoot, @"Rolex\bin\" + own + @"\rolex.exe"),
                    Path.Combine(RepoRoot, @"Rolex\bin\" + other + @"\rolex.exe"),
                    Path.Combine(RepoRoot, "rolex.exe"),
                };
                foreach (var c in candidates)
                    if (File.Exists(c)) return c;
                return null;
            }
        }

        public static string TestDataFile(string name)
        {
            return Path.Combine(Path.GetDirectoryName(new Uri(typeof(TestPaths).Assembly.CodeBase).LocalPath),
                                "TestData", name);
        }

        private static string FindRepoRoot()
        {
            // Lets the suite run from a build output that is not inside the working tree.
            var configured = Environment.GetEnvironmentVariable("ROLEX_REPO_ROOT");
            if (!string.IsNullOrEmpty(configured) && File.Exists(Path.Combine(configured, "Rolex.sln")))
                return configured;

            var dir = Path.GetDirectoryName(new Uri(typeof(TestPaths).Assembly.CodeBase).LocalPath);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "Rolex.sln"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException("Could not locate Rolex.sln above the test assembly.");
        }
    }
}
