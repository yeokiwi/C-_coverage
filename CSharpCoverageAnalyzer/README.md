# CSharpCoverageAnalyzer

A .NET 8 command-line tool that performs **statement coverage**, **decision coverage**, and **MC/DC (Modified Condition/Decision Coverage)** analysis on C# source code. It accepts a single `.cs` file, a Visual Studio project (`.csproj`), or a full solution (`.sln`) as input and produces reports in console, HTML, and JSON formats.

---

## Table of Contents

- [Coverage Types](#coverage-types)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Usage](#usage)
  - [analyze — Static Analysis Mode](#analyze--static-analysis-mode)
  - [instrument — Automatic Instrumentation](#instrument--automatic-instrumentation)
  - [report — Generate Report from Instrumented Run](#report--generate-report-from-instrumented-run)
  - [run — All-in-One Runtime Measurement](#run--all-in-one-runtime-measurement)
  - [Options Reference](#options-reference)
- [Output Formats](#output-formats)
- [Examples](#examples)
- [Project Structure](#project-structure)
- [How It Works](#how-it-works)
- [Running the Tests](#running-the-tests)

---

## Coverage Types

| Type | Description |
|---|---|
| **Statement** | Tracks whether each executable statement in the code has been reached. |
| **Decision** | Tracks whether each decision point (`if`, `while`, `for`, `do-while`, `switch`, ternary `?:`, short-circuit `&&`/`\|\|`) has had both its true and false outcomes exercised. |
| **MC/DC** | For each compound boolean expression (e.g. `A && B \|\| C`), identifies the minimum set of test pairs — called *independence pairs* — that demonstrate each atomic condition independently affects the decision outcome. |

MC/DC is required by safety standards such as **DO-178C** (avionics), **IEC 61508** (industrial), and **ISO 26262** (automotive) for the highest assurance levels.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) or later

Verify your installation:

```bash
dotnet --version
# expected: 8.x.xxx
```

No other runtime or IDE is required. Visual Studio does **not** need to be installed.

---

## Installation

### Clone and build from source

```bash
git clone <repository-url>
cd CSharpCoverageAnalyzer
dotnet build CSharpCoverageAnalyzer.sln
```

### Run directly without installing

```bash
dotnet run --project src/CoverageAnalyzer.CLI/CoverageAnalyzer.CLI.csproj -- <subcommand> [options]
```

### Install as a global .NET tool (optional)

From inside the `CSharpCoverageAnalyzer/` directory:

```bash
dotnet pack src/CoverageAnalyzer.CLI/CoverageAnalyzer.CLI.csproj -c Release
dotnet tool install --global --add-source src/CoverageAnalyzer.CLI/bin/Release CoverageAnalyzer.CLI
```

To upgrade an already-installed version, use `update` instead of `install`:

```bash
dotnet tool update --global --add-source src/CoverageAnalyzer.CLI/bin/Release CoverageAnalyzer.CLI
```

To uninstall:

```bash
dotnet tool uninstall --global CoverageAnalyzer.CLI
```

After installation the tool is available as `coverage-analyzer` on your `PATH`.

---

## Usage

The tool has four subcommands:

```
coverage-analyzer analyze    <path>         [options]   # static analysis
coverage-analyzer instrument <path>         [options]   # rewrite source with probes
coverage-analyzer report     --probes --data [options]  # report from saved data
coverage-analyzer run        <test-project> [options]   # all-in-one
```

---

### `analyze` — Static Analysis Mode

Parses source code **without running it** and reports all coverage requirements: every statement that needs to be reached, every decision branch that needs to be exercised, and every MC/DC independence pair that needs a test case.

```bash
coverage-analyzer analyze <path> [--type <type>] [--format <fmt>...] [--output <dir>]
```

| Argument | Description |
|---|---|
| `<path>` | Path to a `.cs` file, `.csproj` project, or `.sln` solution |

---

### `instrument` — Automatic Instrumentation

**Automatically instruments** the given source code, project, or solution and writes a ready-to-build copy to an output directory. No manual code changes are required.

```bash
coverage-analyzer instrument <path> [--type <type>] [--output <dir>]
```

| Argument | Description |
|---|---|
| `<path>` | Path to a `.cs` file, `.csproj` project, or `.sln` solution |
| `--output` | Directory to write the instrumented tree (default: `instrumented`) |

What the command does:

1. **Analyzes** the source to identify all statements, decisions, and MC/DC conditions.
2. **Rewrites** every `.cs` file with `CoverageTracker` probe calls injected at each coverage point.
3. **Copies** project and solution files to the output directory, patching legacy-style `.csproj` files to include the tracker source.
4. **Embeds** `CoverageAnalyzer.Runtime.CoverageTracker.cs` into each project's output directory so the instrumented code compiles without any additional package references.
5. **Writes** `probes.json` — a probe registry that maps every probe ID back to its source location, used later by the `report` command.
6. **Prints** the exact build/test/report commands to run next.

After running `instrument`, you build and test the output directory **exactly as you would the original project**:

```bash
# Instrument
coverage-analyzer instrument MyApp/MyApp.csproj --output instrumented/

# Build the instrumented copy
dotnet build instrumented/MyApp.csproj

# Run your tests (set the output path for the coverage data)
set COVERAGE_OUTPUT_PATH=coverage-raw.json   # Windows
export COVERAGE_OUTPUT_PATH=coverage-raw.json  # Linux / macOS
dotnet test instrumented/MyApp.csproj

# Generate the coverage report
coverage-analyzer report \
    --probes instrumented/probes.json \
    --data coverage-raw.json \
    --format console html json \
    --output coverage-report/
```

> **Note:** If `COVERAGE_OUTPUT_PATH` is not set, the runtime writes `coverage-raw.json` in the current working directory.

---

### `report` — Generate Report from Instrumented Run

Loads the probe registry saved by `instrument` and the raw coverage data written by the instrumented test run, then generates the final coverage report.

```bash
coverage-analyzer report \
    --probes <probes.json> \
    --data   <coverage-raw.json> \
    [--format <fmt>...] \
    [--output <dir>]
```

| Option | Description |
|---|---|
| `--probes` | Path to `probes.json` written by `instrument` (required) |
| `--data` | Path to `coverage-raw.json` written by the instrumented test run (required) |

---

### `run` — All-in-One Runtime Measurement

Instruments source code entirely in memory, runs your tests via `dotnet test`, collects the probe data, and generates the coverage report — all in a single command. Use this when you do not need to keep the instrumented source.

```bash
coverage-analyzer run <test-project> [--source <path>] [--type <type>] [--format <fmt>...] [--output <dir>]
```

| Argument | Description |
|---|---|
| `<test-project>` | Path to the test `.csproj` or `.sln` to execute |
| `--source` | Path to the source code to measure (defaults to `<test-project>`) |

The tool works with any test framework supported by `dotnet test` — **xUnit**, **NUnit**, and **MSTest** are all compatible.

---

### Options Reference

| Option | Values | Default | Description |
|---|---|---|---|
| `--type` | `statement`, `decision`, `mcdc`, `all` | `all` | Which coverage type(s) to compute |
| `--format` | `console`, `html`, `json` | `console` | Report format(s); repeat the flag or comma-separate values |
| `--output` | directory path | `coverage-report` | Output directory for HTML and JSON files |

---

## Output Formats

### Console

Prints a summary table directly to the terminal with progress bars, coverage percentages, and a list of uncovered items.

```
╔══════════════════════════════════════════════════════════╗
║           C# Coverage Analyzer — Report                  ║
╚══════════════════════════════════════════════════════════╝
  Source : /path/to/Calculator.cs
  Mode   : StaticAnalysis

  Statement Coverage     [####--------------------------]   13.0% (1/8)  [FAIL]
  Decision Coverage      [##############----------------]   50.0% (6/12) [WARN]
  MC/DC Coverage         [##############################]  100.0% (4/4)  [PASS]
```

### HTML

Generates `report.html` in the output directory. Source code is highlighted line-by-line:

- **Green** — statement covered
- **Red** — statement not covered
- **Yellow** — decision point only partially covered (one branch missing)

An MC/DC table at the bottom lists every independence pair and marks it covered or missing.

### JSON

Generates `report.json` with a machine-readable structure suitable for CI pipelines or integration with other tools:

```json
{
  "sourcePath": "/path/to/Calculator.cs",
  "mode": "StaticAnalysis",
  "summary": {
    "statementCoverage": { "covered": 1, "total": 8, "percent": 12.5 },
    "decisionCoverage":  { "coveredBranches": 6, "totalBranches": 12, "percent": 50.0 },
    "mcdcCoverage":      { "coveredPairs": 4, "totalPairs": 4, "percent": 100.0 }
  },
  "statements": [ ... ],
  "decisions":  [ ... ],
  "mcdcRequirements": [ ... ]
}
```

---

## Examples

### Static analysis — list all coverage requirements for a file

```bash
coverage-analyzer analyze src/MyClass.cs
```

### Static analysis of a project with HTML + JSON output

```bash
coverage-analyzer analyze MyApp/MyApp.csproj --type all --format html json --output reports/
```

### Instrument a single file, then report after tests run

```bash
# Step 1: instrument
coverage-analyzer instrument src/Calculator.cs --output instrumented/

# Step 2: build and run (Calculator.cs must be part of a buildable project)
dotnet build MyApp.csproj
COVERAGE_OUTPUT_PATH=coverage-raw.json dotnet test MyApp.Tests.csproj

# Step 3: report
coverage-analyzer report \
    --probes instrumented/probes.json \
    --data coverage-raw.json \
    --format console html
```

### Instrument an entire project automatically

```bash
coverage-analyzer instrument MyApp/MyApp.csproj --output MyApp-instrumented/
dotnet build MyApp-instrumented/MyApp.csproj
dotnet test MyApp-instrumented/MyApp.csproj
coverage-analyzer report \
    --probes MyApp-instrumented/probes.json \
    --data coverage-raw.json \
    --format console html json \
    --output coverage-report/
```

### Instrument a full solution

```bash
coverage-analyzer instrument MyApp.sln --output MySolution-instrumented/
dotnet build MySolution-instrumented/MyApp.sln
dotnet test MySolution-instrumented/MyApp.sln
coverage-analyzer report \
    --probes MySolution-instrumented/probes.json \
    --data coverage-raw.json \
    --format html --output coverage-report/
```

### All-in-one: run tests and get a report in a single command

```bash
coverage-analyzer run MyApp.Tests/MyApp.Tests.csproj \
    --source MyApp/MyApp.csproj \
    --type all \
    --format console html \
    --output coverage-report/
```

### Measure MC/DC only

```bash
coverage-analyzer analyze src/Validator.cs --type mcdc
```

---

## Project Structure

```
CSharpCoverageAnalyzer/
├── CSharpCoverageAnalyzer.sln
├── src/
│   ├── CoverageAnalyzer.Core/          # Roslyn-based parsing, analysis, instrumentation, reporting
│   │   ├── Models/                     # SourceLocation, StatementNode, DecisionNode,
│   │   │                               #   ConditionNode, MCDCRequirement, CoverageReport
│   │   ├── Loading/                    # FileLoader, ProjectLoader, SolutionLoader, SourceLoader
│   │   ├── Analysis/                   # StatementAnalyzer, DecisionAnalyzer, MCDCAnalyzer
│   │   ├── Instrumentation/
│   │   │   ├── CoverageRewriter.cs     # Roslyn SyntaxRewriter — injects probe calls
│   │   │   ├── ProbeRegistry.cs        # Applies runtime data to probe model
│   │   │   ├── InstrumentationWriter.cs# Writes instrumented source tree to disk
│   │   │   └── ProbeRegistrySerializer.cs # JSON round-trip for probes.json
│   │   ├── Reporting/                  # ConsoleReporter, HtmlReporter, JsonReporter
│   │   └── CoverageEngine.cs           # Orchestration entry point
│   ├── CoverageAnalyzer.Runtime/       # Lightweight probe collector (no Roslyn dependency)
│   │   └── CoverageTracker.cs          # RecordStatement / RecordBranch / RecordCondition
│   │                                   # (also embedded verbatim into each instrumented project)
│   └── CoverageAnalyzer.CLI/           # System.CommandLine entry point
│       └── Program.cs                  # analyze / instrument / report / run subcommands
└── tests/
    └── CoverageAnalyzer.Tests/         # xUnit tests for all three analyzers
```

---

## How It Works

### Parsing

Source code is loaded using the **Roslyn** compiler API (`Microsoft.CodeAnalysis.CSharp`). This gives a full, lossless syntax tree without requiring the code to compile or any project toolchain to be present.

- A `.cs` file is parsed directly.
- A `.csproj` is read as XML; all included `.cs` files are loaded.
- A `.sln` is parsed with a regex over project lines; each referenced `.csproj` is loaded in turn.

### Statement Analysis

The syntax tree is walked looking for executable `StatementSyntax` nodes: expression statements, return/throw/break/continue/goto/yield statements, and local declarations that have an initializer. Each is assigned a monotonically increasing integer *probe ID*.

### Decision Analysis

Decision points are located by node type:

| Syntax node | Decision kind |
|---|---|
| `IfStatementSyntax` | `If` |
| `WhileStatementSyntax` | `While` |
| `ForStatementSyntax` (with condition) | `For` |
| `DoStatementSyntax` | `DoWhile` |
| `ConditionalExpressionSyntax` | `Ternary` |
| `SwitchStatementSyntax` | `Switch` (one entry per arm) |
| `BinaryExpressionSyntax` (`&&`) | `ShortCircuitAnd` |
| `BinaryExpressionSyntax` (`\|\|`) | `ShortCircuitOr` |

Each decision (except switch arms) gets two probe IDs: one for the true outcome and one for the false outcome.

### MC/DC Analysis

For every compound boolean expression that is the condition of an `if`, `while`, `for`, `do`, or ternary:

1. **Extract atomic conditions** — recursively decompose `&&`, `||`, `!`, and parentheses until leaf expressions (no logical operators) are reached.
2. **Build a truth table** — evaluate the decision for all 2ⁿ combinations of the *n* atomic conditions.
3. **Find independence pairs** — for each condition *Cᵢ*, search the truth table for two rows where:
   - *Cᵢ* has different values
   - all other conditions have the same values
   - the decision outcome differs

The resulting pairs are stored as `MCDCRequirement` objects, each recording the two test vectors and their expected decision outcomes.

### Instrumentation

A `CSharpSyntaxRewriter` (`CoverageRewriter`) traverses the syntax tree and injects calls into `CoverageAnalyzer.Runtime.CoverageTracker`:

```csharp
// Before each executable statement
CoverageTracker.RecordStatement(probeId);

// Wrapping each decision condition
if (CoverageTracker.RecordBranch(trueId, falseId, <originalCondition>)) { ... }

// Wrapping each atomic sub-condition for MC/DC
CoverageTracker.RecordCondition(condId, <originalSubExpr>)
```

`CoverageTracker` uses concurrent dictionaries, is thread-safe, and serialises all collected data to `coverage-raw.json` on `AppDomain.ProcessExit`. The output path can be overridden via the `COVERAGE_OUTPUT_PATH` environment variable.

**Two instrumentation modes are available:**

| Mode | Command | How it works |
|---|---|---|
| **Persistent** | `instrument` | Rewrites `.cs` files to disk; embeds `CoverageTracker.cs` in the output project; writes `probes.json`. You build, test, then run `report`. |
| **Ephemeral** | `run` | Rewrites sources in a temporary directory, runs `dotnet test`, collects data, and reports — all automatically. No files are kept. |

In persistent mode (`instrument`), `InstrumentationWriter` mirrors the original directory structure under the output directory, patches `.csproj` files as needed, and saves the probe registry to `probes.json`. The `report` command later reads `probes.json` alongside `coverage-raw.json` to reconstruct full coverage results.

---

## Running the Tests

```bash
dotnet test CSharpCoverageAnalyzer.sln
```

The test suite covers:

- **StatementAnalyzerTests** — counts of executable statements, skipping uninitialised declarations, probe ID sequencing across multiple source files.
- **DecisionAnalyzerTests** — detection of all decision kinds (`if`, `while`, `for`, `do-while`, ternary, `switch`, `&&`, `||`), unique probe IDs per decision.
- **MCDCAnalyzerTests** — correct number of independence pairs for 2- and 3-condition expressions, vector correctness (one condition flips, others constant, outcome flips), no pairs for simple non-compound conditions, negated conditions treated as atomic.
