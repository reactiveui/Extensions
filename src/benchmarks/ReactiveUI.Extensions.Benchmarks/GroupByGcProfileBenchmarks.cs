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
/// GC-verbose hotspot profile for <c>GroupBy</c> on a primed key set. Pairs with
/// <see cref="GroupByBenchmarks"/>; trace consumable by <c>rxext-allocreport</c>.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class GroupByGcProfileBenchmarks : IDisposable
{
    /// <summary>Number of emissions pushed through the pipeline per invocation.</summary>
    private const int EmissionCount = 10_000;

    /// <summary>Number of distinct keys cycled through.</summary>
    private const int GroupCount = 4;

    /// <summary>Outer sink (groups).</summary>
    private readonly BenchmarkNoopObserver<GroupedAsyncObservable<int, int>> _outerSink = new();

    /// <summary>Inner sink (values per group).</summary>
    private readonly BenchmarkNoopObserver<int> _innerSink = new();

    /// <summary>Source feeding the GroupBy pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _source = null!;

    /// <summary>Outer subscription consuming each grouped observable as it's emitted.</summary>
    private IAsyncDisposable _outerSubscription = null!;

    /// <summary>Inner subscriptions on each group's emission stream.</summary>
    private List<IAsyncDisposable> _innerSubscriptions = null!;

    /// <summary>Wires the GroupBy pipeline and primes each group.</summary>
    /// <returns>A task that completes when the pipeline is subscribed and primed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<int>();
        _innerSubscriptions = new List<IAsyncDisposable>(GroupCount);

        var pipeline = _source.GroupBy(static x => x & (GroupCount - 1));
        _outerSubscription = await pipeline.SubscribeAsync(
            async (group, ct) => _innerSubscriptions.Add(
                await group.SubscribeAsync(_innerSink, ct).ConfigureAwait(false)),
            null).ConfigureAwait(false);

        for (var i = 0; i < GroupCount; i++)
        {
            await _source.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Tears the pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        for (var i = 0; i < _innerSubscriptions.Count; i++)
        {
            await _innerSubscriptions[i].DisposeAsync().ConfigureAwait(false);
        }

        await _outerSubscription.DisposeAsync().ConfigureAwait(false);
        await _source.DisposeAsync().ConfigureAwait(false);
        await _outerSink.DisposeAsync().ConfigureAwait(false);
        await _innerSink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values across <see cref="GroupCount"/> existing groups.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task GroupBy_PrimedKeyset()
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
