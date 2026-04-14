namespace CoverageAnalyzer.Core.Models;

/// <summary>
/// Represents the MC/DC independence pair requirement for a single atomic condition.
/// Two test vectors (assignments to all conditions in the decision) where:
///   - The target condition differs
///   - All other conditions are the same
///   - The decision outcome differs
/// </summary>
public sealed class MCDCRequirement
{
    public int DecisionProbeId { get; init; }
    public SourceLocation DecisionLocation { get; init; } = new();
    public string DecisionText { get; init; } = string.Empty;

    public int ConditionProbeId { get; init; }
    public string ConditionText { get; init; } = string.Empty;
    public int ConditionIndex { get; init; }

    /// <summary>Vector where the condition is true and decision outcome is true.</summary>
    public bool[] VectorConditionTrue { get; init; } = [];
    public bool DecisionOutcomeWhenTrue { get; init; }

    /// <summary>Vector where the condition is false and decision outcome differs.</summary>
    public bool[] VectorConditionFalse { get; init; } = [];
    public bool DecisionOutcomeWhenFalse { get; init; }

    public bool IsCovered { get; set; }
}
