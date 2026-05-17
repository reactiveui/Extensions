// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-emission cost of the parity-helper filter / projection operators that aren't
/// already covered: <c>AsSignal</c> (projects to <see cref="Unit"/>), <c>Not</c> (boolean negate),
/// <c>WhereTrue</c> / <c>WhereFalse</c> (boolean filters), <c>WhereIsNotNull</c> (null filter),
/// and <c>Pairwise</c> (emits <c>(prev, current)</c> tuples).
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ParityFilterAndProjectionBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Pre-boxed non-null reference reused across <see cref="WhereIsNotNull_AllPassing"/> emissions.</summary>
    private const string SharedNonNull = "x";

    /// <summary>Shared sink for <see cref="Unit"/>-typed output.</summary>
    private readonly BenchmarkNoopObserver<Unit> _unitSink = new();

    /// <summary>Shared sink for <see cref="bool"/>-typed output.</summary>
    private readonly BenchmarkNoopObserver<bool> _boolSink = new();

    /// <summary>Shared sink for <see cref="string"/>-typed output.</summary>
    private readonly BenchmarkNoopObserver<string> _stringSink = new();

    /// <summary>Shared sink for the <c>(prev, current)</c> tuple output of <c>Pairwise</c>.</summary>
    private readonly BenchmarkNoopObserver<(int Previous, int Current)> _pairSink = new();

    /// <summary>Source for the <c>AsSignal</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _signalSource = null!;

    /// <summary>Subscription on the <c>AsSignal</c> pipeline.</summary>
    private IAsyncDisposable _signalSubscription = null!;

    /// <summary>Source for the <c>Not</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<bool> _notSource = null!;

    /// <summary>Subscription on the <c>Not</c> pipeline.</summary>
    private IAsyncDisposable _notSubscription = null!;

    /// <summary>Source for the <c>WhereTrue</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<bool> _whereTrueSource = null!;

    /// <summary>Subscription on the <c>WhereTrue</c> pipeline.</summary>
    private IAsyncDisposable _whereTrueSubscription = null!;

    /// <summary>Source for the <c>WhereIsNotNull</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<string?> _whereNotNullSource = null!;

    /// <summary>Subscription on the <c>WhereIsNotNull</c> pipeline.</summary>
    private IAsyncDisposable _whereNotNullSubscription = null!;

    /// <summary>Source for the <c>Pairwise</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _pairwiseSource = null!;

    /// <summary>Subscription on the <c>Pairwise</c> pipeline.</summary>
    private IAsyncDisposable _pairwiseSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires every pipeline; <c>Pairwise</c> is primed once so subsequent emissions land in the tuple-forwarding steady state.</summary>
    /// <returns>A task that completes when every pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _signalSource = new SerialStatelessSubjectAsync<int>();
        _signalSubscription = await _signalSource.AsSignal()
            .SubscribeAsync(_unitSink, default).ConfigureAwait(false);

        _notSource = new SerialStatelessSubjectAsync<bool>();
        _notSubscription = await _notSource.Not()
            .SubscribeAsync(_boolSink, default).ConfigureAwait(false);

        _whereTrueSource = new SerialStatelessSubjectAsync<bool>();
        _whereTrueSubscription = await _whereTrueSource.WhereTrue()
            .SubscribeAsync(_boolSink, default).ConfigureAwait(false);

        _whereNotNullSource = new SerialStatelessSubjectAsync<string?>();
        _whereNotNullSubscription = await _whereNotNullSource.WhereIsNotNull()
            .SubscribeAsync(_stringSink, default).ConfigureAwait(false);

        _pairwiseSource = new SerialStatelessSubjectAsync<int>();
        _pairwiseSubscription = await _pairwiseSource.Pairwise()
            .SubscribeAsync(_pairSink, default).ConfigureAwait(false);

        // Prime Pairwise so the first benchmark-body emission produces a tuple.
        await _pairwiseSource.OnNextAsync(0, default).ConfigureAwait(false);
    }

    /// <summary>Tears every pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _signalSubscription.DisposeAsync().ConfigureAwait(false);
        await _signalSource.DisposeAsync().ConfigureAwait(false);
        await _notSubscription.DisposeAsync().ConfigureAwait(false);
        await _notSource.DisposeAsync().ConfigureAwait(false);
        await _whereTrueSubscription.DisposeAsync().ConfigureAwait(false);
        await _whereTrueSource.DisposeAsync().ConfigureAwait(false);
        await _whereNotNullSubscription.DisposeAsync().ConfigureAwait(false);
        await _whereNotNullSource.DisposeAsync().ConfigureAwait(false);
        await _pairwiseSubscription.DisposeAsync().ConfigureAwait(false);
        await _pairwiseSource.DisposeAsync().ConfigureAwait(false);
        await _unitSink.DisposeAsync().ConfigureAwait(false);
        await _boolSink.DisposeAsync().ConfigureAwait(false);
        await _stringSink.DisposeAsync().ConfigureAwait(false);
        await _pairSink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives values through <c>AsSignal</c> (every emission becomes <see cref="MarkdownExporterAttribute.Default"/>).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task AsSignal_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _signalSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives alternating booleans through <c>Not</c>; every emission flips.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Not_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _notSource.OnNextAsync((i & 1) == 0, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives uniformly-<c>true</c> booleans through <c>WhereTrue</c> (every value passes).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task WhereTrue_AllPassing()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _whereTrueSource.OnNextAsync(true, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives a shared non-null reference through <c>WhereIsNotNull</c> (every value passes).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task WhereIsNotNull_AllPassing()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _whereNotNullSource.OnNextAsync(SharedNonNull, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives values through <c>Pairwise</c>; primed once in setup so each benchmark emission produces a tuple.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task Pairwise_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _pairwiseSource.OnNextAsync(i, default).ConfigureAwait(false);
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
