namespace CoverageAnalyzer.Core.Models;

public sealed class DecisionNode
{
    public int TrueBranchProbeId { get; init; }
    public int FalseBranchProbeId { get; init; }
    public SourceLocation Location { get; init; } = new();
    public string DecisionText { get; init; } = string.Empty;
    public DecisionKind Kind { get; init; }

    public bool TrueBranchCovered { get; set; }
    public bool FalseBranchCovered { get; set; }

    public bool FullyCovered => TrueBranchCovered && FalseBranchCovered;
}

public enum DecisionKind
{
    If,
    While,
    For,
    DoWhile,
    Switch,
    Ternary,
    ShortCircuitAnd,
    ShortCircuitOr
}
