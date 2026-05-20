// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-emission cost of <c>SynchronizeSynchronous</c>, the synchronous counterpart
/// to <c>SynchronizeAsync</c>. The downstream sink disposes the per-emission Sync handle inline.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SynchronizeSynchronousBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 100;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 1_000;

    /// <summary>Source feeding the SynchronizeSynchronous pipeline.</summary>
    private readonly Subject<int> _source = new();

    /// <summary>Subscription on the SynchronizeSynchronous pipeline.</summary>
    private IDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the SynchronizeSynchronous pipeline; the sink disposes the Sync handle inline.</summary>
    [GlobalSetup]
    public void Setup() =>
        _subscription = _source.SynchronizeSynchronous().Subscribe(static tuple => tuple.Sync.Dispose());

    /// <summary>Tears the pipeline down.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription.Dispose();
        _source.Dispose();
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the SynchronizeSynchronous pipeline.</summary>
    [Benchmark]
    public void SynchronizeSynchronous_FastDispose()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _source.OnNext(i);
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
}
