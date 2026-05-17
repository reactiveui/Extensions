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
/// GC-verbose hotspot profile for the <c>Concat(IEnumerable)</c> pipeline. Pairs with
/// <see cref="ConcatBenchmarks"/>; trace consumable by <c>rxext-allocreport</c>.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class ConcatGcProfileBenchmarks : IDisposable
{
    /// <summary>Number of emissions pushed through the pipeline per invocation.</summary>
    private const int EmissionCount = 10_000;

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>The single inner subject fed during the benchmark body.</summary>
    private SerialStatelessSubjectAsync<int> _inner = null!;

    /// <summary>Subscription on the Concat pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Wires the Concat pipeline.</summary>
    /// <returns>A task that completes when the pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _inner = new SerialStatelessSubjectAsync<int>();
        var sources = new IObservableAsync<int>[] { _inner };
        _subscription = await sources.Concat()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subscription.DisposeAsync().ConfigureAwait(false);
        await _inner.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the inner subject.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Concat_SingleSteadyInner()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _inner.OnNextAsync(i, default).ConfigureAwait(false);
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
