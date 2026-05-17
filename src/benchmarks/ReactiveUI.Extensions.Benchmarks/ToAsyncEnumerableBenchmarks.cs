// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the subscribe-and-drain cost of <c>ToAsyncEnumerable</c> on a finite source. Bridges
/// the observable side into <see cref="IAsyncEnumerable{T}"/> via a buffered <see cref="Channel{T}"/>;
/// the benchmark body enumerates every element.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ToAsyncEnumerableBenchmarks
{
    /// <summary>Low end of the <see cref="ElementCount"/> parameter sweep.</summary>
    private const int SmallCount = 100;

    /// <summary>High end of the <see cref="ElementCount"/> parameter sweep.</summary>
    private const int LargeCount = 1_000;

    /// <summary>Pre-built source emitting <see cref="ElementCount"/> integers then completing.</summary>
    private IObservableAsync<int> _source = null!;

    /// <summary>Gets or sets the number of values the source emits before completing.</summary>
    [Params(SmallCount, LargeCount)]
    public int ElementCount { get; set; }

    /// <summary>Builds the finite source.</summary>
    [GlobalSetup]
    public void Setup() => _source = ObservableAsync.Range(0, ElementCount);

    /// <summary>Drains the observable through a bounded async-enumerable channel and counts the elements consumed.</summary>
    /// <returns>The number of elements yielded by the async enumerable.</returns>
    [Benchmark]
    public async ValueTask<int> ToAsyncEnumerable_BoundedChannel()
    {
        var count = 0;
        await foreach (var element in _source.ToAsyncEnumerable(static () => Channel.CreateUnbounded<int>()).ConfigureAwait(false))
        {
            _ = element;
            count++;
        }

        return count;
    }
}
