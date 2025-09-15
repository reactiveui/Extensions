# ReactiveMarbles.Extensions

A focused collection of high–value Reactive Extensions (Rx) operators that do **not** ship with `System.Reactive` but are commonly needed when building reactive .NET applications.

The goal of this library is to:  
- Reduce boilerplate for frequent reactive patterns (timers, buffering, throttling, heartbeats, etc.)  
- Provide pragmatic, allocation?aware helpers for performance sensitive scenarios  
- Avoid additional dependencies – only `System.Reactive` is required

Supported Target Frameworks: `.NET Standard 2.0`, `.NET 8`, `.NET 9`, `.NET 10`.

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
5. [Performance Notes](#performance-notes)
6. [Thread Safety](#thread-safety)
7. [License](#license)

---
## Installation
```bash
# Package coming soon (example)
dotnet add package ReactiveMarbles.Extensions
```
Reference the project directly while developing locally.

---
## Quick Start
```csharp
using System;
using System.Reactive.Linq;
using ReactiveMarbles.Extensions;

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
| Timing & Scheduling | `SyncTimer`, `Schedule` (overloads), `ScheduleSafe`, `ThrottleFirst`, `DebounceImmediate` |
| Inactivity / Liveness | `Heartbeat`, `DetectStale`, `BufferUntilInactive` |
| Error Handling | `CatchIgnore`, `CatchAndReturn`, `OnErrorRetry` (overloads), `RetryWithBackoff` |
| Combining & Aggregation | `CombineLatestValuesAreAllTrue`, `CombineLatestValuesAreAllFalse`, `GetMax`, `GetMin`, `Partition` |
| Logical / Boolean | `Not`, `WhereTrue`, `WhereFalse` |
| Async / Task | `SelectAsyncSequential`, `SelectLatestAsync`, `SelectAsyncConcurrent`, `SubscribeAsync` (overloads), `SynchronizeSynchronous`, `SynchronizeAsync`, `SubscribeSynchronous` (overloads) |
| Backpressure | `Conflate` |
| Filtering / Conditional | `Filter` (Regex), `TakeUntil` (predicate), `WaitUntil` |
| Buffering | `BufferUntil`, `BufferUntilInactive` |
| Transformation & Utility | `Shuffle`, `ForEach`, `FromArray`, `Using`, `While`, `Start`, `OnNext` (params helper), `DoOnSubscribe`, `DoOnDispose` |

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
a flakySafe = flaky.CatchIgnore();

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
a inputs.SubscribeSynchronous(async i => await Task.Delay(25));
```

### Backpressure / Conflation
```csharp
// Conflate: enforce minimum spacing between emissions while always outputting the most recent value
a var noisy = Observable.Interval(TimeSpan.FromMilliseconds(20)).Take(30);
var conflated = noisy.Conflate(TimeSpan.FromMilliseconds(200), Scheduler.Default);
```

### Selective & Conditional Emission
```csharp
// TakeUntil predicate (inclusive)
var untilFive = Observable.Range(1, 100).TakeUntil(x => x == 5);

// WaitUntil first match then complete
var firstEven = Observable.Range(1, 10).WaitUntil(x => x % 2 == 0);
```

### Buffering & Transformation
```csharp
// BufferUntil - collect chars between delimiters
var chars = "<a><bc><d>".ToCharArray().ToObservable();
var frames = chars.BufferUntil('<', '>'); // emits "<a>", "<bc>", "<d>"

// Shuffle arrays in-place
var arrays = Observable.Return(new[] { 1, 2, 3, 4, 5 });
var shuffled = arrays.Shuffle();
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
```

---
## Performance Notes
- `FastForEach` path avoids iterator allocations for `List<T>`, `IList<T>`, and arrays.
- `SyncTimer` ensures only one shared timer per period reducing timer overhead.
- `Conflate` helps tame high–frequency producers without dropping the final value of a burst.
- `Heartbeat` and `DetectStale` use lightweight scheduling primitives.
- Most operators avoid capturing lambdas in hot loops where practical.

## Thread Safety
- All operators are pure functional transformations unless documented otherwise.
- `SyncTimer` uses a `ConcurrentDictionary` and returns a hot `IConnectableObservable` that connects once per unique `TimeSpan`.
- Methods returning shared observables (`SyncTimer`, `Partition` result sequences) are safe for multi-subscriber usage unless the upstream is inherently side-effecting.

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
- Added flow control (`Conflate`, `ThrottleFirst`, `DebounceImmediate`).

---
Happy reactive coding! ??
