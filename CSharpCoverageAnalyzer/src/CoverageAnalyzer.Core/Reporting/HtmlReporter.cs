using System.Net;
using System.Text;
using CoverageAnalyzer.Core.Models;

namespace CoverageAnalyzer.Core.Reporting;

public sealed class HtmlReporter : IReporter
{
    public void Write(CoverageReport report, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>C# Coverage Report</title>
            <style>
              body { font-family: 'Segoe UI', Arial, sans-serif; margin: 0; background: #f5f5f5; color: #222; }
              header { background: #1e3a5f; color: white; padding: 16px 24px; }
              header h1 { margin: 0; font-size: 1.4em; }
              header p { margin: 4px 0 0; font-size: .85em; opacity: .8; }
              .summary { display: flex; gap: 16px; padding: 16px 24px; flex-wrap: wrap; }
              .card { background: white; border-radius: 8px; padding: 16px 20px; flex: 1; min-width: 180px; box-shadow: 0 1px 4px rgba(0,0,0,.1); }
              .card h2 { margin: 0 0 8px; font-size: .95em; color: #555; }
              .card .pct { font-size: 2em; font-weight: bold; }
              .card .detail { font-size: .8em; color: #888; margin-top: 4px; }
              .pass { color: #2a9d2a; } .warn { color: #e07b00; } .fail { color: #c0392b; }
              .bar-bg { background: #e0e0e0; border-radius: 4px; height: 8px; margin-top: 6px; }
              .bar-fill { height: 8px; border-radius: 4px; background: #2a9d2a; }
              .file-section { background: white; margin: 16px 24px; border-radius: 8px; box-shadow: 0 1px 4px rgba(0,0,0,.1); overflow: hidden; }
              .file-header { background: #2c3e50; color: white; padding: 8px 16px; font-size: .9em; font-family: monospace; }
              table.source { width: 100%; border-collapse: collapse; font-family: 'Consolas','Courier New',monospace; font-size: .85em; }
              table.source tr { vertical-align: top; }
              table.source td.ln { width: 48px; text-align: right; padding: 0 8px; color: #888; background: #f8f8f8; border-right: 1px solid #ddd; user-select: none; }
              table.source td.hits { width: 36px; text-align: center; color: #aaa; background: #f8f8f8; border-right: 1px solid #ddd; font-size: .75em; }
              table.source td.code { padding: 0 8px; white-space: pre; }
              tr.covered td.code { background: #dff0d8; }
              tr.uncovered td.code { background: #fde8e8; }
              tr.partial td.code { background: #fff3cd; }
              tr.neutral td.code { background: white; }
              .legend { display: flex; gap: 16px; padding: 8px 24px; font-size: .8em; align-items: center; }
              .dot { width: 12px; height: 12px; border-radius: 2px; display: inline-block; margin-right: 4px; }
              .mcdc-section { margin: 16px 24px; background: white; border-radius: 8px; box-shadow: 0 1px 4px rgba(0,0,0,.1); overflow: hidden; }
              .mcdc-section h3 { margin: 0; padding: 10px 16px; background: #34495e; color: white; font-size: .95em; }
              table.mcdc { width: 100%; border-collapse: collapse; font-size: .82em; }
              table.mcdc th { background: #ecf0f1; padding: 6px 10px; text-align: left; border-bottom: 1px solid #ddd; }
              table.mcdc td { padding: 5px 10px; border-bottom: 1px solid #f0f0f0; vertical-align: top; }
              table.mcdc tr.req-covered { background: #f0fff0; }
              table.mcdc tr.req-uncovered { background: #fff5f5; }
              .tag { display: inline-block; font-size: .75em; padding: 1px 6px; border-radius: 3px; }
              .tag-pass { background: #2a9d2a; color: white; }
              .tag-fail { background: #c0392b; color: white; }
            </style>
            </head>
            <body>
            """);

        // Header
        sb.AppendLine($"<header><h1>C# Coverage Report</h1>");
        sb.AppendLine($"<p>Source: {WebUtility.HtmlEncode(report.SourcePath)} &nbsp;|&nbsp; Mode: {report.Mode} &nbsp;|&nbsp; {report.GeneratedAt:u}</p></header>");

        // Summary cards
        sb.AppendLine("<div class=\"summary\">");
        AppendCard(sb, "Statement Coverage", report.StatementCoveragePercent,
            $"{report.CoveredStatements} / {report.TotalStatements} statements");
        AppendCard(sb, "Decision Coverage", report.DecisionCoveragePercent,
            $"{report.CoveredDecisionBranches} / {report.TotalDecisionBranches} branches");
        AppendCard(sb, "MC/DC Coverage", report.MCDCCoveragePercent,
            $"{report.CoveredMCDCPairs} / {report.TotalMCDCPairs} independence pairs");
        sb.AppendLine("</div>");

        // Legend
        sb.AppendLine("""
            <div class="legend">
              <span><span class="dot" style="background:#dff0d8"></span> Covered</span>
              <span><span class="dot" style="background:#fde8e8"></span> Not covered</span>
              <span><span class="dot" style="background:#fff3cd"></span> Partial branch</span>
            </div>
            """);

        // Source files
        var files = report.SourceLines.Keys.Distinct().ToList();
        foreach (var file in files)
        {
            var lines = report.SourceLines[file];
            var stmtsByLine = report.Statements
                .Where(s => s.Location.FilePath == file)
                .GroupBy(s => s.Location.StartLine)
                .ToDictionary(g => g.Key, g => g.ToList());

            var decisionsByLine = report.Decisions
                .Where(d => d.Location.FilePath == file)
                .GroupBy(d => d.Location.StartLine)
                .ToDictionary(g => g.Key, g => g.ToList());

            sb.AppendLine($"<div class=\"file-section\">");
            sb.AppendLine($"<div class=\"file-header\">{WebUtility.HtmlEncode(file)}</div>");
            sb.AppendLine("<table class=\"source\">");

            for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
            {
                int lineNo = lineIdx + 1;
                var lineText = lines[lineIdx].TrimEnd('\r');
                string rowClass = GetRowClass(lineNo, stmtsByLine, decisionsByLine, report.Mode);
                string hitsHtml = GetHitsHtml(lineNo, stmtsByLine, report.Mode);

                sb.AppendLine($"<tr class=\"{rowClass}\">");
                sb.AppendLine($"<td class=\"ln\">{lineNo}</td>");
                sb.AppendLine($"<td class=\"hits\">{hitsHtml}</td>");
                sb.AppendLine($"<td class=\"code\">{WebUtility.HtmlEncode(lineText)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table></div>");
        }

        // MC/DC table
        if (report.MCDCRequirements.Count > 0)
        {
            sb.AppendLine("<div class=\"mcdc-section\">");
            sb.AppendLine("<h3>MC/DC Independence Pairs</h3>");
            sb.AppendLine("<table class=\"mcdc\">");
            sb.AppendLine("<thead><tr><th>File:Line</th><th>Decision</th><th>Condition</th><th>True Vector</th><th>False Vector</th><th>Status</th></tr></thead>");
            sb.AppendLine("<tbody>");

            foreach (var req in report.MCDCRequirements)
            {
                string rowCls = req.IsCovered ? "req-covered" : "req-uncovered";
                string statusTag = req.IsCovered
                    ? "<span class=\"tag tag-pass\">COVERED</span>"
                    : "<span class=\"tag tag-fail\">MISSING</span>";

                string trueVec = req.VectorConditionTrue.Length > 0
                    ? "[" + string.Join(",", req.VectorConditionTrue.Select(v => v ? "T" : "F")) + "] → " + (req.DecisionOutcomeWhenTrue ? "T" : "F")
                    : "N/A";
                string falseVec = req.VectorConditionFalse.Length > 0
                    ? "[" + string.Join(",", req.VectorConditionFalse.Select(v => v ? "T" : "F")) + "] → " + (req.DecisionOutcomeWhenFalse ? "T" : "F")
                    : "N/A";

                sb.AppendLine($"<tr class=\"{rowCls}\">");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(req.DecisionLocation.FilePath)}:{req.DecisionLocation.StartLine}</td>");
                sb.AppendLine($"<td><code>{WebUtility.HtmlEncode(Truncate(req.DecisionText, 50))}</code></td>");
                sb.AppendLine($"<td><code>{WebUtility.HtmlEncode(req.ConditionText)}</code></td>");
                sb.AppendLine($"<td><code>{WebUtility.HtmlEncode(trueVec)}</code></td>");
                sb.AppendLine($"<td><code>{WebUtility.HtmlEncode(falseVec)}</code></td>");
                sb.AppendLine($"<td>{statusTag}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></div>");
        }

        sb.AppendLine("</body></html>");

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"HTML report written to: {outputPath}");
    }

    private static void AppendCard(StringBuilder sb, string title, double pct, string detail)
    {
        string cls = pct >= 100 ? "pass" : pct >= 80 ? "warn" : "fail";
        int fill = Math.Clamp((int)pct, 0, 100);
        sb.AppendLine($"""
            <div class="card">
              <h2>{WebUtility.HtmlEncode(title)}</h2>
              <div class="pct {cls}">{pct:F1}%</div>
              <div class="detail">{WebUtility.HtmlEncode(detail)}</div>
              <div class="bar-bg"><div class="bar-fill" style="width:{fill}%"></div></div>
            </div>
            """);
    }

    private static string GetRowClass(
        int lineNo,
        Dictionary<int, List<StatementNode>> stmtsByLine,
        Dictionary<int, List<DecisionNode>> decisionsByLine,
        CoverageMode mode)
    {
        if (mode == CoverageMode.StaticAnalysis) return "neutral";

        bool hasStmt = stmtsByLine.ContainsKey(lineNo);
        bool hasDecision = decisionsByLine.ContainsKey(lineNo);

        if (!hasStmt && !hasDecision) return "neutral";

        bool stmtCovered = hasStmt && stmtsByLine[lineNo].All(s => s.IsCovered);
        bool stmtUncovered = hasStmt && stmtsByLine[lineNo].All(s => !s.IsCovered);
        bool decisionPartial = hasDecision && decisionsByLine[lineNo].Any(d => !d.FullyCovered);

        if (decisionPartial) return "partial";
        if (stmtCovered) return "covered";
        if (stmtUncovered) return "uncovered";
        return "partial";
    }

    private static string GetHitsHtml(
        int lineNo,
        Dictionary<int, List<StatementNode>> stmtsByLine,
        CoverageMode mode)
    {
        if (mode == CoverageMode.StaticAnalysis) return "";
        if (!stmtsByLine.TryGetValue(lineNo, out var stmts)) return "";
        return stmts.Any(s => s.IsCovered) ? "✓" : "✗";
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
