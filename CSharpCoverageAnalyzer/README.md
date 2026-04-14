# CSharpCoverageAnalyzer

A .NET 8 command-line tool that performs **statement coverage**, **decision coverage**, and **MC/DC (Modified Condition/Decision Coverage)** analysis on C# source code. It accepts a single `.cs` file, a Visual Studio project (`.csproj`), or a full solution (`.sln`) as input and produces reports in console, HTML, and JSON formats.

---

## Table of Contents

- [Coverage Types](#coverage-types)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Usage](#usage)
  - [analyze — Static Analysis Mode](#analyze--static-analysis-mode)
  - [run — Runtime Measurement Mode](#run--runtime-measurement-mode)
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

The tool has two subcommands:

```
coverage-analyzer analyze <path> [options]
coverage-analyzer run     <test-project> [options]
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

### `run` — Runtime Measurement Mode

Instruments the source code by injecting tracking probes, runs your tests via `dotnet test`, collects the probe data, and reports which statements, branches, and MC/DC pairs were actually exercised.

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

### Analyze a single file (static, all coverage types, console output)

```bash
coverage-analyzer analyze src/MyClass.cs
```

### Analyze a project and produce an HTML report

```bash
coverage-analyzer analyze MyApp/MyApp.csproj --type all --format html --output reports/
```

### Analyze a full solution and produce HTML + JSON

```bash
coverage-analyzer analyze MyApp.sln --format html --format json --output reports/
```

### Measure MC/DC only, console output

```bash
coverage-analyzer analyze src/Validator.cs --type mcdc
```

### Run tests and measure runtime coverage

```bash
coverage-analyzer run MyApp.Tests/MyApp.Tests.csproj \
    --source MyApp/MyApp.csproj \
    --type all \
    --format console --format html \
    --output coverage-report/
```

### Run with a solution that bundles source and tests

```bash
coverage-analyzer run MyApp.sln --format html --output coverage-report/
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
│   │   ├── Instrumentation/            # CoverageRewriter (SyntaxRewriter), ProbeRegistry
│   │   ├── Reporting/                  # ConsoleReporter, HtmlReporter, JsonReporter
│   │   └── CoverageEngine.cs           # Orchestration entry point
│   ├── CoverageAnalyzer.Runtime/       # Lightweight probe collector (no Roslyn dependency)
│   │   └── CoverageTracker.cs          # RecordStatement / RecordBranch / RecordCondition
│   └── CoverageAnalyzer.CLI/           # System.CommandLine entry point
│       └── Program.cs                  # analyze and run subcommands
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

### Instrumentation (Runtime Mode)

A `CSharpSyntaxRewriter` traverses the syntax tree and injects calls into the `CoverageAnalyzer.Runtime.CoverageTracker` static class:

```csharp
// Before each executable statement
CoverageTracker.RecordStatement(probeId);

// Wrapping each decision condition
if (CoverageTracker.RecordBranch(trueId, falseId, <originalCondition>)) { ... }

// Wrapping each atomic sub-condition for MC/DC
CoverageTracker.RecordCondition(condId, <originalSubExpr>)
```

`CoverageTracker` uses concurrent dictionaries, is thread-safe, and serialises all collected data to `coverage-raw.json` on `AppDomain.ProcessExit`.

---

## Running the Tests

```bash
dotnet test CSharpCoverageAnalyzer.sln
```

The test suite covers:

- **StatementAnalyzerTests** — counts of executable statements, skipping uninitialised declarations, probe ID sequencing across multiple source files.
- **DecisionAnalyzerTests** — detection of all decision kinds (`if`, `while`, `for`, `do-while`, ternary, `switch`, `&&`, `||`), unique probe IDs per decision.
- **MCDCAnalyzerTests** — correct number of independence pairs for 2- and 3-condition expressions, vector correctness (one condition flips, others constant, outcome flips), no pairs for simple non-compound conditions, negated conditions treated as atomic.
