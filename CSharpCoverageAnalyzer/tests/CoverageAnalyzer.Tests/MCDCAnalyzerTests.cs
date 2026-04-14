using CoverageAnalyzer.Core.Analysis;
using CoverageAnalyzer.Core.Loading;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace CoverageAnalyzer.Tests;

public class MCDCAnalyzerTests
{
    private static (List<Core.Models.ConditionNode> conditions, List<Core.Models.MCDCRequirement> requirements)
        Analyze(string code)
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
        return MCDCAnalyzer.Analyze([src], ref id);
    }

    [Fact]
    public void Two_conditions_AND_produces_two_independence_pairs()
    {
        // A && B: need pair for A and pair for B
        var code = "class C { void M(bool a, bool b) { if (a && b) { } } }";
        var (conditions, requirements) = Analyze(code);

        Assert.Equal(2, conditions.Count);
        Assert.Equal(2, requirements.Count);
    }

    [Fact]
    public void Two_conditions_OR_produces_two_independence_pairs()
    {
        var code = "class C { void M(bool a, bool b) { if (a || b) { } } }";
        var (_, requirements) = Analyze(code);
        Assert.Equal(2, requirements.Count);
    }

    [Fact]
    public void Three_conditions_produces_three_independence_pairs()
    {
        // A && B || C
        var code = "class C { void M(bool a, bool b, bool c) { if (a && b || c) { } } }";
        var (conditions, requirements) = Analyze(code);
        Assert.Equal(3, conditions.Count);
        Assert.Equal(3, requirements.Count);
    }

    [Fact]
    public void Independence_pair_vectors_differ_only_in_target_condition()
    {
        // A && B
        var code = "class C { void M(bool a, bool b) { if (a && b) { } } }";
        var (_, requirements) = Analyze(code);

        foreach (var req in requirements)
        {
            if (req.VectorConditionTrue.Length == 0) continue;

            int n = req.VectorConditionTrue.Length;
            int targetIdx = req.ConditionIndex;

            // Target condition must differ between the two vectors
            Assert.NotEqual(req.VectorConditionTrue[targetIdx], req.VectorConditionFalse[targetIdx]);

            // All other conditions must be equal
            for (int i = 0; i < n; i++)
            {
                if (i == targetIdx) continue;
                Assert.Equal(req.VectorConditionTrue[i], req.VectorConditionFalse[i]);
            }

            // Decision outcome must differ
            Assert.NotEqual(req.DecisionOutcomeWhenTrue, req.DecisionOutcomeWhenFalse);
        }
    }

    [Fact]
    public void Simple_condition_no_compound_boolean_produces_no_requirements()
    {
        var code = "class C { void M(bool a) { if (a) { } } }";
        var (_, requirements) = Analyze(code);
        Assert.Empty(requirements);
    }

    [Fact]
    public void Negated_condition_included_as_atomic()
    {
        // !a && b — !a is atomic
        var code = "class C { void M(bool a, bool b) { if (!a && b) { } } }";
        var (conditions, requirements) = Analyze(code);
        Assert.Equal(2, conditions.Count);
        Assert.Equal(2, requirements.Count);
    }

    [Fact]
    public void Condition_probe_ids_are_unique()
    {
        var code = "class C { void M(bool a, bool b, bool c) { if (a && b && c) { } } }";
        var (conditions, _) = Analyze(code);
        var ids = conditions.Select(c => c.ProbeId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
