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
│   └── Internal/                             # Internal helpers (ArgumentExceptionHelper, Heartbeat, etc.)
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

### Style guide reference

The full coding-style guide — Allman braces, `_camelCase` privates,
file-scoped namespaces, expression-bodied members, modern pattern
matching, every other Visual-Studio-default tweak — lives in
[CONTRIBUTING.md](CONTRIBUTING.md). Follow that document when writing
or editing code in this repo. The performance rules below extend those
style rules; when the two could disagree, **the perf rule wins inside
`src/ReactiveUI.Extensions/`**.

### C# Style Rules (quick summary)

- **Braces:** Allman style
- **Indentation:** 4 spaces, no tabs
- **Fields:** `_camelCase` for private/internal
- **Visibility:** Always explicit, visibility first modifier
- **Namespaces:** File-scoped preferred
- **Modern C#:** Nullable reference types, pattern matching, `static` lambdas, collection expressions
- **Language version:** `preview` (enables `extension<T>` syntax and latest features)
- **US English** in identifiers, XML docs, comments, log messages, commit messages

## Performance & Idiomatic C# Rules

These rules apply to **production code** (everything under
`src/ReactiveUI.Extensions/`). Test projects
(`src/ReactiveUI.Extensions.Tests/`) are exempt from the allocation-
discipline rules — `foreach`, LINQ, and capacity-less `List<T>` are fine
in tests where readability beats micro-optimization. The pattern-
matching, switch-expression, and list-pattern rules still apply to tests
because they're style, not perf.

### System.Reactive minimization

- **`System.Reactive` is allowed only for BCL-level contracts** —
  `IObservable<T>`, `IObserver<T>`, `IScheduler`,
  `CompositeDisposable`, `SerialDisposable`,
  `SingleAssignmentDisposable`, `Unit`, `Notification<T>`. Every
  operator we ship (`Select`, `Where`, `CombineLatest`, `Merge`,
  `Throttle`, `Scan`, etc.) is **our own implementation** under
  `Operators/` (sync) or `Async/Operators/` (async).
- **`System.Reactive.Linq.Observable.*` is banned in production code
  paths** outside thin BCL bridges. If a feature feels like it needs
  `Observable.Foo`, add our own operator with the allocation profile we
  want.
- **The async pipeline is fully independent** — `IObservableAsync<T>` /
  `IObserverAsync<T>` / `ObservableAsync` and the async subjects /
  disposables do **not** depend on `System.Reactive` for operator
  semantics. Crossings into / out of `IObservable<T>` live in
  `Async/Bridge/` and are the only sanctioned bridges.
- **Don't rebadge `System.Reactive` types 1:1.** Replacements must be
  tailored, low-allocation, perf-focused, and only as thread-aware as
  needed. A subject that mirrors `Subject<T>` in shape without
  specializing the locking, snapshot semantics, or async observer
  contract is worse than not writing it.

### Allocation discipline

- **Zero-LINQ policy in production code.** No `System.Linq` under
  `src/ReactiveUI.Extensions/`. Use plain `for` loops.
- **`for` over `foreach`.** Indexed `for` over arrays / `Span<T>` /
  `ReadOnlySpan<T>` / `IReadOnlyList<T>` / `List<T>`. `foreach` only
  when the type genuinely lacks an indexer (`HashSet<T>`,
  `IAsyncEnumerable<T>`).
- **Arrays over `List<T>`** when the final length is known up front.
  When `List<T>` is unavoidable, **always pass a capacity** —
  `new List<T>(expectedCount)`, never capacity-less.
- **`IReadOnlyList<T>` for snapshot return types**, not `IList<T>`.
- **Avoid `ImmutableArray<T>` / `ImmutableList<T>` on hot paths.** The
  wrapping struct adds an indirection on every read.
- **Collection expressions `[..]` first.** `[a, b, ..tail]`, `[]`,
  `[..source]` over `new[] { ... }`, ad-hoc array construction, and
  `.ToArray()`.
- **`static` lambdas wherever no capture is needed.**
  `static (state, x) => ...` — pass captured data through a tuple-state
  argument, not closure capture.
- **Pre-size `Dictionary` / `HashSet`** with a capacity hint.
- **Pool transient buffers.** `ArrayPool<T>.Shared.Rent` paired with a
  `try` / `finally` `Return`.
- **`SearchValues<T>`** for repeated multi-character searches; cache
  as `private static readonly`.
- **`FrozenDictionary` / `FrozenSet`** only when a table is built once
  at startup and read many times across pages / workers. Don't reach
  for `Frozen*` on per-subscription / per-instance / short-lived
  tables — the freeze cost dominates.
- **`StringComparer.Ordinal` / `StringComparison.Ordinal`** on every
  dictionary / set / `string.Equals` keyed on identifiers, type
  names, operator names. Culture-aware is wrong and 5-10× slower.

### Async & concurrency

- **`ConfigureAwait(false)` on every library `await`.** No exceptions
  in production code.
- **No sync-over-async** — never `.GetAwaiter().GetResult()`, `.Result`,
  or `.Wait()` inside async operators.
- **`ValueTask` first when zero-alloc is proven; `Task` otherwise.**
  Default *return type* on a new async-pipeline contract is `ValueTask`
  whenever most implementations complete synchronously and the call
  site multiplies (per-emission). Use `Task` only when the path is
  genuinely async-dominant. Obey the consume-once rule.
- **Sync impls return `ValueTask.CompletedTask` / `Task.CompletedTask`.**
  No state machine, no allocation.
- **Cancellation flows through.** Async operators accept a
  `CancellationToken` and pass it down; never swallow, never default
  to `CancellationToken.None` when a real one is in scope.
- **`CancellationTokenSource.CreateLinkedTokenSource` belongs at
  subscribe time, not per-emission.**
- **`Interlocked.Increment` / `Interlocked.Decrement`** for simple
  counters under contention. Reserve `lock` for genuine multi-field
  invariants.
- **`System.Threading.Lock` (NET9+) is the default monitor primitive**
  for new code. Older TFMs fall back to a `private readonly object
  _gate = new();` — never lock on `this`, `typeof(X)`, or public
  fields.

### Pattern matching & flow control

- **Invert `if`s to flatten the happy path.** Guard clauses + early
  `return` / `continue`; no `else` on guarded branches.
- **Switch expressions over `if` / `else` chains** with property,
  positional, and list patterns.
- **List patterns for emptiness / cardinality.** `is [_, ..]` over
  `.Count > 0`; `is []` for empty; `is [var single]` to bind a
  singleton.
- **`is` / `is not` patterns over `==` / `!=`** for null and type
  checks.
- **Avoid `while (true)`.** Termination condition in the loop header
  unless the work is genuinely infinite (cancellation-bound pump).

### API shape

- **No default parameter values.** Provide explicit overloads. Defaults
  bake into every caller's call-site IL and obscure refactors.
- **Concrete collection types** where practical — `IReadOnlyList<T>` /
  `T[]` / `Dictionary<K,V>` / `HashSet<T>` over `IEnumerable<T>` for
  parameters and returns. `IEnumerable<T>` only when streaming
  genuinely avoids materializing the full sequence.
- **Pin the latest non-beta NuGet version** when adding to
  `Directory.Packages.props`; never `-preview` / `-rc` / `-alpha` /
  `-beta`.

### Properties

- **C# 14 `field` keyword by default** when a property needs a backing
  field with extra logic. Reach for an explicit `_name` field only
  for `ref`-passing APIs (`Interlocked`, `Volatile`, `Unsafe.As`),
  constructor bypass, or when storage is referenced outside the
  accessors. Document the reason with a one-line comment.

### Exception helpers

- **Exception helpers compose their own messages.** `ThrowIfNull`-style
  helpers use `[CallerArgumentExpression]` + `[CallerMemberName]`
  internally. Call sites pass only the value — never the `nameof`.

### Type design

- **`sealed` every class** that isn't designed for inheritance.
- **`readonly record struct`** for small immutable shapes (≤ 4-5
  fields or only references).
- **Most methods static.** A method that doesn't touch `this` is
  `static`. Reserve instance methods for outer-layer types holding
  per-instance state. Static-only classes get `static class`.
- **Singleton comparers** (`public static readonly XComparer
  Instance`) instead of allocating a fresh comparer / lambda per
  call.
- **Bundle long parameter lists into a `readonly record struct` or
  `ref struct`** rather than splitting the method.
- **One type per file.**

### Suppressions

- **Fix the code, don't silence the rule.** Refactor the call site
  rather than reaching for an attribute.
- When suppression is genuinely correct, attach a per-symbol
  `[SuppressMessage("Category", "RuleId", Justification = "...")]`
  with a real reason. Project-wide `<NoWarn>` is acceptable only for
  bulk patterns scoped to a project and must carry a comment in the
  `.csproj` explaining the scope.
- **SA1201 from `extension<T>` is the one accepted global false
  positive.** Do not invent new project-wide suppressions on the same
  rule for other reasons.

### Tests

- **Prefer real implementations over mocks** in integration tests.
  Mocked tests passing while a real implementation breaks is the
  failure mode we want to avoid.

## What to Avoid (quick checklist)

- **LINQ in production code paths** - use manual `for` loops over indexed collections
- **`System.Reactive.Linq.Observable.*` operators in production** - use our own operators
- **`IList<T>` for return types** - use `IReadOnlyList<T>` for immutable snapshots
- **`List<T>` when size is known** - use arrays directly
- **Locking on arbitrary objects** - use dedicated `Lock` (NET9+) or `object` lock fields
- **`CancellationTokenSource.CreateLinkedTokenSource` in hot paths** - create once at subscription time, not per-emission
- **Mocking in integration tests** - prefer real implementations
- **Default parameter values** - use explicit overloads
- **Capacity-less `new List<T>()`** - always pass an expected-count capacity

## Commit messages — Conventional Commits 1.0.0

Use the [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/) shape on every commit. Full type table and worked examples (including the `perf` shape with benchmark numbers in the body) are in [CONTRIBUTING.md](CONTRIBUTING.md#commit-style).

## Important Notes

- **Required .NET SDKs:** .NET 8.0, 9.0, and 10.0
- **Library targets:** net8.0;net9.0;net10.0;net462;net472;net481
- **Test targets:** net8.0;net9.0;net10.0
- **No shallow clones:** Repository requires full clone for MinVer (it walks history to the most recent `v*` tag to compute the version)
- **Versioning:** MinVer derives the version from the most recent `v*` git tag. Untagged commits build as `{floor}.0-alpha.0.{height}` (floor lives in `src/Directory.Build.props` as `MinVerMinimumMajorMinor`). Releases tag `vX.Y.Z` and the build picks it up automatically.
- **SA1201 warning:** The `extension<T>` preview syntax causes a false-positive SA1201 from StyleCop — this is expected and unavoidable
