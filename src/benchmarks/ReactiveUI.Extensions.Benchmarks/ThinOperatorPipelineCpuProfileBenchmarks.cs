// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// CPU-sampling hotspot profile for the thin operator pipeline (Select → Where → Scan composed over
/// <see cref="ObserverAsync{T}"/>). Pairs with <see cref="ThinOperatorPipelineGcProfileBenchmarks"/>;
/// this class swaps the profiler to <c>EventPipeProfile.CpuSampling</c> so the produced <c>.nettrace</c>
/// reveals self / inclusive CPU time per managed method (consumable by <c>rxext-cpureport</c>).
/// </summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class ThinOperatorPipelineCpuProfileBenchmarks : IDisposable
{
    /// <summary>Number of emissions pushed through the pipeline per invocation.</summary>
    private const int EmissionCount = 10_000;

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Subject feeding the Select → Where → Scan pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _source = null!;

    /// <summary>Subscription to the Select → Where → Scan pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Builds the pipeline once.</summary>
    /// <returns>A task that completes when setup is done.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<int>();
        var pipeline = _source
            .Select(static x => x + 1)
            .Where(static x => (x & 1) == 0)
            .Scan(0, static (acc, x) => acc + x);
        _subscription = await pipeline.SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subscription.DisposeAsync().ConfigureAwait(false);
        await _source.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the Select → Where → Scan pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task SelectWhereScan()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _source.OnNextAsync(i, default).ConfigureAwait(false);
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
