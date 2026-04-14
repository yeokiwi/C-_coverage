using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

namespace CoverageAnalyzer.Core.Loading;

public static class FileLoader
{
    public static ParsedSource Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"C# source file not found: {filePath}");

        var text = File.ReadAllText(filePath);
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(text, parseOptions, filePath);

        return new ParsedSource
        {
            FilePath = Path.GetFullPath(filePath),
            SourceText = text,
            SyntaxTree = tree,
            Lines = text.Split('\n')
        };
    }
}
