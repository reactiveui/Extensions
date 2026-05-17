# ReactiveUI.Extensions.Benchmarks

BenchmarkDotNet harness for judging perf changes to ReactiveUI.Extensions. Mirrors the layout used by
sibling repos (Akavache, NuStreamDocs).

## Run

```bash
cd src/benchmarks/ReactiveUI.Extensions.Benchmarks
dotnet run -c Release
```

That launches the BenchmarkDotNet switcher; pass `--filter '*'` to run every benchmark, or
`--filter '*Subject*'` to run a named subset. Pass `--list flat` to enumerate without running.

## Benchmark classes

| File | Packet | What it measures |
|---|---|---|
| `SubjectBroadcastBenchmarks.cs` | 4 | Per-emission cost of `SerialSubjectAsync<T>` / `SerialStatelessSubjectAsync<T>` broadcasting to 1 / 8 / 64 observers — exercises the indexed-`for` broadcast loop + the `IsCompletedSuccessfully` fast-path in `Concurrent.*`. |
| `ThinOperatorBenchmarks.cs` | 5 | Per-emission cost through a `Select → Where → Scan` pipeline — exercises the sealed `ObserverAsync<T>` subclass observers that replaced `Create<T>` + async-lambda closures. |
| `CombineLatestBenchmarks.cs` | 2, 6 | Per-emission cost of `CombineLatest2` (Packet 2's linked-CTS elimination) and the 4-source enumerable `CombineLatest` (Packet 6's reusable snapshot buffer + sealed `IndexedObserver`). |
| `BridgeBenchmarks.cs` | 8 | Per-emission cost of `Subject<T>.ToObservableAsync()` — exercises the `WorkItem` readonly record struct queue that replaced the `Queue<Action>` + closures. |
| `SubscribeChurnBenchmarks.cs` | 7 | Per-iteration subscribe + unsubscribe on `SerialStatelessSubjectAsync<T>` — exercises Packet 7's `DisposableAsync.Create<TState>(state, static delegate)` overload that removed the closure from the unsubscribe path. |

Each class is decorated with `[MemoryDiagnoser]` so the report includes allocations per operation.

## Adding new benchmarks

* Drop a new `.cs` file under this directory with a `[SimpleJob(RuntimeMoniker.Net90)]` +
  `[MemoryDiagnoser]` class. Methods marked `[Benchmark]` are picked up automatically by the switcher.
* Loop the operation under test enough times in one method body to amortise BenchmarkDotNet's
  per-invocation overhead — `[Params(1_000, 10_000)]` is a useful shape, ranged so the report can
  show how cost scales.
* Keep observers / sinks as named `NoopObserver` classes (not lambdas) so the benchmark itself doesn't
  allocate inside the measured method.
