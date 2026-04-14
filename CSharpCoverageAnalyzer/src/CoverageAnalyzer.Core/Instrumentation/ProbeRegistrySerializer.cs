using System.Text.Json;
using System.Text.Json.Serialization;
using CoverageAnalyzer.Core.Models;

namespace CoverageAnalyzer.Core.Instrumentation;

/// <summary>
/// Serializes the probe registry (analysis results without runtime coverage data) to
/// a JSON file so the <c>report</c> command can reconstruct it after tests have run.
/// </summary>
public static class ProbeRegistrySerializer
{
    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // ── Save ─────────────────────────────────────────────────────────────────────

    public static void Save(CoverageReport report, string path)
    {
        var dto = new ProbeRegistryDto
        {
            SourcePath = report.SourcePath,
            Statements = report.Statements.Select(s => new StatementDto
            {
                ProbeId = s.ProbeId,
                FilePath = s.Location.FilePath,
                StartLine = s.Location.StartLine,
                EndLine = s.Location.EndLine,
                StartColumn = s.Location.StartColumn,
                EndColumn = s.Location.EndColumn,
                Text = s.Text
            }).ToList(),
            Decisions = report.Decisions.Select(d => new DecisionDto
            {
                TrueBranchProbeId = d.TrueBranchProbeId,
                FalseBranchProbeId = d.FalseBranchProbeId,
                FilePath = d.Location.FilePath,
                StartLine = d.Location.StartLine,
                EndLine = d.Location.EndLine,
                StartColumn = d.Location.StartColumn,
                EndColumn = d.Location.EndColumn,
                DecisionText = d.DecisionText,
                Kind = d.Kind
            }).ToList(),
            Conditions = report.Conditions.Select(c => new ConditionDto
            {
                ProbeId = c.ProbeId,
                DecisionId = c.DecisionId,
                FilePath = c.Location.FilePath,
                StartLine = c.Location.StartLine,
                EndLine = c.Location.EndLine,
                StartColumn = c.Location.StartColumn,
                EndColumn = c.Location.EndColumn,
                Text = c.Text,
                IndexInDecision = c.IndexInDecision
            }).ToList(),
            MCDCRequirements = report.MCDCRequirements.Select(r => new MCDCRequirementDto
            {
                DecisionProbeId = r.DecisionProbeId,
                DecisionFilePath = r.DecisionLocation.FilePath,
                DecisionStartLine = r.DecisionLocation.StartLine,
                DecisionEndLine = r.DecisionLocation.EndLine,
                DecisionStartColumn = r.DecisionLocation.StartColumn,
                DecisionEndColumn = r.DecisionLocation.EndColumn,
                DecisionText = r.DecisionText,
                ConditionProbeId = r.ConditionProbeId,
                ConditionText = r.ConditionText,
                ConditionIndex = r.ConditionIndex,
                VectorConditionTrue = r.VectorConditionTrue,
                DecisionOutcomeWhenTrue = r.DecisionOutcomeWhenTrue,
                VectorConditionFalse = r.VectorConditionFalse,
                DecisionOutcomeWhenFalse = r.DecisionOutcomeWhenFalse
            }).ToList(),
            // Store source lines compactly as arrays of strings per file
            SourceLines = report.SourceLines.ToDictionary(
                kv => kv.Key,
                kv => kv.Value)
        };

        File.WriteAllText(path, JsonSerializer.Serialize(dto, _writeOptions));
    }

    // ── Load ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reconstructs a <see cref="CoverageReport"/> (with all coverage flags at their
    /// default false values) from a previously saved probes.json file.
    /// </summary>
    public static CoverageReport Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Probes file not found: {path}");

        var dto = JsonSerializer.Deserialize<ProbeRegistryDto>(
            File.ReadAllText(path), _readOptions)
            ?? throw new InvalidDataException("probes.json could not be parsed.");

        var statements = dto.Statements.Select(s => new StatementNode
        {
            ProbeId = s.ProbeId,
            Location = new SourceLocation
            {
                FilePath = s.FilePath,
                StartLine = s.StartLine,
                EndLine = s.EndLine,
                StartColumn = s.StartColumn,
                EndColumn = s.EndColumn
            },
            Text = s.Text
        }).ToList();

        var decisions = dto.Decisions.Select(d => new DecisionNode
        {
            TrueBranchProbeId = d.TrueBranchProbeId,
            FalseBranchProbeId = d.FalseBranchProbeId,
            Location = new SourceLocation
            {
                FilePath = d.FilePath,
                StartLine = d.StartLine,
                EndLine = d.EndLine,
                StartColumn = d.StartColumn,
                EndColumn = d.EndColumn
            },
            DecisionText = d.DecisionText,
            Kind = d.Kind
        }).ToList();

        var conditions = dto.Conditions.Select(c => new ConditionNode
        {
            ProbeId = c.ProbeId,
            DecisionId = c.DecisionId,
            Location = new SourceLocation
            {
                FilePath = c.FilePath,
                StartLine = c.StartLine,
                EndLine = c.EndLine,
                StartColumn = c.StartColumn,
                EndColumn = c.EndColumn
            },
            Text = c.Text,
            IndexInDecision = c.IndexInDecision
        }).ToList();

        var requirements = dto.MCDCRequirements.Select(r => new MCDCRequirement
        {
            DecisionProbeId = r.DecisionProbeId,
            DecisionLocation = new SourceLocation
            {
                FilePath = r.DecisionFilePath,
                StartLine = r.DecisionStartLine,
                EndLine = r.DecisionEndLine,
                StartColumn = r.DecisionStartColumn,
                EndColumn = r.DecisionEndColumn
            },
            DecisionText = r.DecisionText,
            ConditionProbeId = r.ConditionProbeId,
            ConditionText = r.ConditionText,
            ConditionIndex = r.ConditionIndex,
            VectorConditionTrue = r.VectorConditionTrue,
            DecisionOutcomeWhenTrue = r.DecisionOutcomeWhenTrue,
            VectorConditionFalse = r.VectorConditionFalse,
            DecisionOutcomeWhenFalse = r.DecisionOutcomeWhenFalse
        }).ToList();

        return new CoverageReport
        {
            SourcePath = dto.SourcePath,
            Mode = CoverageMode.Runtime,
            Statements = statements,
            Decisions = decisions,
            Conditions = conditions,
            MCDCRequirements = requirements,
            SourceLines = dto.SourceLines
        };
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────────

    private sealed class ProbeRegistryDto
    {
        public string SourcePath { get; set; } = string.Empty;
        public List<StatementDto> Statements { get; set; } = [];
        public List<DecisionDto> Decisions { get; set; } = [];
        public List<ConditionDto> Conditions { get; set; } = [];
        public List<MCDCRequirementDto> MCDCRequirements { get; set; } = [];
        public Dictionary<string, string[]> SourceLines { get; set; } = [];
    }

    private sealed class StatementDto
    {
        public int ProbeId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    private sealed class DecisionDto
    {
        public int TrueBranchProbeId { get; set; }
        public int FalseBranchProbeId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public string DecisionText { get; set; } = string.Empty;
        public DecisionKind Kind { get; set; }
    }

    private sealed class ConditionDto
    {
        public int ProbeId { get; set; }
        public int DecisionId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public string Text { get; set; } = string.Empty;
        public int IndexInDecision { get; set; }
    }

    private sealed class MCDCRequirementDto
    {
        public int DecisionProbeId { get; set; }
        public string DecisionFilePath { get; set; } = string.Empty;
        public int DecisionStartLine { get; set; }
        public int DecisionEndLine { get; set; }
        public int DecisionStartColumn { get; set; }
        public int DecisionEndColumn { get; set; }
        public string DecisionText { get; set; } = string.Empty;
        public int ConditionProbeId { get; set; }
        public string ConditionText { get; set; } = string.Empty;
        public int ConditionIndex { get; set; }
        public bool[] VectorConditionTrue { get; set; } = [];
        public bool DecisionOutcomeWhenTrue { get; set; }
        public bool[] VectorConditionFalse { get; set; } = [];
        public bool DecisionOutcomeWhenFalse { get; set; }
    }
}
