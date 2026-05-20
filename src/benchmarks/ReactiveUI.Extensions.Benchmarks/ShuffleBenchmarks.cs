// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-emission cost of <c>Shuffle</c>, which randomises an emitted array in-place
/// or via a fresh copy on each emission. Drives a constant-length array source so the steady-state
/// numbers reflect the operator's shuffle work, not array construction.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ShuffleBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Length of the array emitted per cycle.</summary>
    private const int ArrayLength = 16;

    /// <summary>Source feeding the Shuffle pipeline.</summary>
    private readonly Subject<int[]> _source = new();

    /// <summary>No-op sink absorbing the shuffled arrays.</summary>
    private readonly NoopObserver<int[]> _sink = new();

    /// <summary>Pre-built array emitted once per cycle.</summary>
    private int[] _payload = null!;

    /// <summary>Subscription on the Shuffle pipeline.</summary>
    private IDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the Shuffle pipeline.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _payload = new int[ArrayLength];
        for (var i = 0; i < _payload.Length; i++)
        {
            _payload[i] = i;
        }

        _subscription = _source.Shuffle().Subscribe(_sink);
    }

    /// <summary>Tears the pipeline down.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription.Dispose();
        _source.Dispose();
    }

    /// <summary>Drives <see cref="EmissionCount"/> array emissions through the Shuffle pipeline.</summary>
    [Benchmark]
    public void Shuffle_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _source.OnNext(_payload);
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
