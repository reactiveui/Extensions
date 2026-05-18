// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ReactiveUI.Extensions.Async;

using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// CPU-sampling hotspot profile for the sync→async bridge (<c>ToObservableAsync</c> in
/// <c>Async/Bridge/ObservableBridgeExtensions.cs</c>). Trace consumable by <c>rxext-cpureport</c>.
/// </summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class SyncToAsyncBridgeCpuProfileBenchmarks : IDisposable
{
    /// <summary>Number of values pushed through the bridge per invocation.</summary>
    private const int EmissionCount = 10_000;

    /// <summary>Shared no-op async sink.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Sync source feeding the bridge.</summary>
    private CurrentValueSubject<int> _source = null!;

    /// <summary>Subscription returned by the bridged async observable.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Builds the bridge.</summary>
    /// <returns>A task that completes when setup is done.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new CurrentValueSubject<int>(0);
        _subscription = await _source.ToObservableAsync()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears the bridge down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [SuppressMessage(
        "Critical Bug",
        "S2952:Classes should \"Dispose\" of members from the classes' own \"Dispose\" methods",
        Justification = "CleanupAsync is the BDN sibling of Dispose(bool); Dispose() forwards into it deterministically.")]
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subscription.DisposeAsync().ConfigureAwait(false);
        _source.OnCompleted();
        _source.Dispose();
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Pushes <see cref="EmissionCount"/> values through the sync→async bridge.</summary>
    [Benchmark]
    public void SyncToAsyncBridge()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _source.OnNext(i);
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
