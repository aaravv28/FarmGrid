using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace FarmGrid.Tests
{
    public class ViewPathTests
    {
        private static string RepoRoot([CallerFilePath] string thisFile = "")
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FarmGrid.csproj")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }

        /// <summary>True if the path exists with exactly this casing on every segment (Linux is case-sensitive).</summary>
        private static bool ExistsWithExactCase(string root, string relativePath)
        {
            var current = root;
            foreach (var segment in relativePath.Split('/'))
            {
                var match = Directory.EnumerateFileSystemEntries(current)
                    .Select(Path.GetFileName)
                    .FirstOrDefault(name => name == segment);
                if (match == null)
                {
                    return false;
                }
                current = Path.Combine(current, match);
            }
            return true;
        }

        [Fact]
        public void Every_view_path_named_in_a_controller_exists_with_exact_casing()
        {
            var root = RepoRoot();
            var paths = Directory.GetFiles(Path.Combine(root, "Controllers"), "*.cs")
                .SelectMany(file => Regex.Matches(File.ReadAllText(file), "\"~/(Views/[^\"]+\\.cshtml)\"").Select(m => m.Groups[1].Value))
                .Distinct()
                .ToList();

            Assert.NotEmpty(paths);
            var missing = paths.Where(p => !ExistsWithExactCase(root, p)).ToList();
            Assert.True(missing.Count == 0, "Views not found with exact casing: " + string.Join(", ", missing));
        }
    }
}
