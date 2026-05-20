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
/// Per-emission steady-state cost for the async boolean operators that hadn't been benchmarked:
/// <c>WhereFalse</c> and <c>CombineLatestValuesAreAllFalse</c>. Locks in the zero-alloc baseline
/// on the filter and the per-emission aggregate path.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class BooleanAsyncBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Shared no-op sink.</summary>
    private readonly BenchmarkNoopObserver<bool> _sink = new();

    /// <summary>Source for the WhereFalse pipeline.</summary>
    private SerialStatelessSubjectAsync<bool> _whereFalseSource = null!;

    /// <summary>Subscription on the WhereFalse pipeline.</summary>
    private IAsyncDisposable _whereFalseSubscription = null!;

    /// <summary>Inputs for the CombineLatestValuesAreAllFalse pipeline.</summary>
    private SerialStatelessSubjectAsync<bool> _aggregateA = null!;

    /// <summary>Second input for the aggregate pipeline.</summary>
    private SerialStatelessSubjectAsync<bool> _aggregateB = null!;

    /// <summary>Subscription on the aggregate pipeline.</summary>
    private IAsyncDisposable _aggregateSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires both pipelines.</summary>
    /// <returns>A task that completes when both pipelines are subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _whereFalseSource = new SerialStatelessSubjectAsync<bool>();
        _whereFalseSubscription = await _whereFalseSource.WhereFalse()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _aggregateA = new SerialStatelessSubjectAsync<bool>();
        _aggregateB = new SerialStatelessSubjectAsync<bool>();
        IObservableAsync<bool>[] sources = [_aggregateA, _aggregateB];
        _aggregateSubscription = await sources.CombineLatestValuesAreAllFalse()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        // Prime both sources so the aggregate has values to combine.
        await _aggregateA.OnNextAsync(false, default).ConfigureAwait(false);
        await _aggregateB.OnNextAsync(false, default).ConfigureAwait(false);
    }

    /// <summary>Tears both pipelines down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _whereFalseSubscription.DisposeAsync().ConfigureAwait(false);
        await _aggregateSubscription.DisposeAsync().ConfigureAwait(false);
        await _whereFalseSource.DisposeAsync().ConfigureAwait(false);
        await _aggregateA.DisposeAsync().ConfigureAwait(false);
        await _aggregateB.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives all-false values through the WhereFalse pipeline (every emission passes).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task WhereFalse_AllPassing()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _whereFalseSource.OnNextAsync(false, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives alternating false / false emissions through the aggregate pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task CombineLatestValuesAreAllFalse_SteadyState()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _aggregateA.OnNextAsync(false, default).ConfigureAwait(false);
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
