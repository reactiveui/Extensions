// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the construct-subscribe-take-first cost of the one-shot <c>Timer</c> factory, wired
/// against <see cref="BenchmarkImmediateFireTimeProvider"/> so the dueTime fires synchronously.
/// The measurement reflects subscribe / pump-startup / first-emission cost without scheduler
/// latency.
/// </summary>
/// <remarks>
/// The recurring <c>Interval</c> benchmark uses <see cref="BenchmarkAsyncFireTimeProvider"/>
/// rather than the immediate-fire variant because the immediate-fire variant would recurse
/// infinitely on every tick's rearm (each <c>ITimer.Change</c> would synchronously fire the
/// next tick on the same stack, exhausting it). Queueing callbacks to the threadpool matches
/// the natural shape of <see cref="TimeProvider.System"/> via <c>Task.Delay</c>: every tick
/// reaches a true suspension point so the surrounding await observes its value, unsubscribes,
/// and the pump exits cleanly.
/// </remarks>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class IntervalTimerBenchmarks
{
    /// <summary>Duration parameter ignored at wall-clock level because the time provider fires synchronously.</summary>
    private static readonly TimeSpan IgnoredDuration = TimeSpan.FromMilliseconds(50);

    /// <summary>Deterministic <see cref="TimeProvider"/> that fires each timer synchronously — safe for one-shot Timer.</summary>
    private readonly BenchmarkImmediateFireTimeProvider _immediateProvider = new();

    /// <summary>Asynchronously-firing <see cref="TimeProvider"/> for recurring pumps; yields the threadpool between ticks.</summary>
    private readonly BenchmarkAsyncFireTimeProvider _asyncProvider = new();

    /// <summary>One-shot Timer drained for its first (and only useful) emission.</summary>
    /// <returns>The Timer's first emitted tick value.</returns>
    [Benchmark]
    public ValueTask<long> Timer_FirstTick() =>
        ObservableAsync.Timer(IgnoredDuration, _immediateProvider).FirstAsync();

    /// <summary>Drains the first tick of an Interval; uses the async-fire provider so the pump yields between ticks.</summary>
    /// <returns>The Interval's first emitted tick value.</returns>
    [Benchmark]
    public ValueTask<long> Interval_FirstTick() =>
        ObservableAsync.Interval(IgnoredDuration, _asyncProvider).FirstAsync();
}
