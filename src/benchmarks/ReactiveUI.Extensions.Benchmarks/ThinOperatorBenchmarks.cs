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
/// Measures the per-emission cost of the thin operators (Select / Where / Scan, Packet 5 territory).
/// The pipeline is: SerialStatelessSubjectAsync → Select → Where → Scan → noop observer. Iterating the
/// pipeline at <see cref="EmissionCount"/> emissions per benchmark amortises subscription cost across the
/// invocations that get measured by MemoryDiagnoser.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ThinOperatorBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Shared no-op sink so the benchmark itself doesn't allocate observers in the hot path.</summary>
    private readonly NoopObserver<int> _sink = new();

    /// <summary>The Subject pushing values into the pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _source = null!;

    /// <summary>The subscription returned from subscribing the noop sink to the pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Builds the Select → Where → Scan pipeline and subscribes the noop sink.</summary>
    /// <returns>A task that completes when the pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<int>();
        var pipeline = _source
            .Select(static x => x + 1)
            .Where(static x => (x & 1) == 0)
            .Scan(0, static (acc, x) => acc + x);
        _subscription = await pipeline.SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Disposes the subscription and the source subject.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        if (_subscription is not null)
        {
            await _subscription.DisposeAsync().ConfigureAwait(false);
        }

        if (_source is not null)
        {
            await _source.DisposeAsync().ConfigureAwait(false);
        }

        await _sink.DisposeAsync().ConfigureAwait(false);
        _subscription = null!;
        _source = null!;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Pushes <see cref="EmissionCount"/> values through the Select → Where → Scan pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task SelectWhereScan_Pipeline()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _source.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Releases the managed cleanup state (delegates to <c>CleanupAsync</c>). Virtual so BenchmarkDotNet
    /// fixtures stay overridable; <c>sealed</c> isn't an option because BDN refuses to drive a sealed
    /// benchmark class.
    /// </summary>
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

    /// <summary>No-op async observer used as the pipeline sink.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class NoopObserver<T> : IObserverAsync<T>
    {
        /// <inheritdoc/>
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken) => default;

        /// <inheritdoc/>
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken) => default;

        /// <inheritdoc/>
        public ValueTask OnCompletedAsync(Result result) => default;

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => default;
    }
}
