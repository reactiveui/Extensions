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
/// Measures the per-emission cost of the parity helper <c>Partition</c>, which splits a single
/// upstream into two downstream sequences (true and false branches) using a shared
/// <c>Publish().RefCount()</c> backbone. Both branches are subscribed; the predicate is evaluated
/// exactly once per upstream emission.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class PartitionBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Shared no-op sink for the truthy branch.</summary>
    private readonly BenchmarkNoopObserver<int> _trueSink = new();

    /// <summary>Shared no-op sink for the falsy branch.</summary>
    private readonly BenchmarkNoopObserver<int> _falseSink = new();

    /// <summary>Source feeding the partitioned pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _source = null!;

    /// <summary>Subscription on the truthy branch.</summary>
    private IAsyncDisposable _trueSubscription = null!;

    /// <summary>Subscription on the falsy branch.</summary>
    private IAsyncDisposable _falseSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires both branches of the partitioned pipeline.</summary>
    /// <returns>A task that completes when both branches are subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<int>();
        var (trueBranch, falseBranch) = _source.Partition(static value => (value & 1) == 0);
        _trueSubscription = await trueBranch.SubscribeAsync(_trueSink, default).ConfigureAwait(false);
        _falseSubscription = await falseBranch.SubscribeAsync(_falseSink, default).ConfigureAwait(false);
    }

    /// <summary>Tears both branches and the source down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _trueSubscription.DisposeAsync().ConfigureAwait(false);
        await _falseSubscription.DisposeAsync().ConfigureAwait(false);
        await _source.DisposeAsync().ConfigureAwait(false);
        await _trueSink.DisposeAsync().ConfigureAwait(false);
        await _falseSink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives alternating values through the partition so each branch sees half the emissions.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Partition_AlternatingValues()
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
