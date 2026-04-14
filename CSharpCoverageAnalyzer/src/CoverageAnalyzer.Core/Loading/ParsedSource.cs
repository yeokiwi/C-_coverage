using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CoverageAnalyzer.Core.Loading;

public sealed class ParsedSource
{
    public string FilePath { get; init; } = string.Empty;
    public string SourceText { get; init; } = string.Empty;
    public SyntaxTree SyntaxTree { get; init; } = CSharpSyntaxTree.ParseText(string.Empty);
    public string[] Lines { get; init; } = [];
}
