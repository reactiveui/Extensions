// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the construct-subscribe-drain cost of the async bridges into <c>IObservableAsync</c>:
/// <c>ToObservableAsync(Task)</c>, <c>ToObservableAsync(Task&lt;T&gt;)</c>, and
/// <c>ToObservableAsync(IAsyncEnumerable&lt;T&gt;)</c>. The <c>IEnumerable</c> overload is already
/// covered by <see cref="FactoryObservableBenchmarks"/>; this class focuses on the genuinely-async
/// sources.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "BenchmarkDotNet drives benchmarks through an instance; the methods cannot be static.")]
public class AsyncBridgeBenchmarks
{
    /// <summary>Number of values produced by the async-enumerable source.</summary>
    private const int AsyncEnumerableLength = 100;

    /// <summary>Sentinel value emitted by the <c>Task&lt;T&gt;</c> bridge benchmark.</summary>
    private const int TaskValue = 42;

    /// <summary>Pre-completed <see cref="Task{TResult}"/> reused across iterations to isolate the bridge cost from the task allocation.</summary>
    private static readonly Task<int> CompletedValueTask = Task.FromResult(TaskValue);

    /// <summary>Pre-completed <see cref="Task"/> reused across iterations.</summary>
    private static readonly Task CompletedTask = Task.CompletedTask;

    /// <summary>Bridges a completed <see cref="Task{TResult}"/> into <c>IObservableAsync</c> and drains its single emission.</summary>
    /// <returns>The task's result value.</returns>
    [Benchmark]
    public ValueTask<int> ToObservable_FromCompletedTaskOfT() =>
        CompletedValueTask.ToObservableAsync().FirstAsync();

    /// <summary>Bridges a completed non-generic <see cref="Task"/> into <c>IObservableAsync</c> and drains its <see cref="Unit"/> emission.</summary>
    /// <returns>The unit-typed bridge emission.</returns>
    [Benchmark]
    public ValueTask<Unit> ToObservable_FromCompletedTask() =>
        CompletedTask.ToObservableAsync().FirstAsync();

    /// <summary>Bridges a 100-element async-enumerable into <c>IObservableAsync</c> and counts the emissions.</summary>
    /// <returns>The element count.</returns>
    [Benchmark]
    public ValueTask<int> ToObservable_FromAsyncEnumerable() =>
        BuildAsyncEnumerable().ToObservableAsync().CountAsync();

    /// <summary>Yields integers <c>0..AsyncEnumerableLength-1</c> for the async-enumerable bridge benchmark.</summary>
    /// <returns>The async sequence.</returns>
    private static async IAsyncEnumerable<int> BuildAsyncEnumerable()
    {
        for (var i = 0; i < AsyncEnumerableLength; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}
