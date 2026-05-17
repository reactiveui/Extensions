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
/// Measures the per-emission cost of <c>Timeout</c> on the happy path (no timeout trips). Uses
/// <see cref="TimeProvider.System"/> with a window much larger than the benchmark's wall-time so
/// the timeout never elapses; the operator's per-emission rearm path is the measurement target.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class TimeoutBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Window long enough to never elapse during any BDN iteration; reset on every emission.</summary>
    private static readonly TimeSpan TimeoutWindow = TimeSpan.FromMinutes(10);

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Source feeding the Timeout pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _source = null!;

    /// <summary>Subscription on the Timeout pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the Timeout pipeline against <see cref="TimeProvider.System"/>.</summary>
    /// <returns>A task that completes when the pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<int>();
        _subscription = await _source.Timeout(TimeoutWindow)
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

    /// <summary>Drives <see cref="EmissionCount"/> values through the Timeout pipeline (no timeout fires).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Timeout_HappyPath()
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
