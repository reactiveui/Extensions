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
/// Per-emission broadcast cost of the two stateless replay-latest subjects that hadn't been
/// benchmarked: <see cref="ConcurrentStatelessReplayLatestSubjectAsync{T}"/> and
/// <see cref="SerialStatelessReplayLastSubjectAsync{T}"/>. Each runs with two observers attached
/// so the fan-out path is exercised.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class StatelessReplayLatestSubjectBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>First sink shared across both pipelines.</summary>
    private readonly BenchmarkNoopObserver<int> _sinkA = new();

    /// <summary>Second sink shared across both pipelines.</summary>
    private readonly BenchmarkNoopObserver<int> _sinkB = new();

    /// <summary>Concurrent stateless replay subject under test.</summary>
    private ConcurrentStatelessReplayLatestSubjectAsync<int> _concurrentSubject = null!;

    /// <summary>Serial stateless replay subject under test.</summary>
    private SerialStatelessReplayLastSubjectAsync<int> _serialSubject = null!;

    /// <summary>Concurrent subject's first subscription.</summary>
    private IAsyncDisposable _concurrentSubA = null!;

    /// <summary>Concurrent subject's second subscription.</summary>
    private IAsyncDisposable _concurrentSubB = null!;

    /// <summary>Serial subject's first subscription.</summary>
    private IAsyncDisposable _serialSubA = null!;

    /// <summary>Serial subject's second subscription.</summary>
    private IAsyncDisposable _serialSubB = null!;

    /// <summary>Gets or sets the number of emissions pushed per invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires both subjects with two observers each.</summary>
    /// <returns>A task that completes when setup is done.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _concurrentSubject = new ConcurrentStatelessReplayLatestSubjectAsync<int>(Optional<int>.Empty);
        _concurrentSubA = await _concurrentSubject.SubscribeAsync(_sinkA, default).ConfigureAwait(false);
        _concurrentSubB = await _concurrentSubject.SubscribeAsync(_sinkB, default).ConfigureAwait(false);

        _serialSubject = new SerialStatelessReplayLastSubjectAsync<int>(Optional<int>.Empty);
        _serialSubA = await _serialSubject.SubscribeAsync(_sinkA, default).ConfigureAwait(false);
        _serialSubB = await _serialSubject.SubscribeAsync(_sinkB, default).ConfigureAwait(false);
    }

    /// <summary>Tears every subject and subscription down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _concurrentSubA.DisposeAsync().ConfigureAwait(false);
        await _concurrentSubB.DisposeAsync().ConfigureAwait(false);
        await _serialSubA.DisposeAsync().ConfigureAwait(false);
        await _serialSubB.DisposeAsync().ConfigureAwait(false);
        await _concurrentSubject.DisposeAsync().ConfigureAwait(false);
        await _serialSubject.DisposeAsync().ConfigureAwait(false);
        await _sinkA.DisposeAsync().ConfigureAwait(false);
        await _sinkB.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the concurrent broadcast path.</summary>
    /// <returns>A task that completes when every value has been broadcast.</returns>
    [Benchmark]
    public async Task ConcurrentStatelessReplayLatest_TwoObservers()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _concurrentSubject.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the serial broadcast path.</summary>
    /// <returns>A task that completes when every value has been broadcast.</returns>
    [Benchmark]
    public async Task SerialStatelessReplayLast_TwoObservers()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _serialSubject.OnNextAsync(i, default).ConfigureAwait(false);
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
