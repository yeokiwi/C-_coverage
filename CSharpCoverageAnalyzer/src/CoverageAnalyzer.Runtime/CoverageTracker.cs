using System.Collections.Concurrent;
using System.Text.Json;

namespace CoverageAnalyzer.Runtime;

/// <summary>
/// Lightweight runtime collector injected into instrumented assemblies.
/// Thread-safe. Writes coverage-raw.json on process exit.
/// </summary>
public static class CoverageTracker
{
    private static readonly ConcurrentDictionary<int, byte> _statements = new();
    private static readonly ConcurrentDictionary<int, BranchData> _branches = new();
    private static readonly ConcurrentDictionary<int, ConcurrentBag<bool>> _conditions = new();

    private static string _outputPath = "coverage-raw.json";

    static CoverageTracker()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
    }

    /// <summary>Configure the output path before tests run (optional).</summary>
    public static void Configure(string outputPath) => _outputPath = outputPath;

    // ─── Statement ──────────────────────────────────────────────────────────────

    public static void RecordStatement(int probeId)
    {
        _statements.TryAdd(probeId, 1);
    }

    // ─── Decision / Branch ──────────────────────────────────────────────────────

    /// <summary>
    /// Called for if/while/for/do/ternary conditions.
    /// Returns the value unchanged so it can wrap the original condition expression.
    /// </summary>
    public static bool RecordBranch(int trueProbeId, int falseProbeId, bool value)
    {
        var data = _branches.GetOrAdd(trueProbeId, _ => new BranchData(trueProbeId, falseProbeId));
        if (value) data.TrueSeen = true;
        else data.FalseSeen = true;
        return value;
    }

    /// <summary>Records a switch arm being entered.</summary>
    public static void RecordSwitchArm(int armProbeId)
    {
        var data = _branches.GetOrAdd(armProbeId, _ => new BranchData(armProbeId, -1));
        data.TrueSeen = true;
    }

    // ─── MC/DC Conditions ───────────────────────────────────────────────────────

    /// <summary>
    /// Wraps an atomic boolean condition in a decision expression.
    /// Records the value for MC/DC analysis and returns it unchanged.
    /// </summary>
    public static bool RecordCondition(int conditionProbeId, bool value)
    {
        var bag = _conditions.GetOrAdd(conditionProbeId, _ => new ConcurrentBag<bool>());
        bag.Add(value);
        return value;
    }

    // ─── Flush ──────────────────────────────────────────────────────────────────

    public static void Flush()
    {
        try
        {
            var data = new RawCoverageData
            {
                Statements = _statements.Keys.ToList(),
                Branches = _branches.Values
                    .Select(b => new RawBranchData
                    {
                        TrueProbeId = b.TrueProbeId,
                        FalseProbeId = b.FalseProbeId,
                        TrueSeen = b.TrueSeen,
                        FalseSeen = b.FalseSeen
                    })
                    .ToList(),
                Conditions = _conditions
                    .Select(kv => new RawConditionData
                    {
                        ProbeId = kv.Key,
                        Values = kv.Value.ToList()
                    })
                    .ToList()
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_outputPath, json);
        }
        catch
        {
            // Best-effort: never let flush crash the tested process
        }
    }

    private sealed class BranchData(int trueProbeId, int falseProbeId)
    {
        public int TrueProbeId { get; } = trueProbeId;
        public int FalseProbeId { get; } = falseProbeId;
        public bool TrueSeen { get; set; }
        public bool FalseSeen { get; set; }
    }
}

// ─── JSON Data Transfer Objects ─────────────────────────────────────────────────

public sealed class RawCoverageData
{
    public List<int> Statements { get; set; } = [];
    public List<RawBranchData> Branches { get; set; } = [];
    public List<RawConditionData> Conditions { get; set; } = [];
}

public sealed class RawBranchData
{
    public int TrueProbeId { get; set; }
    public int FalseProbeId { get; set; }
    public bool TrueSeen { get; set; }
    public bool FalseSeen { get; set; }
}

public sealed class RawConditionData
{
    public int ProbeId { get; set; }
    public List<bool> Values { get; set; } = [];
}
