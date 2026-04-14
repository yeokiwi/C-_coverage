using System.Text.Json;
using System.Text.Json.Serialization;
using CoverageAnalyzer.Core.Models;

namespace CoverageAnalyzer.Core.Reporting;

public sealed class JsonReporter : IReporter
{
    public void Write(CoverageReport report, string outputPath)
    {
        var dto = new
        {
            sourcePath = report.SourcePath,
            generatedAt = report.GeneratedAt,
            mode = report.Mode.ToString(),
            summary = new
            {
                statementCoverage = new
                {
                    covered = report.CoveredStatements,
                    total = report.TotalStatements,
                    percent = Math.Round(report.StatementCoveragePercent, 2)
                },
                decisionCoverage = new
                {
                    coveredBranches = report.CoveredDecisionBranches,
                    totalBranches = report.TotalDecisionBranches,
                    percent = Math.Round(report.DecisionCoveragePercent, 2)
                },
                mcdcCoverage = new
                {
                    coveredPairs = report.CoveredMCDCPairs,
                    totalPairs = report.TotalMCDCPairs,
                    percent = Math.Round(report.MCDCCoveragePercent, 2)
                }
            },
            statements = report.Statements.Select(s => new
            {
                probeId = s.ProbeId,
                file = s.Location.FilePath,
                startLine = s.Location.StartLine,
                endLine = s.Location.EndLine,
                text = s.Text,
                covered = s.IsCovered
            }),
            decisions = report.Decisions.Select(d => new
            {
                trueBranchProbeId = d.TrueBranchProbeId,
                falseBranchProbeId = d.FalseBranchProbeId,
                file = d.Location.FilePath,
                startLine = d.Location.StartLine,
                kind = d.Kind.ToString(),
                decisionText = d.DecisionText,
                trueBranchCovered = d.TrueBranchCovered,
                falseBranchCovered = d.FalseBranchCovered
            }),
            mcdcRequirements = report.MCDCRequirements.Select(r => new
            {
                decisionProbeId = r.DecisionProbeId,
                file = r.DecisionLocation.FilePath,
                startLine = r.DecisionLocation.StartLine,
                decisionText = r.DecisionText,
                conditionProbeId = r.ConditionProbeId,
                conditionText = r.ConditionText,
                conditionIndex = r.ConditionIndex,
                vectorConditionTrue = r.VectorConditionTrue,
                vectorConditionFalse = r.VectorConditionFalse,
                decisionOutcomeWhenTrue = r.DecisionOutcomeWhenTrue,
                decisionOutcomeWhenFalse = r.DecisionOutcomeWhenFalse,
                covered = r.IsCovered
            })
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        var json = JsonSerializer.Serialize(dto, options);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"JSON report written to: {outputPath}");
    }
}
