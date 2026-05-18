// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-emission cost of the remaining cardinality / predicate operators:
/// <c>Skip</c>, <c>SkipWhile</c>, and <c>TakeWhile</c>. Each pipeline is wired so the
/// skip / take boundary is crossed during warm-up (or never crossed at all), so the benchmark
/// body measures only the steady-state pass-through cost.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SkipTakeWhileBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Skip cardinality; primed during <see cref="SetupAsync"/> so the benchmark body sees a steady pass-through.</summary>
    private const int SkipBudget = 1;

    /// <summary>SkipWhile predicate threshold; the priming pushes a value above the threshold so subsequent values pass through.</summary>
    private const int SkipWhileThreshold = -1;

    /// <summary>TakeWhile predicate threshold; chosen larger than any value driven during the benchmark body so values always pass through.</summary>
    private const int TakeWhileCeiling = int.MaxValue;

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Source for the <c>Skip</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _skipSource = null!;

    /// <summary>Subscription on the <c>Skip</c> pipeline.</summary>
    private IAsyncDisposable _skipSubscription = null!;

    /// <summary>Source for the <c>SkipWhile</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _skipWhileSource = null!;

    /// <summary>Subscription on the <c>SkipWhile</c> pipeline.</summary>
    private IAsyncDisposable _skipWhileSubscription = null!;

    /// <summary>Source for the <c>TakeWhile</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _takeWhileSource = null!;

    /// <summary>Subscription on the <c>TakeWhile</c> pipeline.</summary>
    private IAsyncDisposable _takeWhileSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the three pipelines and primes the predicate / cardinality boundaries.</summary>
    /// <returns>A task that completes when every pipeline is subscribed and primed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _skipSource = new SerialStatelessSubjectAsync<int>();
        _skipSubscription = await _skipSource.Skip(SkipBudget)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        // Push the skipped values so subsequent emissions exit the skip phase.
        for (var i = 0; i < SkipBudget; i++)
        {
            await _skipSource.OnNextAsync(-1, default).ConfigureAwait(false);
        }

        _skipWhileSource = new SerialStatelessSubjectAsync<int>();
        _skipWhileSubscription = await _skipWhileSource.SkipWhile(static x => x < SkipWhileThreshold)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        // Push one value above the threshold so the predicate latches off — every later emission passes through.
        await _skipWhileSource.OnNextAsync(SkipWhileThreshold + 1, default).ConfigureAwait(false);

        _takeWhileSource = new SerialStatelessSubjectAsync<int>();
        _takeWhileSubscription = await _takeWhileSource.TakeWhile(static x => x < TakeWhileCeiling)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipelines down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _skipSubscription.DisposeAsync().ConfigureAwait(false);
        await _skipSource.DisposeAsync().ConfigureAwait(false);
        await _skipWhileSubscription.DisposeAsync().ConfigureAwait(false);
        await _skipWhileSource.DisposeAsync().ConfigureAwait(false);
        await _takeWhileSubscription.DisposeAsync().ConfigureAwait(false);
        await _takeWhileSource.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives values through the primed <c>Skip</c> pipeline (skip budget already exhausted).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Skip_PostBudget()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _skipSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives values through the primed <c>SkipWhile</c> pipeline (predicate already latched off).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task SkipWhile_PostLatch()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _skipWhileSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives values through the <c>TakeWhile</c> pipeline (predicate never trips).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task TakeWhile_BelowCeiling()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _takeWhileSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Drains async teardown so <see cref="IDisposable.Dispose"/> can return synchronously.</summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
    [SuppressMessage(
        "Major Bug",
        "S4462:Calls to async methods should not be blocking",
        Justification = "IDisposable.Dispose is synchronous by contract; benchmark teardown must wait for async cleanup before returning.")]
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        CleanupAsync().GetAwaiter().GetResult();
    }
}
