namespace CoverageAnalyzer.Core.Loading;

public static class SourceLoader
{
    public static IReadOnlyList<ParsedSource> Load(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".cs" => [FileLoader.Load(path)],
            ".csproj" => ProjectLoader.Load(path),
            ".sln" => SolutionLoader.Load(path),
            _ => throw new ArgumentException(
                $"Unsupported input file type '{ext}'. Expected .cs, .csproj, or .sln.")
        };
    }
}
