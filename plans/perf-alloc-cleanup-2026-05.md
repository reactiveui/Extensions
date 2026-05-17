# Perf / Allocation Cleanup Plan

Source date: 2026-05-16. Audit gathered by 5 read-only sub-agents across
`Async/Operators/`, `Async/Subjects + Internals + Disposables + Observables +
Bridge/`, sync `Operators/` + root files, the cross-cutting `Internal/`
directory, and a dedicated System.Reactive dependency sweep.

This is an **execution plan**, not just a list — items are grouped into
self-contained work packets, ordered by user-visible benefit, with explicit
notes on what is and isn't acceptable to change.

---

## Background and scope

**Operating principle for this plan:** we don't care about churning
non-user-facing code. Internals — observer classes, subscription state
machines, queue payloads, gate primitives, struct vs class shapes,
file-organization — are all fair game for aggressive rewrites if the user
sees perf or allocation wins. Public surface stability is the only hard
constraint.

What that means concretely:

- **`IObservable<T>` / `IObserverAsync<T>` / `IScheduler` / `IAsyncDisposable`
  / `IDisposable` / `Unit` / `Notification<T>`** — public contracts that
  callers depend on. Don't rename, don't change shape, don't remove.
- **Public method signatures and method names** on `ObservableAsync`,
  `ReactiveExtensions`, `ObserverExtensions`, `SubjectAsync`, etc. —
  don't break callers. Adding new overloads is fine; changing return
  types or parameter shapes is not.
- **Everything else is in scope.** Internal classes, internal subscription
  types, internal observer base classes, internal disposables, the entire
  `Internal/` and `Async/Internals/` directories, helper queue/buffer
  shapes, struct-vs-class choices on internal POD types, file splits,
  per-operator subscription class designs, gate primitives, etc. — all
  free to redesign for perf wins.

The audits explicitly confirm the async pipeline is already independent of
`System.Reactive.Linq.*` (rule honored in `Async/**`); the remaining
System.Reactive surface area lives entirely on the synchronous `IObservable<T>`
side, where replacing internal usage is also non-breaking because every call
site returns a plain `IObservable<T>` that consumers see only through that
BCL contract.

---

## Packet 1 — `ConfigureAwait(false)` sweep (mechanical, high correctness payoff)

**Scope:** every `await` in library code without `.ConfigureAwait(false)`.
Approximately 49 operator files plus `Subjects/`, `Internals/MulticastObservableAsync.cs`,
`Internals/CancelableTaskSubscription{T}.cs`, `Internals/SubscriptionHelper.cs`,
`Async/Bridge/`.

**Why now:**

- **Correctness:** under UI synchronization contexts, missing `ConfigureAwait(false)`
  causes continuations to hop back to the captured context — extra thread
  marshalling and a class of subtle deadlocks our consumers will hit eventually.
- **Perf:** every `await` without it captures and restores `SynchronizationContext`
  and (on most paths) `ExecutionContext`. On observer-callback paths that run
  per emitted value, that's measurable.

**Execution:**

- Mechanical. One sub-agent can sweep with explicit per-file edits;
  CONTRIBUTING.md already states this is mandatory in production code.
- `await using (await _gate.LockAsync(...))` — append `.ConfigureAwait(false)`
  on the inner await; the `using` form keeps working.

**User impact:** correctness + reduced continuation overhead on every
emission for the affected operators.

---

## Packet 2 — Per-emission `CancellationTokenSource.CreateLinkedTokenSource` elimination

**Scope:** every operator that allocates a linked CTS inside its `OnNextAsync` /
`OnErrorResumeAsync` / per-value forward path.

Confirmed sites:
- `Async/Operators/Merge.cs:556-573, 581-598`
- `Async/Operators/Delay.cs:84-98`
- `Async/Operators/TakeUntil.cs:448-472, 658-690, 839-870, 1004-1034`
- `Async/Operators/SwitchObservable.cs:189-213, 328-340`
- `Async/Operators/Zip.cs:168, 200`
- All 15 `Async/Operators/CombineLatest{2..16}.cs` — `EmitLatestAsync` and
  `OnErrorResume`
- `Async/Operators/Throttle.cs:206-220`
- `Async/Operators/Timeout.cs:173-187`
- `Async/Subjects/Base/BaseReplayLatestSubjectAsync.cs:72-73, 101-102, 215-216`

**Why now:** rule already documented in `CLAUDE.md` and `CONTRIBUTING.md`:
"`CancellationTokenSource.CreateLinkedTokenSource` belongs at subscribe time,
not per-emission." Per-emission CTS allocation + registration is one of the
most expensive things you can do on a hot path — every call to it touches the
thread-pool work-stealing internals to register the cancellation callback.

**Execution pattern:**

- At subscription construction, cache the linked CTS in a field, expose its
  `Token` as a cached `CancellationToken`. Disposal closes it.
- For each per-emission method that currently builds a fresh linked CTS, build
  the linked-token-source's token *into the call site's CT* via
  `cts.Token.Register(...)` or pass the cached linked token directly.
- For `Throttle` / `Timeout` specifically: net8+ supports
  `CancellationTokenSource.TryReset()`. Use that + `CancelAfter(timeout)` on
  the cached CTS rather than disposing and recreating per emission. Fall back
  to recreate on `TryReset()` failure.
- The generated `CombineLatest{N}.cs` files are templated from
  `/tmp/generate-combine-latest.py` (ephemeral). The fix touches the template
  once and regenerates all 15.

**User impact:** removes one CTS allocation + one callback registration per
emitted value across the busiest operators (`Merge`, `Switch`, every
`CombineLatest` arity, `Delay`, `TakeUntil`, `Throttle`, `Timeout`, replay
subjects). Probably the single biggest measurable per-value alloc win in
this whole plan.

---

## Packet 3 — `Heartbeat<T>` and `Stale<T>` → `readonly record struct`

**Scope:**
- `Internal/Heartbeat.cs` — currently `internal class Heartbeat<T>` with a
  `bool IsHeartbeat` and a `T? Update`. Allocated per emitted heartbeat tick.
- `Internal/Stale.cs` — same shape, `bool IsStale` + `T? Update`. Allocated
  per emission on stale-detection pipelines.

**Why now:** both types are pure POD value-shaped data, never inherited,
never reassigned, always read-only post-construction. The exact case
`readonly record struct` exists for. The interfaces `IHeartbeat<T>` /
`IStale<T>` are public contracts and have to stay — but the implementation
can be a record struct, and consumers using the interface will still work
(boxing on interface dispatch becomes a concern; see plan).

**Execution:**

- Convert both classes to `internal readonly record struct`.
- Audit the interface implementations: if `IHeartbeat<T>` / `IStale<T>` are
  used by consumers via the interface only, boxing on dispatch is per-value
  again — exactly what we're trying to avoid. Two options:
  1. Keep the interface, accept boxing where consumers use the interface
     (most internal call sites use the concrete type directly — confirm
     via grep — and box only at the public boundary).
  2. Change the interface to a value-shaped abstraction: the heartbeat
     operator returns `IObservable<Heartbeat<T>>` directly. Public-API
     decision; cheap.

**User impact:** per-emission allocation on every heartbeat/stale tick
disappears.

---

## Packet 4 — Subjects' broadcast and unsubscribe hot path

**Scope:** `Async/Subjects/Base/` family.

Confirmed issues:
- `BaseSubjectAsync.cs:55-69`, `SerialSubjectAsync.cs:29`,
  `SerialStatelessSubjectAsync.cs:28`, `SerialReplayLatestSubjectAsync.cs:33` —
  broadcast loops `foreach` over `IReadOnlyList<IObserverAsync<T>>` →
  `IEnumerator` allocation per emission. Switch to indexed `for`.
- `BaseSubjectAsync.cs:167-175`, `BaseStatelessSubjectAsync.cs:89-96`,
  `BaseReplayLatestSubjectAsync.cs:242-248` — unsubscribe builds a closure
  via `DisposableAsync.Create(() => { ... Remove(observer); ... })`. Two
  allocs per subscribe. Replace with a sealed `Unsubscriber : IAsyncDisposable`
  class holding `(subject, observer)` fields.
- `Concurrent.cs:49,90,128` — `new Task[count]` + `Task.WhenAll` per
  multicast emission, even when all observers complete synchronously. Probe
  `IsCompletedSuccessfully` first; fall back to `ArrayPool<Task>` when fan-out
  truly needs to wait.
- `BaseReplayLatestSubjectAsync` uses `AsyncGate` (semaphore + AsyncLocal)
  on every `OnNextAsync`. Common case is single-writer UI. Consider a
  `Lock`-based variant for that profile.

**Why now:** subjects are the literal heart of every multicast pipeline.
Every emission walks every observer. Per-emission allocations here compound
multiplicatively with subscriber count.

**Execution:**

- Replace `foreach` with indexed `for` on the broadcast paths.
- Introduce `DisposableAsync.Create<TState>(TState, Func<TState, ValueTask>)`
  overload (also reused by Packet 7).
- Add the `IsCompletedSuccessfully` fast-loop probe to `Concurrent.cs`.
- For replay subjects, introduce a lock-based variant alongside the existing
  gate-based one; subjects pick the right shape via their constructor
  options.

**User impact:** broadcast path is allocation-free in the common case.
Multicast scale-out gets noticeably cheaper.

---

## Packet 5 — Convert thin operators from `Create<T>` + async-lambda to sealed `ObserverAsync<T>` subclasses

**Scope:** `Async/Operators/Select.cs`, `Where.cs`, `Scan.cs`, `Catch.cs`,
plus any others that follow the `Create<T>((observer, token) => @this.SubscribeAsync(async (x, t) => ...))` pattern.

**Why now:** the `Create<T>` + async-lambda shape allocates:
1. The `AnonymousObservableAsync<T>` wrapper from `Create`.
2. A closure object capturing `this`, `selector`, `predicate`, etc.
3. A fresh async state-machine box on every `OnNext` because the inner
   lambda is `async`.

The `Delay.cs` operator already uses the right pattern: a sealed
`ObserverAsync<T>` subclass with `OnNextAsyncCore` etc. The runtime can elide
the state machine when the underlying call completes synchronously
(common case for `Select` and `Where`).

**Execution:**

- For each target operator, write a `*Observer<T>` (or `*Observer<TIn,TOut>`)
  class deriving from `ObserverAsync<T>` and override `OnNextAsyncCore`,
  `OnErrorResumeAsyncCore`, `OnCompletedAsyncCore`.
- Replace the body of the public extension with `new SelectObserver<T,TResult>(observer, selector)` etc.
- Preserve all existing public method signatures.

**User impact:** the busiest pipeline operators stop allocating per-emission
state machines + closures. This is `Select.Where.Select` running clean.

---

## Packet 6 — `CombineLatestEnumerable` snapshot buffer reuse + per-source delegate captures

**Scope:** `Async/Operators/CombineLatestEnumerable.cs`.

Confirmed issues:
- Line 187 / 424-447 — `new T[_values.Length]` snapshot per OnNext.
- Lines 140-152 / 375-390 — `(value, token) => OnNextAsync(currentIndex, value, token)` lambdas captured per source. Three delegate allocations per source × N sources at subscription time, plus the captured-loop-variable closure.

**Why now:** the per-emission array is the dominant allocation. The captured-index lambdas allocate at subscribe-time but matter for high-arity scenarios.

**Execution:**

- Replace the per-emission `new T[_values.Length]` with a reusable buffer held
  on the subscription. The gate already serializes emissions, so a single
  reusable buffer is safe; expose it downstream as `IReadOnlyList<T>`. **One
  caveat:** consumers must not retain the snapshot across emissions. This is
  the existing contract (the projection variant already projects from it
  immediately) but worth documenting on the unprojected overload.
- Replace the captured-index lambdas with a dedicated `sealed class IndexedObserver(parent, index)` so the per-source delegate becomes a method group + a small object — N objects per subscribe instead of 3N delegates + N closure objects.

**User impact:** large-arity `CombineLatest` (the dynamic-enumerable form) becomes per-emission allocation-free.

---

## Packet 7 — `DisposableAsync.Create` state-carrying overload

**Scope:** `Async/Disposables/DisposableAsync.cs` + every call site that
captures locals/`this` in the existing `Create(() => ...)` form.

**Why now:** `DisposableAsync.Create(() => { ... })` is called from every
subject subscribe/unsubscribe, from `MulticastObservableAsync`, from `Bridge/`,
from `Prepend`, and many others. Each call allocates the `Func<ValueTask>`
closure object plus the `AnonymousAsyncDisposable` wrapper.

**Execution:**

- Add `DisposableAsync.Create<TState>(TState state, Func<TState, ValueTask> dispose)` overload.
- Migrate every call site to use a `static` lambda + tuple/typed state. Touches `BaseSubjectAsync`, `BaseStatelessSubjectAsync`, `BaseReplayLatestSubjectAsync`, `MulticastObservableAsync`, `Async/Bridge/ObservableBridgeExtensions.cs:96-100`, `Async/Operators/Prepend.cs`, `Async/Internals/MulticastObservableAsync.cs:72-86`.

**User impact:** ~2 allocations removed per subscribe across most operators. Adds up fast at high subscription churn.

---

## Packet 8 — `Async/Bridge/ObservableBridgeExtensions.cs` `BridgeObserver` work-item struct

**Scope:** `Async/Bridge/ObservableBridgeExtensions.cs` lines 122-189 (the
sync→async bridge queue).

**Why now:** every `OnNext` / `OnError` / `OnCompleted` allocates:
1. Outer `Func<Task>` (the wrapping lambda).
2. Inner `Action` (the queue-payload lambda).
3. A `Task` via `.AsTask()` so the bridge can `GetAwaiter().GetResult()`.

That's 3 allocations per emitted value on the sync-to-async bridge — exactly
the path that fires when a consumer feeds a classic `IObservable<T>` into our
async pipeline.

**Execution:**

- Replace `Queue<Action>` with `Queue<WorkItem>` where `WorkItem` is a
  `readonly struct` carrying `(Kind, T, Exception?)`.
- The drain loop dispatches via a switch instead of invoking a delegate. No
  closures, no `.AsTask()`, no per-emission delegate object.

**User impact:** the sync→async bridge becomes allocation-free per-emission.

---

## Packet 9 — Sync-side System.Reactive replacement

**Scope:** sync `Operators/` directory and `ReactiveExtensions.cs`. Async
pipeline is already clean; this is the remaining System.Reactive surface.

Per the dependency audit:

- `ReactiveExtensions.cs` lines 371-702 — 14× `Observable.Create<T>(observer => ...)` factories for `Schedule*`, `ForEach`, `FromArray`, `TimerCore`, etc. Each subscribe builds an Rx `AnonymousObservable` + a closure capturing parameters. Collapse into 2-3 dedicated `IObservable<T>` classes.
- `ReactiveExtensions.cs:1023, 1030, 1037` — `Not`, `WhereTrue`, `WhereFalse` use `Select`/`Where` with non-static lambdas. Three internal `BoolFilter/BoolMap` observable types.
- `ReactiveExtensions.cs:1227` — `source.Throttle(timeSpan, scheduler)` wrapper. Add our own `ThrottleObservable` next to existing `ThrottleFirstObservable` / `ThrottleDistinctObservable`.
- `ReactiveExtensions.cs:26, 37, 99` — `WhereIsNotNull`, `AsSignal`, `CatchIgnore` use Rx `Where`/`Select`/`Catch`. Three internal observables.
- `Operators/AggregationObservable.cs:24` — uses Rx `CombineLatest` *and* LINQ `values.Max()/Min()`. Double violation (LINQ in production + System.Reactive). Redundant with `MaxObservable` / `MinObservable`; delete or route through them.
- `Operators/ConflateObservable.cs:31` — `source.ObserveOn(scheduler).Subscribe(sink)`. Bake the scheduling into Conflate's own loop.
- `Operators/ThrottleUntilTrueObservable.cs:109` — `Observable.Timer(throttle).Subscribe(...)` per non-matching emission (HOT). Use `IScheduler.Schedule(delay, ...)` + tuple state.
- `Operators/RetryWithDelayObservable.cs:88` — same `Observable.Timer` pattern, per retry. `RetryWithBackoffObservable` already shows the right pattern (`IScheduler.Schedule`).
- `Operators/ReplayLastOnSubscribeObservable.cs:36-39` — `BehaviorSubject<T>` + `CompositeDisposable` allocated per subscribe. Replace with a single-slot replay holder + `DisposableBag`.
- `Operators/FirstMatchFromCandidatesObservable.cs:110` — replace `Disposable.Empty` with `EmptyDisposable.Instance` (our own).

**Why this is internal-only:** every public method involved returns
`IObservable<T>` — a BCL contract. Switching the underlying implementation
type is invisible to consumers. Public method names, signatures, behavior:
unchanged.

**Why now:** removes the largest concentrated System.Reactive footprint and
the redundant allocations that go with it. Each Rx wrapper carries a fully
materialized `AnonymousObservable` + observer chain that we can replace with
purpose-built sealed types.

**Execution:**

- Sub-agent batch by file (similar to the extension-block conversion).
- Each `Observable.Create<T>(observer => body)` call site becomes a sealed
  `internal class XObservable<T>(...) : IObservable<T>` with a typed
  `Subscribe(IObserver<T>)` containing the original `body`. Captured
  parameters become readonly fields.

**User impact:** sync pipelines stop allocating Rx wrappers and closures on
every subscribe; redundant LINQ enumerators on `Max/Min` aggregations go
away; the ThrottleUntilTrue timer allocation per failed-predicate emission
disappears.

---

## Packet 10 — `SyncTimerObservable` per-tick allocations

**Scope:** `Operators/SyncTimerObservable.cs`.

Confirmed issues:
- The shared-timer dictionary uses `ConcurrentDictionary<(TimeSpan, IScheduler), Lazy<SharedTimer>>` — `Lazy<>` factory + delegate allocated per timer key. Replace with `GetOrAdd` and a static factory state pattern.
- Per-subscribe: `new ActionDisposable(() => { lock(_gate) { _observers.Remove(observer); ... } })` — closure captures `observer`, `_gate`, `_observers`, `_timerSubscription`. Replace with a sealed `TimerSubscription` class.
- Per-tick: `_observers.ToArray()` snapshot — wasted allocation when observer set is stable. Swap-on-write `IObserver<DateTime>[]` for lock-free reads.

**Why now:** shared timers fire continuously. Every tick allocates regardless of whether observer set changed.

**User impact:** UI heartbeat / polling streams stop accumulating GC pressure.

---

## Packet 11 — Cross-cutting quick wins

**Scope:** small, mechanical, high-confidence changes.

1. **Per-emission `lock (_disposeCts)`** in `CombineLatest{2..16}` — locking on the public-typed CTS violates the "no locks on arbitrary objects" rule. Use a dedicated `_gate` field. (Generator template change.)
2. **`Lock` vs `object _gate`** — ~25 operator files still use bare `private readonly object _gate = new()` without the `#if NET9_0_OR_GREATER private readonly Lock _gate = new(); #else ... #endif` pattern that `DisposableBag` uses. Mechanical sweep.
3. **`ShuffleObservable._buffer = new byte[4]`** → `Span<byte> buffer = stackalloc byte[4]; _random.GetBytes(buffer); BinaryPrimitives.ReadUInt32LittleEndian(buffer);` — remove the field + `BitConverter` boxing-free call.
4. **`Continuation.cs:62, 77`** — `Task.Run(() => ...)` captures `this`. Use `Task.Run(static state => ..., this)`.
5. **`AsyncGate.WaitForReleaseAsync`** — returns `Task<Releaser>`; convert to `async ValueTask<Releaser>` for the uncontended acquisition path (.NET 8+ pools the state machine box).
6. **`Async/Operators/PartitionObservable.cs:270-318`** — 5 `foreach` sites over `IObserver<T>[]`. Convert to indexed `for`.
7. **`Async/Internals/MulticastObservableAsync.cs:57-58, 74`** — `CreateLinkedTokenSource` per `ConnectAsync` and per unsubscribe. Cache one at construction.
8. **`Async/Observables/Empty.cs:30, Return.cs:24, Throw.cs:32`** — route through heavy `Create` + `CancelableTaskSubscription`. Replace with dedicated singleton-style observables (`Empty<T>` already has `NeverObservableAsync<T>.Instance` as a model).
9. **`ConcurrencyLimiter.cs:152`** — `ContinueWith(ant => ProcessTaskCompletion(observer, ant), ...)` captures `this`+`observer`. Use the `ContinueWith(Action<Task, object?>, state)` overload with a static continuation.

---

## What is explicitly NOT in this plan

- Renaming any public method on `ObservableAsync`, `ReactiveExtensions`,
  `ObserverExtensions`, or any `SubjectAsync` factory.
- Changing public return types (`IObservable<T>`, `IObservableAsync<T>`,
  `ValueTask`, `Task`, etc.).
- Removing any public overload (`S2360` already split everything into explicit
  pairs; those stay).
- Reworking the `IObservable<T>` ↔ `IObservableAsync<T>` bridge contract —
  the `Async/Bridge/` types stay where they are; only their internal
  implementations change (Packet 8).
- Replacing System.Reactive types that appear in public signatures
  (`Notification<T>`, `Unit`, `IScheduler`, `CompositeDisposable`). They're
  BCL-level and stay.

---

## Suggested execution order

1. **Packet 1** — `ConfigureAwait(false)` sweep. Mechanical, big correctness win, easiest to verify. Do first.
2. **Packet 3** — `Heartbeat<T>` / `Stale<T>` record-struct conversion. Small surface, high per-emission payoff.
3. **Packet 2** — Per-emission linked-CTS elimination. Hits the most operators; needs careful per-class refactor. Tackle `Merge`, `Delay`, `Switch`, `Throttle`, `Timeout` first, then loop through `CombineLatest{N}` via the generator template, then `TakeUntil`, `Zip`, `BaseReplayLatestSubjectAsync`.
4. **Packet 7** — `DisposableAsync.Create` state overload + migrate call sites. Unblocks the closure cleanup in Packets 4 + 8.
5. **Packet 4** — Subjects' broadcast / unsubscribe path. Depends on Packet 7.
6. **Packet 8** — `BridgeObserver` work-item struct. Self-contained.
7. **Packet 5** — `Select` / `Where` / `Scan` / `Catch` to sealed observer classes. Independent.
8. **Packet 6** — `CombineLatestEnumerable` snapshot buffer + per-source delegates. Independent.
9. **Packet 10** — `SyncTimerObservable` per-tick allocations. Independent.
10. **Packet 9** — Sync-side System.Reactive replacement. Largest surface; tackle after the async-side cleanup is settled. Sub-agent friendly.
11. **Packet 11** — Quick wins. Sprinkle through as small PRs alongside the bigger packets.

## Verification

Each packet should:

1. Compile clean on net8 / net9 / net10 / net462 / net472 / net481.
2. Pass the existing TUnit suite (it already covers most operators).
3. Where appropriate, add a BenchmarkDotNet entry (`memory diagnoser` on) under
   a new `benchmarks/` project once it exists — at minimum for Packet 2
   (CTS-per-emission was the biggest hammer) and Packet 5 (state-machine
   elimination on `Select`/`Where`/`Scan`).

No suppressions or analyzer disables should be needed for any packet — the
fixes are real code changes, not silencing.
