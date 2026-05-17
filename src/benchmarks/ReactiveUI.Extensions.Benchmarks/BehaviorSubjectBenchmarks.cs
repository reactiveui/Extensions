// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-emission broadcast cost of the behaviour-subject family — subjects created
/// via <see cref="SubjectAsync.CreateBehavior{T}(T)"/> and <see cref="SubjectAsync.CreateReplayLatest{T}()"/>.
/// Complements <see cref="ReplayLatestSubjectBenchmarks"/> by exercising the factory-style
/// constructors that wrap the same underlying types.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class BehaviorSubjectBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Number of attached observers on each subject.</summary>
    private const int ObserverCount = 4;

    /// <summary>Initial value seeded into the behaviour subject.</summary>
    private const int BehaviorSeed = -1;

    /// <summary>Shared no-op sink reused across every attached observer slot.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Subject created via <see cref="SubjectAsync.CreateBehavior{T}(T)"/>.</summary>
    private ISubjectAsync<int> _behavior = null!;

    /// <summary>Observers attached to the behaviour subject.</summary>
    private List<IAsyncDisposable> _behaviorSubscriptions = null!;

    /// <summary>Subject created via <see cref="SubjectAsync.CreateReplayLatest{T}()"/>.</summary>
    private ISubjectAsync<int> _replayLatest = null!;

    /// <summary>Observers attached to the replay-latest subject.</summary>
    private List<IAsyncDisposable> _replayLatestSubscriptions = null!;

    /// <summary>Gets or sets the number of broadcast emissions per invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Builds both subjects and attaches <see cref="ObserverCount"/> observers to each.</summary>
    /// <returns>A task that completes when setup is done.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _behavior = SubjectAsync.CreateBehavior(BehaviorSeed);
        _behaviorSubscriptions = new List<IAsyncDisposable>(ObserverCount);
        for (var i = 0; i < ObserverCount; i++)
        {
            _behaviorSubscriptions.Add(await _behavior.Values.SubscribeAsync(_sink, default).ConfigureAwait(false));
        }

        _replayLatest = SubjectAsync.CreateReplayLatest<int>();
        _replayLatestSubscriptions = new List<IAsyncDisposable>(ObserverCount);
        for (var i = 0; i < ObserverCount; i++)
        {
            _replayLatestSubscriptions.Add(await _replayLatest.Values.SubscribeAsync(_sink, default).ConfigureAwait(false));
        }
    }

    /// <summary>Tears every subscription and the subjects down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        for (var i = 0; i < _behaviorSubscriptions.Count; i++)
        {
            await _behaviorSubscriptions[i].DisposeAsync().ConfigureAwait(false);
        }

        await _behavior.DisposeAsync().ConfigureAwait(false);

        for (var i = 0; i < _replayLatestSubscriptions.Count; i++)
        {
            await _replayLatestSubscriptions[i].DisposeAsync().ConfigureAwait(false);
        }

        await _replayLatest.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Broadcasts through a behaviour subject with <see cref="ObserverCount"/> observers.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task BehaviorSubject_Broadcast()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _behavior.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Broadcasts through a replay-latest subject with <see cref="ObserverCount"/> observers.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task ReplayLatestSubject_Broadcast()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _replayLatest.OnNextAsync(i, default).ConfigureAwait(false);
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
