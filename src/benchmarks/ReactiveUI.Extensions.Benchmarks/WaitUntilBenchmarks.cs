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
/// Measures the subscribe-and-first-match cost of <c>WaitUntil(predicate)</c>. Each iteration
/// of the benchmark body builds a fresh subscription and feeds one matching value, so the result
/// reflects the cold subscribe / first-emission / dispose cycle — the natural shape of a
/// "wait for the first matching value" use case. Currently implemented as
/// <c>Where(predicate).Take(1)</c> — a fusion candidate.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class WaitUntilBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="WaitCount"/> parameter sweep.</summary>
    private const int SmallWaitCount = 1_000;

    /// <summary>High end of the <see cref="WaitCount"/> parameter sweep.</summary>
    private const int LargeWaitCount = 10_000;

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Shared source reused across subscribe / unsubscribe cycles in the benchmark body.</summary>
    private SerialStatelessSubjectAsync<int> _source = null!;

    /// <summary>Gets or sets the number of subscribe + first-match cycles per benchmark invocation.</summary>
    [Params(SmallWaitCount, LargeWaitCount)]
    public int WaitCount { get; set; }

    /// <summary>Builds the shared source.</summary>
    /// <returns>A task that completes when setup is done.</returns>
    [GlobalSetup]
    public Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<int>();
        return Task.CompletedTask;
    }

    /// <summary>Tears the shared source down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _source.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Subscribes a fresh <c>WaitUntil</c> pipeline, pushes one matching value, lets the Take(1) terminate the pipeline.</summary>
    /// <returns>A task that completes after every subscribe / drain cycle has finished.</returns>
    [Benchmark]
    public async Task WaitUntil_SubscribeAndFirstMatch()
    {
        for (var i = 0; i < WaitCount; i++)
        {
            await using var subscription = await _source.WaitUntil(static x => x >= 0)
                .SubscribeAsync(_sink, default).ConfigureAwait(false);
            await _source.OnNextAsync(i, default).ConfigureAwait(false);
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
