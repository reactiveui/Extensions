// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the construct-subscribe-drain cost of the small factory observables: <c>Return</c>,
/// <c>Empty</c>, <c>Throw</c>, <c>Defer</c>, <c>FromAsync</c>, and <c>ToObservableAsync</c> over
/// an <see cref="IEnumerable{T}"/>. Each benchmark builds the observable per invocation (these
/// factories are not typically cached) so the measurement reflects the cold subscribe path.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "BenchmarkDotNet drives benchmarks through an instance; the methods cannot be static.")]
public class FactoryObservableBenchmarks
{
    /// <summary>Length of the enumerable source fed into the ToObservableAsync benchmark.</summary>
    private const int EnumerableLength = 100;

    /// <summary>Sentinel value emitted by the Return benchmark.</summary>
    private const int ReturnValue = 42;

    /// <summary>Sentinel value emitted by the Defer benchmark's inner Return.</summary>
    private const int DeferValue = 7;

    /// <summary>Sentinel value emitted by the FromAsync benchmark's synchronously-completing factory.</summary>
    private const int FromAsyncValue = 99;

    /// <summary>Cached enumerable so the ToObservableAsync benchmark doesn't measure list allocation.</summary>
    private static readonly int[] EnumerableSource = [.. Enumerable.Range(0, EnumerableLength)];

    /// <summary>Builds <c>Return(value)</c> and drains its single emission.</summary>
    /// <returns>The first (and only) emitted value.</returns>
    [Benchmark]
    public ValueTask<int> Return_Drain() => ObservableAsync.Return(ReturnValue).FirstAsync();

    /// <summary>Builds <c>Empty</c> and confirms completion via <c>FirstOrDefaultAsync</c>.</summary>
    /// <returns>The element type's default value.</returns>
    [Benchmark]
    public ValueTask<int> Empty_Drain() => ObservableAsync.Empty<int>().FirstOrDefaultAsync();

    /// <summary>Builds a <c>Defer</c> factory that returns a single-element <c>Return</c>, then drains it.</summary>
    /// <returns>The deferred-built source's single value.</returns>
    [Benchmark]
    public ValueTask<int> Defer_ReturningReturn() =>
        ObservableAsync.Defer(static () => ObservableAsync.Return(DeferValue)).FirstAsync();

    /// <summary>Builds <c>FromAsync</c> from a synchronously-completed value factory and drains the single emission.</summary>
    /// <returns>The factory's emitted value.</returns>
    [Benchmark]
    public ValueTask<int> FromAsync_SyncFactory() =>
        ObservableAsync.FromAsync(static _ => new ValueTask<int>(FromAsyncValue)).FirstAsync();

    /// <summary>Builds <c>ToObservableAsync</c> from a 100-element enumerable and counts the emissions.</summary>
    /// <returns>The element count, expected to be 100.</returns>
    [Benchmark]
    public ValueTask<int> ToObservableAsync_FromEnumerable() =>
        EnumerableSource.ToObservableAsync().CountAsync();
}
