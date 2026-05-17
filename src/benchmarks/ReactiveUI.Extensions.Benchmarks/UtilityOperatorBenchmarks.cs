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
/// Measures the per-emission cost of the small utility operators: <c>OnDispose</c> (synchronous
/// finally hook), <c>OnErrorResumeAsFailure</c> (rewrites OnErrorResume as completion failure
/// downstream), and <c>Yield</c> (forces an async hop). Each pipeline is pre-subscribed; the
/// benchmark body measures steady-state pass-through cost only.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class UtilityOperatorBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Source for the <c>OnDispose</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _onDisposeSource = null!;

    /// <summary>Subscription on the <c>OnDispose</c> pipeline.</summary>
    private IAsyncDisposable _onDisposeSubscription = null!;

    /// <summary>Source for the <c>OnErrorResumeAsFailure</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _resumeAsFailureSource = null!;

    /// <summary>Subscription on the <c>OnErrorResumeAsFailure</c> pipeline.</summary>
    private IAsyncDisposable _resumeAsFailureSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the OnDispose and OnErrorResumeAsFailure pipelines.</summary>
    /// <returns>A task that completes when every pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _onDisposeSource = new SerialStatelessSubjectAsync<int>();
        _onDisposeSubscription = await _onDisposeSource.OnDispose(static () => { })
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _resumeAsFailureSource = new SerialStatelessSubjectAsync<int>();
        _resumeAsFailureSubscription = await _resumeAsFailureSource.OnErrorResumeAsFailure()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipelines down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _onDisposeSubscription.DisposeAsync().ConfigureAwait(false);
        await _onDisposeSource.DisposeAsync().ConfigureAwait(false);
        await _resumeAsFailureSubscription.DisposeAsync().ConfigureAwait(false);
        await _resumeAsFailureSource.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the OnDispose pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task OnDispose_SteadyState()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _onDisposeSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the OnErrorResumeAsFailure pipeline (no errors).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task OnErrorResumeAsFailure_HappyPath()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _resumeAsFailureSource.OnNextAsync(i, default).ConfigureAwait(false);
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
