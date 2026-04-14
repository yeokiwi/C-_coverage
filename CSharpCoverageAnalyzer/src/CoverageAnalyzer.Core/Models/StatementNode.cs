namespace CoverageAnalyzer.Core.Models;

public sealed class StatementNode
{
    public int ProbeId { get; init; }
    public SourceLocation Location { get; init; } = new();
    public string Text { get; init; } = string.Empty;
    public bool IsCovered { get; set; }
}
