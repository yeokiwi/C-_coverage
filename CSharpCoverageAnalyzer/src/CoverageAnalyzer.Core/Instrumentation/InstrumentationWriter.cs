using System.Text;
using System.Xml.Linq;
using CoverageAnalyzer.Core.Loading;
using CoverageAnalyzer.Core.Models;

namespace CoverageAnalyzer.Core.Instrumentation;

/// <summary>
/// Writes a fully instrumented copy of the source tree to an output directory.
///
/// For each input source:
///   - Rewrites the .cs file with CoverageTracker probe calls injected.
///   - Preserves the relative directory structure under <outputDir>.
///
/// For each .csproj encountered:
///   - Copies it to the output, adding CoverageTracker.cs as a compile item
///     when the project uses explicit &lt;Compile&gt; includes (legacy style).
///   - SDK-style projects pick up CoverageTracker.cs automatically because it
///     is written into the same directory tree.
///
/// Also writes:
///   - CoverageTracker.cs in each project's root output directory.
///   - probes.json alongside the instrumented tree (consumed by 'report').
/// </summary>
public static class InstrumentationWriter
{
    // ── Public entry point ───────────────────────────────────────────────────────

    /// <summary>
    /// Instruments all sources and writes the output tree under <paramref name="outputDir"/>.
    /// Returns the path of the written probes.json.
    /// </summary>
    public static string Write(
        string inputPath,
        IReadOnlyList<ParsedSource> sources,
        CoverageReport report,
        string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        string inputExt = Path.GetExtension(inputPath).ToLowerInvariant();

        // Determine the "root" directory used for computing relative output paths.
        string rootDir = inputExt switch
        {
            ".cs" => Path.GetDirectoryName(Path.GetFullPath(inputPath))!,
            ".csproj" => Path.GetDirectoryName(Path.GetFullPath(inputPath))!,
            ".sln" => Path.GetDirectoryName(Path.GetFullPath(inputPath))!,
            _ => Path.GetDirectoryName(Path.GetFullPath(inputPath))!
        };

        // Group sources by the project they belong to so we know where to write
        // CoverageTracker.cs (one copy per project root).
        var projectRoots = DiscoverProjectRoots(inputPath, inputExt, rootDir);

        // 1. Write instrumented .cs files ────────────────────────────────────────
        foreach (var src in sources)
        {
            var rewriter = new CoverageRewriter(
                src.FilePath,
                report.Statements,
                report.Decisions,
                report.Conditions);

            var newRoot = rewriter.Visit(src.SyntaxTree.GetRoot());
            if (newRoot == null) continue;

            string destPath = MapSourceToOutput(src.FilePath, rootDir, outputDir);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.WriteAllText(destPath, newRoot.ToFullString(), Encoding.UTF8);
            Console.WriteLine($"  Instrumented : {src.FilePath}");
            Console.WriteLine($"             → {destPath}");
        }

        // 2. Copy / modify project files ─────────────────────────────────────────
        foreach (var projPath in projectRoots.ProjectFiles)
        {
            string destProj = MapSourceToOutput(projPath, rootDir, outputDir);
            Directory.CreateDirectory(Path.GetDirectoryName(destProj)!);
            CopyAndPatchCsproj(projPath, destProj);
            Console.WriteLine($"  Project file : {destProj}");
        }

        // 3. Copy solution file if applicable ────────────────────────────────────
        if (inputExt == ".sln")
        {
            string destSln = MapSourceToOutput(Path.GetFullPath(inputPath), rootDir, outputDir);
            Directory.CreateDirectory(Path.GetDirectoryName(destSln)!);
            File.Copy(Path.GetFullPath(inputPath), destSln, overwrite: true);
            Console.WriteLine($"  Solution     : {destSln}");
        }

        // 4. Write CoverageTracker.cs into each project root ─────────────────────
        foreach (var projRoot in projectRoots.ProjectDirs)
        {
            string trackerDest = Path.Combine(
                MapDirectoryToOutput(projRoot, rootDir, outputDir),
                "CoverageAnalyzer.Runtime.CoverageTracker.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(trackerDest)!);
            File.WriteAllText(trackerDest, CoverageTrackerSource(), Encoding.UTF8);
            Console.WriteLine($"  Tracker      : {trackerDest}");
        }

        // 5. Write probes.json ────────────────────────────────────────────────────
        string probesPath = Path.Combine(outputDir, "probes.json");
        ProbeRegistrySerializer.Save(report, probesPath);
        Console.WriteLine($"  Probes       : {probesPath}");

        return probesPath;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private sealed class ProjectRootInfo
    {
        public List<string> ProjectFiles { get; } = [];
        public List<string> ProjectDirs  { get; } = [];
    }

    private static ProjectRootInfo DiscoverProjectRoots(
        string inputPath, string inputExt, string rootDir)
    {
        var info = new ProjectRootInfo();

        switch (inputExt)
        {
            case ".cs":
                // No project file — use the containing directory as the project root
                info.ProjectDirs.Add(rootDir);
                break;

            case ".csproj":
                info.ProjectFiles.Add(Path.GetFullPath(inputPath));
                info.ProjectDirs.Add(rootDir);
                break;

            case ".sln":
            {
                // Re-parse the solution to find all .csproj paths
                var slnDir = Path.GetDirectoryName(Path.GetFullPath(inputPath))!;
                var projPaths = SolutionCsprojPaths(Path.GetFullPath(inputPath), slnDir);
                foreach (var p in projPaths)
                {
                    info.ProjectFiles.Add(p);
                    info.ProjectDirs.Add(Path.GetDirectoryName(p)!);
                }
                break;
            }
        }

        return info;
    }

    private static IEnumerable<string> SolutionCsprojPaths(string slnPath, string slnDir)
    {
        var regex = new System.Text.RegularExpressions.Regex(
            @"Project\(""\{[^}]+\}""\)\s*=\s*""[^""]+""\s*,\s*""([^""]+\.csproj)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var line in File.ReadLines(slnPath))
        {
            var m = regex.Match(line);
            if (!m.Success) continue;
            var rel = m.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(slnDir, rel));
            if (File.Exists(full))
                yield return full;
        }
    }

    /// <summary>Maps an absolute source path to its destination path under outputDir.</summary>
    private static string MapSourceToOutput(string absPath, string rootDir, string outputDir)
    {
        var rel = Path.GetRelativePath(rootDir, absPath);
        return Path.GetFullPath(Path.Combine(outputDir, rel));
    }

    /// <summary>Maps an absolute directory path to its destination under outputDir.</summary>
    private static string MapDirectoryToOutput(string absDir, string rootDir, string outputDir)
    {
        var rel = Path.GetRelativePath(rootDir, absDir);
        return Path.GetFullPath(Path.Combine(outputDir, rel));
    }

    /// <summary>
    /// Copies a .csproj to destPath, adding a CoverageTracker.cs compile item
    /// when the project uses legacy explicit-include style.
    /// </summary>
    private static void CopyAndPatchCsproj(string srcProj, string destProj)
    {
        var doc = XDocument.Load(srcProj);

        bool isLegacyStyle = doc.Descendants("Compile").Any();
        if (isLegacyStyle)
        {
            // Add <Compile Include="CoverageAnalyzer.Runtime.CoverageTracker.cs" /> if absent
            const string trackerFileName = "CoverageAnalyzer.Runtime.CoverageTracker.cs";
            bool alreadyPresent = doc.Descendants("Compile")
                .Any(e => string.Equals(
                    e.Attribute("Include")?.Value,
                    trackerFileName,
                    StringComparison.OrdinalIgnoreCase));

            if (!alreadyPresent)
            {
                // Find the first ItemGroup with Compile items and append to it
                var itemGroup = doc.Descendants("ItemGroup")
                    .FirstOrDefault(g => g.Elements("Compile").Any());

                itemGroup ??= new XElement("ItemGroup");

                itemGroup.Add(new XElement("Compile",
                    new XAttribute("Include", trackerFileName)));

                // Make sure the ItemGroup is part of the document
                if (itemGroup.Parent == null)
                    doc.Root!.Add(itemGroup);
            }
        }
        // SDK-style: CoverageTracker.cs is auto-included by file glob — no XML change needed.

        doc.Save(destProj);
    }

    // ── Embedded CoverageTracker source ──────────────────────────────────────────

    /// <summary>
    /// Returns the complete source text of CoverageTracker.cs to be written into
    /// each instrumented project so it compiles without an external package reference.
    /// </summary>
    private static string CoverageTrackerSource() => """
        // Auto-generated by CoverageAnalyzer.CLI — do not edit manually.
        using System.Collections.Concurrent;
        using System.Text.Json;

        namespace CoverageAnalyzer.Runtime;

        public static class CoverageTracker
        {
            private static readonly ConcurrentDictionary<int, byte> _statements = new();
            private static readonly ConcurrentDictionary<int, BranchData> _branches = new();
            private static readonly ConcurrentDictionary<int, ConcurrentBag<bool>> _conditions = new();

            private static string _outputPath =
                Environment.GetEnvironmentVariable("COVERAGE_OUTPUT_PATH")
                ?? "coverage-raw.json";

            static CoverageTracker()
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
            }

            public static void Configure(string outputPath) => _outputPath = outputPath;

            public static void RecordStatement(int probeId)
                => _statements.TryAdd(probeId, 1);

            public static bool RecordBranch(int trueProbeId, int falseProbeId, bool value)
            {
                var data = _branches.GetOrAdd(trueProbeId, _ => new BranchData(trueProbeId, falseProbeId));
                if (value) data.TrueSeen = true; else data.FalseSeen = true;
                return value;
            }

            public static void RecordSwitchArm(int armProbeId)
            {
                var data = _branches.GetOrAdd(armProbeId, _ => new BranchData(armProbeId, -1));
                data.TrueSeen = true;
            }

            public static bool RecordCondition(int conditionProbeId, bool value)
            {
                _conditions.GetOrAdd(conditionProbeId, _ => new ConcurrentBag<bool>()).Add(value);
                return value;
            }

            public static void Flush()
            {
                try
                {
                    var data = new
                    {
                        Statements = _statements.Keys.ToList(),
                        Branches = _branches.Values.Select(b => new
                        {
                            b.TrueProbeId, b.FalseProbeId, b.TrueSeen, b.FalseSeen
                        }).ToList(),
                        Conditions = _conditions.Select(kv => new
                        {
                            ProbeId = kv.Key, Values = kv.Value.ToList()
                        }).ToList()
                    };
                    File.WriteAllText(_outputPath,
                        JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch { /* best-effort */ }
            }

            private sealed class BranchData(int trueProbeId, int falseProbeId)
            {
                public int TrueProbeId { get; } = trueProbeId;
                public int FalseProbeId { get; } = falseProbeId;
                public bool TrueSeen { get; set; }
                public bool FalseSeen { get; set; }
            }
        }
        """;
}
