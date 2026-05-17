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
/// Measures the per-emission cost of <c>SelectMany</c> with a steady inner source. The outer
/// subject's selector always returns the same inner subject, so the benchmark measures the
/// steady-state fan-out path rather than the inner-subscribe / unsubscribe churn.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SelectManyBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Steady inner subject reused for every outer emission.</summary>
    private SerialStatelessSubjectAsync<int> _inner = null!;

    /// <summary>Outer source that drives the inner-source fan-out (subscribed once, then values fed into <see cref="_inner"/>).</summary>
    private SerialStatelessSubjectAsync<int> _outer = null!;

    /// <summary>Subscription on the SelectMany pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the inner subject per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the SelectMany pipeline and primes the outer with the inner.</summary>
    /// <returns>A task that completes when the pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _outer = new SerialStatelessSubjectAsync<int>();
        _inner = new SerialStatelessSubjectAsync<int>();
        var pipeline = _outer.SelectMany(_ => _inner);
        _subscription = await pipeline.SubscribeAsync(_sink, default).ConfigureAwait(false);

        // Push the inner observable once so subsequent inner emissions traverse the fan-out path.
        await _outer.OnNextAsync(0, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subscription.DisposeAsync().ConfigureAwait(false);
        await _inner.DisposeAsync().ConfigureAwait(false);
        await _outer.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the inner subject.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task SelectMany_SteadyInnerSource()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _inner.OnNextAsync(i, default).ConfigureAwait(false);
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
