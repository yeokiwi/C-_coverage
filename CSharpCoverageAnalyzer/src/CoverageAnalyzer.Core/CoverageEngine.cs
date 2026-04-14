using CoverageAnalyzer.Core.Analysis;
using CoverageAnalyzer.Core.Loading;
using CoverageAnalyzer.Core.Models;

namespace CoverageAnalyzer.Core;

/// <summary>
/// Orchestrates loading, analysis, and report construction.
/// </summary>
public static class CoverageEngine
{
    /// <summary>
    /// Performs static analysis only — identifies all coverage requirements
    /// without running any code.
    /// </summary>
    public static CoverageReport AnalyzeStatic(string inputPath, CoverageType types = CoverageType.All)
    {
        var sources = SourceLoader.Load(inputPath);
        return BuildReport(inputPath, sources, types, CoverageMode.StaticAnalysis);
    }

    /// <summary>
    /// Builds a CoverageReport from already-loaded sources (used internally and by tests).
    /// </summary>
    public static CoverageReport BuildReport(
        string sourcePath,
        IReadOnlyList<ParsedSource> sources,
        CoverageType types,
        CoverageMode mode)
    {
        int nextProbeId = 0;

        var statements = types.HasFlag(CoverageType.Statement)
            ? StatementAnalyzer.Analyze(sources, ref nextProbeId)
            : [];

        var decisions = types.HasFlag(CoverageType.Decision)
            ? DecisionAnalyzer.Analyze(sources, ref nextProbeId)
            : [];

        List<ConditionNode> conditions = [];
        List<MCDCRequirement> requirements = [];
        if (types.HasFlag(CoverageType.MCDC))
        {
            (conditions, requirements) = MCDCAnalyzer.Analyze(sources, ref nextProbeId);
        }

        var sourceLines = sources.ToDictionary(
            s => s.FilePath,
            s => s.Lines);

        return new CoverageReport
        {
            SourcePath = sourcePath,
            Mode = mode,
            Statements = statements,
            Decisions = decisions,
            Conditions = conditions,
            MCDCRequirements = requirements,
            SourceLines = sourceLines
        };
    }
}

[Flags]
public enum CoverageType
{
    Statement = 1,
    Decision = 2,
    MCDC = 4,
    All = Statement | Decision | MCDC
}
