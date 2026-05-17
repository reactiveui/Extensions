// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the subscribe-and-drain cost of <c>ToDictionaryAsync</c>. The key selector is a
/// no-capture identity projection so the measurement reflects the operator's per-element work
/// (dictionary growth, key insertion) plus the constant subscribe / teardown overhead.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ToDictionaryAsyncBenchmarks
{
    /// <summary>Low end of the <see cref="ElementCount"/> parameter sweep.</summary>
    private const int SmallCount = 100;

    /// <summary>High end of the <see cref="ElementCount"/> parameter sweep.</summary>
    private const int LargeCount = 1_000;

    /// <summary>Pre-built source emitting <see cref="ElementCount"/> integers then completing.</summary>
    private IObservableAsync<int> _source = null!;

    /// <summary>Gets or sets the number of values the source emits before completing.</summary>
    [Params(SmallCount, LargeCount)]
    public int ElementCount { get; set; }

    /// <summary>Builds the finite source.</summary>
    [GlobalSetup]
    public void Setup() => _source = ObservableAsync.Range(0, ElementCount);

    /// <summary>Drains the source into a <see cref="Dictionary{TKey, TValue}"/> keyed by the identity projection.</summary>
    /// <returns>The materialised dictionary.</returns>
    [Benchmark]
    public ValueTask<Dictionary<int, int>> ToDictionary_IdentityKey() =>
        _source.ToDictionaryAsync(static x => x);
}
