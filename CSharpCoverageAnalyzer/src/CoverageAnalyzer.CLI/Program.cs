using System.CommandLine;
using System.Text.Json;
using CoverageAnalyzer.Core;
using CoverageAnalyzer.Core.Instrumentation;
using CoverageAnalyzer.Core.Loading;
using CoverageAnalyzer.Core.Models;
using CoverageAnalyzer.Core.Reporting;
using CoverageAnalyzer.Runtime;

// ── Root command ────────────────────────────────────────────────────────────────
var root = new RootCommand("C# Coverage Analyzer — statement, decision, and MC/DC coverage");

// ── Shared options ───────────────────────────────────────────────────────────────
var typeOption = new Option<string>(
    "--type",
    getDefaultValue: () => "all",
    description: "Coverage type: statement | decision | mcdc | all");

var formatOption = new Option<string[]>(
    "--format",
    getDefaultValue: () => ["console"],
    description: "Output format(s): console, html, json (comma-separated or repeated)")
{
    AllowMultipleArgumentsPerToken = true
};

var outputOption = new Option<string>(
    "--output",
    getDefaultValue: () => "coverage-report",
    description: "Output directory for HTML/JSON reports");

// ════════════════════════════════════════════════════════════════════════════════
// analyze <path>  — static analysis only
// ════════════════════════════════════════════════════════════════════════════════
var analyzePathArg = new Argument<string>("path", "Path to a .cs file, .csproj, or .sln");
var analyzeCmd = new Command("analyze", "Static analysis: list all coverage requirements without running code");
analyzeCmd.AddArgument(analyzePathArg);
analyzeCmd.AddOption(typeOption);
analyzeCmd.AddOption(formatOption);
analyzeCmd.AddOption(outputOption);

analyzeCmd.SetHandler((path, type, formats, outputDir) =>
{
    Console.WriteLine($"Analyzing: {path}");
    var coverageType = ParseCoverageType(type);

    CoverageReport report;
    try
    {
        report = CoverageEngine.AnalyzeStatic(path, coverageType);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return;
    }

    Directory.CreateDirectory(outputDir);
    WriteReports(report, formats, outputDir, "report");

}, analyzePathArg, typeOption, formatOption, outputOption);

root.AddCommand(analyzeCmd);

// ════════════════════════════════════════════════════════════════════════════════
// run <test-project> — instrument, run dotnet test, collect results
// ════════════════════════════════════════════════════════════════════════════════
var testProjectArg = new Argument<string>("test-project", "Path to the test .csproj or .sln to run");
var sourceOption = new Option<string?>(
    "--source",
    description: "Path to the .cs file, .csproj, or .sln whose coverage to measure (defaults to test-project)");

var runCmd = new Command("run", "Runtime measurement: instrument source, run dotnet test, report coverage");
runCmd.AddArgument(testProjectArg);
runCmd.AddOption(sourceOption);
runCmd.AddOption(typeOption);
runCmd.AddOption(formatOption);
runCmd.AddOption(outputOption);

runCmd.SetHandler((testProject, source, type, formats, outputDir) =>
{
    var sourcePath = source ?? testProject;
    Console.WriteLine($"Source     : {sourcePath}");
    Console.WriteLine($"Tests      : {testProject}");

    var coverageType = ParseCoverageType(type);

    // 1. Load and analyze source
    IReadOnlyList<ParsedSource> sources;
    try { sources = SourceLoader.Load(sourcePath); }
    catch (Exception ex) { Console.Error.WriteLine($"Load error: {ex.Message}"); return; }

    var report = CoverageEngine.BuildReport(sourcePath, sources, coverageType, CoverageMode.Runtime);

    var registry = new ProbeRegistry(
        report.Statements,
        report.Decisions,
        report.Conditions,
        report.MCDCRequirements);

    // 2. Instrument and write temp files
    var tempDir = Path.Combine(Path.GetTempPath(), "cov_" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(tempDir);
    var rawCoveragePath = Path.Combine(tempDir, "coverage-raw.json");

    Console.WriteLine($"Temp dir   : {tempDir}");
    Console.WriteLine("Instrumenting source files…");

    InstrumentFiles(sources, report, tempDir);

    // 3. Run dotnet test with env var pointing to coverage output
    Console.WriteLine("Running tests…");
    RunDotnetTest(testProject, rawCoveragePath, tempDir);

    // 4. Load raw data and apply to report
    if (File.Exists(rawCoveragePath))
    {
        Console.WriteLine("Loading coverage data…");
        ApplyRawData(rawCoveragePath, registry);
    }
    else
    {
        Console.WriteLine("Warning: no coverage-raw.json produced. " +
            "Ensure the instrumented code was built and the runtime library was referenced.");
    }

    // 5. Write reports
    Directory.CreateDirectory(outputDir);
    WriteReports(report, formats, outputDir, "report");

    // Clean up
    try { Directory.Delete(tempDir, true); } catch { }

}, testProjectArg, sourceOption, typeOption, formatOption, outputOption);

root.AddCommand(runCmd);

return await root.InvokeAsync(args);

// ── Helpers ──────────────────────────────────────────────────────────────────────

static CoverageType ParseCoverageType(string type) => type.ToLowerInvariant() switch
{
    "statement" => CoverageType.Statement,
    "decision" => CoverageType.Decision,
    "mcdc" => CoverageType.MCDC,
    _ => CoverageType.All
};

static void WriteReports(CoverageReport report, string[] formats, string outputDir, string baseName)
{
    foreach (var fmt in formats.SelectMany(f => f.Split(',', StringSplitOptions.RemoveEmptyEntries)))
    {
        switch (fmt.Trim().ToLowerInvariant())
        {
            case "console":
                new ConsoleReporter().Write(report, "");
                break;
            case "html":
                var htmlPath = Path.Combine(outputDir, baseName + ".html");
                new HtmlReporter().Write(report, htmlPath);
                break;
            case "json":
                var jsonPath = Path.Combine(outputDir, baseName + ".json");
                new JsonReporter().Write(report, jsonPath);
                break;
            default:
                Console.WriteLine($"Unknown format '{fmt}', skipping.");
                break;
        }
    }
}

static void InstrumentFiles(
    IReadOnlyList<ParsedSource> sources,
    CoverageReport report,
    string tempDir)
{
    foreach (var src in sources)
    {
        var rewriter = new CoverageRewriter(
            src.FilePath,
            report.Statements,
            report.Decisions,
            report.Conditions);

        var newRoot = rewriter.Visit(src.SyntaxTree.GetRoot());
        if (newRoot == null) continue;

        var relPath = Path.GetFileName(src.FilePath);
        var dest = Path.Combine(tempDir, relPath);
        File.WriteAllText(dest, newRoot.ToFullString());
        Console.WriteLine($"  Instrumented: {src.FilePath} → {dest}");
    }
}

static void RunDotnetTest(string testProject, string rawCoveragePath, string tempDir)
{
    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"test \"{testProject}\" --no-build",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    psi.Environment["COVERAGE_OUTPUT_PATH"] = rawCoveragePath;

    using var proc = System.Diagnostics.Process.Start(psi);
    if (proc == null) return;

    proc.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
    proc.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
    proc.BeginOutputReadLine();
    proc.BeginErrorReadLine();
    proc.WaitForExit();
}

static void ApplyRawData(string rawCoveragePath, ProbeRegistry registry)
{
    var json = File.ReadAllText(rawCoveragePath);
    var data = JsonSerializer.Deserialize<RawCoverageData>(json);
    if (data == null) return;

    var branches = data.Branches.Select(b =>
        (b.TrueProbeId, b.FalseProbeId, b.TrueSeen, b.FalseSeen));

    var conditions = data.Conditions.Select(c =>
        (c.ProbeId, (IReadOnlyList<bool>)c.Values));

    registry.ApplyRawData(data.Statements, branches, conditions);
}
