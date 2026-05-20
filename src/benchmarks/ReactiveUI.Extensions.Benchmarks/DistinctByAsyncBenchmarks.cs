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
/// Measures the per-emission cost of <c>DistinctBy</c> (per-subscription <see cref="HashSet{T}"/>
/// of seen keys) and <c>DistinctUntilChangedBy</c> (one cached previous key). Drives a fully
/// distinct key sequence so the worst-case HashSet-add path is exercised for <c>DistinctBy</c>.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class DistinctByAsyncBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Shared no-op sink.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Source for the DistinctBy pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _distinctBySource = null!;

    /// <summary>Subscription on the DistinctBy pipeline.</summary>
    private IAsyncDisposable _distinctBySubscription = null!;

    /// <summary>Source for the DistinctUntilChangedBy pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _distinctUntilChangedBySource = null!;

    /// <summary>Subscription on the DistinctUntilChangedBy pipeline.</summary>
    private IAsyncDisposable _distinctUntilChangedBySubscription = null!;

    /// <summary>Gets or sets the number of emissions per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires both pipelines.</summary>
    /// <returns>A task that completes when both pipelines are subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _distinctBySource = new SerialStatelessSubjectAsync<int>();
        _distinctBySubscription = await _distinctBySource
            .DistinctBy(static x => x)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _distinctUntilChangedBySource = new SerialStatelessSubjectAsync<int>();
        _distinctUntilChangedBySubscription = await _distinctUntilChangedBySource
            .DistinctUntilChangedBy(static x => x)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears both pipelines down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _distinctBySubscription.DisposeAsync().ConfigureAwait(false);
        await _distinctBySource.DisposeAsync().ConfigureAwait(false);
        await _distinctUntilChangedBySubscription.DisposeAsync().ConfigureAwait(false);
        await _distinctUntilChangedBySource.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> all-distinct values through the DistinctBy pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task DistinctBy_AllDistinctKeys()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _distinctBySource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> all-distinct values through the DistinctUntilChangedBy pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task DistinctUntilChangedBy_AllDistinctKeys()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _distinctUntilChangedBySource.OnNextAsync(i, default).ConfigureAwait(false);
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
