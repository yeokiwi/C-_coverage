using CoverageAnalyzer.Core.Loading;
using CoverageAnalyzer.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoverageAnalyzer.Core.Analysis;

/// <summary>
/// Analyzes compound boolean expressions to produce MC/DC independence pair requirements.
/// For each atomic condition inside a compound decision, it finds two truth-table rows
/// where that condition changes value, all others are fixed, and the decision outcome changes.
/// </summary>
public static class MCDCAnalyzer
{
    public static (List<ConditionNode> conditions, List<MCDCRequirement> requirements) Analyze(
        IEnumerable<ParsedSource> sources, ref int nextProbeId)
    {
        var allConditions = new List<ConditionNode>();
        var allRequirements = new List<MCDCRequirement>();
        int nextDecisionId = 0;

        foreach (var src in sources)
        {
            AnalyzeSource(src, allConditions, allRequirements, ref nextProbeId, ref nextDecisionId);
        }

        return (allConditions, allRequirements);
    }

    private static void AnalyzeSource(
        ParsedSource src,
        List<ConditionNode> allConditions,
        List<MCDCRequirement> allRequirements,
        ref int nextProbeId,
        ref int nextDecisionId)
    {
        var root = src.SyntaxTree.GetRoot();

        // Find all compound boolean expressions that are direct decision expressions
        // (i.e., conditions of if/while/for/do/ternary or standalone logical expressions)
        var decisionExpressions = FindDecisionExpressions(root);

        foreach (var (decisionExpr, decisionLocation) in decisionExpressions)
        {
            var atomicConditions = ExtractAtomicConditions(decisionExpr).ToList();
            if (atomicConditions.Count < 2)
                continue; // MC/DC only meaningful for 2+ conditions

            int decisionId = nextDecisionId++;
            var span = src.SyntaxTree.GetLineSpan(decisionExpr.Span);
            var location = new SourceLocation
            {
                FilePath = src.FilePath,
                StartLine = span.StartLinePosition.Line + 1,
                EndLine = span.EndLinePosition.Line + 1,
                StartColumn = span.StartLinePosition.Character + 1,
                EndColumn = span.EndLinePosition.Character + 1
            };

            // Assign probe IDs to each condition
            var condNodes = new List<ConditionNode>();
            for (int i = 0; i < atomicConditions.Count; i++)
            {
                var condSpan = src.SyntaxTree.GetLineSpan(atomicConditions[i].Span);
                var condNode = new ConditionNode
                {
                    ProbeId = nextProbeId++,
                    DecisionId = decisionId,
                    Location = new SourceLocation
                    {
                        FilePath = src.FilePath,
                        StartLine = condSpan.StartLinePosition.Line + 1,
                        EndLine = condSpan.EndLinePosition.Line + 1,
                        StartColumn = condSpan.StartLinePosition.Character + 1,
                        EndColumn = condSpan.EndLinePosition.Character + 1
                    },
                    Text = atomicConditions[i].ToString().Trim(),
                    IndexInDecision = i
                };
                condNodes.Add(condNode);
            }

            allConditions.AddRange(condNodes);

            // Build truth table and find independence pairs
            var requirements = ComputeIndependencePairs(
                decisionId,
                location,
                decisionExpr.ToString(),
                condNodes,
                decisionExpr);

            allRequirements.AddRange(requirements);
        }
    }

    private static IEnumerable<(ExpressionSyntax expr, SyntaxNode context)> FindDecisionExpressions(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes())
        {
            ExpressionSyntax? expr = node switch
            {
                IfStatementSyntax s => s.Condition,
                WhileStatementSyntax s => s.Condition,
                ForStatementSyntax s => s.Condition,
                DoStatementSyntax s => s.Condition,
                ConditionalExpressionSyntax s => s.Condition,
                _ => null
            };

            if (expr != null && IsCompoundBoolean(expr))
                yield return (expr, node);
        }
    }

    private static bool IsCompoundBoolean(ExpressionSyntax expr)
    {
        return expr.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().Any(b =>
            b.IsKind(SyntaxKind.LogicalAndExpression) ||
            b.IsKind(SyntaxKind.LogicalOrExpression));
    }

    /// <summary>
    /// Recursively extracts atomic (leaf) boolean conditions from a compound expression.
    /// Atomic = no logical &&/|| operators at the top level.
    /// </summary>
    private static IEnumerable<ExpressionSyntax> ExtractAtomicConditions(ExpressionSyntax expr)
    {
        switch (expr)
        {
            case BinaryExpressionSyntax bin when
                bin.IsKind(SyntaxKind.LogicalAndExpression) ||
                bin.IsKind(SyntaxKind.LogicalOrExpression):
                foreach (var c in ExtractAtomicConditions(bin.Left))
                    yield return c;
                foreach (var c in ExtractAtomicConditions(bin.Right))
                    yield return c;
                break;

            case PrefixUnaryExpressionSyntax prefix when
                prefix.IsKind(SyntaxKind.LogicalNotExpression):
                foreach (var c in ExtractAtomicConditions(prefix.Operand))
                    yield return c;
                break;

            case ParenthesizedExpressionSyntax paren:
                foreach (var c in ExtractAtomicConditions(paren.Expression))
                    yield return c;
                break;

            default:
                yield return expr;
                break;
        }
    }

    /// <summary>
    /// Builds a truth table for the decision and finds MC/DC independence pairs
    /// for each atomic condition.
    /// </summary>
    private static List<MCDCRequirement> ComputeIndependencePairs(
        int decisionId,
        SourceLocation location,
        string decisionText,
        List<ConditionNode> conditions,
        ExpressionSyntax decisionExpr)
    {
        int n = conditions.Count;
        int totalRows = 1 << n; // 2^n

        // Build truth table: evaluate decision for each combination
        var truthTable = new (bool[] values, bool outcome)[totalRows];
        for (int row = 0; row < totalRows; row++)
        {
            var values = new bool[n];
            for (int i = 0; i < n; i++)
                values[i] = (row & (1 << i)) != 0;

            bool outcome = EvaluateExpression(decisionExpr, conditions, values);
            truthTable[row] = (values, outcome);
        }

        var requirements = new List<MCDCRequirement>();

        // For each condition, find an independence pair
        for (int ci = 0; ci < n; ci++)
        {
            bool foundPair = false;
            for (int rowA = 0; rowA < totalRows && !foundPair; rowA++)
            {
                for (int rowB = 0; rowB < totalRows && !foundPair; rowB++)
                {
                    var (vA, oA) = truthTable[rowA];
                    var (vB, oB) = truthTable[rowB];

                    // conditions[ci] must differ
                    if (vA[ci] == vB[ci]) continue;
                    // all other conditions must be the same
                    bool othersMatch = true;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == ci) continue;
                        if (vA[j] != vB[j]) { othersMatch = false; break; }
                    }
                    if (!othersMatch) continue;
                    // decision outcome must differ
                    if (oA == oB) continue;

                    // Found a valid independence pair
                    // Ensure rowA is the one where condition[ci] is TRUE
                    var (vecTrue, outTrue, vecFalse, outFalse) = vA[ci]
                        ? (vA, oA, vB, oB)
                        : (vB, oB, vA, oA);

                    requirements.Add(new MCDCRequirement
                    {
                        DecisionProbeId = decisionId,
                        DecisionLocation = location,
                        DecisionText = decisionText,
                        ConditionProbeId = conditions[ci].ProbeId,
                        ConditionText = conditions[ci].Text,
                        ConditionIndex = ci,
                        VectorConditionTrue = vecTrue,
                        DecisionOutcomeWhenTrue = outTrue,
                        VectorConditionFalse = vecFalse,
                        DecisionOutcomeWhenFalse = outFalse
                    });

                    foundPair = true;
                }
            }

            if (!foundPair)
            {
                // Some conditions may not have an independence pair (e.g., redundant condition)
                // Record a requirement with empty vectors to flag it
                requirements.Add(new MCDCRequirement
                {
                    DecisionProbeId = decisionId,
                    DecisionLocation = location,
                    DecisionText = decisionText,
                    ConditionProbeId = conditions[ci].ProbeId,
                    ConditionText = conditions[ci].Text,
                    ConditionIndex = ci,
                    VectorConditionTrue = [],
                    VectorConditionFalse = []
                });
            }
        }

        return requirements;
    }

    /// <summary>
    /// Symbolically evaluates the compound boolean expression given assignments to each atomic condition.
    /// </summary>
    private static bool EvaluateExpression(
        ExpressionSyntax expr,
        List<ConditionNode> conditions,
        bool[] values)
    {
        switch (expr)
        {
            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.LogicalAndExpression):
                return EvaluateExpression(bin.Left, conditions, values) &&
                       EvaluateExpression(bin.Right, conditions, values);

            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.LogicalOrExpression):
                return EvaluateExpression(bin.Left, conditions, values) ||
                       EvaluateExpression(bin.Right, conditions, values);

            case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.LogicalNotExpression):
                return !EvaluateExpression(prefix.Operand, conditions, values);

            case ParenthesizedExpressionSyntax paren:
                return EvaluateExpression(paren.Expression, conditions, values);

            default:
                // Atomic condition — find its index
                var condText = expr.ToString().Trim();
                for (int i = 0; i < conditions.Count; i++)
                {
                    if (conditions[i].Text == condText)
                        return values[i];
                }
                // Fallback: match by index position in left-to-right order
                return false;
        }
    }
}
