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
/// Measures the per-emission cost of broadcasting through <see cref="SerialReplayLatestSubjectAsync{T}"/>
/// with multiple attached observers. The behaviour-subject family stores the latest value behind a
/// gate; this benchmark complements <see cref="SubjectBroadcastBenchmarks"/> (which covers the
/// stateless serial subject) by exercising the gated last-value-cache hot path.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ReplayLatestSubjectBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Number of attached observers on the broadcast subject.</summary>
    private const int ObserverCount = 4;

    /// <summary>Shared no-op sink reused across every attached observer slot.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Subject under broadcast measurement.</summary>
    private SerialReplayLatestSubjectAsync<int> _subject = null!;

    /// <summary>Observers attached to the broadcast subject.</summary>
    private List<IAsyncDisposable> _subscriptions = null!;

    /// <summary>Gets or sets the number of broadcast emissions per invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Builds the subject (no seed value) and attaches <see cref="ObserverCount"/> observers.</summary>
    /// <returns>A task that completes when setup is done.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _subject = new SerialReplayLatestSubjectAsync<int>(Optional<int>.Empty);
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
    public async Task ReplayLatestSubject_Broadcast()
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
