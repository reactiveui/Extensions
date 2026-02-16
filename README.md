<a href="https://www.nuget.org/packages/splat">
        <img src="https://img.shields.io/nuget/dt/reactiveui.extensions.svg">
</a>
<a href="https://reactiveui.net/slack">
        <img src="https://img.shields.io/badge/chat-slack-blue.svg">
</a>

[![Build](https://github.com/reactiveui/Extensions/actions/workflows/ci-build.yml/badge.svg)](https://github.com/reactiveui/Extensions/actions/workflows/ci-build.yml)
[![codecov](https://codecov.io/gh/reactiveui/Extensions/graph/badge.svg?token=7u1lNF5imh)](https://codecov.io/gh/reactiveui/Extensions)

# ReactiveUI.Extensions

A focused collection of high–value Reactive Extensions (Rx) operators that do **not** ship with `System.Reactive` but are commonly needed when building reactive .NET applications.

The goal of this library is to:  
- Reduce boilerplate for frequent reactive patterns (timers, buffering, throttling, heartbeats, etc.)  
- Provide pragmatic, allocation‑aware helpers for performance sensitive scenarios  
- Avoid additional dependencies – only `System.Reactive` is required

Supported Target Frameworks: `.NET 4.6.2`, `.NET 4.7.2`, `.NET 4.8.1`, `.NET 8`, `.NET 9`, `.NET 10`.

---
## Table of Contents
1. [Installation](#installation)
2. [Quick Start](#quick-start)
3. [API Catalog](#api-catalog)
4. [Operator Categories & Examples](#operator-categories--examples)
   - [Null / Signal Helpers](#null--signal-helpers)
   - [Timing, Scheduling & Flow Control](#timing-scheduling--flow-control)
   - [Inactivity / Liveness](#inactivity--liveness)
   - [Error Handling & Resilience](#error-handling--resilience)
   - [Combining, Partitioning & Logical Helpers](#combining-partitioning--logical-helpers)
   - [Async / Task Integration](#async--task-integration)
   - [Backpressure / Conflation](#backpressure--conflation)
   - [Selective & Conditional Emission](#selective--conditional-emission)
   - [Buffering & Transformation](#buffering--transformation)
   - [Subscription / Side Effects](#subscription--side-effects)
   - [Utility & Miscellaneous](#utility--miscellaneous)
5. [Async Observables (`IObservableAsync<T>`)](#async-observables-iobservableasynct)
   - [Async Quick Start](#async-quick-start)
   - [Async API Catalog](#async-api-catalog)
   - [Core Interfaces & Types](#core-interfaces--types)
   - [Factory Methods](#factory-methods)
   - [Transformation Operators](#transformation-operators)
   - [Filtering Operators](#filtering-operators)
   - [Combining & Merging](#combining--merging)
   - [Error Handling & Retry](#error-handling--retry)
   - [Timing & Scheduling (Async)](#timing--scheduling-async)
   - [Aggregation & Element Operators](#aggregation--element-operators)
   - [Side Effects & Lifecycle](#side-effects--lifecycle)
   - [Multicasting & Sharing](#multicasting--sharing)
   - [Bridging & Conversion](#bridging--conversion)
   - [Subjects](#subjects)
   - [Async Disposables](#async-disposables)
6. [Performance Notes](#performance-notes)
7. [Thread Safety](#thread-safety)
8. [License](#license)

---
## Installation
```bash
# Package coming soon (example)
dotnet add package ReactiveUI.Extensions
```
Reference the project directly while developing locally.

---
## Quick Start
```csharp
using System;
using System.Reactive.Linq;
using ReactiveUI.Extensions;

var source = Observable.Interval(TimeSpan.FromMilliseconds(120))
                       .Take(10)
                       .Select(i => (long?) (i % 3 == 0 ? null : i));

// 1. Filter nulls + convert to a Unit signal.
var signal = source.WhereIsNotNull().AsSignal();

// 2. Add a heartbeat if the upstream goes quiet for 500ms.
var withHeartbeat = source.WhereIsNotNull()
                          .Heartbeat(TimeSpan.FromMilliseconds(500), Scheduler.Default);

// 3. Retry with exponential backoff up to 5 times.
var resilient = Observable.Defer(() =>
        Observable.Throw<long>(new InvalidOperationException("Boom")))
    .RetryWithBackoff(maxRetries: 5, initialDelay: TimeSpan.FromMilliseconds(100));

// 4. Conflate bursty updates.
var conflated = source.Conflate(TimeSpan.FromMilliseconds(300), Scheduler.Default);

using (conflated.Subscribe(Console.WriteLine))
{
    Console.ReadLine();
}
```

---
## API Catalog
Below is the full list of extension methods (grouped logically).  
Some overloads omitted for brevity.

| Category | Operators |
|----------|-----------|
| Null & Signal | `WhereIsNotNull`, `AsSignal` |
| Timing & Scheduling | `SyncTimer`, `Schedule` (overloads), `ScheduleSafe`, `ThrottleFirst`, `ThrottleDistinct`, `DebounceImmediate` |
| Inactivity / Liveness | `Heartbeat`, `DetectStale`, `BufferUntilInactive` |
| Error Handling | `CatchIgnore`, `CatchAndReturn`, `OnErrorRetry` (overloads), `RetryWithBackoff` |
| Combining & Aggregation | `CombineLatestValuesAreAllTrue`, `CombineLatestValuesAreAllFalse`, `GetMax`, `GetMin`, `Partition` |
| Logical / Boolean | `Not`, `WhereTrue`, `WhereFalse` |
| Async / Task | `SelectAsyncSequential`, `SelectLatestAsync`, `SelectAsyncConcurrent`, `SubscribeAsync` (overloads), `SynchronizeSynchronous`, `SynchronizeAsync`, `SubscribeSynchronous` (overloads), `ToHotTask` |
| Backpressure | `Conflate` |
| Filtering / Conditional | `Filter` (Regex), `TakeUntil` (predicate), `WaitUntil`, `SampleLatest`, `SwitchIfEmpty`, `DropIfBusy` |
| Buffering | `BufferUntil`, `BufferUntilInactive`, `BufferUntilIdle`, `Pairwise`, `ScanWithInitial` |
| Transformation & Utility | `Shuffle`, `ForEach`, `FromArray`, `Using`, `While`, `Start`, `OnNext` (params helper), `DoOnSubscribe`, `DoOnDispose`, `ToReadOnlyBehavior`, `ToPropertyObservable` |

---
## Operator Categories & Examples
### Null / Signal Helpers
```csharp
IObservable<string?> raw = GetPossiblyNullStream();
IObservable<string> cleaned = raw.WhereIsNotNull();
IObservable<Unit> signal = cleaned.AsSignal();
```

### Timing, Scheduling & Flow Control
```csharp
// Shared timer for a given period (one underlying timer per distinct TimeSpan)
var sharedTimer = ReactiveExtensions.SyncTimer(TimeSpan.FromSeconds(1));

// Delay emission of a single value
42.Schedule(TimeSpan.FromMilliseconds(250), Scheduler.Default)
  .Subscribe(v => Console.WriteLine($"Delayed: {v}"));

// Safe scheduling when a scheduler may be null
IScheduler? maybeScheduler = null;
maybeScheduler.ScheduleSafe(() => Console.WriteLine("Ran inline"));

// ThrottleFirst: allow first item per window, ignore rest
var throttled = Observable.Interval(TimeSpan.FromMilliseconds(50))
                          .ThrottleFirst(TimeSpan.FromMilliseconds(200));

// DebounceImmediate: emit first immediately then debounce rest
var debounced = Observable.Interval(TimeSpan.FromMilliseconds(40))
                          .DebounceImmediate(TimeSpan.FromMilliseconds(250));

// ThrottleDistinct: throttle but only emit when the value actually changes
var source = Observable.Interval(TimeSpan.FromMilliseconds(50)).Take(20);
var distinctThrottled = source.ThrottleDistinct(TimeSpan.FromMilliseconds(200));
```

### Inactivity / Liveness
```csharp
// Heartbeat emits IHeartbeat<T> where IsHeartbeat == true during quiet periods
var heartbeats = Observable.Interval(TimeSpan.FromMilliseconds(400))
                           .Take(5)
                           .Heartbeat(TimeSpan.FromMilliseconds(300), Scheduler.Default);

// DetectStale emits IStale<T>: one stale marker after inactivity, or fresh update wrappers
var staleAware = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(500))
                           .Take(3)
                           .DetectStale(TimeSpan.FromMilliseconds(300), Scheduler.Default);

// BufferUntilInactive groups events separated by inactivity
var bursts = Observable.Interval(TimeSpan.FromMilliseconds(60)).Take(20);
var groups = bursts.BufferUntilInactive(TimeSpan.FromMilliseconds(200));
```

### Error Handling & Resilience
```csharp
var flaky = Observable.Create<int>(o =>
{
    o.OnNext(1);
    o.OnError(new InvalidOperationException("Fail"));
    return () => { };
});

// Ignore all errors and complete silently
var flakySafe = flaky.CatchIgnore();

// Replace error with a fallback value
var withFallback = flaky.CatchAndReturn(-1);

// Retry only specific exception type with logging
var retried = flaky.OnErrorRetry<int, InvalidOperationException>(ex => Console.WriteLine(ex.Message), retryCount: 3);

// Retry with exponential backoff
var backoff = flaky.RetryWithBackoff(maxRetries: 5, initialDelay: TimeSpan.FromMilliseconds(100));
```

### Combining, Partitioning & Logical Helpers
```csharp
var a = Observable.Interval(TimeSpan.FromMilliseconds(150)).Select(i => i % 2 == 0);
var b = Observable.Interval(TimeSpan.FromMilliseconds(170)).Select(i => i % 3 == 0);

var allTrue = new[] { a, b }.CombineLatestValuesAreAllTrue();
var allFalse = new[] { a, b }.CombineLatestValuesAreAllFalse();

var numbers = Observable.Range(1, 10);
var (even, odd) = numbers.Partition(n => n % 2 == 0); // Partition stream

var toggles = a.Not(); // Negate booleans
```

### Async / Task Integration
```csharp
IObservable<int> inputs = Observable.Range(1, 5);

// Sequential (preserves order)
var seq = inputs.SelectAsyncSequential(async i => { await Task.Delay(50); return i * 2; });

// Latest only (cancels previous)
var latest = inputs.SelectLatestAsync(async i => { await Task.Delay(100); return i; });

// Limited parallelism
var concurrent = inputs.SelectAsyncConcurrent(async i => { await Task.Delay(100); return i; }, maxConcurrency: 2);

// Asynchronous subscription (serializing tasks)
inputs.SubscribeAsync(async i => await Task.Delay(10));

// Synchronous gate: ensures per-item async completion before next is emitted
inputs.SubscribeSynchronous(async i => await Task.Delay(25));

// ToHotTask: convert an observable to a Task that starts immediately
var source = Observable.Return(42);
var task = source.ToHotTask();
var result = await task; // 42
```

### Backpressure / Conflation
```csharp
// Conflate: enforce minimum spacing between emissions while always outputting the most recent value
var noisy = Observable.Interval(TimeSpan.FromMilliseconds(20)).Take(30);
var conflated = noisy.Conflate(TimeSpan.FromMilliseconds(200), Scheduler.Default);
```

### Selective & Conditional Emission
```csharp
// TakeUntil predicate (inclusive)
var untilFive = Observable.Range(1, 100).TakeUntil(x => x == 5);

// WaitUntil first match then complete
var firstEven = Observable.Range(1, 10).WaitUntil(x => x % 2 == 0);

// SampleLatest: sample the latest value whenever a trigger fires
var source = Observable.Interval(TimeSpan.FromMilliseconds(100)).Take(10);
var trigger = Observable.Interval(TimeSpan.FromMilliseconds(300)).Take(3);
var sampled = source.SampleLatest(trigger);

// SwitchIfEmpty: provide a fallback if the source completes without emitting
var empty = Observable.Empty<int>();
var fallback = Observable.Return(42);
var result = empty.SwitchIfEmpty(fallback); // emits 42

// DropIfBusy: drop values if the previous async operation is still running
var inputs = Observable.Range(1, 5);
var processed = inputs.DropIfBusy(async x => { await Task.Delay(200); Console.WriteLine(x); });
```

### Buffering & Transformation
```csharp
// BufferUntil - collect chars between delimiters
var chars = "<a><bc><d>".ToCharArray().ToObservable();
var frames = chars.BufferUntil('<', '>'); // emits "<a>", "<bc>", "<d>"

// Shuffle arrays in-place
var arrays = Observable.Return(new[] { 1, 2, 3, 4, 5 });
var shuffled = arrays.Shuffle();

// BufferUntilIdle: emit a batch when the stream goes quiet
var events = Observable.Interval(TimeSpan.FromMilliseconds(100)).Take(10);
var batches = events.BufferUntilIdle(TimeSpan.FromMilliseconds(250));

// Pairwise: emit consecutive pairs
var numbers = Observable.Range(1, 5);
var pairs = numbers.Pairwise(); // emits (1,2), (2,3), (3,4), (4,5)

// ScanWithInitial: scan that always emits the initial value first
var values = Observable.Return(5);
var accumulated = values.ScanWithInitial(10, (acc, x) => acc + x); // emits 10, then 15
```

### Subscription & Side Effects
```csharp
var stream = Observable.Range(1, 3)
    .DoOnSubscribe(() => Console.WriteLine("Subscribed"))
    .DoOnDispose(() => Console.WriteLine("Disposed"));

using (stream.Subscribe(Console.WriteLine))
{
    // auto dispose at using end
}
```

### Utility & Miscellaneous
```csharp
// Emit list contents quickly with low allocations
var listSource = Observable.Return<IEnumerable<int>>(new List<int> { 1, 2, 3 });
listSource.ForEach().Subscribe(Console.WriteLine);

// Using helper for deterministic disposal
var value = new MemoryStream().Using(ms => ms.Length);

// While loop (reactive)
var counter = 0;
ReactiveExtensions.While(() => counter++ < 3, () => Console.WriteLine(counter))
                   .Subscribe();

// Batch push with OnNext params
var subj = new Subject<int>();
subj.OnNext(1, 2, 3, 4);

// ToReadOnlyBehavior: create a read-only behavior subject
var (observable, observer) = ReactiveExtensions.ToReadOnlyBehavior(10);
observer.OnNext(20); // observable emits 10, then 20

// ToPropertyObservable: observe property changes on INotifyPropertyChanged
public class ViewModel : INotifyPropertyChanged
{
    private string _name;
    public string Name
    {
        get => _name;
        set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

var vm = new ViewModel();
var nameChanges = vm.ToPropertyObservable(x => x.Name);
vm.Name = "Hello"; // observable emits "Hello"
```

---
## Async Observables (`IObservableAsync<T>`)

A fully **async-native** observable framework that mirrors the `System.Reactive` programming model but uses `ValueTask`, `CancellationToken`, and `IAsyncDisposable` throughout. Every observer callback (`OnNextAsync`, `OnErrorResumeAsync`, `OnCompletedAsync`) is asynchronous, enabling true end-to-end `async`/`await` pipelines without blocking threads.

**Key design goals:**
- **Async all the way down** – no synchronous observer callbacks; every notification is `ValueTask`-based
- **Cancellation-first** – every operator and subscription accepts `CancellationToken`
- **Async disposal** – subscriptions return `IAsyncDisposable` for clean async resource cleanup
- **Interop** – bidirectional bridge between `IObservable<T>` and `IObservableAsync<T>`
- **Configurable concurrency** – subjects offer serial or concurrent publishing, stateful or stateless modes

> **Note:** Async Observables require **.NET 8** or later.

### Async Quick Start
```csharp
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;
using ReactiveUI.Extensions.Async.Disposables;

// 1. Create an async observable from a factory
var source = ObservableAsync.Range(1, 5);

// 2. Apply operators – filter, transform, throttle
var pipeline = source
    .Where(x => x % 2 != 0)           // keep odd numbers
    .Select(x => x * 10)              // multiply by 10
    .Do(x => Console.WriteLine($"Processing: {x}"));

// 3. Subscribe asynchronously
await using var subscription = await pipeline.SubscribeAsync(
    async (value, ct) => Console.WriteLine($"Received: {value}"),
    CancellationToken.None);

// 4. Use subjects for multicasting
var subject = SubjectAsync.Create<string>();
await using var sub = await subject.Values.SubscribeAsync(
    async (msg, ct) => Console.WriteLine(msg),
    CancellationToken.None);

await subject.OnNextAsync("Hello, async Rx!", CancellationToken.None);

// 5. Bridge from classic IObservable<T>
var classic = Observable.Interval(TimeSpan.FromMilliseconds(200)).Take(5);
var asyncVersion = classic.ToObservableAsync();

// 6. Aggregate results
int count = await source.CountAsync(CancellationToken.None);
int first = await source.FirstAsync(CancellationToken.None);
var items = await source.ToListAsync(CancellationToken.None);
```

### Async API Catalog

| Category | Operators / Types |
|----------|-------------------|
| Core Interfaces | `IObservableAsync<T>`, `IObserverAsync<T>`, `ObservableAsync<T>`, `ConnectableObservableAsync<T>` |
| Factory Methods | `Create`, `CreateAsBackgroundJob`, `Return`, `Empty`, `Never`, `Throw`, `Range`, `Interval`, `Timer`, `Defer`, `FromAsync`, `ToObservableAsync` (Task / IAsyncEnumerable / IEnumerable) |
| Transformation | `Select` (sync & async), `SelectMany`, `Scan` (sync & async), `Cast`, `OfType`, `GroupBy` |
| Filtering | `Where` (sync & async), `Take`, `Skip`, `TakeWhile`, `SkipWhile`, `TakeUntil` (observable / task / token / predicate), `Distinct`, `DistinctBy`, `DistinctUntilChanged`, `DistinctUntilChangedBy` |
| Combining | `Merge` (overloads), `Concat` (overloads), `Switch`, `CombineLatest` (2–8 sources), `Zip`, `Prepend`, `StartWith` |
| Error Handling | `Catch`, `CatchAndIgnoreErrorResume`, `Retry` (infinite & count-limited), `OnErrorResumeAsFailure` |
| Timing | `Throttle`, `Delay`, `Timeout` (with optional fallback) |
| Aggregation | `AggregateAsync`, `CountAsync`, `LongCountAsync`, `AnyAsync`, `AllAsync`, `ContainsAsync`, `FirstAsync`, `FirstOrDefaultAsync`, `LastAsync`, `LastOrDefaultAsync`, `SingleAsync`, `SingleOrDefaultAsync`, `ToListAsync`, `ToDictionaryAsync` |
| Side Effects | `Do` (sync & async), `OnDispose` (sync & async), `Using` |
| Multicasting | `Publish`, `StatelessPublish`, `ReplayLatest`, `RefCount`, `Multicast` |
| Scheduling | `ObserveOn` (AsyncContext / SynchronizationContext / TaskScheduler / IScheduler), `Yield` |
| Subscription | `SubscribeAsync` (multiple overloads), `ForEachAsync`, `WaitCompletionAsync` |
| Conversion | `ToObservable` (async → classic), `ToObservableAsync` (classic → async), `ToAsyncEnumerable`, `Wrap` |
| Subjects | `SubjectAsync.Create`, `SubjectAsync.CreateBehavior`, `SubjectAsync.CreateReplayLatest` |
| Disposables | `DisposableAsync`, `CompositeDisposableAsync` |
| Mixins | `AsObserverAsync`, `MapValues`, `ToDisposableAsync` |

---
### Core Interfaces & Types

| Type | Description |
|------|-------------|
| `IObservableAsync<T>` | Async push-based notification provider. `SubscribeAsync` returns `ValueTask<IAsyncDisposable>`. |
| `IObserverAsync<T>` | Receives async notifications: `OnNextAsync`, `OnErrorResumeAsync`, `OnCompletedAsync`. Implements `IAsyncDisposable`. |
| `ObservableAsync<T>` | Abstract base class for building custom async observables. Override `SubscribeAsyncCore`. |
| `ConnectableObservableAsync<T>` | Extends `ObservableAsync<T>` with `ConnectAsync` for deferred subscription to the underlying source. |
| `ISubjectAsync<T>` | Async subject: exposes `Values` (`IObservableAsync<T>`) and publishing methods (`OnNextAsync`, `OnErrorResumeAsync`, `OnCompletedAsync`). |
| `Result` | Completion token carrying either success or a failure `Exception`. Used with `OnCompletedAsync`. |
| `AsyncContext` | Wraps a `SynchronizationContext` or `TaskScheduler` for scheduling async observer notifications. |

```csharp
// Implementing a custom async observable
public class TickObservable : ObservableAsync<int>
{
    protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(
        IObserverAsync<int> observer, CancellationToken cancellationToken)
    {
        for (int i = 0; i < 5 && !cancellationToken.IsCancellationRequested; i++)
        {
            await observer.OnNextAsync(i, cancellationToken);
            await Task.Delay(100, cancellationToken);
        }

        await observer.OnCompletedAsync(Result.Success);
        return DisposableAsync.Empty;
    }
}
```

### Factory Methods

| Method | Description |
|--------|-------------|
| `ObservableAsync.Create<T>(subscribe)` | Create from an async delegate that receives `(observer, ct)` and returns `IAsyncDisposable`. |
| `ObservableAsync.CreateAsBackgroundJob<T>(subscribe)` | Like `Create`, but the subscribe delegate runs as a background job, returning immediately. |
| `ObservableAsync.Return<T>(value)` | Emits a single value then completes. |
| `ObservableAsync.Empty<T>()` | Completes immediately without emitting any values. |
| `ObservableAsync.Never<T>()` | Never emits and never completes. |
| `ObservableAsync.Throw<T>(exception)` | Completes immediately with the specified error. |
| `ObservableAsync.Range(start, count)` | Emits a sequence of integers. |
| `ObservableAsync.Interval(period)` | Emits incrementing `long` values at a regular interval. Accepts optional `TimeProvider`. |
| `ObservableAsync.Timer(dueTime)` | Emits `0L` after the specified delay. Supports one-shot and periodic overloads. Accepts optional `TimeProvider`. |
| `ObservableAsync.Defer<T>(factory)` | Defers creation of the observable until subscription time. |
| `ObservableAsync.FromAsync<T>(func)` | Wraps an async function as a single-element observable. |
| `task.ToObservableAsync()` | Converts a `Task<T>` to an async observable. |
| `asyncEnumerable.ToObservableAsync()` | Converts an `IAsyncEnumerable<T>` to an async observable. |
| `enumerable.ToObservableAsync()` | Converts an `IEnumerable<T>` to an async observable. |

```csharp
// Create from factory delegate
var custom = ObservableAsync.Create<string>(async (observer, ct) =>
{
    await observer.OnNextAsync("Hello", ct);
    await observer.OnNextAsync("World", ct);
    await observer.OnCompletedAsync(Result.Success);
    return DisposableAsync.Empty;
});

// From a Task
var fromTask = Task.FromResult(42).ToObservableAsync();

// From IAsyncEnumerable
async IAsyncEnumerable<int> GenerateAsync()
{
    for (int i = 0; i < 5; i++)
    {
        await Task.Delay(50);
        yield return i;
    }
}

var fromAsyncEnum = GenerateAsync().ToObservableAsync();

// Periodic timer (with TimeProvider support)
var ticks = ObservableAsync.Interval(TimeSpan.FromSeconds(1));

// Deferred creation
var deferred = ObservableAsync.Defer<int>(() =>
    ObservableAsync.Return(DateTime.Now.Second));
```

### Transformation Operators

| Operator | Description |
|----------|-------------|
| `Select(selector)` | Projects each element using a synchronous transform function. |
| `Select(asyncSelector)` | Projects each element using an async transform function. |
| `SelectMany(selector)` | Flattens nested async observables produced by a projection. Multiple overloads for collection selectors. |
| `Scan(seed, accumulator)` | Applies an accumulator over the sequence, emitting each intermediate result. Sync and async overloads. |
| `Cast<TResult>()` | Casts each element to the specified type. |
| `OfType<TResult>()` | Filters elements that are assignable to the specified type. |
| `GroupBy(keySelector)` | Groups elements by key, emitting `GroupedAsyncObservable<TKey, T>` per group. |

```csharp
var source = ObservableAsync.Range(1, 5);

// Sync projection
var doubled = source.Select(x => x * 2);

// Async projection
var projected = source.Select(async (x, ct) =>
{
    await Task.Delay(10, ct);
    return x.ToString();
});

// Flat map: each item produces a sub-sequence
var flattened = source.SelectMany(x => ObservableAsync.Range(x * 10, 3));

// Running total with Scan
var runningTotal = source.Scan(0, (acc, x) => acc + x); // 1, 3, 6, 10, 15

// Group by even/odd
var grouped = source.GroupBy(x => x % 2 == 0 ? "even" : "odd");
```

### Filtering Operators

| Operator | Description |
|----------|-------------|
| `Where(predicate)` | Filters elements using a synchronous predicate. |
| `Where(asyncPredicate)` | Filters elements using an async predicate. |
| `Take(count)` | Takes the first N elements then completes. |
| `Skip(count)` | Skips the first N elements. |
| `TakeWhile(predicate)` | Takes elements while the predicate holds, then completes. |
| `SkipWhile(predicate)` | Skips elements while the predicate holds, then emits the rest. |
| `TakeUntil(other)` | Takes elements until another async observable, Task, CancellationToken, or predicate signals. |
| `Distinct()` | Emits only elements not previously seen. |
| `DistinctBy(keySelector)` | Emits only elements with unique keys. |
| `DistinctUntilChanged()` | Suppresses consecutive duplicate elements. |
| `DistinctUntilChangedBy(keySelector)` | Suppresses consecutive elements with the same key. |

```csharp
var source = ObservableAsync.Range(1, 10);

// Basic filter
var evens = source.Where(x => x % 2 == 0); // 2, 4, 6, 8, 10

// Async filter
var asyncFiltered = source.Where(async (x, ct) =>
{
    await Task.Delay(1, ct);
    return x > 5;
});

// Take and Skip
var firstThree = source.Take(3);       // 1, 2, 3
var skipThree = source.Skip(3);        // 4, 5, 6, 7, 8, 9, 10

// TakeWhile / SkipWhile
var whileLow = source.TakeWhile(x => x < 4);   // 1, 2, 3
var afterLow = source.SkipWhile(x => x < 4);   // 4, 5, 6, 7, 8, 9, 10

// TakeUntil with a cancellation token
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var bounded = ObservableAsync.Interval(TimeSpan.FromMilliseconds(100))
    .TakeUntil(cts.Token);

// Distinct and DistinctUntilChanged
var items = new[] { 1, 2, 2, 3, 1, 3 }.ToObservableAsync();
var unique = items.Distinct();                   // 1, 2, 3
var noConsecDups = items.DistinctUntilChanged(); // 1, 2, 3, 1, 3
```

### Combining & Merging

| Operator | Description |
|----------|-------------|
| `Merge(sources)` | Merges multiple async observables, interleaving values as they arrive. Supports max concurrency. |
| `Concat(sources)` | Concatenates multiple async observables sequentially – subscribes to the next only after the previous completes. |
| `Switch()` | Flattens a higher-order async observable by always switching to the most recent inner sequence. |
| `CombineLatest(other, selector)` | Combines the latest values from 2–8 sources whenever any source emits. |
| `Zip(other)` | Pairs elements from two sources by index. Optional result selector. |
| `Prepend(value)` | Prepends a single value before the source sequence. |
| `StartWith(values)` | Prepends one or more values before the source sequence. |

```csharp
var a = ObservableAsync.Range(1, 3);   // 1, 2, 3
var b = ObservableAsync.Range(10, 3);  // 10, 11, 12

// Merge: interleave two streams
var merged = ObservableAsync.Merge(a, b);

// Concat: sequential
var sequential = ObservableAsync.Concat(a, b); // 1, 2, 3, 10, 11, 12

// CombineLatest
var combined = a.CombineLatest(b, (x, y) => $"{x}+{y}");

// Zip by index
var zipped = a.Zip(b, (x, y) => x + y); // 11, 13, 15

// Prepend / StartWith
var withPrefix = a.Prepend(0);              // 0, 1, 2, 3
var withMany = a.StartWith([-2, -1, 0]);    // -2, -1, 0, 1, 2, 3
```

### Error Handling & Retry

| Operator | Description |
|----------|-------------|
| `Catch(handler)` | Catches errors and switches to a fallback async observable. |
| `CatchAndIgnoreErrorResume()` | Catches error-resume notifications and suppresses them, allowing the sequence to continue. |
| `Retry()` | Re-subscribes to the source indefinitely on error. |
| `Retry(count)` | Re-subscribes up to `count` times on error. |
| `OnErrorResumeAsFailure()` | Converts `OnErrorResumeAsync` calls into `OnCompletedAsync` with a `Result.Failure`. |

```csharp
// Catch and switch to fallback
var flaky = ObservableAsync.Throw<int>(new InvalidOperationException("Oops"));
var safe = flaky.Catch(ex => ObservableAsync.Return(-1)); // emits -1

// Retry up to 3 times
var retried = flaky.Retry(3);

// Retry indefinitely
var forever = flaky.Retry();

// Convert error-resume to failure completion
var strict = someSource.OnErrorResumeAsFailure();
```

### Timing & Scheduling (Async)

| Operator | Description |
|----------|-------------|
| `Throttle(timeSpan)` | Suppresses values that arrive within the specified window after the last emission. Accepts optional `TimeProvider`. |
| `Delay(timeSpan)` | Delays every emission by the specified duration. Accepts optional `TimeProvider`. |
| `Timeout(timeSpan)` | Raises a `TimeoutException` if no value arrives within the specified window. |
| `Timeout(timeSpan, fallback)` | Switches to a fallback source on timeout instead of faulting. |
| `ObserveOn(context)` | Shifts observer notifications to the specified `AsyncContext`, `SynchronizationContext`, `TaskScheduler`, or `IScheduler`. |
| `Yield()` | Yields control (via `Task.Yield`) between each notification, ensuring other work can interleave. |

```csharp
var source = ObservableAsync.Interval(TimeSpan.FromMilliseconds(50)).Take(10);

// Throttle rapid emissions (debounce)
var throttled = source.Throttle(TimeSpan.FromMilliseconds(200));

// Delay each value by 100ms
var delayed = source.Delay(TimeSpan.FromMilliseconds(100));

// Timeout if nothing arrives in 2 seconds
var guarded = source.Timeout(TimeSpan.FromSeconds(2));

// Timeout with fallback
var withFallback = source.Timeout(
    TimeSpan.FromSeconds(2),
    ObservableAsync.Return(999L));

// Schedule notifications onto a specific context
var onContext = source.ObserveOn(new AsyncContext(SynchronizationContext.Current!));

// Yield between each notification
var yielded = source.Yield();
```

### Aggregation & Element Operators

| Operator | Description |
|----------|-------------|
| `AggregateAsync(seed, accumulator)` | Applies an accumulator over the sequence and returns the final result. |
| `CountAsync()` | Returns the number of elements. |
| `LongCountAsync()` | Returns the element count as `long`. |
| `AnyAsync()` / `AnyAsync(predicate)` | Returns `true` if the sequence has any elements (optionally matching a predicate). |
| `AllAsync(predicate)` | Returns `true` if all elements match the predicate. |
| `ContainsAsync(value)` | Returns `true` if the sequence contains the specified value. |
| `FirstAsync()` | Returns the first element or throws if empty. |
| `FirstOrDefaultAsync()` | Returns the first element or `default` if empty. |
| `LastAsync()` | Returns the last element or throws if empty. |
| `LastOrDefaultAsync()` | Returns the last element or `default` if empty. |
| `SingleAsync()` | Returns the only element; throws if empty or more than one. |
| `SingleOrDefaultAsync()` | Returns the only element or `default`; throws if more than one. |
| `ToListAsync()` | Collects all elements into a `List<T>`. |
| `ToDictionaryAsync(keySelector)` | Collects all elements into a `Dictionary<TKey, T>`. |
| `ForEachAsync(action)` | Invokes an async action for each element. |
| `WaitCompletionAsync()` | Awaits the completion of the observable without capturing values. |

```csharp
var source = ObservableAsync.Range(1, 5);
var ct = CancellationToken.None;

int count = await source.CountAsync(ct);                        // 5
int sum = await source.AggregateAsync(0, (a, x) => a + x, ct); // 15
bool hasEvens = await source.AnyAsync(x => x % 2 == 0, ct);    // true
bool allPos = await source.AllAsync(x => x > 0, ct);            // true
bool has3 = await source.ContainsAsync(3, ct);                  // true

int first = await source.FirstAsync(ct);                        // 1
int last = await source.LastAsync(ct);                          // 5
List<int> all = await source.ToListAsync(ct);                   // [1, 2, 3, 4, 5]

var dict = await source.ToDictionaryAsync(x => x.ToString(), ct); // {"1":1, "2":2, ...}

// Iterate with async action
await source.ForEachAsync(async (x, ct2) =>
    Console.WriteLine($"Item: {x}"), ct);

// Wait for completion without capturing values
await source.WaitCompletionAsync(ct);
```

### Side Effects & Lifecycle

| Operator | Description |
|----------|-------------|
| `Do(onNext)` | Performs a synchronous side effect for each element without altering the sequence. |
| `Do(asyncOnNext)` | Performs an async side effect for each element. |
| `OnDispose(action)` | Registers a synchronous action to run when the subscription is disposed. |
| `OnDispose(asyncAction)` | Registers an async action to run on disposal. |
| `Using(resourceFactory, observableFactory)` | Creates a disposable resource tied to the subscription lifetime. |

```csharp
var source = ObservableAsync.Range(1, 3);

// Sync side effect
var logged = source.Do(x => Console.WriteLine($"[LOG] {x}"));

// Async side effect
var asyncLogged = source.Do(async (x, ct) =>
{
    await Task.Delay(1, ct);
    Console.WriteLine($"[ASYNC LOG] {x}");
});

// Cleanup on disposal
var withCleanup = source.OnDispose(() => Console.WriteLine("Cleaned up!"));

// Async cleanup
var withAsyncCleanup = source.OnDispose(async () =>
{
    await Task.Delay(10);
    Console.WriteLine("Async cleanup done!");
});

// Using: tie a resource to the subscription lifetime
var withResource = ObservableAsync.Using(
    () => new MemoryStream(),
    stream => ObservableAsync.Return(stream.Length));
```

### Multicasting & Sharing

| Operator | Description |
|----------|-------------|
| `Publish()` | Returns a `ConnectableObservableAsync<T>` that multicasts the source to multiple observers using a serial subject. |
| `StatelessPublish()` | Returns a connectable observable using a stateless serial subject. |
| `ReplayLatest()` | Returns a connectable observable that replays the most recent value to new subscribers. |
| `RefCount()` | Automatically connects a connectable observable when the first observer subscribes and disconnects when the last unsubscribes. |
| `Multicast(subjectFactory)` | General-purpose multicasting with a custom subject factory. |

```csharp
var source = ObservableAsync.Interval(TimeSpan.FromMilliseconds(100)).Take(5);

// Publish + explicit connect
var published = source.Publish();
await using var sub1 = await published.SubscribeAsync(
    async (v, ct) => Console.WriteLine($"Sub1: {v}"), CancellationToken.None);
await using var sub2 = await published.SubscribeAsync(
    async (v, ct) => Console.WriteLine($"Sub2: {v}"), CancellationToken.None);
await using var connection = await published.ConnectAsync(CancellationToken.None);

// RefCount: auto-connect on first subscriber, auto-disconnect on last
var shared = source.Publish().RefCount();
await using var sub3 = await shared.SubscribeAsync(
    async (v, ct) => Console.WriteLine($"Shared: {v}"), CancellationToken.None);

// ReplayLatest: new subscribers get the most recent value immediately
var replayed = source.ReplayLatest().RefCount();
```

### Bridging & Conversion

| Method | Description |
|--------|-------------|
| `observable.ToObservableAsync()` | Wraps a classic `IObservable<T>` as `IObservableAsync<T>`. Synchronous notifications are forwarded sequentially through async callbacks. |
| `asyncObservable.ToObservable()` | Exposes an `IObservableAsync<T>` as a classic `IObservable<T>`. Async callbacks are awaited; the synchronous observer is notified on the completing thread. |
| `asyncObservable.ToAsyncEnumerable()` | Converts an async observable to `IAsyncEnumerable<T>` for consumption with `await foreach`. |
| `source.Wrap(converter)` | Wraps each value using a converter function producing a new async observable type. |

```csharp
// Classic IObservable<T> → IObservableAsync<T>
IObservable<int> classic = Observable.Range(1, 5);
IObservableAsync<int> async1 = classic.ToObservableAsync();

// IObservableAsync<T> → classic IObservable<T>
IObservable<int> backToClassic = async1.ToObservable();

// IObservableAsync<T> → IAsyncEnumerable<T>
var asyncEnum = ObservableAsync.Range(1, 5).ToAsyncEnumerable();
await foreach (var item in asyncEnum)
{
    Console.WriteLine(item);
}
```

### Subjects

Subjects act as both an observer and an observable, enabling manual publishing of values to multiple subscribers.

| Factory Method | Description |
|----------------|-------------|
| `SubjectAsync.Create<T>()` | Creates a subject with default options (serial publishing, stateful). |
| `SubjectAsync.Create<T>(options)` | Creates a subject with configurable publishing (`Serial` / `Concurrent`) and statefulness. |
| `SubjectAsync.CreateBehavior<T>(startValue)` | Creates a behavior subject that replays the latest value (starting with `startValue`) to new subscribers. |
| `SubjectAsync.CreateReplayLatest<T>()` | Creates a subject that replays only the most recent value to new subscribers (no initial value required). |

**Publishing options:**

| Option | Behavior |
|--------|----------|
| `PublishingOption.Serial` | Notifications are delivered to observers one at a time. Safe for single-producer scenarios. |
| `PublishingOption.Concurrent` | Notifications are delivered concurrently to all observers. Useful when observers are independent. |

```csharp
// Basic subject
var subject = SubjectAsync.Create<int>();
await using var sub = await subject.Values.SubscribeAsync(
    async (value, ct) => Console.WriteLine($"Got: {value}"),
    CancellationToken.None);

await subject.OnNextAsync(1, CancellationToken.None);
await subject.OnNextAsync(2, CancellationToken.None);
await subject.OnCompletedAsync(Result.Success);

// Behavior subject: new subscribers receive the current value
var behavior = SubjectAsync.CreateBehavior("initial");
await using var behaviorSub = await behavior.Values.SubscribeAsync(
    async (value, ct) => Console.WriteLine($"Behavior: {value}"), // prints "initial"
    CancellationToken.None);

await behavior.OnNextAsync("updated", CancellationToken.None); // prints "updated"

// Concurrent publishing for fan-out scenarios
var concurrent = SubjectAsync.Create<int>(new SubjectCreationOptions
{
    PublishingOption = PublishingOption.Concurrent,
    IsStateless = false
});

// ReplayLatest: replays the last value without a required start value
var replay = SubjectAsync.CreateReplayLatest<double>();
await replay.OnNextAsync(3.14, CancellationToken.None);
// Late subscriber still receives 3.14
await using var lateSub = await replay.Values.SubscribeAsync(
    async (v, ct) => Console.WriteLine($"Replay: {v}"),
    CancellationToken.None);
```

### Async Disposables

| Type | Description |
|------|-------------|
| `DisposableAsync.Empty` | A no-op `IAsyncDisposable` – useful as a default or placeholder. |
| `DisposableAsync.Create(func)` | Creates an `IAsyncDisposable` from a `Func<ValueTask>` delegate. Ensures the delegate runs at most once. |
| `CompositeDisposableAsync` | A thread-safe collection of `IAsyncDisposable` objects that are disposed together. Supports `Add`, `Remove`, and bulk disposal. |

```csharp
// Create from a delegate
var disposable = DisposableAsync.Create(async () =>
{
    await Task.Delay(10);
    Console.WriteLine("Resource released");
});
await disposable.DisposeAsync();

// Compose multiple async disposables
var composite = new CompositeDisposableAsync();
composite.Add(DisposableAsync.Create(() => { Console.WriteLine("A"); return default; }));
composite.Add(DisposableAsync.Create(() => { Console.WriteLine("B"); return default; }));

Console.WriteLine($"Count: {composite.Count}"); // 2
await composite.DisposeAsync();                  // disposes A and B
Console.WriteLine($"IsDisposed: {composite.IsDisposed}"); // true
```

---
## Performance Notes
- `FastForEach` path avoids iterator allocations for `List<T>`, `IList<T>`, and arrays.
- `SyncTimer` ensures only one shared timer per period reducing timer overhead.
- `Conflate` helps tame high–frequency producers without dropping the final value of a burst.
- `Heartbeat` and `DetectStale` use lightweight scheduling primitives.
- Most operators avoid capturing lambdas in hot loops where practical.
- Async observable operators use `ValueTask` throughout to minimize allocations on the hot path.
- `ObserverAsync<T>` base class detects concurrent re-entrant calls, raising `ConcurrentObserverCallsException` in debug scenarios.
- Subject variants (serial vs. concurrent, stateful vs. stateless) let you choose the right trade-off between safety and throughput.

## Thread Safety
- All operators are pure functional transformations unless documented otherwise.
- `SyncTimer` uses a `ConcurrentDictionary` and returns a hot `IConnectableObservable` that connects once per unique `TimeSpan`.
- Methods returning shared observables (`SyncTimer`, `Partition` result sequences) are safe for multi-subscriber usage unless the upstream is inherently side-effecting.
- `ISubjectAsync<T>` subjects with `PublishingOption.Serial` serialize notifications; `PublishingOption.Concurrent` variants deliver to all observers concurrently.
- `CompositeDisposableAsync` is thread-safe for concurrent `Add`/`Remove`/`DisposeAsync` calls.

## License
MIT – see LICENSE file.

---
## Contributing
Issues / PRs welcome. Please keep additions dependency–free and focused on broadly useful reactive patterns.

---
## Change Log (Excerpt)
(Keep this section updated as the library evolves.)
- Added async task projection helpers (`SelectAsyncSequential`, `SelectLatestAsync`, `SelectAsyncConcurrent`).
- Added liveness operators (`Heartbeat`, `DetectStale`, `BufferUntilInactive`).
- Added resilience (`RetryWithBackoff`, expanded `OnErrorRetry` overloads).
- Added flow control (`Conflate`, `ThrottleFirst`, `DebounceImmediate`, `ThrottleDistinct`).
- Added buffering and transformation operators (`BufferUntilIdle`, `Pairwise`, `ScanWithInitial`).
- Added filtering and conditional operators (`SampleLatest`, `SwitchIfEmpty`, `DropIfBusy`).
- Added utility operators (`ToReadOnlyBehavior`, `ToHotTask`, `ToPropertyObservable`).
- Fixed `SynchronizeSynchronous` to properly propagate OnError and OnCompleted events.
- Removed DisposeWith extension use System.Reactive.Disposables.Fluent from System.Reactive.
- Added fully async-native observable framework (`IObservableAsync<T>`, `IObserverAsync<T>`) with 60+ operators, subjects, bridge extensions, and async disposables.

---
Happy reactive coding!
