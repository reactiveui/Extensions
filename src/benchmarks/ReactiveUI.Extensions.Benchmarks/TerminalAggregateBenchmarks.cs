// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the full subscribe-and-drain cost of the terminal aggregate operators against an
/// observable that emits a fixed-size sequence and completes. Unlike the streaming operator
/// benchmarks (which measure per-emission cost on a pre-subscribed pipeline), each invocation
/// here pays both the subscription / dispose cost and the per-element work.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class TerminalAggregateBenchmarks
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 100;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 1_000;

    /// <summary>Pre-built source emitting <see cref="EmissionCount"/> integers then completing.</summary>
    private IObservableAsync<int> _source = null!;

    /// <summary>Gets or sets the number of values the source emits before completing.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Builds the finite source.</summary>
    [GlobalSetup]
    public void Setup() => _source = ObservableAsync.Range(0, EmissionCount);

    /// <summary>Subscribes, awaits the first value, disposes.</summary>
    /// <returns>The first emitted value.</returns>
    [Benchmark]
    public ValueTask<int> FirstAsync_FirstElement() => _source.FirstAsync();

    /// <summary>Drains the entire sequence and returns its count.</summary>
    /// <returns>The number of elements emitted.</returns>
    [Benchmark]
    public ValueTask<int> CountAsync_FullDrain() => _source.CountAsync();

    /// <summary>Drains the entire sequence into a list.</summary>
    /// <returns>The materialised list of every emitted element.</returns>
    [Benchmark]
    public ValueTask<List<int>> ToListAsync_FullDrain() => _source.ToListAsync();

    /// <summary>Subscribes, awaits the last value, disposes.</summary>
    /// <returns>The last emitted value.</returns>
    [Benchmark]
    public ValueTask<int> LastAsync_FullDrain() => _source.LastAsync();
}
