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

        /// <summary>The freshly built generator, preferred over the copy in the solution root.</summary>
        public static string RolexExe
        {
            get
            {
                var candidates = new[]
                {
                    Path.Combine(RepoRoot, @"Rolex\bin\Release\rolex.exe"),
                    Path.Combine(RepoRoot, @"Rolex\bin\Debug\rolex.exe"),
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
