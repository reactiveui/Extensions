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
/// Measures the per-emission cost of <c>GetMax</c> and <c>CombineLatestValuesAreAllTrue</c> —
/// parity-helper combinators built on top of <c>CombineLatest</c>. Each pipeline has four
/// pre-primed sources so every emission produces a downstream value.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class GetMinMaxAndBooleanCombineBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Number of sources combined.</summary>
    private const int SourceCount = 4;

    /// <summary>Shared no-op sink for <c>GetMax</c>'s integer output.</summary>
    private readonly BenchmarkNoopObserver<int> _intSink = new();

    /// <summary>Shared no-op sink for the boolean combinator's output.</summary>
    private readonly BenchmarkNoopObserver<bool> _boolSink = new();

    /// <summary>Sources for the <c>GetMax</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int>[] _maxSources = null!;

    /// <summary>Subscription on the <c>GetMax</c> pipeline.</summary>
    private IAsyncDisposable _maxSubscription = null!;

    /// <summary>Sources for the <c>CombineLatestValuesAreAllTrue</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<bool>[] _allTrueSources = null!;

    /// <summary>Subscription on the <c>CombineLatestValuesAreAllTrue</c> pipeline.</summary>
    private IAsyncDisposable _allTrueSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed into the first source per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires both combinators and primes every source.</summary>
    /// <returns>A task that completes when every pipeline is subscribed and primed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _maxSources = new SerialStatelessSubjectAsync<int>[SourceCount];
        for (var i = 0; i < SourceCount; i++)
        {
            _maxSources[i] = new SerialStatelessSubjectAsync<int>();
        }

        var maxOthers = new IObservableAsync<int>[SourceCount - 1];
        for (var i = 1; i < SourceCount; i++)
        {
            maxOthers[i - 1] = _maxSources[i];
        }

        _maxSubscription = await _maxSources[0].GetMax(maxOthers)
            .SubscribeAsync(_intSink, default).ConfigureAwait(false);

        for (var i = 0; i < SourceCount; i++)
        {
            await _maxSources[i].OnNextAsync(0, default).ConfigureAwait(false);
        }

        _allTrueSources = new SerialStatelessSubjectAsync<bool>[SourceCount];
        var allTrueSnapshot = new IObservableAsync<bool>[SourceCount];
        for (var i = 0; i < SourceCount; i++)
        {
            _allTrueSources[i] = new SerialStatelessSubjectAsync<bool>();
            allTrueSnapshot[i] = _allTrueSources[i];
        }

        _allTrueSubscription = await allTrueSnapshot.CombineLatestValuesAreAllTrue()
            .SubscribeAsync(_boolSink, default).ConfigureAwait(false);

        for (var i = 0; i < SourceCount; i++)
        {
            await _allTrueSources[i].OnNextAsync(true, default).ConfigureAwait(false);
        }
    }

    /// <summary>Tears every subscription and source down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _maxSubscription.DisposeAsync().ConfigureAwait(false);
        for (var i = 0; i < _maxSources.Length; i++)
        {
            await _maxSources[i].DisposeAsync().ConfigureAwait(false);
        }

        await _allTrueSubscription.DisposeAsync().ConfigureAwait(false);
        for (var i = 0; i < _allTrueSources.Length; i++)
        {
            await _allTrueSources[i].DisposeAsync().ConfigureAwait(false);
        }

        await _intSink.DisposeAsync().ConfigureAwait(false);
        await _boolSink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Pushes values into the first source of the <c>GetMax</c> pipeline; every emission produces a downstream value because all four are primed.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task GetMax_FourPrimedSources()
    {
        var src0 = _maxSources[0];
        for (var i = 0; i < EmissionCount; i++)
        {
            await src0.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Pushes <c>true</c> values into the first source of the <c>CombineLatestValuesAreAllTrue</c> pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task CombineLatestValuesAreAllTrue_FourPrimedSources()
    {
        var src0 = _allTrueSources[0];
        for (var i = 0; i < EmissionCount; i++)
        {
            await src0.OnNextAsync(true, default).ConfigureAwait(false);
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
