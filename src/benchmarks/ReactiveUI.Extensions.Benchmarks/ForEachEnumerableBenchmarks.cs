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
/// Measures the per-emission cost of <c>ForEach</c>, which flattens each emitted
/// <see cref="IEnumerable{T}"/> into individual values. Currently implemented as
/// <c>SelectMany(values =&gt; values.ToObservableAsync())</c> — surfaces the cost of building a
/// new inner observable per upstream emission.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ForEachEnumerableBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 100;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 1_000;

    /// <summary>Pre-built 4-element inner array reused across every upstream emission so the bench only measures the operator.</summary>
    private static readonly int[] SharedInner = [1, 2, 3, 4];

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Source feeding the <c>ForEach</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<IEnumerable<int>> _source = null!;

    /// <summary>Subscription on the pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Gets or sets the number of upstream emissions per benchmark invocation. Each emission yields <see cref="SharedInner"/>.Length values downstream.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the pipeline.</summary>
    /// <returns>A task that completes when the pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<IEnumerable<int>>();
        _subscription = await _source.ForEach()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subscription.DisposeAsync().ConfigureAwait(false);
        await _source.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives upstream emissions of a shared 4-element array through <c>ForEach</c>.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task ForEach_SharedInnerArray()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _source.OnNextAsync(SharedInner, default).ConfigureAwait(false);
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
