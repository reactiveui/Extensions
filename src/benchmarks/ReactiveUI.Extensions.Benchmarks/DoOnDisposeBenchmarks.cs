// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the steady-state per-emission cost and the subscribe/dispose churn cost of
/// <c>DoOnDispose</c>, which fires a caller-supplied action when the subscription is disposed.
/// Steady-state numbers reflect pass-through; churn numbers reflect the per-subscribe + dispose
/// allocations including the disposal callback dispatch.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class DoOnDisposeBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="WorkCount"/> parameter sweep.</summary>
    private const int SmallWorkCount = 1_000;

    /// <summary>High end of the <see cref="WorkCount"/> parameter sweep.</summary>
    private const int LargeWorkCount = 10_000;

    /// <summary>Static no-op dispose action shared by every subscription.</summary>
    private static readonly Action NoopDisposeAction = static () => { };

    /// <summary>Source for the steady-state benchmark.</summary>
    private readonly Subject<int> _steadySource = new();

    /// <summary>Source for the subscribe-churn benchmark.</summary>
    private readonly Subject<int> _churnSource = new();

    /// <summary>No-op sink for both pipelines.</summary>
    private readonly NoopObserver<int> _sink = new();

    /// <summary>Pre-built DoOnDispose observable reused across churn iterations.</summary>
    private IObservable<int> _churnPipeline = null!;

    /// <summary>Subscription on the steady-state DoOnDispose pipeline.</summary>
    private IDisposable _steadySubscription = null!;

    /// <summary>Gets or sets the number of emissions / churn cycles per benchmark invocation.</summary>
    [Params(SmallWorkCount, LargeWorkCount)]
    public int WorkCount { get; set; }

    /// <summary>Wires both pipelines.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _steadySubscription = _steadySource.DoOnDispose(NoopDisposeAction).Subscribe(_sink);
        _churnPipeline = _churnSource.DoOnDispose(NoopDisposeAction);
    }

    /// <summary>Tears both pipelines down.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _steadySubscription.Dispose();
        _steadySource.Dispose();
        _churnSource.Dispose();
    }

    /// <summary>Drives <see cref="WorkCount"/> emissions through the long-lived DoOnDispose pipeline.</summary>
    [Benchmark]
    public void DoOnDispose_SteadyState()
    {
        for (var i = 0; i < WorkCount; i++)
        {
            _steadySource.OnNext(i);
        }
    }

    /// <summary>Subscribes and immediately disposes the DoOnDispose pipeline <see cref="WorkCount"/> times.</summary>
    [Benchmark]
    public void DoOnDispose_SubscribeAndDispose()
    {
        for (var i = 0; i < WorkCount; i++)
        {
            using var subscription = _churnPipeline.Subscribe(_sink);
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

    /// <summary>No-op observer used as the terminal sink.</summary>
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
