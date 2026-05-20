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
/// Measures the per-emission cost of <c>StatelessReplayLatestPublish</c> (multicast variant that
/// replays the most recent value to late subscribers, with no replay buffer beyond one slot).
/// Two observers are connected so the broadcast loop is exercised.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class StatelessReplayLatestPublishBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>First downstream sink.</summary>
    private readonly BenchmarkNoopObserver<int> _sinkA = new();

    /// <summary>Second downstream sink.</summary>
    private readonly BenchmarkNoopObserver<int> _sinkB = new();

    /// <summary>Source feeding the multicast.</summary>
    private SerialStatelessSubjectAsync<int> _source = null!;

    /// <summary>Connect-disposable from <c>ConnectAsync</c>.</summary>
    private IAsyncDisposable _connect = null!;

    /// <summary>First subscription on the published observable.</summary>
    private IAsyncDisposable _subA = null!;

    /// <summary>Second subscription on the published observable.</summary>
    private IAsyncDisposable _subB = null!;

    /// <summary>Gets or sets the number of emissions pushed per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the multicast with two observers attached and the source connected.</summary>
    /// <returns>A task that completes when setup is done.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<int>();
        var connectable = _source.StatelessReplayLatestPublish();

        _subA = await connectable.SubscribeAsync(_sinkA, default).ConfigureAwait(false);
        _subB = await connectable.SubscribeAsync(_sinkB, default).ConfigureAwait(false);
        _connect = await connectable.ConnectAsync(default).ConfigureAwait(false);
    }

    /// <summary>Tears the multicast down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subA.DisposeAsync().ConfigureAwait(false);
        await _subB.DisposeAsync().ConfigureAwait(false);
        await _connect.DisposeAsync().ConfigureAwait(false);
        await _source.DisposeAsync().ConfigureAwait(false);
        await _sinkA.DisposeAsync().ConfigureAwait(false);
        await _sinkB.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the published multicast.</summary>
    /// <returns>A task that completes when every value has been broadcast.</returns>
    [Benchmark]
    public async Task StatelessReplayLatestPublish_TwoObservers()
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
