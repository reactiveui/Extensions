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
/// Measures the per-emission cost of the Multicast.Publish family beyond the basic
/// <c>Publish</c>+<c>RefCount</c> exercised in <see cref="MulticastBenchmarks"/>:
/// <c>Publish(initialValue)</c> (behaviour-style),
/// <c>StatelessPublish(initialValue)</c> (replay-latest stateless), and
/// <c>ReplayLatestPublish</c>.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class PublishVariantsBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Number of downstream observers attached to each multicast pipeline.</summary>
    private const int ObserverCount = 4;

    /// <summary>Sentinel seed value emitted from the behaviour-publish before steady state.</summary>
    private const int SeedValue = -1;

    /// <summary>Shared no-op sink reused across every attached observer.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Upstream subject feeding the <c>Publish(initialValue)</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _publishWithInitialSource = null!;

    /// <summary>Subscriptions on the <c>Publish(initialValue)</c> pipeline.</summary>
    private List<IAsyncDisposable> _publishWithInitialSubscriptions = null!;

    /// <summary>Upstream subject feeding the <c>ReplayLatestPublish</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _replayLatestSource = null!;

    /// <summary>Subscriptions on the <c>ReplayLatestPublish</c> pipeline.</summary>
    private List<IAsyncDisposable> _replayLatestSubscriptions = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the multicast pipelines and attaches <see cref="ObserverCount"/> observers to each.</summary>
    /// <returns>A task that completes when every observer is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _publishWithInitialSource = new SerialStatelessSubjectAsync<int>();
        var behaviourShared = _publishWithInitialSource.Publish(SeedValue).RefCount();
        _publishWithInitialSubscriptions = new List<IAsyncDisposable>(ObserverCount);
        for (var i = 0; i < ObserverCount; i++)
        {
            _publishWithInitialSubscriptions.Add(await behaviourShared.SubscribeAsync(_sink, default).ConfigureAwait(false));
        }

        _replayLatestSource = new SerialStatelessSubjectAsync<int>();
        var replayShared = _replayLatestSource.ReplayLatestPublish().RefCount();
        _replayLatestSubscriptions = new List<IAsyncDisposable>(ObserverCount);
        for (var i = 0; i < ObserverCount; i++)
        {
            _replayLatestSubscriptions.Add(await replayShared.SubscribeAsync(_sink, default).ConfigureAwait(false));
        }
    }

    /// <summary>Tears every subscription and source subject down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        for (var i = 0; i < _publishWithInitialSubscriptions.Count; i++)
        {
            await _publishWithInitialSubscriptions[i].DisposeAsync().ConfigureAwait(false);
        }

        await _publishWithInitialSource.DisposeAsync().ConfigureAwait(false);

        for (var i = 0; i < _replayLatestSubscriptions.Count; i++)
        {
            await _replayLatestSubscriptions[i].DisposeAsync().ConfigureAwait(false);
        }

        await _replayLatestSource.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Pushes <see cref="EmissionCount"/> values through the <c>Publish(initialValue)</c>+<c>RefCount</c> pipeline.</summary>
    /// <returns>A task that completes when every observer has been notified for every emission.</returns>
    [Benchmark]
    public async Task PublishWithInitial_FourObservers()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _publishWithInitialSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Pushes <see cref="EmissionCount"/> values through the <c>ReplayLatestPublish</c>+<c>RefCount</c> pipeline.</summary>
    /// <returns>A task that completes when every observer has been notified for every emission.</returns>
    [Benchmark]
    public async Task ReplayLatestPublish_FourObservers()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _replayLatestSource.OnNextAsync(i, default).ConfigureAwait(false);
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
