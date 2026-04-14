using CoverageAnalyzer.Core.Analysis;
using CoverageAnalyzer.Core.Loading;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace CoverageAnalyzer.Tests;

public class StatementAnalyzerTests
{
    private static List<Core.Models.StatementNode> Analyze(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var src = new ParsedSource
        {
            FilePath = "test.cs",
            SourceText = code,
            SyntaxTree = tree,
            Lines = code.Split('\n')
        };
        int id = 0;
        return StatementAnalyzer.Analyze([src], ref id);
    }

    [Fact]
    public void Counts_expression_statements()
    {
        var code = """
            class C {
                void M() {
                    int x = 1;
                    Console.WriteLine(x);
                    x = 2;
                }
            }
            """;
        var stmts = Analyze(code);
        // int x = 1 (has initializer), Console.WriteLine, x = 2
        Assert.Equal(3, stmts.Count);
    }

    [Fact]
    public void Skips_declaration_without_initializer()
    {
        var code = """
            class C {
                void M() {
                    int x;
                    x = 5;
                }
            }
            """;
        var stmts = Analyze(code);
        // int x (no initializer — skipped), x = 5 (expression stmt)
        Assert.Single(stmts);
    }

    [Fact]
    public void Counts_return_and_throw()
    {
        var code = """
            class C {
                int M(bool flag) {
                    if (flag) return 1;
                    throw new Exception();
                }
            }
            """;
        var stmts = Analyze(code);
        // return 1, throw
        Assert.Equal(2, stmts.Count);
    }

    [Fact]
    public void Probe_ids_are_sequential_starting_at_zero()
    {
        var code = """
            class C { void M() { int x = 1; int y = 2; int z = 3; } }
            """;
        var stmts = Analyze(code);
        Assert.Equal(3, stmts.Count);
        Assert.Equal(0, stmts[0].ProbeId);
        Assert.Equal(1, stmts[1].ProbeId);
        Assert.Equal(2, stmts[2].ProbeId);
    }

    [Fact]
    public void Probe_id_counter_continues_across_multiple_sources()
    {
        var code1 = "class A { void M() { int x = 1; } }";
        var code2 = "class B { void M() { int y = 2; } }";

        var tree1 = CSharpSyntaxTree.ParseText(code1);
        var tree2 = CSharpSyntaxTree.ParseText(code2);

        var sources = new[]
        {
            new ParsedSource { FilePath = "a.cs", SourceText = code1, SyntaxTree = tree1, Lines = code1.Split('\n') },
            new ParsedSource { FilePath = "b.cs", SourceText = code2, SyntaxTree = tree2, Lines = code2.Split('\n') }
        };

        int id = 10;
        var stmts = StatementAnalyzer.Analyze(sources, ref id);
        Assert.Equal(2, stmts.Count);
        Assert.Equal(10, stmts[0].ProbeId);
        Assert.Equal(11, stmts[1].ProbeId);
        Assert.Equal(12, id);
    }
}
