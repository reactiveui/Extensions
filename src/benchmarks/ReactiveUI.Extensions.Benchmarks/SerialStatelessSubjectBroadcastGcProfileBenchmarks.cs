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
/// GC-verbose hotspot profile for <see cref="SerialStatelessSubjectAsync{T}"/>'s multi-observer broadcast
/// path. Captures per-emission allocations as observers are fanned out through
/// <c>Concurrent.ForwardOnNextConcurrently</c>; trace consumable by <c>rxext-allocreport</c>.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class SerialStatelessSubjectBroadcastGcProfileBenchmarks : IDisposable
{
    /// <summary>Number of broadcast emissions per invocation.</summary>
    private const int EmissionCount = 10_000;

    /// <summary>Number of attached observers on the broadcast subject.</summary>
    private const int ObserverCount = 8;

    /// <summary>Shared no-op sink reused across every attached observer slot.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Subject under broadcast measurement.</summary>
    private SerialStatelessSubjectAsync<int> _subject = null!;

    /// <summary>Observers attached to the broadcast subject.</summary>
    private List<IAsyncDisposable> _subscriptions = null!;

    /// <summary>Builds the subject and attaches <see cref="ObserverCount"/> observers.</summary>
    /// <returns>A task that completes when setup is done.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _subject = new SerialStatelessSubjectAsync<int>();
        _subscriptions = new List<IAsyncDisposable>(ObserverCount);
        for (var i = 0; i < ObserverCount; i++)
        {
            _subscriptions.Add(await _subject.SubscribeAsync(_sink, default).ConfigureAwait(false));
        }
    }

    /// <summary>Tears every subscription and the subject down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        for (var i = 0; i < _subscriptions.Count; i++)
        {
            await _subscriptions[i].DisposeAsync().ConfigureAwait(false);
        }

        await _subject.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Broadcasts <see cref="EmissionCount"/> values across <see cref="ObserverCount"/> observers.</summary>
    /// <returns>A task that completes when every observer has been notified for every emission.</returns>
    [Benchmark]
    public async Task SubjectBroadcast()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _subject.OnNextAsync(i, default).ConfigureAwait(false);
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
