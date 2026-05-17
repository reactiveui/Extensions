// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the additional terminal-aggregate overloads that take projection callbacks:
/// <c>ToDictionaryAsync(keySelector, valueSelector)</c> and the
/// <c>AggregateAsync(seed, accumulator, resultSelector)</c> form. Complements
/// <see cref="ToDictionaryAsyncBenchmarks"/> and <see cref="AggregateAndPredicateAsyncBenchmarks"/>
/// by exercising the overloads with extra delegate work per element.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class AdditionalProjectionAsyncBenchmarks
{
    /// <summary>Low end of the <see cref="ElementCount"/> parameter sweep.</summary>
    private const int SmallCount = 100;

    /// <summary>High end of the <see cref="ElementCount"/> parameter sweep.</summary>
    private const int LargeCount = 1_000;

    /// <summary>Multiplier applied by the Aggregate result-selector benchmark.</summary>
    private const int ResultMultiplier = 2;

    /// <summary>Pre-built source emitting <see cref="ElementCount"/> integers then completing.</summary>
    private IObservableAsync<int> _source = null!;

    /// <summary>Gets or sets the number of values the source emits before completing.</summary>
    [Params(SmallCount, LargeCount)]
    public int ElementCount { get; set; }

    /// <summary>Builds the finite source.</summary>
    [GlobalSetup]
    public void Setup() => _source = ObservableAsync.Range(0, ElementCount);

    /// <summary>Drains the source into a <see cref="Dictionary{TKey, TValue}"/> with a non-identity value projection.</summary>
    /// <returns>The materialised dictionary keyed by element value and projected to its negation.</returns>
    [Benchmark]
    public ValueTask<Dictionary<int, int>> ToDictionary_KeyAndValueSelectors() =>
        _source.ToDictionaryAsync(static x => x, static x => -x);

    /// <summary>Drains the source with the result-selector overload of <c>AggregateAsync</c>.</summary>
    /// <returns>The selector-projected aggregate result.</returns>
    [Benchmark]
    public ValueTask<int> Aggregate_WithResultSelector() =>
        _source.AggregateAsync(0, static (acc, x) => acc + x, static acc => acc * ResultMultiplier);
}
