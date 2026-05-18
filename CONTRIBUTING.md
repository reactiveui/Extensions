# Contributing to ReactiveUI.Extensions

Thanks for your interest in contributing. This document is the
human-readable digest of how we build the project: coding style,
engineering rules, build / test commands. `CLAUDE.md` is the companion
machine-facing rule book — both must agree; if they diverge, `CLAUDE.md`
wins for hot-path detail and this doc wins for narrative.

## Getting set up

Prerequisites: .NET 8.0, 9.0, and 10.0 SDKs installed side-by-side. No
shallow clones — `Nerdbank.GitVersioning` walks the full history.

```sh
cd src
dotnet restore ReactiveUI.Extensions.slnx
dotnet build   ReactiveUI.Extensions.slnx -c Release
dotnet test    --project ReactiveUI.Extensions.Tests/ReactiveUI.Extensions.Tests.csproj -c Release
```

Tests use **Microsoft Testing Platform (MTP) + TUnit** (not VSTest).
See [CLAUDE.md](CLAUDE.md#test-commands-microsoft-testing-platform) for
the full command set including treenode-filter syntax and coverage runs.

## Engineering rules

Code under `src/ReactiveUI.Extensions/` follows every rule below. Test
projects (`src/ReactiveUI.Extensions.Tests/`) relax the allocation rules
but keep the style and pattern-matching rules.

### Scope and System.Reactive dependency

- **Minimize the System.Reactive surface area.** This library targets
  the hot path of UI reactive code. `System.Reactive` is allowed where
  the BCL contract genuinely lives there (`IObservable<T>`,
  `IObserver<T>`, `IScheduler`, `CompositeDisposable`,
  `SerialDisposable`, `SingleAssignmentDisposable`, `Unit`,
  `Notification<T>`), but **every operator and combinator we ship is
  our own implementation** — `Select`, `Where`, `CombineLatest`,
  `Merge`, `Throttle`, `Scan`, etc. No `System.Reactive.Linq.Observable`
  in production code paths outside thin bridges.
- **Don't rebadge `System.Reactive` types 1:1.** Our replacements must
  be tailored, low-allocation, perf-focused, and only as
  thread-aware-as-needed. A `SubjectAsync<T>` that mirrors `Subject<T>`
  in shape without specializing the locking, the snapshot semantics, or
  the async observer contract is worse than not writing it.
- **The async pipeline is fully independent.** `IObservableAsync<T>` /
  `IObserverAsync<T>` / `ObservableAsync` / the async subjects and
  disposables are self-contained — they do **not** depend on
  `System.Reactive` for operator semantics. Bridges into and out of
  `IObservable<T>` live in `Async/Bridge/` and are the only sanctioned
  crossing point.
- **Audit new dependencies on `System.Reactive` before adding them.** If
  a feature feels like it needs an `Observable.Foo` from
  `System.Reactive.Linq`, the answer is almost always to add our own
  operator that ships with the allocation profile we want. Reach into
  `System.Reactive.Linq` only when wrapping for the public
  `IObservable<T>` BCL contract at the API edge.

### Architecture

- **`extension<T>` syntax for async operators.** C# preview
  `extension<T>` syntax is used so async-pipeline operators read as
  instance methods on `IObservableAsync<T>`. The SA1201 false positive
  it triggers is expected and accepted; don't suppress with NoWarn at
  project scope.
- **`ObservableAsync` partial class.** Each operator file contributes
  to the same partial class. One operator per file; file name matches
  operator name (`Select.cs`, `Throttle.cs`).
- **`IReadOnlyList<T>` for snapshots, not `IList<T>`.** Snapshot /
  immutable return shapes use `IReadOnlyList<T>` so the renderer
  treats it as immutable by convention.

### Async-first (the async pipeline)

- **Async-first, end to end.** Every operator on `IObservableAsync<T>`
  is `Task` / `ValueTask`-aware. There is **no** sync-over-async:
  never `.GetAwaiter().GetResult()`, never `.Result`, never `.Wait()`
  inside async operators.
- **`ValueTask` first when zero-alloc is proven; `Task` otherwise.**
  Use `ValueTask` whenever most implementations complete synchronously
  and the call site multiplies (per-emission paths). Use `Task` only
  when the path is genuinely async-dominant (I/O, network, scheduler
  trampolines that always trampoline). Obey the consume-once rule for
  `ValueTask`: never `await` the same instance twice, never store it
  in a field.
- **Sync impls return cached completed tasks.** `=> ValueTask.CompletedTask`
  (or `Task.CompletedTask`) when there is nothing async to do. No
  state machine, no allocation.
- **`ConfigureAwait(false)` on every library `await`.** No exceptions
  in production code. Tests don't need it.
- **Cancellation flows through.** Every async operator accepts a
  `CancellationToken` where the contract supports it and passes it
  down — never swallow, never default to `CancellationToken.None`
  when a real one is in scope.
- **`CancellationTokenSource.CreateLinkedTokenSource` belongs at
  subscribe time, not per-emission.** Create the linked source once
  when the subscription is established; reuse it for every value.

### Allocation discipline

- **Zero-LINQ policy in production code.** No `System.Linq` in
  `src/ReactiveUI.Extensions/`. LINQ pulls in lambdas + iterators on
  every call. Use plain `for` loops.
- **`for` over `foreach`.** Indexed `for` over arrays / `Span<T>` /
  `ReadOnlySpan<T>` / `List<T>` / `IReadOnlyList<T>`. `foreach` only
  when the type genuinely lacks an indexer (`HashSet<T>`,
  `IAsyncEnumerable<T>`).
- **Arrays over `List<T>`** when the final length is known up front.
  Pre-size and write by index. When `List<T>` is unavoidable, **always
  pass a `capacity` to the constructor** (`new List<T>(expectedCount)`);
  never capacity-less.
- **Avoid `ImmutableArray<T>` / `ImmutableList<T>` on hot paths.** The
  wrapping struct adds an indirection on every read and the builder
  churns intermediate arrays. Reach for an immutable collection only
  when the API is genuinely public and consumers must not mutate.
  Otherwise expose `IReadOnlyList<T>` / `T[]` and treat it as
  immutable by convention.
- **Collection expressions `[..]` first.** `[a, b, ..tail]`, `[]`,
  `[..source]` for final materialization. Never `.ToArray()` when a
  collection expression does the job.
- **`static` lambdas everywhere there is no capture.** `static (x) => ...`
  / `static (state, x) => ...` lets the JIT skip the closure object.
  Pass any captured state through a tuple argument rather than closing
  over locals.
- **Pre-size `Dictionary` / `HashSet`** with a capacity hint that
  reflects the expected size.
- **Pool transient buffers.** `ArrayPool<T>.Shared.Rent` paired with a
  `try` / `finally` `Return` for transient byte / char / object
  buffers in operator chains that allocate per-emission.
- **`Interlocked.Increment` / `Interlocked.Decrement`** for simple
  counters under contention. Reserve `lock` for genuine multi-field
  invariants.
- **`System.Threading.Lock` (NET9+) is the default monitor primitive**
  for new code that genuinely needs a private gate around shared
  mutable state: `private readonly Lock _gate = new();` and
  `lock (_gate)`. On `net8.0` / `net462` / `net472` / `net481` we fall
  back to a `private readonly object _gate = new();` — the multi-TFM
  conditional is hidden behind a `Lock`-style helper where the
  call-site readability matters.
- **No locks on arbitrary objects** (`this`, `typeof(X)`, public
  fields). Always a dedicated `_gate`-style field.

### Pattern matching & flow control

- **Invert `if`s to flatten the happy path.** Guard clauses + early
  `return` / `continue` first; main logic stays unindented. No `else`
  clauses on guarded branches.
- **Switch expressions over `if` / `else` chains** — use property
  patterns (`{ HasValue: true }`), positional patterns, and list
  patterns.
- **List patterns for emptiness / cardinality.** `is [_, ..]` over
  `.Count > 0`. `is []` for empty. `is [var single]` to bind a
  single-element collection in one shot.
- **`is` / `is not` patterns over `==` / `!=`** for null and type
  checks. Combine type test + property check in one line:
  `notification is { Kind: NotificationKind.OnNext } onNext`.
- **Choose the most-pattern-matching control flow available.** Order
  of preference: switch expression → switch statement → list of
  `if` / `else if` chains. Reach for the next form only when the
  prior one cannot express the dispatch (mutating `ref` / `out`
  values, side-effects in branches, fall-through).
- **Avoid `while (true)`.** Every loop must have its termination
  condition expressed in the loop header. The exception is genuinely
  infinite work (an event pump exiting via cancellation token); and
  even those should be `while (!cancellationToken.IsCancellationRequested)`.

### API shape

- **No default parameter values.** Provide explicit overloads instead.
  Default values bake the constant into every caller's call-site IL —
  bumping it later requires a recompile of every consumer. One overload
  per legal call shape; each one delegates to the most-specific
  overload that takes everything explicitly.
- **Concrete collection types in production APIs** where practical.
  Prefer `IReadOnlyList<T>` / `T[]` / `Dictionary<K,V>` /
  `HashSet<T>` over `IEnumerable<T>` for parameters and return
  values. `IEnumerable<T>` is allowed only when a streaming
  yield-based shape genuinely avoids materializing the full sequence
  (rare in this codebase — most Rx operators emit one value at a time
  via the observer contract, which is already streaming).
- **Pin the latest non-beta version** when adding to
  `Directory.Packages.props`. Check
  `https://api.nuget.org/v3-flatcontainer/<lower-cased-id>/index.json`
  for the highest stable release; never `-preview` / `-rc` /
  `-alpha` / `-beta`. Same rule for bumps.

### Type design

- **`sealed` every class** that isn't designed for inheritance. The
  default in this repo. Helps inlining and avoids accidental override
  surface.
- **`readonly record struct`** for immutable value-shaped data: small
  (≤ 4-5 fields) or holding only references. Equality and hashing
  come for free, no GC pressure.
- **`sealed record` (class)** when the record participates in
  inheritance hierarchies or holds many fields.
- **Most methods static.** A method that doesn't touch `this` should
  be `static` — fewer hidden allocations, clearer call sites,
  devirtualization comes free. Reserve instance methods for outer-
  layer types that hold genuine per-instance state (operator
  observable instances, subjects). If a class ends up with only
  static methods, mark the class `static` too.
- **`internal static` helpers** for stateless cross-type utilities.
  Group by responsibility, not by feature. Keep the public surface
  narrow.
- **Singleton comparers** (`private sealed class XComparer : IComparer<T>`
  with `public static readonly XComparer Instance`) instead of
  allocating a fresh comparer / lambda per `Array.Sort` call.
- **Bundle long parameter lists into a `readonly record struct` or
  `ref struct`** rather than splitting the method. State types
  document the relationship between values and let the JIT keep them
  in registers.

### Properties

- **C# 14 `field` keyword by default.** When you need a backing field
  with extra logic (lazy init, validation, change-tracking), use
  `field` inside the property accessors instead of declaring a
  separate `_name` field. Valid reasons to keep an explicit backing
  field:
    - **`ref`-passing APIs** — `Interlocked.Increment(ref _counter)`,
      `Volatile.Read(ref _state)`, `Unsafe.As<T>(ref _slot)`.
      Document with a one-line comment when used.
    - **Constructor assignment that must bypass setter logic.**
    - **Storage referenced from a method outside the property** (rare).

### Exception helpers

- **Exception helpers compose their own messages.** `ThrowIfNull`-style
  helpers use `[CallerArgumentExpression]` + `[CallerMemberName]` to
  build the message internally. Call sites pass only the value:
  `Guard.ThrowIfNull(observer)`, never
  `Guard.ThrowIfNull(observer, nameof(observer))`. Single source of
  truth for the formatting; one-line call sites.

### Span / search APIs

- **`SearchValues<T>` for repeated multi-character searches.** Cache
  as `private static readonly SearchValues<char>` and pass to
  `IndexOfAny` / `IndexOfAnyExcept`. Faster than `IndexOfAny([...])`
  for any call site hit more than once.
- **`Span<char>` / `ReadOnlySpan<char>` + range expressions** for
  prefix checks, slicing, parsing — never allocate a temporary
  `string` to call `.StartsWith` / `.Substring`.
- **`TryFormat` / `TryParse` over `ToString` / `Parse`** when writing
  into a span buffer — skips the intermediate string allocation.

### Read-mostly lookups

- **`FrozenDictionary<TKey,TValue>` / `FrozenSet<T>`** for tables
  built once at startup and read many times. Build with
  `ToFrozenDictionary(StringComparer.Ordinal)`.
  **Don't** reach for `Frozen*` for per-instance, short-lived, or
  per-subscription tables — the freeze cost will dominate the work
  the table actually does. The four-test rule: built once → queried
  many times → read-only after construction → genuinely hot.
- **Always pass `StringComparer.Ordinal`** to dictionaries / sets
  keyed on identifiers, type names, operator names. Default
  culture-aware comparison is wrong here and 5-10× slower. Same for
  `StringComparison.Ordinal` on `string.Equals` / `IndexOf`.

### Style

- **US English in identifiers and prose.** Type names, member names,
  XML docs, comments, log / diagnostic messages, and commit messages
  use US spelling. Examples: `Normalize` (not the UK `-ise` form),
  `Serialize`, `Initialize`, `Color`, `Behavior`. The `.editorconfig`,
  analyzer rule names, and the BCL itself are US English; mixing
  dialects splits casing across tooling search.
- **XML doc comments describe the contract, not the implementation.**
  Every public *and* internal type, method, property, parameter, and
  return gets a `<summary>` / `<param>` / `<returns>` that answers
  "what is this for?" — the consumer-facing contract. Implementation
  detail (which collection backs it, what algorithm is used, table
  sizes, hashing strategy, GC behavior, whether it's pooled / cached
  / lazy, "wraps Z" phrasings) does **not** belong in `<summary>` and
  rarely belongs anywhere.
- **`<remarks>` is reserved for genuinely confusing implementation
  detail** — a non-obvious algorithm, a workaround for a specific
  bug, a perf-driven choice that would surprise a reader. **Not** a
  place to dump "stores X as Y" sentences. Most types should have
  **no** `<remarks>` at all.
- **Multi-line XML doc content puts text between newline-delimited
  tags.** Opening and closing tags sit on their own lines, content
  lines wrap between them. Never let a single tag-and-content line
  stretch past 200 chars (S103 will trip). Single-line summaries
  stay single-line.
- **`var` for locals; targeted `new()` for typed slots.** Always
  `var x = ...` for locals; never repeat the type on both sides.
  `Foo x = new Foo()` is banned — use `var x = new Foo()` or
  `Foo x = new(...)` depending on which side the type already lives
  on.
- **Expression-bodied members whenever possible.** Single-expression
  methods, properties, indexers, operators, and constructors collapse
  to `=>`-form.
- **One type per file.** Splits matching the StyleCop default keep
  greps fast and diffs small.
- **Prefer newer C# syntax by default.** When a newer language form
  is available and doesn't introduce a perf or allocation penalty,
  use it. The bar for staying with older syntax is not "this is
  familiar"; it's "the newer form materially hurts the generated
  code or makes the intent harder to follow in this specific case".

### Analyzers and suppressions

- **Fix the code, don't silence the rule.** The analyzer set
  (StyleCop, Roslynator, .NET CA, plus the ReactiveUI conventions)
  catches real perf and correctness issues; suppressing is the last
  resort.
- When suppression is genuinely correct, attach a per-symbol
  `[SuppressMessage("Category", "RuleId", Justification = "...")]`
  with a real reason. Project-wide `<NoWarn>` is acceptable only for
  bulk patterns scoped to a project (e.g. across an entire test
  project) and must carry a comment in the `.csproj` explaining the
  scope.
- **SA1201 from `extension<T>` is the one known false positive** that
  we accept globally — it fires on the preview syntax. Do not invent
  new project-wide suppressions on the same rule for other reasons.

### Tests

- **TUnit + MTP** under `src/ReactiveUI.Extensions.Tests/`. Treat
  tests as documentation — the names and asserts should communicate
  the contract.
- **Prefer real implementations over mocks** in integration tests.
  Mocked tests passing while a real implementation breaks is the
  failure mode we want to avoid.
- **Test allocation rules are relaxed.** `foreach`, LINQ, capacity-
  less `List<T>` are fine in tests where readability beats
  micro-optimization. The pattern-matching, switch-expression, and
  list-pattern style rules still apply because they're style, not
  perf.

## C# style guide

Baseline is "Visual Studio defaults plus the rules above". The rules in
the [Engineering rules](#engineering-rules) section take precedence when
this list and that list disagree (e.g. our project bans default
parameter values; the general C# guide allows them).

- **Allman braces.** Each `{` on its own line.
- **Four-space indentation, no tabs.**
- **`_camelCase`** for `internal` / `private` instance fields; mark
  them `readonly` whenever possible. `static readonly` (in that
  order) for static fields.
- **Avoid `this.`** unless absolutely necessary.
- **Always specify visibility**, even when it's the default.
  Visibility comes first in the modifier list (`public abstract`, not
  `abstract public`).
- **File-scoped namespaces** (`namespace Foo;`).
- **One blank line at most** between members. Never two consecutive
  blank lines.
- **Usings outside the namespace.** System namespaces first
  (alphabetical), then third-party (alphabetical). `global using` only
  when a prefix appears in nearly every file in the project — see
  `src/ReactiveUI.Extensions/Usings.cs` for the curated list.
- **Language keywords over BCL types** (`int`, `string`, `float`,
  `int.Parse`).
- **`PascalCase` for constants** (`const`, `static readonly` value
  fields, and `private const` locals).
- **`nameof(...)`** instead of literal strings for member references
  whenever possible.
- **Fields at the top of the type declaration**, then constructors,
  then properties, then methods. StyleCop enforces this order.
- **Nullable reference types are on** project-wide; honor the
  warnings.
- **Range / index expressions** (`x[..n]`, `x[^1]`) for slicing.
  `Substring`, `Skip`, `Take` are last-resort.
- **`using` declarations** instead of nested `using` blocks where the
  scope reaches the end of the enclosing block.
- **Static local functions** when a local function captures nothing.
  Marks intent and stops accidental closure capture.
- **`record` / `record struct`** for data-centric types with value
  semantics. `readonly record struct` is the default for small
  immutable shapes.
- **`init`-only setters** for properties that should only be set
  during initialization when a record is the wrong shape.
- **Raw string literals** (`""" ... """`) for multi-line / regex /
  JSON content; never escape-soup.
- **`required` modifier** on members that must be set during
  initialization.
- **Method groups** (`list.ForEach(Console.WriteLine)`) are encouraged
  over equivalent lambdas.

The repository ships an `.editorconfig` that enforces most of the
above. If a rule fires you don't expect, the rule wins — don't suppress
it without a documented Justification.

## Commit style

We follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/)
so the `git log` is mechanically scannable and tools (release-notes
generators, bots) can group changes by intent.

### Format

```
<type>(<optional scope>): <subject>

<body>

<footers>
```

### Types

| Type | When to use |
|---|---|
| `feat` | A new user-visible feature (a new operator, a new subject, a new bridge). |
| `fix` | A bug fix. |
| `perf` | A change that improves performance — typically backed by a benchmark number in the body. |
| `refactor` | An internal restructure that doesn't change behavior. |
| `docs` | README / CONTRIBUTING / xmldoc / inline-comment changes. |
| `test` | Adding or fixing tests, with no production-code change. |
| `build` | Changes to the build system, MSBuild props, NuGet packaging. |
| `ci` | Changes to GitHub Actions / Dependabot / Renovate config. |
| `chore` | Anything that doesn't fit above (lockfile bumps, repo housekeeping). |
| `revert` | Reverting an earlier commit. |

### Scope

Scope is the affected operator family or subsystem, lowercase, no
`ReactiveUI.Extensions.` prefix. Examples: `async`, `async.subjects`,
`async.disposables`, `combine-latest`, `throttle`, `bridge`, `build`.
Omit the scope when the change spans many areas in roughly equal
measure.

### Subject

- ~70 characters max, imperative mood (`add`, `fix`, `cut`), lowercase
  initial letter.
- Don't end with a period.
- A single-sentence summary of *what changed and why it matters*.

### Body

- Explains the *why*, not the *what* (the diff shows the what).
- Wraps at ~80 chars.
- For `perf` commits, include benchmark numbers (before / after,
  scenario, allocation delta) so reviewers can trust the win is real.
- Describe constraints, alternatives considered, follow-ups.

### Footers

- `BREAKING CHANGE: <text>` (or `!` after the type — e.g.
  `feat(async)!:`) for any change that alters a public API.
- Reference task numbers from the project's issue tracker
  (`Closes #123`, `Refs #456`).

### Examples

```
perf(async.combine-latest): cut per-emission alloc 96 B → 0 B

Replace the per-emission ToArray() snapshot of source observables with
an indexed read over the cached IReadOnlyList<IObservableAsync<T>>;
the array lifetime now matches the subscription, not the emission.
Verified on CombineLatestArityTests bench: 124 ns → 71 ns, alloc
96 B → 0 B.

Refs #210
```

```
feat(async.subjects): add ReplaySubjectAsync<T> with bounded capacity

Mirrors the windowing semantics of Subject<T> with a ring buffer and
an async-safe replay path. Single Lock gate around append + snapshot;
SubscribeAsync replays under the gate before handing the cursor to
the consumer.
```

```
refactor(async.operators): split ObservableAsync.Combine into
per-operator files

Was: every CombineLatest / Zip / Merge variant lived in one 800-line
partial. Now: one file per operator family, partial-class contribution
preserved. No behavior change.
```

## Reporting issues / proposing features

Open a GitHub issue with a minimal repro for bugs; for features,
describe the use case and prior art (System.Reactive operator
equivalent, if any) before sending a PR.
