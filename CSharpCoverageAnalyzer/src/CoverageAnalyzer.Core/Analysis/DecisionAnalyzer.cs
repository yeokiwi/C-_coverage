using CoverageAnalyzer.Core.Loading;
using CoverageAnalyzer.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoverageAnalyzer.Core.Analysis;

public static class DecisionAnalyzer
{
    public static List<DecisionNode> Analyze(IEnumerable<ParsedSource> sources, ref int nextProbeId)
    {
        var results = new List<DecisionNode>();
        foreach (var src in sources)
            CollectDecisions(src, results, ref nextProbeId);
        return results;
    }

    private static void CollectDecisions(ParsedSource src, List<DecisionNode> results, ref int nextProbeId)
    {
        var root = src.SyntaxTree.GetRoot();

        foreach (var node in root.DescendantNodes())
        {
            DecisionNode? decision = null;

            switch (node)
            {
                case IfStatementSyntax ifStmt:
                    decision = MakeDecision(src, ifStmt, ifStmt.Condition.ToString(),
                        DecisionKind.If, ref nextProbeId);
                    break;

                case WhileStatementSyntax whileStmt:
                    decision = MakeDecision(src, whileStmt, whileStmt.Condition.ToString(),
                        DecisionKind.While, ref nextProbeId);
                    break;

                case ForStatementSyntax forStmt when forStmt.Condition != null:
                    decision = MakeDecision(src, forStmt, forStmt.Condition.ToString(),
                        DecisionKind.For, ref nextProbeId);
                    break;

                case DoStatementSyntax doStmt:
                    decision = MakeDecision(src, doStmt, doStmt.Condition.ToString(),
                        DecisionKind.DoWhile, ref nextProbeId);
                    break;

                case ConditionalExpressionSyntax ternary:
                    decision = MakeDecision(src, ternary, ternary.Condition.ToString(),
                        DecisionKind.Ternary, ref nextProbeId);
                    break;

                case SwitchStatementSyntax switchStmt:
                    // Each section in the switch is a separate branch
                    CollectSwitchDecisions(src, switchStmt, results, ref nextProbeId);
                    continue;

                case BinaryExpressionSyntax binary when
                    binary.IsKind(SyntaxKind.LogicalAndExpression) ||
                    binary.IsKind(SyntaxKind.LogicalOrExpression):
                    // Only register top-level short-circuit operators (not nested ones
                    // that are already children of another short-circuit op)
                    if (!IsChildOfShortCircuit(binary))
                    {
                        var kind = binary.IsKind(SyntaxKind.LogicalAndExpression)
                            ? DecisionKind.ShortCircuitAnd
                            : DecisionKind.ShortCircuitOr;
                        decision = MakeDecision(src, binary, binary.ToString(), kind, ref nextProbeId);
                    }
                    break;
            }

            if (decision != null)
                results.Add(decision);
        }
    }

    private static void CollectSwitchDecisions(
        ParsedSource src,
        SwitchStatementSyntax switchStmt,
        List<DecisionNode> results,
        ref int nextProbeId)
    {
        // Treat the whole switch as a set of decision nodes: one per case section
        foreach (var section in switchStmt.Sections)
        {
            var span = src.SyntaxTree.GetLineSpan(section.Span);
            var labels = string.Join(", ", section.Labels.Select(l => l.ToString().TrimEnd(':')));
            results.Add(new DecisionNode
            {
                TrueBranchProbeId = nextProbeId++,
                FalseBranchProbeId = -1, // N/A for switch arms
                Location = new SourceLocation
                {
                    FilePath = src.FilePath,
                    StartLine = span.StartLinePosition.Line + 1,
                    EndLine = span.EndLinePosition.Line + 1,
                    StartColumn = span.StartLinePosition.Character + 1,
                    EndColumn = span.EndLinePosition.Character + 1
                },
                DecisionText = $"case {labels}",
                Kind = DecisionKind.Switch
            });
        }
    }

    private static DecisionNode MakeDecision(
        ParsedSource src,
        SyntaxNode node,
        string conditionText,
        DecisionKind kind,
        ref int nextProbeId)
    {
        var span = src.SyntaxTree.GetLineSpan(node.Span);
        return new DecisionNode
        {
            TrueBranchProbeId = nextProbeId++,
            FalseBranchProbeId = nextProbeId++,
            Location = new SourceLocation
            {
                FilePath = src.FilePath,
                StartLine = span.StartLinePosition.Line + 1,
                EndLine = span.EndLinePosition.Line + 1,
                StartColumn = span.StartLinePosition.Character + 1,
                EndColumn = span.EndLinePosition.Character + 1
            },
            DecisionText = conditionText.Trim(),
            Kind = kind
        };
    }

    private static bool IsChildOfShortCircuit(SyntaxNode node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is BinaryExpressionSyntax bin &&
                (bin.IsKind(SyntaxKind.LogicalAndExpression) ||
                 bin.IsKind(SyntaxKind.LogicalOrExpression)))
                return true;
            // Stop ascending once we leave expression territory
            if (parent is StatementSyntax)
                break;
            parent = parent.Parent;
        }
        return false;
    }
}
