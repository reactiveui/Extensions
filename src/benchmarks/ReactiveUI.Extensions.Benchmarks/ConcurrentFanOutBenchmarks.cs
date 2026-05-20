// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-call cost of the observer fan-out helpers: <c>ObserverExtensions.FastForEach</c>
/// (bulk push into a synchronous observer) and the <c>Concurrent.ForwardOn*Concurrently</c> helpers
/// that broadcast a notification to an <see cref="ImmutableArray{T}"/> of async observers. Sinks are
/// no-ops so the benchmark captures the fan-out machinery rather than downstream work.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ConcurrentFanOutBenchmarks : IDisposable
{
    /// <summary>Number of observers in the broadcast array.</summary>
    private const int ObserverCount = 4;

    /// <summary>Length of the sequence pushed through <c>FastForEach</c> per call.</summary>
    private const int SequenceLength = 100;

    /// <summary>Sentinel value broadcast by the forward benchmarks.</summary>
    private const int Value = 42;

    /// <summary>Synchronous no-op sink for <c>FastForEach</c>.</summary>
    private readonly NoopObserver<int> _syncSink = new();

    /// <summary>Cached sequence pushed through <c>FastForEach</c> so its allocation isn't measured.</summary>
    private readonly int[] _sequence = [.. Enumerable.Range(0, SequenceLength)];

    /// <summary>Cached error reused by the error-resume broadcast.</summary>
    private readonly InvalidOperationException _error = new("benchmark");

    /// <summary>Async observers targeted by the broadcast benchmarks.</summary>
    private BenchmarkNoopObserver<int>[] _asyncSinks = null!;

    /// <summary>Immutable snapshot handed to the forward helpers.</summary>
    private ImmutableArray<IObserverAsync<int>> _observers;

    /// <summary>Builds the async observer array.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _asyncSinks = new BenchmarkNoopObserver<int>[ObserverCount];
        var builder = ImmutableArray.CreateBuilder<IObserverAsync<int>>(ObserverCount);
        for (var i = 0; i < ObserverCount; i++)
        {
            _asyncSinks[i] = new BenchmarkNoopObserver<int>();
            builder.Add(_asyncSinks[i]);
        }

        _observers = builder.ToImmutable();
    }

    /// <summary>Tears the async observers down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        for (var i = 0; i < _asyncSinks.Length; i++)
        {
            await _asyncSinks[i].DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Pushes a 100-element sequence into a synchronous observer via <c>FastForEach</c>.</summary>
    [Benchmark]
    public void FastForEach_HundredElements() => _syncSink.FastForEach(_sequence);

    /// <summary>Broadcasts a single value to four async observers via <c>ForwardOnNextConcurrently</c>.</summary>
    /// <returns>A task that completes when every observer has been notified.</returns>
    [Benchmark]
    public ValueTask ForwardOnNextConcurrently_FourObservers() =>
        Concurrent.ForwardOnNextConcurrently(_observers, Value, default);

    /// <summary>Broadcasts an error to four async observers via <c>ForwardOnErrorResumeConcurrently</c>.</summary>
    /// <returns>A task that completes when every observer has been notified.</returns>
    [Benchmark]
    public ValueTask ForwardOnErrorResumeConcurrently_FourObservers() =>
        Concurrent.ForwardOnErrorResumeConcurrently(_observers, _error, default);

    /// <summary>Broadcasts completion to four async observers via <c>ForwardOnCompletedConcurrently</c>.</summary>
    /// <returns>A task that completes when every observer has been notified.</returns>
    [Benchmark]
    public ValueTask ForwardOnCompletedConcurrently_FourObservers() =>
        Concurrent.ForwardOnCompletedConcurrently(_observers, Result.Success);

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

    /// <summary>No-op synchronous observer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class NoopObserver<T> : IObserver<T>
    {
        /// <inheritdoc/>
        public void OnNext(T value)
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }
    }
}
