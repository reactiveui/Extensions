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
/// GC-verbose hotspot profile for the <c>Publish</c> + <c>RefCount</c> pipeline. Allocations
/// surfaced by the steady-state <see cref="MulticastBenchmarks"/> are tracked here so
/// <c>rxext-allocreport</c> can attribute them to specific types and call stacks.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class MulticastGcProfileBenchmarks : IDisposable
{
    /// <summary>Number of broadcast emissions per invocation.</summary>
    private const int EmissionCount = 10_000;

    /// <summary>Number of downstream observers attached to the multicast pipeline.</summary>
    private const int ObserverCount = 4;

    /// <summary>Shared no-op sink reused across every attached observer.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Upstream subject feeding the multicast.</summary>
    private SerialStatelessSubjectAsync<int> _source = null!;

    /// <summary>Subscriptions returned by each attached observer.</summary>
    private List<IAsyncDisposable> _subscriptions = null!;

    /// <summary>Wires the Publish + RefCount pipeline and subscribes <see cref="ObserverCount"/> observers.</summary>
    /// <returns>A task that completes when every observer is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<int>();
        var shared = _source.StatelessPublish().RefCount();
        _subscriptions = new List<IAsyncDisposable>(ObserverCount);
        for (var i = 0; i < ObserverCount; i++)
        {
            _subscriptions.Add(await shared.SubscribeAsync(_sink, default).ConfigureAwait(false));
        }
    }

    /// <summary>Tears every subscription and the source subject down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        for (var i = 0; i < _subscriptions.Count; i++)
        {
            await _subscriptions[i].DisposeAsync().ConfigureAwait(false);
        }

        await _source.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Broadcasts <see cref="EmissionCount"/> values through the multicast pipeline.</summary>
    /// <returns>A task that completes when every observer has been notified for every emission.</returns>
    [Benchmark]
    public async Task PublishRefCount_FourSharedObservers()
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
