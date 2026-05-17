// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the subscribe-and-drain cost of the single-value and default-value terminal operators:
/// <c>SingleAsync</c> on a single-element source, plus
/// <c>FirstOrDefaultAsync</c> / <c>LastOrDefaultAsync</c> / <c>SingleOrDefaultAsync</c> against an
/// <c>Empty</c> source so the default value is returned.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SingleAndDefaultAsyncBenchmarks
{
    /// <summary>Pre-built single-element source for <see cref="SingleAsync_SingleElement"/>.</summary>
    private IObservableAsync<int> _single = null!;

    /// <summary>Pre-built empty source for the default-value benchmarks.</summary>
    private IObservableAsync<int> _empty = null!;

    /// <summary>Builds the test sources.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _single = ObservableAsync.Range(0, 1);
        _empty = ObservableAsync.Empty<int>();
    }

    /// <summary>Drains a single-element source through <c>SingleAsync</c>.</summary>
    /// <returns>The single emitted value.</returns>
    [Benchmark]
    public ValueTask<int> SingleAsync_SingleElement() => _single.SingleAsync();

    /// <summary>Drains an empty source through <c>FirstOrDefaultAsync</c>; returns the default.</summary>
    /// <returns>The element type's default value.</returns>
    [Benchmark]
    public ValueTask<int> FirstOrDefault_Empty() => _empty.FirstOrDefaultAsync();

    /// <summary>Drains an empty source through <c>LastOrDefaultAsync</c>; returns the default.</summary>
    /// <returns>The element type's default value.</returns>
    [Benchmark]
    public ValueTask<int> LastOrDefault_Empty() => _empty.LastOrDefaultAsync();

    /// <summary>Drains an empty source through <c>SingleOrDefaultAsync</c>; returns the default.</summary>
    /// <returns>The element type's default value.</returns>
    [Benchmark]
    public ValueTask<int> SingleOrDefault_Empty() => _empty.SingleOrDefaultAsync();
}
