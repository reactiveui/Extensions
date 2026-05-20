// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Per-subscribe cost of the async observable factories that hadn't been benchmarked:
/// <c>Defer</c>, <c>Create</c>, <c>CreateAsBackgroundJob</c>, <c>Empty</c>, <c>Never</c>.
/// Locks in the per-instance allocation profile each factory imposes.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class AsyncFactoryBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int SmallInvocationCount = 1_000;

    /// <summary>High end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int LargeInvocationCount = 10_000;

    /// <summary>Pre-built inner observable returned by the Defer factory delegate.</summary>
    private static readonly IObservableAsync<int> _innerObservable = ObservableAsync.Return(0);

    /// <summary>Static factory delegate captured once; the deferred path subscribes through it per call.</summary>
    private static readonly Func<IObservableAsync<int>> _deferFactory = static () => _innerObservable;

    /// <summary>Sink used by every drain.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Gets or sets the number of subscribe + drain cycles per benchmark invocation.</summary>
    [Params(SmallInvocationCount, LargeInvocationCount)]
    public int InvocationCount { get; set; }

    /// <summary>Tears the sink down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync() => await _sink.DisposeAsync().ConfigureAwait(false);

    /// <summary>Loops <c>Defer</c> subscribe-and-drain cycles.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task Defer_SubscribeAndDrain()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            await using var sub = await ObservableAsync.Defer(_deferFactory).SubscribeAsync(_sink, default).ConfigureAwait(false);
        }
    }

    /// <summary>Loops <c>Create</c> subscribe cycles. The factory delegate is a static no-capture that
    /// emits one value and returns <see cref="DisposableAsync.Empty"/>.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task Create_SubscribeAndDrain()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            await using var sub = await ObservableAsync.Create<int>(static async (observer, ct) =>
            {
                await observer.OnNextAsync(0, ct).ConfigureAwait(false);
                await observer.OnCompletedAsync(Result.Success).ConfigureAwait(false);
                return DisposableAsync.Empty;
            }).SubscribeAsync(_sink, default).ConfigureAwait(false);
        }
    }

    /// <summary>Loops <c>CreateAsBackgroundJob</c> subscribe cycles. The deferred body runs as a
    /// fire-and-forget job; the benchmark measures the per-Subscribe machinery, not the job body.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task CreateAsBackgroundJob_SubscribeAndDrain()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            await using var sub = await ObservableAsync.CreateAsBackgroundJob<int>(static async (observer, ct) =>
            {
                await observer.OnNextAsync(0, ct).ConfigureAwait(false);
                await observer.OnCompletedAsync(Result.Success).ConfigureAwait(false);
            }).SubscribeAsync(_sink, default).ConfigureAwait(false);
        }
    }

    /// <summary>Loops <c>Empty</c> subscribe cycles. Empty is a singleton instance; the benchmark
    /// confirms zero per-cycle allocation overhead.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task Empty_SubscribeAndDrain()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            await using var sub = await ObservableAsync.Empty<int>().SubscribeAsync(_sink, default).ConfigureAwait(false);
        }
    }

    /// <summary>Loops <c>Never</c> subscribe / dispose cycles. Never emits nothing; the benchmark
    /// measures the per-subscribe + per-dispose overhead without any emission cost.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task Never_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            await using var sub = await ObservableAsync.Never<int>().SubscribeAsync(_sink, default).ConfigureAwait(false);
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
