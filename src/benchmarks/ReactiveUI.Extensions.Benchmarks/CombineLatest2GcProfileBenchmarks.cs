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
/// GC-verbose hotspot profile for the arity-2 typed <c>CombineLatest</c> observable (the
/// <c>CombineLatestObservable2</c> family in <c>Async/Operators/</c>) together with its supporting
/// <c>AsyncGate</c>. Trace consumable by <c>rxext-allocreport</c>; complements
/// <see cref="CombineLatestBenchmarks"/>.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class CombineLatest2GcProfileBenchmarks : IDisposable
{
    /// <summary>Number of emissions pushed through the typed CombineLatest2 per invocation.</summary>
    private const int EmissionCount = 10_000;

    /// <summary>Shared no-op sink for CombineLatest's tuple output.</summary>
    private readonly BenchmarkNoopObserver<(int, int)> _sink = new();

    /// <summary>First source for the CombineLatest2 pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _src1 = null!;

    /// <summary>Second source for the CombineLatest2 pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _src2 = null!;

    /// <summary>Subscription returned by the CombineLatest2 pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Wires the CombineLatest2 pipeline and primes both sources.</summary>
    /// <returns>A task that completes when setup is done.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _src1 = new SerialStatelessSubjectAsync<int>();
        _src2 = new SerialStatelessSubjectAsync<int>();
        _subscription = await _src1
            .CombineLatest(_src2, static (a, b) => (a, b))
            .SubscribeAsync(_sink, default)
            .ConfigureAwait(false);
        await _src1.OnNextAsync(0, default).ConfigureAwait(false);
        await _src2.OnNextAsync(0, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subscription.DisposeAsync().ConfigureAwait(false);
        await _src1.DisposeAsync().ConfigureAwait(false);
        await _src2.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the typed CombineLatest2.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task CombineLatest2()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _src1.OnNextAsync(i, default).ConfigureAwait(false);
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
