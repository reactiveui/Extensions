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
/// Steady-state per-emission cost of a cluster of async operators that hadn't been benchmarked:
/// <c>OnDispose</c>, <c>OnErrorResumeAsFailure</c>, <c>RefCount</c>, <c>TakeWhile</c>,
/// <c>WaitCompletionAsync</c>, plus the observer <c>Wrap</c> factory.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class AsyncOperatorCoverageBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Static no-op dispose action shared by the OnDispose pipeline.</summary>
    private static readonly Action _noopDisposeAction = static () => { };

    /// <summary>Shared no-op sink.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Source feeding the OnDispose pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _onDisposeSource = null!;

    /// <summary>Subscription on the OnDispose pipeline.</summary>
    private IAsyncDisposable _onDisposeSubscription = null!;

    /// <summary>Source feeding the OnErrorResumeAsFailure pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _onErrorSource = null!;

    /// <summary>Subscription on the OnErrorResumeAsFailure pipeline.</summary>
    private IAsyncDisposable _onErrorSubscription = null!;

    /// <summary>Source feeding the RefCount pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _refCountSource = null!;

    /// <summary>Subscription on the RefCount pipeline (keeps the upstream connected).</summary>
    private IAsyncDisposable _refCountSubscription = null!;

    /// <summary>Source feeding the TakeWhile pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _takeWhileSource = null!;

    /// <summary>Subscription on the TakeWhile pipeline.</summary>
    private IAsyncDisposable _takeWhileSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each per-emission pipeline.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires every long-lived pipeline.</summary>
    /// <returns>A task that completes when setup is done.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _onDisposeSource = new SerialStatelessSubjectAsync<int>();
        _onDisposeSubscription = await _onDisposeSource
            .OnDispose(_noopDisposeAction)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _onErrorSource = new SerialStatelessSubjectAsync<int>();
        _onErrorSubscription = await _onErrorSource
            .OnErrorResumeAsFailure()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _refCountSource = new SerialStatelessSubjectAsync<int>();
        var refCount = _refCountSource.Publish().RefCount();
        _refCountSubscription = await refCount.SubscribeAsync(_sink, default).ConfigureAwait(false);

        _takeWhileSource = new SerialStatelessSubjectAsync<int>();
        _takeWhileSubscription = await _takeWhileSource
            .TakeWhile(static _ => true)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears every pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _onDisposeSubscription.DisposeAsync().ConfigureAwait(false);
        await _onErrorSubscription.DisposeAsync().ConfigureAwait(false);
        await _refCountSubscription.DisposeAsync().ConfigureAwait(false);
        await _takeWhileSubscription.DisposeAsync().ConfigureAwait(false);
        await _onDisposeSource.DisposeAsync().ConfigureAwait(false);
        await _onErrorSource.DisposeAsync().ConfigureAwait(false);
        await _refCountSource.DisposeAsync().ConfigureAwait(false);
        await _takeWhileSource.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the OnDispose pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task OnDispose_SteadyState()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _onDisposeSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the OnErrorResumeAsFailure pipeline (no errors).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task OnErrorResumeAsFailure_HappyPath()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _onErrorSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the RefCount pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task RefCount_SteadyState()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _refCountSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the TakeWhile pipeline (predicate always true).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task TakeWhile_AllPassing()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _takeWhileSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drains <see cref="EmissionCount"/> single-shot <see cref="ObservableAsync.Return{T}"/> sources via <c>WaitCompletionAsync</c>.</summary>
    /// <returns>A task that completes when every drain has finished.</returns>
    [Benchmark]
    public async Task WaitCompletionAsync_SingleValueSource()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await ObservableAsync.Return(i).WaitCompletionAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Loops the <see cref="ObservableAsync.Wrap{T}(IObserverAsync{T})"/> factory call.</summary>
    [Benchmark]
    public void Wrap_PerCall()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _ = _sink.Wrap();
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
