// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Per-emission steady-state cost for a cluster of remaining sync helpers that hadn't yet been
/// benchmarked: <c>SwitchIfEmpty</c>, <c>SampleLatest</c>, <c>ReplayLastOnSubscribe</c>,
/// <c>ObserveOnIf</c> (immediate scheduler — measures the conditional-bypass overhead),
/// <c>FromArray</c>, and <c>RunAll</c>.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class MiscSyncOperatorBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Initial value used by the <c>ReplayLastOnSubscribe</c> pipeline.</summary>
    private const int InitialValue = -1;

    /// <summary>Sampler-trigger value reused by every <c>SampleLatest</c> tick.</summary>
    private static readonly object _samplerSentinel = new();

    /// <summary>Pre-built array source for the <c>FromArray</c> benchmark.</summary>
    private static readonly int[] _arrayPayload = [0, 1, 2, 3, 4, 5, 6, 7];

    /// <summary>Pre-built one-shot Unit observables for the <c>RunAll</c> benchmark.</summary>
    private static readonly IReadOnlyList<IObservable<Unit>> _runAllSources =
    [
        Observables.Return(Unit.Default),
        Observables.Return(Unit.Default),
        Observables.Return(Unit.Default),
        Observables.Return(Unit.Default),
    ];

    /// <summary>Source for the SwitchIfEmpty pipeline.</summary>
    private readonly Subject<int> _switchIfEmptySource = new();

    /// <summary>Source for the SampleLatest pipeline.</summary>
    private readonly Subject<int> _sampleLatestSource = new();

    /// <summary>Trigger for the SampleLatest pipeline.</summary>
    private readonly Subject<object> _sampleLatestTrigger = new();

    /// <summary>Source for the ReplayLastOnSubscribe pipeline.</summary>
    private readonly Subject<int> _replayLastSource = new();

    /// <summary>Source for the ObserveOnIf pipeline.</summary>
    private readonly Subject<int> _observeOnIfSource = new();

    /// <summary>Reused sink.</summary>
    private readonly NoopObserver<int> _intSink = new();

    /// <summary>Reused unit sink.</summary>
    private readonly NoopObserver<Unit> _unitSink = new();

    /// <summary>Subscription on the SwitchIfEmpty pipeline.</summary>
    private IDisposable _switchIfEmptySubscription = null!;

    /// <summary>Subscription on the SampleLatest pipeline.</summary>
    private IDisposable _sampleLatestSubscription = null!;

    /// <summary>Subscription on the ReplayLastOnSubscribe pipeline.</summary>
    private IDisposable _replayLastSubscription = null!;

    /// <summary>Subscription on the ObserveOnIf pipeline.</summary>
    private IDisposable _observeOnIfSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed per invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires every pipeline.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _switchIfEmptySubscription = _switchIfEmptySource
            .SwitchIfEmpty(Observable.Empty<int>())
            .Subscribe(_intSink);
        _sampleLatestSubscription = _sampleLatestSource
            .SampleLatest(_sampleLatestTrigger)
            .Subscribe(_intSink);
        _replayLastSubscription = _replayLastSource
            .ReplayLastOnSubscribe(InitialValue)
            .Subscribe(_intSink);
        _observeOnIfSubscription = _observeOnIfSource
            .ObserveOnIf(false, Scheduler.Immediate)
            .Subscribe(_intSink);
    }

    /// <summary>Tears every pipeline down.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _switchIfEmptySubscription.Dispose();
        _sampleLatestSubscription.Dispose();
        _replayLastSubscription.Dispose();
        _observeOnIfSubscription.Dispose();
        _switchIfEmptySource.Dispose();
        _sampleLatestSource.Dispose();
        _sampleLatestTrigger.Dispose();
        _replayLastSource.Dispose();
        _observeOnIfSource.Dispose();
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the SwitchIfEmpty pipeline (no empty fallback fires).</summary>
    [Benchmark]
    public void SwitchIfEmpty_HappyPath()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _switchIfEmptySource.OnNext(i);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> source+trigger pairs through the SampleLatest pipeline.</summary>
    [Benchmark]
    public void SampleLatest_AlternatingTriggers()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _sampleLatestSource.OnNext(i);
            _sampleLatestTrigger.OnNext(_samplerSentinel);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the ReplayLastOnSubscribe steady-state path.</summary>
    [Benchmark]
    public void ReplayLastOnSubscribe_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _replayLastSource.OnNext(i);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the bypass branch of ObserveOnIf.</summary>
    [Benchmark]
    public void ObserveOnIf_FalseBypass()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _observeOnIfSource.OnNext(i);
        }
    }

    /// <summary>Per-invocation FromArray subscribe + drain over a small array.</summary>
    [Benchmark]
    public void FromArray_DrainAndDispose()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            using var sub = _arrayPayload.FromArray().Subscribe(_intSink);
        }
    }

    /// <summary>Per-invocation RunAll subscribe + drain over a static 4-source list.</summary>
    [Benchmark]
    public void RunAll_FourReturnSources()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            using var sub = _runAllSources.RunAll().Subscribe(_unitSink);
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

    /// <summary>No-op observer.</summary>
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
