// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Drives values through <c>SynchronizeAsync</c> and immediately disposes the per-emission Sync
/// handle. Exercises the <c>Continuation</c> phase-barrier path that signals via
/// <c>Task.Factory.StartNew</c> + state — measures throughput and per-emission allocations on the
/// fire-and-forget signal task.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SynchronizeBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 100;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 1_000;

    /// <summary>Source feeding the synchronize pipeline.</summary>
    private readonly Subject<int> _source = new();

    /// <summary>Subscription on the synchronize pipeline.</summary>
    private IDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the pipeline per invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the pipeline; the downstream observer immediately disposes the per-emission Sync handle.</summary>
    [GlobalSetup]
    public void Setup() =>
        _subscription = _source.SynchronizeAsync().Subscribe(static tuple => tuple.Sync.Dispose());

    /// <summary>Tears the pipeline down.</summary>
    [SuppressMessage(
        "Critical Bug",
        "S2952:Classes should \"Dispose\" of members from the classes' own \"Dispose\" methods",
        Justification = "Cleanup is the BDN sibling of Dispose(bool); Dispose() forwards into it deterministically.")]
    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription.Dispose();
        _source.Dispose();
    }

    /// <summary>Drives values through the synchronize pipeline; each tuple's Sync is disposed inline by the sink.</summary>
    [Benchmark]
    public void SynchronizeAsync_FastDispose()
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
