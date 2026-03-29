# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Zero Tolerance Policy

- **NEVER abandon work halfway through** - if something gets difficult, push through it
- **NEVER use `git stash`** to hide incomplete work - fix the problem directly
- **NEVER give up because a task is complex** - break it down and keep going
- If a tool call is rejected, adapt your approach immediately and continue

## Build & Test Commands

This project uses **Microsoft Testing Platform (MTP)** with the **TUnit** testing framework. Test commands differ significantly from traditional VSTest.

See: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test?tabs=dotnet-test-with-mtp

### Prerequisites

```powershell
# Check .NET installation (.NET 8.0, 9.0, and 10.0 required)
dotnet --info

# Restore NuGet packages
cd src
dotnet restore ReactiveUI.Extensions.slnx
```

**Note:** This project uses the modern `.slnx` (XML-based solution file) format instead of the legacy `.sln` format.

### Build Commands

**CRITICAL:** The working folder must be `./src` folder. These commands won't function properly without the correct working folder.

```powershell
# Build the solution
dotnet build ReactiveUI.Extensions.slnx -c Release

# Build with warnings as errors (includes StyleCop violations)
dotnet build ReactiveUI.Extensions.slnx -c Release -warnaserror

# Clean the solution
dotnet clean ReactiveUI.Extensions.slnx
```

### Test Commands (Microsoft Testing Platform)

**CRITICAL:** This repository uses MTP configured in `testconfig.json`. All TUnit-specific arguments must be passed after `--`:

The working folder must be `./src` folder.

**IMPORTANT:**
- Do NOT use `--no-build` flag when running tests. Always build before testing to ensure all code changes are compiled.
- Use `--output Detailed` to see Console.WriteLine output from tests (place BEFORE any `--` separator).

```powershell
# Run all tests in the solution
dotnet test --solution ReactiveUI.Extensions.slnx -c Release

# Run all tests in a specific project
dotnet test --project ReactiveUI.Extensions.Tests/ReactiveUI.Extensions.Tests.csproj -c Release

# Run a single test method using treenode-filter
dotnet test --project ReactiveUI.Extensions.Tests/ReactiveUI.Extensions.Tests.csproj -- --treenode-filter "/*/*/*/MyTestMethod"

# Run all tests in a specific class
dotnet test --project ReactiveUI.Extensions.Tests/ReactiveUI.Extensions.Tests.csproj -- --treenode-filter "/*/*/ParityOperatorTests/*"

# Run tests with code coverage
dotnet test --solution ReactiveUI.Extensions.slnx -- --coverage --coverage-output-format cobertura
```

### TUnit Treenode-Filter Syntax

The `--treenode-filter` follows the pattern: `/{AssemblyName}/{Namespace}/{ClassName}/{TestMethodName}`

- Single test: `--treenode-filter "/*/*/*/MyTestMethod"`
- All tests in class: `--treenode-filter "/*/*/MyClassName/*"`
- Use single asterisks (`*`) to match segments.

### Key Configuration Files

- `src/ReactiveUI.Extensions.slnx` - Modern XML-based solution file
- `src/testconfig.json` - Configures test execution and code coverage
- `src/Directory.Build.props` - Common build properties, package metadata, target frameworks
- `src/Directory.Packages.props` - Central package management
- `src/Directory.Build.targets` - Build targets

### Code Coverage

Code coverage uses **Microsoft.Testing.Extensions.CodeCoverage** configured in `src/testconfig.json`. Coverage is collected for production assemblies only (test projects are excluded).

**Note:** If a code coverage MCP server is available, prefer using it over manual report generation — it is far more efficient.

```powershell
# Run tests with code coverage (from src/ folder)
dotnet test --solution ReactiveUI.Extensions.slnx -c Release -- --coverage --coverage-output-format cobertura

# Generate HTML report using ReportGenerator (install if needed: dotnet tool install -g dotnet-reportgenerator-globaltool)
reportgenerator \
  -reports:"ReactiveUI.Extensions.Tests/**/TestResults/**/*.cobertura.xml" \
  -targetdir:/tmp/code_coverage \
  -reporttypes:"Html;TextSummary"

# View the text summary
cat /tmp/code_coverage/Summary.txt

# Open HTML report in browser
xdg-open /tmp/code_coverage/index.html   # Linux
open /tmp/code_coverage/index.html        # macOS
```

**Key configuration** (`src/testconfig.json`):
- `modulePaths.include`: `Extensions\\..*` — covers all production assemblies
- `modulePaths.exclude`: `.*Tests.*`, `.*TestRunner.*` — excludes test/runner assemblies
- `skipAutoProperties: true` — auto-properties excluded from coverage metrics

**Tips:**
- Always clean `bin/` and `obj/` folders before coverage runs to avoid stale results
- Put coverage reports in `/tmp/` to avoid accidentally committing them

## Architecture Overview

### What This Project Does

ReactiveUI.Extensions provides a focused collection of high-value Reactive Extensions (Rx) operators that complement System.Reactive, plus a fully async-native `IObservableAsync<T>` pipeline with its own set of operators, subjects, and coordination primitives.

### Project Structure

```
src/
├── ReactiveUI.Extensions/                    # Main library (net8.0;net9.0;net10.0;net462;net472;net481)
│   ├── ReactiveExtensions.cs                 # Synchronous Rx operator extensions
│   ├── Async/                                # Async-native observable pipeline
│   │   ├── IObservableAsync.cs               # Core async observable interface
│   │   ├── IObserverAsync.cs                 # Core async observer interface
│   │   ├── ObservableAsync.cs                # Factory methods (Create, Return, Empty, etc.)
│   │   ├── Operators/                        # Async operator implementations
│   │   │   ├── ParityHelpers.cs              # Parity helpers (AsSignal, CatchIgnore, Pairwise, etc.)
│   │   │   ├── CombineLatestEnumerable.cs    # CombineLatest for IEnumerable<IObservableAsync<T>>
│   │   │   └── ...                           # Select, Where, Scan, Throttle, etc.
│   │   ├── Subjects/                         # Async subjects (SubjectAsync, BehaviorSubject, etc.)
│   │   ├── Disposables/                      # Async disposable primitives
│   │   └── Internals/                        # AsyncGate, Result, Optional<T>, etc.
│   └── Internal/                             # Internal helpers (EnumerableIList, etc.)
│
└── ReactiveUI.Extensions.Tests/              # Unit tests (net8.0;net9.0;net10.0)
    ├── Async/                                # Async operator tests
    │   ├── ParityOperatorTests.cs
    │   ├── ParityAndInfrastructureCoverageTests.cs
    │   ├── CombineLatestOperatorTests.cs
    │   └── ...
    └── ReactiveExtensionsTests.cs            # Sync operator tests
```

### Target Frameworks

- **Library:** `net8.0;net9.0;net10.0;net462;net472;net481`
- **Tests:** `net8.0;net9.0;net10.0` only
- `EnableWindowsTargeting` is set so .NET Framework targets compile on all platforms (Linux, macOS, Windows)

### Key Design Patterns

- **Async operators use `extension<T>` syntax** (C# preview feature) for instance-style extension methods
- **`ObservableAsync` partial class** — operators are split across files, all contributing to the same partial class
- **`static` lambdas** used throughout for zero-allocation delegates where no captures are needed
- **Manual `for` loops over `IReadOnlyList<T>`** preferred over LINQ in hot paths to avoid enumerator allocations
- **`IReadOnlyList<T>`** preferred over `IList<T>` for snapshot/immutable collection return types

## Code Style & Quality Requirements

**CRITICAL:** All code must comply with ReactiveUI contribution guidelines: https://www.reactiveui.net/contribute/index.html

### Style Enforcement

- EditorConfig rules (`.editorconfig`)
- StyleCop Analyzers - builds fail on violations
- Roslynator Analyzers - additional code quality rules
- **All public APIs require XML documentation comments**

### C# Style Rules

- **Braces:** Allman style
- **Indentation:** 4 spaces, no tabs
- **Fields:** `_camelCase` for private/internal
- **Visibility:** Always explicit, visibility first modifier
- **Namespaces:** File-scoped preferred
- **Modern C#:** Nullable reference types, pattern matching, `static` lambdas, collection expressions
- **Language version:** `preview` (enables `extension<T>` syntax and latest features)

## What to Avoid

- **LINQ in hot paths** - use manual `for` loops over indexed collections to avoid enumerator allocations
- **`IList<T>` for return types** - use `IReadOnlyList<T>` for immutable snapshots
- **`List<T>` when size is known** - use arrays directly
- **Locking on arbitrary objects** - use dedicated `Lock` (NET9+) or `object` lock fields
- **`CancellationTokenSource.CreateLinkedTokenSource` in hot paths** - create once at subscription time, not per-emission
- **Mocking in integration tests** - prefer real implementations

## Important Notes

- **Required .NET SDKs:** .NET 8.0, 9.0, and 10.0
- **Library targets:** net8.0;net9.0;net10.0;net462;net472;net481
- **Test targets:** net8.0;net9.0;net10.0
- **No shallow clones:** Repository requires full clone for Nerdbank.GitVersioning
- **SA1201 warning:** The `extension<T>` preview syntax causes a false-positive SA1201 from StyleCop — this is expected and unavoidable
