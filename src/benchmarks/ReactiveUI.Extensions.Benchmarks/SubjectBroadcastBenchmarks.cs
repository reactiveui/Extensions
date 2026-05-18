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
/// Measures the cost of the broadcast hot path on the async subjects (Packet 4 territory).
/// The synchronous-completion sink used here lets BenchmarkDotNet's MemoryDiagnoser capture per-emission
/// allocations without scheduler / continuation noise.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SubjectBroadcastBenchmarks : IDisposable
{
    /// <summary>Sentinel value used as the broadcast payload.</summary>
    private const int BroadcastValue = 42;

    /// <summary>Single-observer sweep point.</summary>
    private const int OneObserver = 1;

    /// <summary>Eight-observer sweep point.</summary>
    private const int EightObservers = 8;

    /// <summary>Sixty-four-observer sweep point.</summary>
    private const int SixtyFourObservers = 64;

    /// <summary>Two subjects are exercised, so the subscription list is sized for <c>ObserverCount × <see cref="SubjectsUnderTest"/></c>.</summary>
    private const int SubjectsUnderTest = 2;

    /// <summary>Shared no-op observer reused for every subscription so the benchmark itself doesn't allocate observers in the hot path.</summary>
    private readonly NoopObserver _noopObserver = new();

    /// <summary>The serial subject under test in <see cref="SerialSubjectAsync_OnNext"/>.</summary>
    private SerialSubjectAsync<int> _serial = null!;

    /// <summary>The stateless serial subject under test in <see cref="SerialStatelessSubjectAsync_OnNext"/>.</summary>
    private SerialStatelessSubjectAsync<int> _serialStateless = null!;

    /// <summary>Subscriptions created in <see cref="SetupAsync"/> so <see cref="CleanupAsync"/> can dispose them.</summary>
    private List<IAsyncDisposable> _subscriptions = null!;

    /// <summary>Gets or sets the number of observers attached to each subject before the broadcast is measured.</summary>
    [Params(OneObserver, EightObservers, SixtyFourObservers)]
    public int ObserverCount { get; set; }

    /// <summary>Builds the subjects and pre-attaches <see cref="ObserverCount"/> noop observers.</summary>
    /// <returns>A task that completes when both subjects are subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _serial = new SerialSubjectAsync<int>();
        _serialStateless = new SerialStatelessSubjectAsync<int>();
        _subscriptions = new List<IAsyncDisposable>(ObserverCount * SubjectsUnderTest);

        for (var i = 0; i < ObserverCount; i++)
        {
            _subscriptions.Add(await _serial.SubscribeAsync(_noopObserver, default).ConfigureAwait(false));
            _subscriptions.Add(await _serialStateless.SubscribeAsync(_noopObserver, default).ConfigureAwait(false));
        }
    }

    /// <summary>Disposes every subscription and both subjects.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        if (_subscriptions is not null)
        {
            for (var i = 0; i < _subscriptions.Count; i++)
            {
                await _subscriptions[i].DisposeAsync().ConfigureAwait(false);
            }

            _subscriptions = null!;
        }

        if (_serial is not null)
        {
            await _serial.DisposeAsync().ConfigureAwait(false);
        }

        if (_serialStateless is not null)
        {
            await _serialStateless.DisposeAsync().ConfigureAwait(false);
        }

        await _noopObserver.DisposeAsync().ConfigureAwait(false);
        _serial = null!;
        _serialStateless = null!;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Broadcasts one value through <see cref="SerialSubjectAsync{T}"/>.</summary>
    /// <returns>A <see cref="ValueTask"/> that completes when every observer has been notified.</returns>
    [Benchmark]
    [BenchmarkCategory("Serial")]
    public ValueTask SerialSubjectAsync_OnNext() => _serial.OnNextAsync(BroadcastValue, default);

    /// <summary>Broadcasts one value through <see cref="SerialStatelessSubjectAsync{T}"/>.</summary>
    /// <returns>A <see cref="ValueTask"/> that completes when every observer has been notified.</returns>
    [Benchmark]
    [BenchmarkCategory("Stateless")]
    public ValueTask SerialStatelessSubjectAsync_OnNext() => _serialStateless.OnNextAsync(BroadcastValue, default);

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

    /// <summary>No-op async observer used as the sink for every broadcast.</summary>
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
