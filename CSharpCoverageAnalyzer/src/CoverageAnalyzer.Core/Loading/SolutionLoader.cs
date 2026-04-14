using System.Text.RegularExpressions;

namespace CoverageAnalyzer.Core.Loading;

public static class SolutionLoader
{
    // Matches: Project("{...}") = "Name", "relative\path.csproj", "{GUID}"
    private static readonly Regex ProjectLineRegex = new(
        @"Project\(""\{[^}]+\}""\)\s*=\s*""[^""]+""\s*,\s*""([^""]+\.csproj)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<ParsedSource> Load(string solutionPath)
    {
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");

        var solutionDir = Path.GetDirectoryName(Path.GetFullPath(solutionPath))!;
        var lines = File.ReadAllLines(solutionPath);

        var results = new List<ParsedSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var match = ProjectLineRegex.Match(line);
            if (!match.Success) continue;

            var relPath = match.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(solutionDir, relPath));

            if (!seen.Add(fullPath) || !File.Exists(fullPath))
                continue;

            try
            {
                results.AddRange(ProjectLoader.Load(fullPath));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: could not load project {fullPath}: {ex.Message}");
            }
        }

        return results;
    }
}
