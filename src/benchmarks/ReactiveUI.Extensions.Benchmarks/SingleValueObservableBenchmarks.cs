// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Subscribe + emit + complete cost of <see cref="SingleValueObservable{T}"/>. Each iteration
/// produces one full subscribe-then-terminate cycle.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SingleValueObservableBenchmarks
{
    /// <summary>Low end of the <see cref="SubscribeCount"/> parameter sweep.</summary>
    private const int SmallSubscribeCount = 1_000;

    /// <summary>High end of the <see cref="SubscribeCount"/> parameter sweep.</summary>
    private const int LargeSubscribeCount = 10_000;

    /// <summary>Shared observable instance — the typical usage pattern caches it.</summary>
    private readonly SingleValueObservable<int> _observable = new(42);

    /// <summary>Shared sink used for every subscribe.</summary>
    private readonly NoopObserver _sink = new();

    /// <summary>Gets or sets the number of subscribe + emit + complete cycles per benchmark invocation.</summary>
    [Params(SmallSubscribeCount, LargeSubscribeCount)]
    public int SubscribeCount { get; set; }

    /// <summary>Subscribes the shared sink repeatedly; each subscribe emits 42 and completes synchronously.</summary>
    [Benchmark]
    public void Subscribe_EmitsValueAndCompletes()
    {
        for (var i = 0; i < SubscribeCount; i++)
        {
            _observable.Subscribe(_sink);
        }
    }

    /// <summary>No-op observer used as the terminal sink.</summary>
    private sealed class NoopObserver : IObserver<int>
    {
        /// <inheritdoc/>
        public void OnNext(int value)
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }
    }
}
