// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>GC-verbose hotspot profile for <c>Delay</c> with an immediate-fire <see cref="TimeProvider"/>.</summary>
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class DelayGcProfileBenchmarks : IDisposable
{
    /// <summary>Number of emissions pushed through the pipeline per invocation.</summary>
    private const int EmissionCount = 10_000;

    /// <summary>Delay window; ignored at the wall-clock level because the time provider fires synchronously.</summary>
    private static readonly TimeSpan DelayWindow = TimeSpan.FromMilliseconds(50);

    /// <summary>Deterministic <see cref="TimeProvider"/> that fires each timer synchronously.</summary>
    private readonly BenchmarkImmediateFireTimeProvider _timeProvider = new();

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Source feeding the Delay pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _source = null!;

    /// <summary>Subscription on the Delay pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Wires the Delay pipeline.</summary>
    /// <returns>A task that completes when the pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<int>();
        _subscription = await _source.Delay(DelayWindow, _timeProvider)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subscription.DisposeAsync().ConfigureAwait(false);
        await _source.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the Delay pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Delay_ImmediateFire()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
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
