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
/// Measures the per-emission cost of <c>Merge</c> on a fixed-arity enumerable of inner subjects.
/// The inner sources are pre-subscribed at <c>GlobalSetup</c>; the benchmark drives values into the
/// inner subjects so every emission goes through the merge gate without paying subscribe / completion
/// overhead.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class MergeBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Number of inner subjects merged in <see cref="Merge_FourInnerSources"/>.</summary>
    private const int InnerSourceCount = 4;

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Inner sources fed by the benchmark body.</summary>
    private SerialStatelessSubjectAsync<int>[] _inners = null!;

    /// <summary>Subscription on the merged pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the merge per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires four inner subjects through <c>Merge(IEnumerable&lt;IObservableAsync&lt;T&gt;&gt;)</c>.</summary>
    /// <returns>A task that completes when the pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _inners = new SerialStatelessSubjectAsync<int>[InnerSourceCount];
        var snapshot = new IObservableAsync<int>[InnerSourceCount];
        for (var i = 0; i < InnerSourceCount; i++)
        {
            _inners[i] = new SerialStatelessSubjectAsync<int>();
            snapshot[i] = _inners[i];
        }

        _subscription = await snapshot.Merge()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subscription.DisposeAsync().ConfigureAwait(false);
        for (var i = 0; i < _inners.Length; i++)
        {
            await _inners[i].DisposeAsync().ConfigureAwait(false);
        }

        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Round-robins <see cref="EmissionCount"/> values across the four inner subjects.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Merge_FourInnerSources()
    {
        var inners = _inners;
        for (var i = 0; i < EmissionCount; i++)
        {
            await inners[i & (InnerSourceCount - 1)].OnNextAsync(i, default).ConfigureAwait(false);
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
