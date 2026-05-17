// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>GC-verbose hotspot profile for <c>Publish(initialValue)</c> + <c>RefCount</c>.</summary>
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class PublishVariantsGcProfileBenchmarks : IDisposable
{
    /// <summary>Number of broadcast emissions per invocation.</summary>
    private const int EmissionCount = 10_000;

    /// <summary>Number of downstream observers on the multicast pipeline.</summary>
    private const int ObserverCount = 4;

    /// <summary>Sentinel seed value for the behaviour-publish.</summary>
    private const int SeedValue = -1;

    /// <summary>Shared no-op sink reused across every attached observer.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Upstream subject feeding the pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _source = null!;

    /// <summary>Subscriptions on the pipeline.</summary>
    private List<IAsyncDisposable> _subscriptions = null!;

    /// <summary>Wires the behaviour-style multicast pipeline and attaches <see cref="ObserverCount"/> observers.</summary>
    /// <returns>A task that completes when every observer is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<int>();
        var shared = _source.Publish(SeedValue).RefCount();
        _subscriptions = new List<IAsyncDisposable>(ObserverCount);
        for (var i = 0; i < ObserverCount; i++)
        {
            _subscriptions.Add(await shared.SubscribeAsync(_sink, default).ConfigureAwait(false));
        }
    }

    /// <summary>Tears every subscription and source subject down.</summary>
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

    /// <summary>Drives <see cref="EmissionCount"/> values through the behaviour-publish pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task PublishWithInitial_FourObservers()
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
