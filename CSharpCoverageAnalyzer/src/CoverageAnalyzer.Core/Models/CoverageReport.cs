namespace CoverageAnalyzer.Core.Models;

public sealed class CoverageReport
{
    public string SourcePath { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public CoverageMode Mode { get; init; }

    public List<StatementNode> Statements { get; init; } = [];
    public List<DecisionNode> Decisions { get; init; } = [];
    public List<ConditionNode> Conditions { get; init; } = [];
    public List<MCDCRequirement> MCDCRequirements { get; init; } = [];

    // Per-file source lines (for HTML reporting)
    public Dictionary<string, string[]> SourceLines { get; init; } = [];

    public int TotalStatements => Statements.Count;
    public int CoveredStatements => Statements.Count(s => s.IsCovered);
    public double StatementCoveragePercent =>
        TotalStatements == 0 ? 100.0 : 100.0 * CoveredStatements / TotalStatements;

    public int TotalDecisionBranches => Decisions.Count * 2;
    public int CoveredDecisionBranches =>
        Decisions.Count(d => d.TrueBranchCovered) + Decisions.Count(d => d.FalseBranchCovered);
    public double DecisionCoveragePercent =>
        TotalDecisionBranches == 0 ? 100.0 : 100.0 * CoveredDecisionBranches / TotalDecisionBranches;

    public int TotalMCDCPairs => MCDCRequirements.Count;
    public int CoveredMCDCPairs => MCDCRequirements.Count(r => r.IsCovered);
    public double MCDCCoveragePercent =>
        TotalMCDCPairs == 0 ? 100.0 : 100.0 * CoveredMCDCPairs / TotalMCDCPairs;
}

public enum CoverageMode
{
    StaticAnalysis,
    Runtime
}
