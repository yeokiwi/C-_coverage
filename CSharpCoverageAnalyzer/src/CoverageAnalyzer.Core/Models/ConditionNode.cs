namespace CoverageAnalyzer.Core.Models;

public sealed class ConditionNode
{
    public int ProbeId { get; init; }
    public int DecisionId { get; init; }
    public SourceLocation Location { get; init; } = new();
    public string Text { get; init; } = string.Empty;
    public int IndexInDecision { get; init; }

    public bool TrueValueSeen { get; set; }
    public bool FalseValueSeen { get; set; }

    public bool BothValuesSeen => TrueValueSeen && FalseValueSeen;
}
