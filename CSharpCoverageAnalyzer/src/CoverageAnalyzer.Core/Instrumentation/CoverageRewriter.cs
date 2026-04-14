using CoverageAnalyzer.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CoverageAnalyzer.Core.Instrumentation;

/// <summary>
/// Roslyn SyntaxRewriter that injects CoverageTracker probe calls into a syntax tree.
/// Produces a new tree with statement, branch, and condition probes inserted.
/// </summary>
public sealed class CoverageRewriter : CSharpSyntaxRewriter
{
    private readonly IReadOnlyList<StatementNode> _statements;
    private readonly IReadOnlyList<DecisionNode> _decisions;
    private readonly IReadOnlyList<ConditionNode> _conditions;
    private readonly string _filePath;

    // Maps from original node span start → probe IDs (populated during rewrite)
    private readonly Dictionary<int, StatementNode> _stmtByPosition;
    private readonly Dictionary<int, DecisionNode> _decisionByPosition;
    private readonly Dictionary<int, ConditionNode> _conditionByPosition;

    public CoverageRewriter(
        string filePath,
        IReadOnlyList<StatementNode> statements,
        IReadOnlyList<DecisionNode> decisions,
        IReadOnlyList<ConditionNode> conditions)
    {
        _filePath = filePath;
        _statements = statements;
        _decisions = decisions;
        _conditions = conditions;

        _stmtByPosition = statements
            .Where(s => s.Location.FilePath == filePath)
            .ToDictionary(s => s.Location.StartLine * 10000 + s.Location.StartColumn);

        _decisionByPosition = decisions
            .Where(d => d.Location.FilePath == filePath)
            .ToDictionary(d => d.Location.StartLine * 10000 + d.Location.StartColumn);

        _conditionByPosition = conditions
            .Where(c => c.Location.FilePath == filePath)
            .ToDictionary(c => c.Location.StartLine * 10000 + c.Location.StartColumn);
    }

    // ─── Statement probes ────────────────────────────────────────────────────────

    public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        var visited = (ExpressionStatementSyntax)base.VisitExpressionStatement(node)!;
        var probe = FindStatementProbe(node);
        if (probe == null) return visited;
        return Block(
            MakeStatementProbeCall(probe.ProbeId),
            visited);
    }

    public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
    {
        var visited = (ReturnStatementSyntax)base.VisitReturnStatement(node)!;
        var probe = FindStatementProbe(node);
        if (probe == null) return visited;
        return Block(
            MakeStatementProbeCall(probe.ProbeId),
            visited);
    }

    public override SyntaxNode? VisitThrowStatement(ThrowStatementSyntax node)
    {
        var visited = (ThrowStatementSyntax)base.VisitThrowStatement(node)!;
        var probe = FindStatementProbe(node);
        if (probe == null) return visited;
        return Block(
            MakeStatementProbeCall(probe.ProbeId),
            visited);
    }

    public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        var visited = (LocalDeclarationStatementSyntax)base.VisitLocalDeclarationStatement(node)!;
        var probe = FindStatementProbe(node);
        if (probe == null) return visited;
        return Block(
            MakeStatementProbeCall(probe.ProbeId),
            visited);
    }

    // ─── Decision (branch) probes ────────────────────────────────────────────────

    public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
    {
        // Visit children first (so nested ifs get probes)
        var visited = (IfStatementSyntax)base.VisitIfStatement(node)!;
        var decision = FindDecisionProbe(node);
        if (decision == null) return visited;

        // Wrap condition: if (CoverageTracker.RecordBranch(trueId, falseId, <condition>))
        var wrappedCondition = MakeBranchProbeCall(
            decision.TrueBranchProbeId,
            decision.FalseBranchProbeId,
            visited.Condition);

        return visited.WithCondition(wrappedCondition);
    }

    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
    {
        var visited = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
        var decision = FindDecisionProbe(node);
        if (decision == null) return visited;

        return visited.WithCondition(
            MakeBranchProbeCall(decision.TrueBranchProbeId, decision.FalseBranchProbeId, visited.Condition));
    }

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        var visited = (ForStatementSyntax)base.VisitForStatement(node)!;
        if (visited.Condition == null) return visited;
        var decision = FindDecisionProbe(node);
        if (decision == null) return visited;

        return visited.WithCondition(
            MakeBranchProbeCall(decision.TrueBranchProbeId, decision.FalseBranchProbeId, visited.Condition));
    }

    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
    {
        var visited = (DoStatementSyntax)base.VisitDoStatement(node)!;
        var decision = FindDecisionProbe(node);
        if (decision == null) return visited;

        return visited.WithCondition(
            MakeBranchProbeCall(decision.TrueBranchProbeId, decision.FalseBranchProbeId, visited.Condition));
    }

    public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        var visited = (ConditionalExpressionSyntax)base.VisitConditionalExpression(node)!;
        var decision = FindDecisionProbe(node);
        if (decision == null) return visited;

        return visited.WithCondition(
            MakeBranchProbeCall(decision.TrueBranchProbeId, decision.FalseBranchProbeId, visited.Condition));
    }

    // ─── MC/DC condition probes ──────────────────────────────────────────────────

    public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        var visited = (BinaryExpressionSyntax)base.VisitBinaryExpression(node)!;

        if (!node.IsKind(SyntaxKind.LogicalAndExpression) &&
            !node.IsKind(SyntaxKind.LogicalOrExpression))
            return visited;

        // Wrap left and right operands if they are atomic conditions with probes
        var left = WrapAtomicCondition(node.Left, visited.Left);
        var right = WrapAtomicCondition(node.Right, visited.Right);

        return visited.WithLeft(left).WithRight(right);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private StatementNode? FindStatementProbe(SyntaxNode node)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        int key = (lineSpan.StartLinePosition.Line + 1) * 10000 +
                  (lineSpan.StartLinePosition.Character + 1);
        return _stmtByPosition.GetValueOrDefault(key);
    }

    private DecisionNode? FindDecisionProbe(SyntaxNode node)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        int key = (lineSpan.StartLinePosition.Line + 1) * 10000 +
                  (lineSpan.StartLinePosition.Character + 1);
        return _decisionByPosition.GetValueOrDefault(key);
    }

    private ExpressionSyntax WrapAtomicCondition(ExpressionSyntax original, ExpressionSyntax visited)
    {
        // Only wrap if it is truly atomic (not itself a logical op)
        if (original is BinaryExpressionSyntax bin &&
            (bin.IsKind(SyntaxKind.LogicalAndExpression) ||
             bin.IsKind(SyntaxKind.LogicalOrExpression)))
            return visited;

        var lineSpan = original.SyntaxTree.GetLineSpan(original.Span);
        int key = (lineSpan.StartLinePosition.Line + 1) * 10000 +
                  (lineSpan.StartLinePosition.Character + 1);
        if (!_conditionByPosition.TryGetValue(key, out var condNode))
            return visited;

        return MakeConditionProbeCall(condNode.ProbeId, visited);
    }

    // CoverageTracker.RecordStatement(id);
    private static StatementSyntax MakeStatementProbeCall(int probeId) =>
        ExpressionStatement(
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("global::CoverageAnalyzer.Runtime.CoverageTracker"),
                    IdentifierName("RecordStatement")))
            .WithArgumentList(
                ArgumentList(SingletonSeparatedList(
                    Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(probeId)))))));

    // CoverageTracker.RecordBranch(trueId, falseId, <expr>)
    private static ExpressionSyntax MakeBranchProbeCall(int trueId, int falseId, ExpressionSyntax condition) =>
        InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("global::CoverageAnalyzer.Runtime.CoverageTracker"),
                IdentifierName("RecordBranch")))
        .WithArgumentList(
            ArgumentList(SeparatedList(new ArgumentSyntax[]
            {
                Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(trueId))),
                Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(falseId))),
                Argument(condition)
            })));

    // CoverageTracker.RecordCondition(id, <expr>)
    private static ExpressionSyntax MakeConditionProbeCall(int condId, ExpressionSyntax expr) =>
        InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("global::CoverageAnalyzer.Runtime.CoverageTracker"),
                IdentifierName("RecordCondition")))
        .WithArgumentList(
            ArgumentList(SeparatedList(new ArgumentSyntax[]
            {
                Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(condId))),
                Argument(expr)
            })));
}
