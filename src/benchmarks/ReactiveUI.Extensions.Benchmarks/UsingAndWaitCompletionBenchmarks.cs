// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the construct-subscribe-drain cost of <c>Using</c> (resource-scoped observable) and
/// the subscribe-and-await-completion cost of <c>WaitCompletionAsync</c>. Each invocation builds
/// a fresh pipeline so the measurement reflects cold subscribe / teardown overhead.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "BenchmarkDotNet drives benchmarks through an instance; the methods cannot be static.")]
public class UsingAndWaitCompletionBenchmarks
{
    /// <summary>Number of elements emitted by the finite source backing the WaitCompletion benchmark.</summary>
    private const int FiniteCount = 100;

    /// <summary>Sentinel value emitted by the Using benchmark's inner observable.</summary>
    private const int UsingSentinel = 7;

    /// <summary>Pre-built finite source for the <see cref="WaitCompletion_FullDrain"/> benchmark.</summary>
    private IObservableAsync<int> _finite = null!;

    /// <summary>Builds the finite source once.</summary>
    [GlobalSetup]
    public void Setup() => _finite = ObservableAsync.Range(0, FiniteCount);

    /// <summary>Drains a <c>Using</c>-scoped Range-of-1 and returns its single value; exercises resource creation + dispose.</summary>
    /// <returns>The single emitted value.</returns>
    [Benchmark]
    public ValueTask<int> Using_SingleEmissionResource() =>
        ObservableAsync.Using(
            static _ => new ValueTask<NoopAsyncDisposable>(NoopAsyncDisposable.Instance),
            static _ => ObservableAsync.Return(UsingSentinel))
            .FirstAsync();

    /// <summary>Awaits completion of a fully-drained finite source via <c>WaitCompletionAsync</c>.</summary>
    /// <returns>A task that completes when the source signals OnCompleted.</returns>
    [Benchmark]
    public ValueTask WaitCompletion_FullDrain() => _finite.WaitCompletionAsync();

    /// <summary>No-op <see cref="IAsyncDisposable"/> used as the cheap shared resource for the <c>Using</c> benchmark.</summary>
    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        /// <summary>Shared instance.</summary>
        public static readonly NoopAsyncDisposable Instance = new();

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => default;
    }
}
