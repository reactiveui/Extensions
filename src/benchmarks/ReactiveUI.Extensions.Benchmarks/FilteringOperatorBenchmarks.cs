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
/// Measures the per-emission cost of the cardinality / equality filters: <c>Distinct</c>,
/// <c>DistinctUntilChanged</c>, and <c>Take</c>. All three sit on the hot path of typical UI state
/// pipelines; <c>DistinctUntilChanged</c> in particular is rarely absent.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class FilteringOperatorBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Take's element budget. Set larger than <see cref="LargeEmissionCount"/> so Take never auto-completes
    /// mid-run; we want to measure steady-state per-emission cost, not the completion edge.</summary>
    private const int TakeBudget = LargeEmissionCount + 1;

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Source for the <c>Distinct</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _distinctSource = null!;

    /// <summary>Subscription on the <c>Distinct</c> pipeline.</summary>
    private IAsyncDisposable _distinctSubscription = null!;

    /// <summary>Source for the <c>DistinctUntilChanged</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _ducSource = null!;

    /// <summary>Subscription on the <c>DistinctUntilChanged</c> pipeline.</summary>
    private IAsyncDisposable _ducSubscription = null!;

    /// <summary>Source for the <c>Take</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _takeSource = null!;

    /// <summary>Subscription on the <c>Take</c> pipeline.</summary>
    private IAsyncDisposable _takeSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Builds the three filtering pipelines.</summary>
    /// <returns>A task that completes when every pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _distinctSource = new SerialStatelessSubjectAsync<int>();
        _distinctSubscription = await _distinctSource.Distinct()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _ducSource = new SerialStatelessSubjectAsync<int>();
        _ducSubscription = await _ducSource.DistinctUntilChanged()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _takeSource = new SerialStatelessSubjectAsync<int>();
        _takeSubscription = await _takeSource.Take(TakeBudget)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipelines down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _distinctSubscription.DisposeAsync().ConfigureAwait(false);
        await _distinctSource.DisposeAsync().ConfigureAwait(false);
        await _ducSubscription.DisposeAsync().ConfigureAwait(false);
        await _ducSource.DisposeAsync().ConfigureAwait(false);
        await _takeSubscription.DisposeAsync().ConfigureAwait(false);
        await _takeSource.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives unique values through <c>Distinct</c> (every value passes the set check).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Distinct_UniqueValues()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _distinctSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives alternating duplicates through <c>DistinctUntilChanged</c> (half pass, half drop).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task DistinctUntilChanged_AlternatingValues()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _ducSource.OnNextAsync(i & 1, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives values through <c>Take</c> with a budget large enough that none trigger completion.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Take_BelowBudget()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _takeSource.OnNextAsync(i, default).ConfigureAwait(false);
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
