// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the subscribe-and-drain cost of the predicate-driven terminal aggregates:
/// <c>AggregateAsync</c> (full-drain reduction), <c>LongCountAsync</c> (full drain), and the
/// short-circuiting <c>AnyAsync</c> / <c>AllAsync</c> / <c>ContainsAsync</c>. Each benchmark
/// subscribes to a fresh <see cref="ObservableAsync.Range"/> per invocation; the short-circuit
/// variants exit early on the first match / mismatch.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class AggregateAndPredicateAsyncBenchmarks
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

    /// <summary>Drains the entire sequence and folds it into a running sum.</summary>
    /// <returns>The accumulated sum.</returns>
    [Benchmark]
    public ValueTask<int> Aggregate_RunningSum() =>
        _source.AggregateAsync(0, static (acc, x) => acc + x);

    /// <summary>Drains the entire sequence and returns its long count.</summary>
    /// <returns>The element count as a long.</returns>
    [Benchmark]
    public ValueTask<long> LongCount_FullDrain() => _source.LongCountAsync();

    /// <summary>Returns true on the first element — short-circuits on the very first emission.</summary>
    /// <returns><see langword="true"/> as long as the source emits at least one value.</returns>
    [Benchmark]
    public ValueTask<bool> Any_FirstElement() => _source.AnyAsync();

    /// <summary>Returns true only if every element matches; drains the entire sequence on a uniformly-passing predicate.</summary>
    /// <returns><see langword="true"/> when every emission matches the predicate.</returns>
    [Benchmark]
    public ValueTask<bool> All_FullDrain() =>
        _source.AllAsync(static x => x >= 0);

    /// <summary>Searches for a value that does not exist, drains the entire sequence.</summary>
    /// <returns><see langword="false"/> when the target is absent.</returns>
    [Benchmark]
    public ValueTask<bool> Contains_MissingTarget() =>
        _source.ContainsAsync(-1);
}
