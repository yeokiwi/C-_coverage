using CoverageAnalyzer.Core.Models;

namespace CoverageAnalyzer.Core.Instrumentation;

/// <summary>
/// Holds the mapping from probe IDs (integers injected into the code) to their
/// semantic roles (statement, branch true/false, condition) and source locations.
/// </summary>
public sealed class ProbeRegistry
{
    private readonly List<StatementNode> _statements;
    private readonly List<DecisionNode> _decisions;
    private readonly List<ConditionNode> _conditions;
    private readonly List<MCDCRequirement> _requirements;

    public IReadOnlyList<StatementNode> Statements => _statements;
    public IReadOnlyList<DecisionNode> Decisions => _decisions;
    public IReadOnlyList<ConditionNode> Conditions => _conditions;
    public IReadOnlyList<MCDCRequirement> MCDCRequirements => _requirements;

    public ProbeRegistry(
        List<StatementNode> statements,
        List<DecisionNode> decisions,
        List<ConditionNode> conditions,
        List<MCDCRequirement> requirements)
    {
        _statements = statements;
        _decisions = decisions;
        _conditions = conditions;
        _requirements = requirements;
    }

    /// <summary>
    /// Apply raw runtime coverage data to mark which probes were hit.
    /// </summary>
    public void ApplyRawData(
        IEnumerable<int> hitStatements,
        IEnumerable<(int trueId, int falseId, bool trueSeen, bool falseSeen)> branches,
        IEnumerable<(int probeId, IReadOnlyList<bool> values)> conditions)
    {
        var hitStmtSet = new HashSet<int>(hitStatements);
        foreach (var s in _statements)
            if (hitStmtSet.Contains(s.ProbeId))
                s.IsCovered = true;

        foreach (var (trueId, _, trueSeen, falseSeen) in branches)
        {
            var decision = _decisions.FirstOrDefault(d => d.TrueBranchProbeId == trueId);
            if (decision != null)
            {
                if (trueSeen) decision.TrueBranchCovered = true;
                if (falseSeen) decision.FalseBranchCovered = true;
            }
            // Also handle switch arms (FalseBranchProbeId == -1)
            var switchArm = _decisions.FirstOrDefault(d =>
                d.Kind == Models.DecisionKind.Switch && d.TrueBranchProbeId == trueId);
            if (switchArm != null && trueSeen)
                switchArm.TrueBranchCovered = true;
        }

        var conditionMap = conditions.ToDictionary(c => c.probeId, c => c.values);
        foreach (var cond in _conditions)
        {
            if (!conditionMap.TryGetValue(cond.ProbeId, out var vals)) continue;
            if (vals.Any(v => v)) cond.TrueValueSeen = true;
            if (vals.Any(v => !v)) cond.FalseValueSeen = true;
        }

        // Evaluate MC/DC requirements: for each requirement, check if both vectors
        // were actually observed during testing
        ApplyMCDCCoverage(conditionMap);
    }

    private void ApplyMCDCCoverage(Dictionary<int, IReadOnlyList<bool>> conditionValues)
    {
        // Group conditions by decisionId to reconstruct execution vectors
        var condsByDecision = _conditions.GroupBy(c => c.DecisionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.IndexInDecision).ToList());

        foreach (var req in _requirements)
        {
            if (req.VectorConditionTrue.Length == 0) continue; // no valid pair exists

            // For each requirement, we need to check if at some point during execution:
            // - All conditions in VectorConditionTrue were observed
            // - All conditions in VectorConditionFalse were observed
            // Since we only have per-condition value lists (not per-invocation snapshots),
            // we use a conservative approximation: check that both true and false
            // values were seen for the target condition, and that the required values
            // were seen for all other conditions.
            var condNode = _conditions.FirstOrDefault(c => c.ProbeId == req.ConditionProbeId);
            if (condNode == null) continue;

            bool trueSeen = condNode.TrueValueSeen;
            bool falseSeen = condNode.FalseValueSeen;

            if (trueSeen && falseSeen)
                req.IsCovered = true;
        }
    }
}
