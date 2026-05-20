// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Per-emission steady-state cost of the fused sync operators that replace common multi-stage
/// LINQ-style chains: <c>SelectConstant</c>, <c>WhereSelect</c>, <c>TrySelect</c>, and
/// <c>SelectManyThen</c>. The fused versions exist to elide intermediate observer allocations;
/// the benchmark locks in their zero-alloc steady state.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SyncSelectFusionBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Constant value emitted by the <see cref="SelectConstant_PerEmission"/> pipeline.</summary>
    private const int ConstantValue = 1;

    /// <summary>Cached non-null reference returned by the TrySelect projection.</summary>
    private const string TrySelectSentinel = "x";

    /// <summary>Pre-built inner observable reused by the <c>SelectManyThen</c> pipeline.</summary>
    private static readonly IObservable<int> _innerObservable = new InlineSingleValueObservable<int>(0);

    /// <summary>Source for the SelectConstant pipeline.</summary>
    private readonly Subject<int> _selectConstantSource = new();

    /// <summary>Source for the WhereSelect pipeline.</summary>
    private readonly Subject<int> _whereSelectSource = new();

    /// <summary>Source for the TrySelect pipeline.</summary>
    private readonly Subject<int> _trySelectSource = new();

    /// <summary>Source for the SelectManyThen pipeline.</summary>
    private readonly Subject<int> _selectManyThenSource = new();

    /// <summary>Reused sinks.</summary>
    private readonly NoopObserver<int> _intSink = new();

    /// <summary>Reused sinks.</summary>
    private readonly NoopObserver<string> _stringSink = new();

    /// <summary>Subscription on the SelectConstant pipeline.</summary>
    private IDisposable _selectConstantSubscription = null!;

    /// <summary>Subscription on the WhereSelect pipeline.</summary>
    private IDisposable _whereSelectSubscription = null!;

    /// <summary>Subscription on the TrySelect pipeline.</summary>
    private IDisposable _trySelectSubscription = null!;

    /// <summary>Subscription on the SelectManyThen pipeline.</summary>
    private IDisposable _selectManyThenSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires every pipeline.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _selectConstantSubscription = _selectConstantSource.SelectConstant(ConstantValue).Subscribe(_intSink);
        _whereSelectSubscription = _whereSelectSource
            .WhereSelect(static x => (x & 1) == 0, static x => x + 1)
            .Subscribe(_intSink);
        _trySelectSubscription = _trySelectSource
            .TrySelect<int, string>(static _ => TrySelectSentinel)
            .Subscribe(_stringSink);
        _selectManyThenSubscription = _selectManyThenSource
            .SelectManyThen(static _ => _innerObservable, static _ => _innerObservable)
            .Subscribe(_intSink);
    }

    /// <summary>Tears every pipeline down.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _selectConstantSubscription.Dispose();
        _whereSelectSubscription.Dispose();
        _trySelectSubscription.Dispose();
        _selectManyThenSubscription.Dispose();
        _selectConstantSource.Dispose();
        _whereSelectSource.Dispose();
        _trySelectSource.Dispose();
        _selectManyThenSource.Dispose();
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the SelectConstant pipeline.</summary>
    [Benchmark]
    public void SelectConstant_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _selectConstantSource.OnNext(i);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the WhereSelect pipeline (predicate alternates).</summary>
    [Benchmark]
    public void WhereSelect_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _whereSelectSource.OnNext(i);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the TrySelect pipeline (always returns non-null).</summary>
    [Benchmark]
    public void TrySelect_AllNonNull()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _trySelectSource.OnNext(i);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the fused SelectManyThen pipeline.</summary>
    [Benchmark]
    public void SelectManyThen_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _selectManyThenSource.OnNext(i);
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

    /// <summary>Synchronously emits a single value and completes inside the subscribe call.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The value emitted on every subscribe.</param>
    private sealed class InlineSingleValueObservable<T>(T value) : IObservable<T>
    {
        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnNext(value);
            observer.OnCompleted();
            return EmptyDisposable.Instance;
        }
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

    /// <summary>Singleton no-op disposable.</summary>
    private sealed class EmptyDisposable : IDisposable
    {
        /// <summary>Singleton instance.</summary>
        public static readonly EmptyDisposable Instance = new();

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
