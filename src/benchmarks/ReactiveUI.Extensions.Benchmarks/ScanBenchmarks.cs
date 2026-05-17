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
/// Measures the per-emission cost of plain <c>Scan</c> (sync and async accumulator variants).
/// Complements <see cref="ScanWithInitialBenchmarks"/>, which fuses a leading seed emission into
/// the operator.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ScanBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Initial accumulator value for both Scan variants.</summary>
    private const int InitialSeed = 0;

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Source feeding the sync-Scan pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _syncSource = null!;

    /// <summary>Subscription on the sync-Scan pipeline.</summary>
    private IAsyncDisposable _syncSubscription = null!;

    /// <summary>Source feeding the async-Scan pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _asyncSource = null!;

    /// <summary>Subscription on the async-Scan pipeline.</summary>
    private IAsyncDisposable _asyncSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires every pipeline.</summary>
    /// <returns>A task that completes when every pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _syncSource = new SerialStatelessSubjectAsync<int>();
        _syncSubscription = await _syncSource.Scan(InitialSeed, static (acc, value) => acc + value)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _asyncSource = new SerialStatelessSubjectAsync<int>();
        _asyncSubscription = await _asyncSource.Scan(InitialSeed, static (acc, value, _) => new ValueTask<int>(acc + value))
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears every pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _syncSubscription.DisposeAsync().ConfigureAwait(false);
        await _syncSource.DisposeAsync().ConfigureAwait(false);
        await _asyncSubscription.DisposeAsync().ConfigureAwait(false);
        await _asyncSource.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives values through <c>Scan</c> with a synchronous accumulator.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Scan_Sync_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _syncSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives values through <c>Scan</c> with an asynchronous accumulator that completes synchronously.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Scan_Async_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _asyncSource.OnNextAsync(i, default).ConfigureAwait(false);
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
