// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-emission cost of the sync filter / projection helpers in
/// <see cref="ReactiveExtensions"/>: <c>AsSignal</c>, <c>Not</c>, <c>WhereTrue</c>,
/// <c>WhereIsNotNull</c>. Each helper now has its own typed <see cref="IObservable{T}"/> so this
/// benchmark exists primarily to lock in the single-observer-layer baseline and catch any future
/// regressions on the sync surface.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SyncFilterAndProjectionBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Pre-boxed non-null reference reused across <see cref="WhereIsNotNull_AllPassing"/> emissions.</summary>
    private const string SharedNonNull = "x";

    /// <summary>Source for the <c>AsSignal</c> pipeline; initial-value replay fires once during setup.</summary>
    private readonly CurrentValueSubject<int> _signalSource = new(0);

    /// <summary>Source for the <c>Not</c> pipeline; initial-value replay fires once during setup.</summary>
    private readonly CurrentValueSubject<bool> _notSource = new(false);

    /// <summary>Source for the <c>WhereTrue</c> pipeline; initial-value replay fires once during setup.</summary>
    private readonly CurrentValueSubject<bool> _whereTrueSource = new(false);

    /// <summary>Source for the <c>WhereIsNotNull</c> pipeline; initial-value replay fires once during setup.</summary>
    private readonly CurrentValueSubject<string> _whereNotNullSource = new(SharedNonNull);

    /// <summary>No-op sink used for the <see cref="Unit"/> stream.</summary>
    private readonly NoopObserver<Unit> _unitSink = new();

    /// <summary>No-op sink used for the boolean streams.</summary>
    private readonly NoopObserver<bool> _boolSink = new();

    /// <summary>No-op sink used for the string stream.</summary>
    private readonly NoopObserver<string> _stringSink = new();

    /// <summary>Subscription on the <c>AsSignal</c> pipeline.</summary>
    private IDisposable _signalSubscription = null!;

    /// <summary>Subscription on the <c>Not</c> pipeline.</summary>
    private IDisposable _notSubscription = null!;

    /// <summary>Subscription on the <c>WhereTrue</c> pipeline.</summary>
    private IDisposable _whereTrueSubscription = null!;

    /// <summary>Subscription on the <c>WhereIsNotNull</c> pipeline.</summary>
    private IDisposable _whereNotNullSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires each pipeline.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _signalSubscription = _signalSource.AsSignal().Subscribe(_unitSink);
        _notSubscription = _notSource.Not().Subscribe(_boolSink);
        _whereTrueSubscription = _whereTrueSource.WhereTrue().Subscribe(_boolSink);
        _whereNotNullSubscription = _whereNotNullSource.WhereIsNotNull().Subscribe(_stringSink);
    }

    /// <summary>Tears each pipeline down.</summary>
    [SuppressMessage(
        "Critical Bug",
        "S2952:Classes should \"Dispose\" of members from the classes' own \"Dispose\" methods",
        Justification = "Cleanup is the BDN sibling of Dispose(bool); Dispose() forwards into it deterministically.")]
    [GlobalCleanup]
    public void Cleanup()
    {
        _signalSubscription.Dispose();
        _signalSource.Dispose();
        _notSubscription.Dispose();
        _notSource.Dispose();
        _whereTrueSubscription.Dispose();
        _whereTrueSource.Dispose();
        _whereNotNullSubscription.Dispose();
        _whereNotNullSource.Dispose();
    }

    /// <summary>Drives values through <c>AsSignal</c>; every emission becomes <see cref="Unit.Default"/>.</summary>
    [Benchmark]
    public void AsSignal_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _signalSource.OnNext(i);
        }
    }

    /// <summary>Drives alternating booleans through <c>Not</c>; every emission flips.</summary>
    [Benchmark]
    public void Not_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _notSource.OnNext((i & 1) == 0);
        }
    }

    /// <summary>Drives uniformly-<c>true</c> booleans through <c>WhereTrue</c> (every value passes).</summary>
    [Benchmark]
    public void WhereTrue_AllPassing()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _whereTrueSource.OnNext(true);
        }
    }

    /// <summary>Drives a shared non-null reference through <c>WhereIsNotNull</c> (every value passes).</summary>
    [Benchmark]
    public void WhereIsNotNull_AllPassing()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _whereNotNullSource.OnNext(SharedNonNull);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Drains the synchronous teardown.</summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Cleanup();
    }

    /// <summary>No-op observer used as the terminal sink for each pipeline.</summary>
    /// <typeparam name="T">The element type the sink consumes.</typeparam>
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
