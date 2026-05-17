// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the subscribe-and-drain cost of <c>ForEachAsync</c> with sync and async callbacks.
/// Each invocation subscribes to a fresh finite source and awaits the drain.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ForEachAsyncBenchmarks
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 100;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 1_000;

    /// <summary>Pre-built source emitting <see cref="EmissionCount"/> integers then completing.</summary>
    private IObservableAsync<int> _source = null!;

    /// <summary>Gets or sets the number of values the source emits before completing.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Builds the finite source.</summary>
    [GlobalSetup]
    public void Setup() => _source = ObservableAsync.Range(0, EmissionCount);

    /// <summary>Drains the source through a no-capture synchronous action.</summary>
    /// <returns>A <see cref="ValueTask"/> that completes when the drain finishes.</returns>
    [Benchmark]
    public ValueTask ForEach_SyncCallback() =>
        _source.ForEachAsync(static _ => { });

    /// <summary>Drains the source through a no-capture async callback that completes synchronously.</summary>
    /// <returns>A <see cref="ValueTask"/> that completes when the drain finishes.</returns>
    [Benchmark]
    public ValueTask ForEach_AsyncCallback() =>
        _source.ForEachAsync(static (_, _) => default);
}
