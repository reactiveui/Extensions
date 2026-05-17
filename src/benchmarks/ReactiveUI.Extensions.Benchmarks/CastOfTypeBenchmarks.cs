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
/// Measures the per-emission cost of the type-coercion operators: <c>Cast</c> (assumes every value
/// matches the target type) and <c>OfType</c> (filters values to a target type). The source emits
/// <see cref="object"/> values that are always <see cref="string"/> instances, so both operators
/// pass every value through. <see cref="string"/> is used as the target rather than <see cref="int"/>
/// because <c>OfType</c> requires a reference-type constraint.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class CastOfTypeBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Pre-interned reference value reused across every emission so the benchmark doesn't allocate a fresh string per push.</summary>
    private static readonly object SharedValue = "x";

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<string> _sink = new();

    /// <summary>Source for the <c>Cast</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<object> _castSource = null!;

    /// <summary>Subscription on the <c>Cast</c> pipeline.</summary>
    private IAsyncDisposable _castSubscription = null!;

    /// <summary>Source for the <c>OfType</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<object> _ofTypeSource = null!;

    /// <summary>Subscription on the <c>OfType</c> pipeline.</summary>
    private IAsyncDisposable _ofTypeSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the Cast and OfType pipelines.</summary>
    /// <returns>A task that completes when every pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _castSource = new SerialStatelessSubjectAsync<object>();
        _castSubscription = await _castSource.Cast<object, string>()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _ofTypeSource = new SerialStatelessSubjectAsync<object>();
        _ofTypeSubscription = await _ofTypeSource.OfType<object, string>()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipelines down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _castSubscription.DisposeAsync().ConfigureAwait(false);
        await _castSource.DisposeAsync().ConfigureAwait(false);
        await _ofTypeSubscription.DisposeAsync().ConfigureAwait(false);
        await _ofTypeSource.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the Cast pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Cast_MatchingType()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _castSource.OnNextAsync(SharedValue, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through the OfType pipeline (every value matches the target type).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task OfType_AllPassing()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _ofTypeSource.OnNextAsync(SharedValue, default).ConfigureAwait(false);
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
