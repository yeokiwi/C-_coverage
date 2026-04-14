using CoverageAnalyzer.Core.Analysis;
using CoverageAnalyzer.Core.Loading;
using CoverageAnalyzer.Core.Models;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace CoverageAnalyzer.Tests;

public class DecisionAnalyzerTests
{
    private static List<DecisionNode> Analyze(string code)
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
        return DecisionAnalyzer.Analyze([src], ref id);
    }

    [Fact]
    public void Detects_if_statement()
    {
        var code = "class C { void M(bool b) { if (b) { } } }";
        var decisions = Analyze(code);
        Assert.Contains(decisions, d => d.Kind == DecisionKind.If);
    }

    [Fact]
    public void Detects_while_loop()
    {
        var code = "class C { void M(bool b) { while (b) { } } }";
        var decisions = Analyze(code);
        Assert.Contains(decisions, d => d.Kind == DecisionKind.While);
    }

    [Fact]
    public void Detects_for_loop_with_condition()
    {
        var code = "class C { void M() { for (int i = 0; i < 10; i++) { } } }";
        var decisions = Analyze(code);
        Assert.Contains(decisions, d => d.Kind == DecisionKind.For);
    }

    [Fact]
    public void Detects_do_while()
    {
        var code = "class C { void M(bool b) { do { } while (b); } }";
        var decisions = Analyze(code);
        Assert.Contains(decisions, d => d.Kind == DecisionKind.DoWhile);
    }

    [Fact]
    public void Detects_ternary()
    {
        var code = "class C { int M(bool b) => b ? 1 : 0; }";
        var decisions = Analyze(code);
        Assert.Contains(decisions, d => d.Kind == DecisionKind.Ternary);
    }

    [Fact]
    public void Detects_switch_sections()
    {
        var code = """
            class C {
                void M(int x) {
                    switch (x) {
                        case 1: break;
                        case 2: break;
                        default: break;
                    }
                }
            }
            """;
        var decisions = Analyze(code);
        var switchDecisions = decisions.Where(d => d.Kind == DecisionKind.Switch).ToList();
        Assert.Equal(3, switchDecisions.Count);
    }

    [Fact]
    public void Detects_short_circuit_and()
    {
        var code = "class C { bool M(bool a, bool b) => a && b; }";
        var decisions = Analyze(code);
        Assert.Contains(decisions, d => d.Kind == DecisionKind.ShortCircuitAnd);
    }

    [Fact]
    public void Detects_short_circuit_or()
    {
        var code = "class C { bool M(bool a, bool b) => a || b; }";
        var decisions = Analyze(code);
        Assert.Contains(decisions, d => d.Kind == DecisionKind.ShortCircuitOr);
    }

    [Fact]
    public void Each_decision_has_distinct_true_false_probe_ids()
    {
        var code = """
            class C {
                void M(bool a, bool b) {
                    if (a) { }
                    if (b) { }
                }
            }
            """;
        var decisions = Analyze(code).Where(d => d.Kind == DecisionKind.If).ToList();
        Assert.Equal(2, decisions.Count);
        Assert.NotEqual(decisions[0].TrueBranchProbeId, decisions[1].TrueBranchProbeId);
        Assert.NotEqual(decisions[0].FalseBranchProbeId, decisions[1].FalseBranchProbeId);
    }
}
