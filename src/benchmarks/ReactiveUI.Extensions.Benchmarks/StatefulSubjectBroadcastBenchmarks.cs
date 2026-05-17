// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-emission broadcast cost of the stateful subject variants
/// (<see cref="SerialSubjectAsync{T}"/> and <see cref="ConcurrentSubjectAsync{T}"/>). The
/// stateless variants are covered by <see cref="SubjectBroadcastBenchmarks"/> and
/// <see cref="ConcurrentStatelessSubjectBroadcastBenchmarks"/>; this class isolates the cost of
/// the stateful machinery (result-tracking, completion guard) on top of the broadcast pipeline.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class StatefulSubjectBroadcastBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Number of attached observers on each subject.</summary>
    private const int ObserverCount = 4;

    /// <summary>Shared no-op sink reused across every attached observer slot.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Stateful serial subject under broadcast measurement.</summary>
    private SerialSubjectAsync<int> _serialSubject = null!;

    /// <summary>Observers attached to the serial subject.</summary>
    private List<IAsyncDisposable> _serialSubscriptions = null!;

    /// <summary>Stateful concurrent subject under broadcast measurement.</summary>
    private ConcurrentSubjectAsync<int> _concurrentSubject = null!;

    /// <summary>Observers attached to the concurrent subject.</summary>
    private List<IAsyncDisposable> _concurrentSubscriptions = null!;

    /// <summary>Gets or sets the number of broadcast emissions per invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Builds the subjects and attaches <see cref="ObserverCount"/> observers to each.</summary>
    /// <returns>A task that completes when setup is done.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _serialSubject = new SerialSubjectAsync<int>();
        _serialSubscriptions = new List<IAsyncDisposable>(ObserverCount);
        for (var i = 0; i < ObserverCount; i++)
        {
            _serialSubscriptions.Add(await _serialSubject.SubscribeAsync(_sink, default).ConfigureAwait(false));
        }

        _concurrentSubject = new ConcurrentSubjectAsync<int>();
        _concurrentSubscriptions = new List<IAsyncDisposable>(ObserverCount);
        for (var i = 0; i < ObserverCount; i++)
        {
            _concurrentSubscriptions.Add(await _concurrentSubject.SubscribeAsync(_sink, default).ConfigureAwait(false));
        }
    }

    /// <summary>Tears every subscription and the subjects down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        for (var i = 0; i < _serialSubscriptions.Count; i++)
        {
            await _serialSubscriptions[i].DisposeAsync().ConfigureAwait(false);
        }

        await _serialSubject.DisposeAsync().ConfigureAwait(false);

        for (var i = 0; i < _concurrentSubscriptions.Count; i++)
        {
            await _concurrentSubscriptions[i].DisposeAsync().ConfigureAwait(false);
        }

        await _concurrentSubject.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Broadcasts through the stateful serial subject.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task SerialSubject_Broadcast()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _serialSubject.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Broadcasts through the stateful concurrent subject.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task ConcurrentSubject_Broadcast()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _concurrentSubject.OnNextAsync(i, default).ConfigureAwait(false);
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
