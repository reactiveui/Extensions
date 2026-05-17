// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Per-emission broadcast cost of <see cref="CurrentValueSubject{T}"/> with one and four observers,
/// and the subscribe+replay cost. Locks in the single-observer fast path and the immutable-array
/// snapshot iteration.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class CurrentValueSubjectBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Initial seed value held by the subject.</summary>
    private const int Seed = 0;

    /// <summary>Subject driven by the single-observer broadcast benchmark.</summary>
    private readonly CurrentValueSubject<int> _singleObserverSubject = new(Seed);

    /// <summary>Subject driven by the four-observer broadcast benchmark.</summary>
    private readonly CurrentValueSubject<int> _multiObserverSubject = new(Seed);

    /// <summary>No-op observer used as the single-observer sink.</summary>
    private readonly NoopObserver _singleSink = new();

    /// <summary>Sinks attached to the multi-observer subject.</summary>
    private readonly NoopObserver[] _multiSinks =
    [
        new(),
        new(),
        new(),
        new(),
    ];

    /// <summary>Subscription on the single-observer subject.</summary>
    private IDisposable _singleSubscription = null!;

    /// <summary>Subscriptions on the multi-observer subject.</summary>
    private IDisposable[] _multiSubscriptions = null!;

    /// <summary>Gets or sets the number of emissions pushed through each subject per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires both subjects and their observers.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _singleSubscription = _singleObserverSubject.Subscribe(_singleSink);
        _multiSubscriptions = new IDisposable[_multiSinks.Length];
        for (var i = 0; i < _multiSinks.Length; i++)
        {
            _multiSubscriptions[i] = _multiObserverSubject.Subscribe(_multiSinks[i]);
        }
    }

    /// <summary>Tears the subjects and subscriptions down.</summary>
    [SuppressMessage(
        "Critical Bug",
        "S2952:Classes should \"Dispose\" of members from the classes' own \"Dispose\" methods",
        Justification = "Cleanup is the BDN sibling of Dispose(bool); Dispose() forwards into it deterministically.")]
    [GlobalCleanup]
    public void Cleanup()
    {
        _singleSubscription.Dispose();
        _singleObserverSubject.Dispose();
        for (var i = 0; i < _multiSubscriptions.Length; i++)
        {
            _multiSubscriptions[i].Dispose();
        }

        _multiObserverSubject.Dispose();
    }

    /// <summary>Broadcasts through the single-observer fast path (length-1 snapshot).</summary>
    [Benchmark]
    public void Broadcast_SingleObserver()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _singleObserverSubject.OnNext(i);
        }
    }

    /// <summary>Broadcasts through the multi-observer loop (length-4 snapshot).</summary>
    [Benchmark]
    public void Broadcast_FourObservers()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _multiObserverSubject.OnNext(i);
        }
    }

    /// <summary>Measures subscribe + replay + immediate dispose churn.</summary>
    [Benchmark]
    public void SubscribeReplayDispose()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            using var subscription = _singleObserverSubject.Subscribe(_singleSink);
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

    /// <summary>No-op observer reused as the terminal sink.</summary>
    private sealed class NoopObserver : IObserver<int>
    {
        /// <inheritdoc/>
        public void OnNext(int value)
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
