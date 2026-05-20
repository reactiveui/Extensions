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
/// Measures the per-emission cost of the async→sync bridge
/// (<c>ToObservable</c> in <c>Async/Bridge/ObservableBridgeExtensions.cs</c>): an async subject is
/// bridged into a classic <see cref="IObservable{T}"/>, a synchronous observer is attached, and
/// values are pushed through. This complements the sync→async direction (<c>ToObservableAsync</c>)
/// already covered by the bridge profile benchmarks.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class AsyncToSyncBridgeBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Synchronous no-op sink subscribed to the bridged observable.</summary>
    private readonly NoopObserver<int> _sink = new();

    /// <summary>Async subject feeding the bridge.</summary>
    private SerialStatelessSubjectAsync<int> _source = null!;

    /// <summary>Subscription returned by the bridged sync observable.</summary>
    private IDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the bridge per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Builds the bridge and attaches the sync sink.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _source = new SerialStatelessSubjectAsync<int>();
        _subscription = _source.ToObservable().Subscribe(_sink);
    }

    /// <summary>Tears the bridge down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        _subscription.Dispose();
        await _source.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Pushes <see cref="EmissionCount"/> values through the async→sync bridge.</summary>
    /// <returns>A task that completes when every value has been propagated to the sync sink.</returns>
    [Benchmark]
    public async Task AsyncToSyncBridge_PerEmission()
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

    /// <summary>No-op synchronous observer used as the bridge's terminal sink.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class NoopObserver<T> : IObserver<T>
    {
        /// <inheritdoc/>
        public void OnNext(T value)
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }
    }
}
