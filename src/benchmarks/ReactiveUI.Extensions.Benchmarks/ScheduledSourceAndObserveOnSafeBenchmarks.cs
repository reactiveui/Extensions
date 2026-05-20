// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-emission cost of the sync source-scheduling operators:
/// <c>Schedule(source, dueTime, scheduler)</c> and <c>ObserveOnSafe(scheduler)</c>. Both run on
/// <see cref="Scheduler.Immediate"/> with a zero delay so each scheduled action executes inline,
/// isolating the operator's per-emission overhead from real timer latency.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ScheduledSourceAndObserveOnSafeBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Shared no-op sink.</summary>
    private readonly NoopObserver<int> _sink = new();

    /// <summary>Source feeding the <c>Schedule(source)</c> pipeline.</summary>
    private readonly Subject<int> _scheduleSource = new();

    /// <summary>Source feeding the <c>ObserveOnSafe</c> pipeline.</summary>
    private readonly Subject<int> _observeOnSafeSource = new();

    /// <summary>Subscription on the <c>Schedule(source)</c> pipeline.</summary>
    private IDisposable _scheduleSubscription = null!;

    /// <summary>Subscription on the <c>ObserveOnSafe</c> pipeline.</summary>
    private IDisposable _observeOnSafeSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires both pipelines against the immediate scheduler.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _scheduleSubscription = ((IObservable<int>)_scheduleSource).Schedule(TimeSpan.Zero, Scheduler.Immediate).Subscribe(_sink);
        _observeOnSafeSubscription = _observeOnSafeSource.ObserveOnSafe(Scheduler.Immediate).Subscribe(_sink);
    }

    /// <summary>Tears both pipelines down.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _scheduleSubscription.Dispose();
        _observeOnSafeSubscription.Dispose();
        _scheduleSource.Dispose();
        _observeOnSafeSource.Dispose();
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through <c>Schedule(source, TimeSpan.Zero, Immediate)</c>.</summary>
    [Benchmark]
    public void ScheduleSource_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _scheduleSource.OnNext(i);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through <c>ObserveOnSafe(Immediate)</c>.</summary>
    [Benchmark]
    public void ObserveOnSafe_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _observeOnSafeSource.OnNext(i);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Drains synchronous teardown.</summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Cleanup();
    }

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
