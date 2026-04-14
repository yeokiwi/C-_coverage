using CoverageAnalyzer.Core.Models;

namespace CoverageAnalyzer.Core.Reporting;

public sealed class ConsoleReporter : IReporter
{
    public void Write(CoverageReport report, string outputPath)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           C# Coverage Analyzer — Report                  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine($"  Source : {report.SourcePath}");
        Console.WriteLine($"  Mode   : {report.Mode}");
        Console.WriteLine($"  Time   : {report.GeneratedAt:u}");
        Console.WriteLine();

        PrintSection("Statement Coverage", report.CoveredStatements, report.TotalStatements,
            report.StatementCoveragePercent);
        PrintSection("Decision Coverage",
            report.CoveredDecisionBranches, report.TotalDecisionBranches,
            report.DecisionCoveragePercent);
        PrintSection("MC/DC Coverage", report.CoveredMCDCPairs, report.TotalMCDCPairs,
            report.MCDCCoveragePercent);

        Console.WriteLine();

        if (report.Mode == CoverageMode.StaticAnalysis)
        {
            PrintStaticDetails(report);
        }
        else
        {
            PrintRuntimeDetails(report);
        }
    }

    private static void PrintSection(string label, int covered, int total, double pct)
    {
        string bar = BuildBar(pct, 30);
        string status = pct >= 100 ? "PASS" : pct >= 80 ? "WARN" : "FAIL";
        Console.WriteLine($"  {label,-22} [{bar}] {pct,6:F1}% ({covered}/{total})  [{status}]");
    }

    private static string BuildBar(double pct, int width)
    {
        int filled = (int)(pct / 100.0 * width);
        return new string('#', filled) + new string('-', width - filled);
    }

    private static void PrintStaticDetails(CoverageReport report)
    {
        Console.WriteLine("── Statements (all require coverage) ──────────────────────");
        foreach (var s in report.Statements.Take(20))
            Console.WriteLine($"  [{s.ProbeId,4}] {s.Location.FilePath}:{s.Location.StartLine}  {Truncate(s.Text, 60)}");
        if (report.Statements.Count > 20)
            Console.WriteLine($"  ... and {report.Statements.Count - 20} more.");

        Console.WriteLine();
        Console.WriteLine("── Decisions (both branches require coverage) ──────────────");
        foreach (var d in report.Decisions.Take(20))
            Console.WriteLine($"  [{d.TrueBranchProbeId,4}/{d.FalseBranchProbeId,4}] {d.Location.FilePath}:{d.Location.StartLine}  {d.Kind}: {Truncate(d.DecisionText, 50)}");
        if (report.Decisions.Count > 20)
            Console.WriteLine($"  ... and {report.Decisions.Count - 20} more.");

        Console.WriteLine();
        Console.WriteLine("── MC/DC Independence Pairs ────────────────────────────────");
        foreach (var req in report.MCDCRequirements.Take(30))
        {
            var trueVec = string.Join(",", req.VectorConditionTrue.Select(v => v ? "T" : "F"));
            var falseVec = string.Join(",", req.VectorConditionFalse.Select(v => v ? "T" : "F"));
            Console.WriteLine($"  Decision @ {req.DecisionLocation.StartLine}: condition '{Truncate(req.ConditionText, 30)}'");
            Console.WriteLine($"    True  vector: [{trueVec}] → outcome={req.DecisionOutcomeWhenTrue}");
            Console.WriteLine($"    False vector: [{falseVec}] → outcome={req.DecisionOutcomeWhenFalse}");
        }
        if (report.MCDCRequirements.Count > 30)
            Console.WriteLine($"  ... and {report.MCDCRequirements.Count - 30} more.");
    }

    private static void PrintRuntimeDetails(CoverageReport report)
    {
        var uncoveredStmts = report.Statements.Where(s => !s.IsCovered).ToList();
        if (uncoveredStmts.Count > 0)
        {
            Console.WriteLine("── Uncovered Statements ────────────────────────────────────");
            foreach (var s in uncoveredStmts.Take(20))
                Console.WriteLine($"  {s.Location.FilePath}:{s.Location.StartLine}  {Truncate(s.Text, 70)}");
            if (uncoveredStmts.Count > 20)
                Console.WriteLine($"  ... and {uncoveredStmts.Count - 20} more.");
        }

        var partialDecisions = report.Decisions.Where(d => !d.FullyCovered).ToList();
        if (partialDecisions.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("── Partially Covered Decisions ─────────────────────────────");
            foreach (var d in partialDecisions.Take(20))
            {
                string missing = (!d.TrueBranchCovered ? "TRUE " : "") + (!d.FalseBranchCovered ? "FALSE" : "");
                Console.WriteLine($"  {d.Location.FilePath}:{d.Location.StartLine}  {d.Kind}: {Truncate(d.DecisionText, 40)}  [missing: {missing}]");
            }
        }

        var uncoveredMCDC = report.MCDCRequirements.Where(r => !r.IsCovered).ToList();
        if (uncoveredMCDC.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("── Uncovered MC/DC Pairs ───────────────────────────────────");
            foreach (var req in uncoveredMCDC.Take(20))
                Console.WriteLine($"  {req.DecisionLocation.FilePath}:{req.DecisionLocation.StartLine}  condition '{Truncate(req.ConditionText, 40)}'");
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
