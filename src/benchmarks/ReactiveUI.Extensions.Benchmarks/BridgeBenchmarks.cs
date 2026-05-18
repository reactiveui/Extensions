// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the sync→async bridge's per-emission cost (Packet 8 — WorkItem struct queue replacing the
/// previous Func{Task} + Action closure trio).
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class BridgeBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>No-op async sink that the bridge forwards each notification to.</summary>
    private readonly NoopObserver<int> _sink = new();

    /// <summary>The synchronous System.Reactive subject that feeds the bridge.</summary>
    private CurrentValueSubject<int> _syncSource = null!;

    /// <summary>The subscription returned from subscribing the noop sink to the bridged async observable.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Gets or sets the number of values pushed through the bridge per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Builds a Subject, wraps it as an async observable, and subscribes the noop sink.</summary>
    /// <returns>A task that completes once the bridge is wired up.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _syncSource = new CurrentValueSubject<int>(0);
        var asyncObservable = _syncSource.ToObservableAsync();
        _subscription = await asyncObservable.SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Disposes the subscription, completes and disposes the source subject.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [SuppressMessage(
        "Critical Bug",
        "S2952:Classes should \"Dispose\" of members from the classes' own \"Dispose\" methods",
        Justification = "CleanupAsync is the BDN sibling of Dispose(bool); Dispose() forwards into it deterministically.")]
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        if (_subscription is not null)
        {
            await _subscription.DisposeAsync().ConfigureAwait(false);
        }

        _syncSource?.OnCompleted();
        _syncSource?.Dispose();
        await _sink.DisposeAsync().ConfigureAwait(false);
        _subscription = null!;
        _syncSource = null!;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Pushes <see cref="EmissionCount"/> values into the sync Subject so the bridge enqueues + drains them onto the async observer.</summary>
    [Benchmark]
    public void SyncToAsync_OnNext()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _syncSource.OnNext(i);
        }
    }

    /// <summary>
    /// Releases the managed cleanup state (delegates to <c>CleanupAsync</c>). Virtual so BenchmarkDotNet
    /// fixtures stay overridable; <c>sealed</c> isn't an option because BDN refuses to drive a sealed
    /// benchmark class.
    /// </summary>
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

    /// <summary>No-op async observer used as the bridge's downstream sink.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class NoopObserver<T> : IObserverAsync<T>
    {
        /// <inheritdoc/>
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken) => default;

        /// <inheritdoc/>
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken) => default;

        /// <inheritdoc/>
        public ValueTask OnCompletedAsync(Result result) => default;

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => default;
    }
}
