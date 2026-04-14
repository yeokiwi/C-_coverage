using CoverageAnalyzer.Core.Loading;
using CoverageAnalyzer.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoverageAnalyzer.Core.Analysis;

public static class StatementAnalyzer
{
    public static List<StatementNode> Analyze(IEnumerable<ParsedSource> sources, ref int nextProbeId)
    {
        var results = new List<StatementNode>();
        foreach (var src in sources)
            CollectStatements(src, results, ref nextProbeId);
        return results;
    }

    private static void CollectStatements(ParsedSource src, List<StatementNode> results, ref int nextProbeId)
    {
        var root = src.SyntaxTree.GetRoot();
        foreach (var node in root.DescendantNodes())
        {
            if (!IsExecutableStatement(node)) continue;

            var span = src.SyntaxTree.GetLineSpan(node.Span);
            results.Add(new StatementNode
            {
                ProbeId = nextProbeId++,
                Location = new SourceLocation
                {
                    FilePath = src.FilePath,
                    StartLine = span.StartLinePosition.Line + 1,
                    EndLine = span.EndLinePosition.Line + 1,
                    StartColumn = span.StartLinePosition.Character + 1,
                    EndColumn = span.EndLinePosition.Character + 1
                },
                Text = node.ToString().Trim()
            });
        }
    }

    private static bool IsExecutableStatement(SyntaxNode node)
    {
        // Only consider statement-level nodes, not container blocks
        return node switch
        {
            ExpressionStatementSyntax => true,
            ReturnStatementSyntax => true,
            ThrowStatementSyntax => true,
            LocalDeclarationStatementSyntax decl =>
                // Only count if it has at least one initializer
                decl.Declaration.Variables.Any(v => v.Initializer != null),
            BreakStatementSyntax => true,
            ContinueStatementSyntax => true,
            GotoStatementSyntax => true,
            YieldStatementSyntax => true,
            LocalFunctionStatementSyntax => false, // the body will be analyzed
            _ => false
        };
    }
}
