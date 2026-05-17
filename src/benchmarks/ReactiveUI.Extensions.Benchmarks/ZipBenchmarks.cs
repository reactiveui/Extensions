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
/// Measures the per-emission cost of arity-2 <c>Zip</c>. Each emission pairs one value from each
/// source, requiring both sources to advance lock-step. The benchmark body alternates a push to
/// source 1 then source 2 so a paired tuple is emitted exactly once per outer iteration.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ZipBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="PairCount"/> parameter sweep.</summary>
    private const int SmallPairCount = 1_000;

    /// <summary>High end of the <see cref="PairCount"/> parameter sweep.</summary>
    private const int LargePairCount = 10_000;

    /// <summary>Shared no-op sink for Zip's tuple output.</summary>
    private readonly BenchmarkNoopObserver<(int, int)> _sink = new();

    /// <summary>First source feeding the Zip pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _src1 = null!;

    /// <summary>Second source feeding the Zip pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _src2 = null!;

    /// <summary>Subscription on the Zip pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Gets or sets the number of pairs emitted per benchmark invocation.</summary>
    [Params(SmallPairCount, LargePairCount)]
    public int PairCount { get; set; }

    /// <summary>Wires the Zip pipeline.</summary>
    /// <returns>A task that completes when the pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _src1 = new SerialStatelessSubjectAsync<int>();
        _src2 = new SerialStatelessSubjectAsync<int>();
        _subscription = await _src1.Zip(_src2)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
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

    /// <summary>Alternates pushes across both sources so every outer iteration produces one paired tuple.</summary>
    /// <returns>A task that completes when every pair has been propagated.</returns>
    [Benchmark]
    public async Task Zip_PairedTwoSources()
    {
        for (var i = 0; i < PairCount; i++)
        {
            await _src1.OnNextAsync(i, default).ConfigureAwait(false);
            await _src2.OnNextAsync(i, default).ConfigureAwait(false);
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
