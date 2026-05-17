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
/// Measures the subscribe/unsubscribe hot path on the async subjects — exercises Packet 7's state-carrying
/// DisposableAsync.Create overload via BaseSubjectAsync's unsubscribe lambda, which now uses
/// (this, observer) state and a static delegate instead of closing over locals.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SubscribeChurnBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="Iterations"/> parameter sweep.</summary>
    private const int SmallIterationCount = 100;

    /// <summary>High end of the <see cref="Iterations"/> parameter sweep.</summary>
    private const int LargeIterationCount = 1_000;

    /// <summary>Shared no-op observer that every iteration subscribes and then immediately disposes.</summary>
    private readonly NoopObserver _sink = new();

    /// <summary>The subject every iteration subscribes / unsubscribes against.</summary>
    private SerialStatelessSubjectAsync<int> _subject = null!;

    /// <summary>Gets or sets the number of subscribe + immediately-unsubscribe iterations per benchmark invocation.</summary>
    [Params(SmallIterationCount, LargeIterationCount)]
    public int Iterations { get; set; }

    /// <summary>Creates the subject under test.</summary>
    [GlobalSetup]
    public void Setup() => _subject = new SerialStatelessSubjectAsync<int>();

    /// <summary>Disposes the subject.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        if (_subject is not null)
        {
            await _subject.DisposeAsync().ConfigureAwait(false);
        }

        await _sink.DisposeAsync().ConfigureAwait(false);
        _subject = null!;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Subscribes, then immediately disposes, <see cref="Iterations"/> times.</summary>
    /// <returns>A task that completes when every subscribe + dispose pair has finished.</returns>
    [Benchmark]
    public async Task SubscribeAndUnsubscribe()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var subscription = await _subject.SubscribeAsync(_sink, default).ConfigureAwait(false);
            await subscription.DisposeAsync().ConfigureAwait(false);
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

    /// <summary>No-op async observer.</summary>
    private sealed class NoopObserver : IObserverAsync<int>
    {
        /// <inheritdoc/>
        public ValueTask OnNextAsync(int value, CancellationToken cancellationToken) => default;

        /// <inheritdoc/>
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken) => default;

        /// <inheritdoc/>
        public ValueTask OnCompletedAsync(Result result) => default;

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => default;
    }
}
