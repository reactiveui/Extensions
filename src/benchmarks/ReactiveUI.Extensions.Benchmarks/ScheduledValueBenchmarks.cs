// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the construct-subscribe-emit cost of the single-value scheduling operators
/// (<c>Schedule(value, ...)</c> backed by <c>ScheduledValueObservable</c>) and the
/// <c>ScheduleSafe</c> helper. Every variant runs on <see cref="Scheduler.Immediate"/> with a zero
/// or already-elapsed due time so the scheduled work executes inline during subscribe, capturing the
/// operator's construction and dispatch overhead rather than timer latency.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "BenchmarkDotNet drives benchmarks through an instance; the methods cannot be static.")]
public class ScheduledValueBenchmarks
{
    /// <summary>Sentinel value scheduled by every benchmark.</summary>
    private const int Value = 42;

    /// <summary>Already-elapsed absolute due time so the absolute-schedule variant fires immediately.</summary>
    private static readonly DateTimeOffset ElapsedDueTime = DateTimeOffset.MinValue;

    /// <summary>Shared no-op sink for the scheduled-value pipelines.</summary>
    private static readonly NoopObserver<int> Sink = new();

    /// <summary>Schedules a single value with a zero <see cref="TimeSpan"/> delay and drains it.</summary>
    [Benchmark]
    public void ScheduleValue_TimeSpan() =>
        Value.Schedule(TimeSpan.Zero, Scheduler.Immediate).Subscribe(Sink).Dispose();

    /// <summary>Schedules a single value at an already-elapsed absolute time and drains it.</summary>
    [Benchmark]
    public void ScheduleValue_DateTimeOffset() =>
        Value.Schedule(ElapsedDueTime, Scheduler.Immediate).Subscribe(Sink).Dispose();

    /// <summary>Schedules a single value with a transform function (no delay) and drains it.</summary>
    [Benchmark]
    public void ScheduleValue_Transform() =>
        Value.Schedule(Scheduler.Immediate, static x => x + 1).Subscribe(Sink).Dispose();

    /// <summary>Schedules a single value with an inspection action and drains it.</summary>
    [Benchmark]
    public void ScheduleValue_Action() =>
        Value.Schedule(TimeSpan.Zero, Scheduler.Immediate, static _ => { }).Subscribe(Sink).Dispose();

    /// <summary>Schedules a no-op action immediately via <c>ScheduleSafe</c>.</summary>
    [Benchmark]
    public void ScheduleSafe_Immediate() =>
        Scheduler.Immediate.ScheduleSafe(static () => { }).Dispose();

    /// <summary>Schedules a no-op action with a zero delay via <c>ScheduleSafe</c>.</summary>
    [Benchmark]
    public void ScheduleSafe_Delayed() =>
        Scheduler.Immediate.ScheduleSafe(TimeSpan.Zero, static () => { }).Dispose();

    /// <summary>No-op synchronous observer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class NoopObserver<T> : IObserver<T>
    {
        /// <inheritdoc/>
        public void OnNext(T value)
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }
    }
}
