// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Per-cycle cost of the two sequential-state-machine sync operators that hadn't been benchmarked:
/// <see cref="ReactiveExtensions.FirstMatchFromCandidates{TKey, TRaw, TResult}"/> (walks an
/// ordered candidate list and emits the first match) and
/// <see cref="ReactiveExtensions.WithLimitedConcurrency{T}"/> (caps concurrent task execution).
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SequentialMatchAndConcurrencyBenchmarks
{
    /// <summary>Low end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int SmallInvocationCount = 100;

    /// <summary>High end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int LargeInvocationCount = 1_000;

    /// <summary>Fallback result emitted if no candidate matches.</summary>
    private const int Fallback = -1;

    /// <summary>Concurrency cap for the <c>WithLimitedConcurrency</c> bench.</summary>
    private const int MaxConcurrency = 4;

    /// <summary>Size of the cached task set fed into the concurrency-limited bench.</summary>
    private const int TaskPoolSize = 8;

    /// <summary>Candidate keys walked by the <c>FirstMatchFromCandidates</c> bench.</summary>
    private static readonly int[] _candidates = [1, 2, 3, 4];

    /// <summary>Static projection from key to a single-value observable; matches on the first key.</summary>
    private static readonly Func<int, IObservable<int>> _project = static key => Observables.Return(key);

    /// <summary>Static transform applied to each raw value.</summary>
    private static readonly Func<int, int> _transform = static raw => raw;

    /// <summary>Static predicate that matches the first candidate, so the operator terminates eagerly.</summary>
    private static readonly Func<int, bool> _predicate = static value => value == 1;

    /// <summary>Reusable no-op sink so allocation tracking attributes only to the operator paths.</summary>
    private readonly NoopObserver<int> _sink = new();

    /// <summary>Pre-completed tasks pumped into the <c>WithLimitedConcurrency</c> bench.</summary>
    private Task<int>[] _completedTasks = null!;

    /// <summary>Gets or sets the number of invocations per benchmark iteration.</summary>
    [Params(SmallInvocationCount, LargeInvocationCount)]
    public int InvocationCount { get; set; }

    /// <summary>Builds the cached task set used by the concurrency-limited bench.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _completedTasks = new Task<int>[TaskPoolSize];
        for (var i = 0; i < _completedTasks.Length; i++)
        {
            _completedTasks[i] = Task.FromResult(i);
        }
    }

    /// <summary>Loops <c>FirstMatchFromCandidates</c> subscribe-and-drain cycles (matches on first candidate).</summary>
    [Benchmark]
    public void FirstMatchFromCandidates_FirstHit()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _candidates.FirstMatchFromCandidates(_project, _transform, _predicate, Fallback)
                .Subscribe(_sink);
        }
    }

    /// <summary>Loops <c>WithLimitedConcurrency</c> subscribe cycles over a pre-built task set.</summary>
    [Benchmark]
    public void WithLimitedConcurrency_PreCompletedTasks()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _completedTasks.WithLimitedConcurrency(MaxConcurrency).Subscribe(_sink);
        }
    }

    /// <summary>No-op observer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class NoopObserver<T> : IObserver<T>
    {
        /// <inheritdoc/>
        public void OnNext(T value)
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
