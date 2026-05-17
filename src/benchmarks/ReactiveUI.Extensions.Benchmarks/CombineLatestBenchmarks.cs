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
/// Measures CombineLatest's per-emission cost — both the arity-N typed form (CombineLatest2, Packet 2
/// linked-CTS removal) and the enumerable form (Packet 6's snapshot-buffer reuse).
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class CombineLatestBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Priming value pushed into every source during setup so subsequent emissions actually project a result.</summary>
    private const int PrimeValue = 0;

    /// <summary>Sink for the typed CombineLatest2 benchmark.</summary>
    private readonly NoopObserver<(int, int)> _typedSink = new();

    /// <summary>Sink for the enumerable CombineLatest benchmark.</summary>
    private readonly NoopObserver<IReadOnlyList<int>> _enumerableSink = new();

    /// <summary>First source observable for both benchmarks.</summary>
    private SerialStatelessSubjectAsync<int> _src1 = null!;

    /// <summary>Second source observable.</summary>
    private SerialStatelessSubjectAsync<int> _src2 = null!;

    /// <summary>Third source observable, used by the enumerable benchmark.</summary>
    private SerialStatelessSubjectAsync<int> _src3 = null!;

    /// <summary>Fourth source observable, used by the enumerable benchmark.</summary>
    private SerialStatelessSubjectAsync<int> _src4 = null!;

    /// <summary>Subscription returned by the typed CombineLatest2 pipeline.</summary>
    private IAsyncDisposable _typedSubscription = null!;

    /// <summary>Subscription returned by the enumerable CombineLatest pipeline.</summary>
    private IAsyncDisposable _enumerableSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the operator per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Builds the typed CombineLatest2 and the four-source enumerable CombineLatest pipelines, then primes every source so subsequent emissions actually project a result.</summary>
    /// <returns>A task that completes when both pipelines are ready.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _src1 = new SerialStatelessSubjectAsync<int>();
        _src2 = new SerialStatelessSubjectAsync<int>();
        _src3 = new SerialStatelessSubjectAsync<int>();
        _src4 = new SerialStatelessSubjectAsync<int>();

        _typedSubscription = await _src1
            .CombineLatest(_src2, static (a, b) => (a, b))
            .SubscribeAsync(_typedSink, default)
            .ConfigureAwait(false);

        var sources = new IObservableAsync<int>[] { _src1, _src2, _src3, _src4 };
        _enumerableSubscription = await sources
            .CombineLatest()
            .SubscribeAsync(_enumerableSink, default)
            .ConfigureAwait(false);

        await _src1.OnNextAsync(PrimeValue, default).ConfigureAwait(false);
        await _src2.OnNextAsync(PrimeValue, default).ConfigureAwait(false);
        await _src3.OnNextAsync(PrimeValue, default).ConfigureAwait(false);
        await _src4.OnNextAsync(PrimeValue, default).ConfigureAwait(false);
    }

    /// <summary>Disposes every subscription and source subject.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        if (_typedSubscription is not null)
        {
            await _typedSubscription.DisposeAsync().ConfigureAwait(false);
        }

        if (_enumerableSubscription is not null)
        {
            await _enumerableSubscription.DisposeAsync().ConfigureAwait(false);
        }

        if (_src1 is not null)
        {
            await _src1.DisposeAsync().ConfigureAwait(false);
        }

        if (_src2 is not null)
        {
            await _src2.DisposeAsync().ConfigureAwait(false);
        }

        if (_src3 is not null)
        {
            await _src3.DisposeAsync().ConfigureAwait(false);
        }

        if (_src4 is not null)
        {
            await _src4.DisposeAsync().ConfigureAwait(false);
        }

        await _typedSink.DisposeAsync().ConfigureAwait(false);
        await _enumerableSink.DisposeAsync().ConfigureAwait(false);
        _typedSubscription = null!;
        _enumerableSubscription = null!;
        _src1 = null!;
        _src2 = null!;
        _src3 = null!;
        _src4 = null!;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Pushes <see cref="EmissionCount"/> values into source 1 of a typed CombineLatest2.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    [BenchmarkCategory("Typed2")]
    public async Task CombineLatest2_Typed()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _src1.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Pushes <see cref="EmissionCount"/> values into source 1 of a four-source enumerable CombineLatest.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    [BenchmarkCategory("Enumerable4")]
    public async Task CombineLatest4_Enumerable()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _src1.OnNextAsync(i, default).ConfigureAwait(false);
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

    /// <summary>No-op async observer used as the sink for both benchmark variants.</summary>
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
