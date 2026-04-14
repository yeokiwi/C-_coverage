using System.Xml.Linq;

namespace CoverageAnalyzer.Core.Loading;

public static class ProjectLoader
{
    public static IReadOnlyList<ParsedSource> Load(string projectPath)
    {
        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        var projectDir = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
        var doc = XDocument.Load(projectPath);

        var results = new List<ParsedSource>();

        // Explicit <Compile Include="..." /> items
        var explicitIncludes = doc.Descendants("Compile")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => v != null)
            .Cast<string>()
            .ToList();

        if (explicitIncludes.Count > 0)
        {
            foreach (var include in explicitIncludes)
            {
                var full = Path.GetFullPath(Path.Combine(projectDir, include));
                if (File.Exists(full))
                    results.Add(FileLoader.Load(full));
            }
        }
        else
        {
            // SDK-style project: include all .cs files except obj/ bin/
            foreach (var cs in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(projectDir, cs);
                if (rel.StartsWith("obj") || rel.StartsWith("bin"))
                    continue;
                results.Add(FileLoader.Load(cs));
            }
        }

        return results;
    }
}
