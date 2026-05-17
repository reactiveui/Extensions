// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>GC-verbose hotspot profile for the terminal aggregates.</summary>
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class TerminalAggregateGcProfileBenchmarks
{
    /// <summary>Number of values emitted per drain.</summary>
    private const int DrainCount = 1_000;

    /// <summary>Pre-built finite source.</summary>
    private IObservableAsync<int> _source = null!;

    /// <summary>Builds the source once.</summary>
    [GlobalSetup]
    public void Setup() => _source = ObservableAsync.Range(0, DrainCount);

    /// <summary>FirstAsync drain.</summary>
    /// <returns>The first value.</returns>
    [Benchmark]
    public ValueTask<int> FirstAsync_FirstElement() => _source.FirstAsync();

    /// <summary>CountAsync full drain.</summary>
    /// <returns>The element count.</returns>
    [Benchmark]
    public ValueTask<int> CountAsync_FullDrain() => _source.CountAsync();

    /// <summary>ToListAsync full drain.</summary>
    /// <returns>The materialised list.</returns>
    [Benchmark]
    public ValueTask<List<int>> ToListAsync_FullDrain() => _source.ToListAsync();

    /// <summary>LastAsync full drain.</summary>
    /// <returns>The last value.</returns>
    [Benchmark]
    public ValueTask<int> LastAsync_FullDrain() => _source.LastAsync();
}
