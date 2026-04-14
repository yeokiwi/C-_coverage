namespace CoverageAnalyzer.Core.Models;

public sealed class SourceLocation
{
    public string FilePath { get; init; } = string.Empty;
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public int StartColumn { get; init; }
    public int EndColumn { get; init; }

    public override string ToString() =>
        $"{FilePath}:{StartLine}:{StartColumn}-{EndLine}:{EndColumn}";
}
