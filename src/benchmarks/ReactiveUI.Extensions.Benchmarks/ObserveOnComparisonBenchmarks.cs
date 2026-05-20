// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Head-to-head spike comparing our own <c>ObserveOnSafe</c> (backed by <c>ObserveOnObservable</c>)
/// against <c>System.Reactive</c>'s <c>Observable.ObserveOn</c> on the immediate scheduler (where our
/// operator takes a synchronous pass-through fast-path) and the current-thread scheduler (where both
/// run the queue-and-drain marshaller). Sinks are no-ops so the numbers reflect the marshalling
/// machinery, not downstream work.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ObserveOnComparisonBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Shared no-op sink.</summary>
    private readonly NoopObserver<int> _sink = new();

    /// <summary>Source feeding our immediate-scheduler pipeline.</summary>
    private readonly Subject<int> _oursImmediateSource = new();

    /// <summary>Source feeding the System.Reactive immediate-scheduler pipeline.</summary>
    private readonly Subject<int> _rxImmediateSource = new();

    /// <summary>Source feeding our current-thread pipeline.</summary>
    private readonly Subject<int> _oursCurrentThreadSource = new();

    /// <summary>Source feeding the System.Reactive current-thread pipeline.</summary>
    private readonly Subject<int> _rxCurrentThreadSource = new();

    /// <summary>Subscription on our immediate-scheduler pipeline.</summary>
    private IDisposable _oursImmediateSub = null!;

    /// <summary>Subscription on the System.Reactive immediate-scheduler pipeline.</summary>
    private IDisposable _rxImmediateSub = null!;

    /// <summary>Subscription on our current-thread pipeline.</summary>
    private IDisposable _oursCurrentThreadSub = null!;

    /// <summary>Subscription on the System.Reactive current-thread pipeline.</summary>
    private IDisposable _rxCurrentThreadSub = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires all four pipelines.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _oursImmediateSub = _oursImmediateSource.ObserveOnSafe(Scheduler.Immediate).Subscribe(_sink);
        _rxImmediateSub = _rxImmediateSource.ObserveOn(Scheduler.Immediate).Subscribe(_sink);
        _oursCurrentThreadSub = _oursCurrentThreadSource.ObserveOnSafe(Scheduler.CurrentThread).Subscribe(_sink);
        _rxCurrentThreadSub = _rxCurrentThreadSource.ObserveOn(Scheduler.CurrentThread).Subscribe(_sink);
    }

    /// <summary>Tears all four pipelines down.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _oursImmediateSub.Dispose();
        _rxImmediateSub.Dispose();
        _oursCurrentThreadSub.Dispose();
        _rxCurrentThreadSub.Dispose();
        _oursImmediateSource.Dispose();
        _rxImmediateSource.Dispose();
        _oursCurrentThreadSource.Dispose();
        _rxCurrentThreadSource.Dispose();
    }

    /// <summary>Our ObserveOnSafe on the immediate scheduler (synchronous pass-through fast-path).</summary>
    [Benchmark(Baseline = true)]
    public void Ours_Immediate()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _oursImmediateSource.OnNext(i);
        }
    }

    /// <summary>System.Reactive ObserveOn on the immediate scheduler.</summary>
    [Benchmark]
    public void Rx_Immediate()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _rxImmediateSource.OnNext(i);
        }
    }

    /// <summary>Our ObserveOnSafe on the current-thread scheduler (queue + drain).</summary>
    [Benchmark]
    public void Ours_CurrentThread()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _oursCurrentThreadSource.OnNext(i);
        }
    }

    /// <summary>System.Reactive ObserveOn on the current-thread scheduler.</summary>
    [Benchmark]
    public void Rx_CurrentThread()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _rxCurrentThreadSource.OnNext(i);
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
