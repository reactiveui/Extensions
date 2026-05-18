// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the subscribe-drain-resolve cost of <c>FirstOrDefaultAsync</c> and
/// <c>LastOrDefaultAsync</c>. <c>FirstOrDefault</c> short-circuits after one emission; <c>LastOrDefault</c>
/// drains the full sequence. Both are exercised against a primed <c>ReplayLatest</c> source so the
/// benchmark body is purely the terminal operator + a single completion signal.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "BenchmarkDotNet drives benchmarks through an instance; the methods cannot be static.")]
public class FirstOrDefaultAndLastOrDefaultBenchmarks
{
    /// <summary>Number of values produced by <see cref="BuildRange"/>.</summary>
    private const int ElementCount = 1_000;

    /// <summary>Sentinel default returned when the source is empty.</summary>
    private const int DefaultSentinel = -1;

    /// <summary>Bridges a sync <see cref="IEnumerable{T}"/> producing <see cref="ElementCount"/> integers and resolves the first.</summary>
    /// <returns>The first element.</returns>
    [Benchmark]
    public ValueTask<int> FirstOrDefault_NonEmpty() =>
        BuildRange().ToObservableAsync().FirstOrDefaultAsync(DefaultSentinel);

    /// <summary>Bridges an empty sequence and resolves the default.</summary>
    /// <returns>The default sentinel.</returns>
    [Benchmark]
    public ValueTask<int> FirstOrDefault_EmptyReturnsDefault() =>
        ObservableAsync.Empty<int>().FirstOrDefaultAsync(DefaultSentinel);

    /// <summary>Bridges a sync <see cref="IEnumerable{T}"/> producing <see cref="ElementCount"/> integers and resolves the last after full drain.</summary>
    /// <returns>The last element.</returns>
    [Benchmark]
    public ValueTask<int> LastOrDefault_NonEmpty() =>
        BuildRange().ToObservableAsync().LastOrDefaultAsync(DefaultSentinel);

    /// <summary>Bridges an empty sequence and resolves the default.</summary>
    /// <returns>The default sentinel.</returns>
    [Benchmark]
    public ValueTask<int> LastOrDefault_EmptyReturnsDefault() =>
        ObservableAsync.Empty<int>().LastOrDefaultAsync(DefaultSentinel);

    /// <summary>Enumerates 0..<see cref="ElementCount"/>-1 for the bridge benchmarks.</summary>
    /// <returns>The integer range.</returns>
    private static IEnumerable<int> BuildRange()
    {
        for (var i = 0; i < ElementCount; i++)
        {
            yield return i;
        }
    }
}
