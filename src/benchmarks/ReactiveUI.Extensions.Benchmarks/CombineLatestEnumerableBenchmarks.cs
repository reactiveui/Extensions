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
/// Measures the per-emission cost of the enumerable / N-ary form of <c>CombineLatest</c> (Packet 6's
/// snapshot-buffer reuse). Sits on a different implementation than the typed
/// <c>CombineLatest2..16</c> overloads — the enumerable form rents a snapshot buffer from the pool
/// per emission and projects via <c>IReadOnlyList&lt;T&gt;</c>.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class CombineLatestEnumerableBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Number of sources combined.</summary>
    private const int SourceCount = 4;

    /// <summary>Shared no-op sink for the combined snapshot output.</summary>
    private readonly BenchmarkNoopObserver<IReadOnlyList<int>> _sink = new();

    /// <summary>Underlying source subjects.</summary>
    private SerialStatelessSubjectAsync<int>[] _sources = null!;

    /// <summary>Subscription on the combined pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the combined pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the enumerable CombineLatest and primes every source.</summary>
    /// <returns>A task that completes when the pipeline is subscribed and primed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _sources = new SerialStatelessSubjectAsync<int>[SourceCount];
        var snapshot = new IObservableAsync<int>[SourceCount];
        for (var i = 0; i < SourceCount; i++)
        {
            _sources[i] = new SerialStatelessSubjectAsync<int>();
            snapshot[i] = _sources[i];
        }

        _subscription = await snapshot.CombineLatest()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        // Prime every source so the combined pipeline emits on subsequent value updates.
        for (var i = 0; i < SourceCount; i++)
        {
            await _sources[i].OnNextAsync(0, default).ConfigureAwait(false);
        }
    }

    /// <summary>Tears the pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subscription.DisposeAsync().ConfigureAwait(false);
        for (var i = 0; i < _sources.Length; i++)
        {
            await _sources[i].DisposeAsync().ConfigureAwait(false);
        }

        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Pushes <see cref="EmissionCount"/> values into the first source so every emission projects a snapshot.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task CombineLatestEnumerable_PrimedFourSources()
    {
        var src0 = _sources[0];
        for (var i = 0; i < EmissionCount; i++)
        {
            await src0.OnNextAsync(i, default).ConfigureAwait(false);
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
