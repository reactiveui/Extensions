// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-emission steady-state cost of <c>CatchReturn(T)</c> and
/// <c>CatchReturnUnit</c> — both forward source values verbatim on the happy path; the fallback
/// fires only on error. Locks in the pass-through baseline so future error-handling additions
/// don't accidentally regress the no-error path.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class CatchReturnBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Fallback value used by the typed <c>CatchReturn</c> pipeline (never triggered on the happy path).</summary>
    private const int Fallback = -1;

    /// <summary>Source for the typed CatchReturn pipeline.</summary>
    private readonly Subject<int> _intSource = new();

    /// <summary>Source for the Unit CatchReturnUnit pipeline.</summary>
    private readonly Subject<Unit> _unitSource = new();

    /// <summary>No-op int sink.</summary>
    private readonly NoopObserver<int> _intSink = new();

    /// <summary>No-op Unit sink.</summary>
    private readonly NoopObserver<Unit> _unitSink = new();

    /// <summary>Subscription on the typed CatchReturn pipeline.</summary>
    private IDisposable _intSubscription = null!;

    /// <summary>Subscription on the Unit CatchReturnUnit pipeline.</summary>
    private IDisposable _unitSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires both CatchReturn pipelines.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _intSubscription = _intSource.CatchReturn(Fallback).Subscribe(_intSink);
        _unitSubscription = _unitSource.CatchReturnUnit().Subscribe(_unitSink);
    }

    /// <summary>Tears both pipelines down.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _intSubscription.Dispose();
        _unitSubscription.Dispose();
        _intSource.Dispose();
        _unitSource.Dispose();
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the typed CatchReturn pipeline (no errors).</summary>
    [Benchmark]
    public void CatchReturn_HappyPath()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _intSource.OnNext(i);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> Unit values through the CatchReturnUnit pipeline (no errors).</summary>
    [Benchmark]
    public void CatchReturnUnit_HappyPath()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _unitSource.OnNext(Unit.Default);
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
