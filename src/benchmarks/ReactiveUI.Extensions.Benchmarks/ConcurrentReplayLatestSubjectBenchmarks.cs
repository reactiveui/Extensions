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
/// Per-emission broadcast cost of <see cref="ConcurrentReplayLatestSubjectAsync{T}"/> with two
/// observers attached. Complements the existing serial-replay benchmarks; locks in the concurrent
/// fan-out path's overhead and surfaces any per-emission allocation.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ConcurrentReplayLatestSubjectBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>First downstream sink.</summary>
    private readonly BenchmarkNoopObserver<int> _sinkA = new();

    /// <summary>Second downstream sink.</summary>
    private readonly BenchmarkNoopObserver<int> _sinkB = new();

    /// <summary>The subject under test.</summary>
    private ConcurrentReplayLatestSubjectAsync<int> _subject = null!;

    /// <summary>First subscription.</summary>
    private IAsyncDisposable _subA = null!;

    /// <summary>Second subscription.</summary>
    private IAsyncDisposable _subB = null!;

    /// <summary>Gets or sets the number of emissions pushed per invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the subject and two observers.</summary>
    /// <returns>A task that completes when both observers are subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _subject = new ConcurrentReplayLatestSubjectAsync<int>(Optional<int>.Empty);
        _subA = await _subject.SubscribeAsync(_sinkA, default).ConfigureAwait(false);
        _subB = await _subject.SubscribeAsync(_sinkB, default).ConfigureAwait(false);
    }

    /// <summary>Tears the subject and subscriptions down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subA.DisposeAsync().ConfigureAwait(false);
        await _subB.DisposeAsync().ConfigureAwait(false);
        await _subject.DisposeAsync().ConfigureAwait(false);
        await _sinkA.DisposeAsync().ConfigureAwait(false);
        await _sinkB.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the concurrent broadcast path.</summary>
    /// <returns>A task that completes when every value has been broadcast.</returns>
    [Benchmark]
    public async Task Broadcast_TwoObservers()
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
