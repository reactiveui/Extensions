// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-emission cost of <c>SubscribeSynchronous</c> with the
/// <c>Func&lt;T, ValueTask&gt;</c> handler surface. Two variants:
/// <list type="bullet">
///   <item><c>SubscribeSynchronous_TrivialHandler</c> — handler returns <c>default</c> (no async machinery).</item>
///   <item><c>SubscribeSynchronous_RealisticAsyncHandler</c> — handler is <c>async _ =&gt; await cached</c>;
///   the awaited task is pre-completed, so the path exercises the async builder's sync-completion
///   fast path end to end.</item>
/// </list>
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SubscribeSynchronousBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 100;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 1_000;

    /// <summary>Pre-completed task awaited by the realistic-async-handler variant.</summary>
    private static readonly Task<int> _completedResult = Task.FromResult(0);

    /// <summary>Source feeding the trivial (no-await) pipeline.</summary>
    private readonly Subject<int> _trivialSource = new();

    /// <summary>Source feeding the realistic async-handler pipeline.</summary>
    private readonly Subject<int> _realisticSource = new();

    /// <summary>Source feeding the Task-bridged-handler comparison pipeline.</summary>
    private readonly Subject<int> _taskBridgedSource = new();

    /// <summary>Subscription on the trivial pipeline.</summary>
    private IDisposable _trivialSubscription = null!;

    /// <summary>Subscription on the realistic-async-handler pipeline.</summary>
    private IDisposable _realisticSubscription = null!;

    /// <summary>Subscription on the Task-bridged-handler pipeline.</summary>
    private IDisposable _taskBridgedSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires both pipelines.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _trivialSubscription = _trivialSource.SubscribeSynchronous(static _ => default);
        _realisticSubscription = _realisticSource.SubscribeSynchronous(static async value =>
        {
            await _completedResult.ConfigureAwait(false);
            BlackHole(value);
        });
        _taskBridgedSubscription = _taskBridgedSource.SubscribeSynchronous(static value => new ValueTask(TaskHelper(value)));
    }

    /// <summary>Tears every pipeline down.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _trivialSubscription.Dispose();
        _realisticSubscription.Dispose();
        _taskBridgedSubscription.Dispose();
        _trivialSource.Dispose();
        _realisticSource.Dispose();
        _taskBridgedSource.Dispose();
    }

    /// <summary>Trivial handler (<c>_ =&gt; default</c>) — measures the operator's per-emission overhead.</summary>
    [Benchmark]
    public void SubscribeSynchronous_TrivialHandler()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _trivialSource.OnNext(i);
        }
    }

    /// <summary>Realistic <c>async _ =&gt; await ...</c> handler that sync-completes against a cached
    /// completed task — measures the operator + async-state-machine cost end to end.</summary>
    [Benchmark]
    public void SubscribeSynchronous_RealisticAsyncHandler()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _realisticSource.OnNext(i);
        }
    }

    /// <summary>Comparison benchmark: handler bridges from a <see cref="Task"/>-returning helper
    /// method via <c>new ValueTask(helper(x))</c>. The helper is <c>async Task</c>; the value-task
    /// wrap is a struct constructor (free). Proves Task-vs-ValueTask handler perf is neutral —
    /// the async state machine box dominates in both shapes.</summary>
    [Benchmark]
    public void SubscribeSynchronous_TaskBridgedHandler()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _taskBridgedSource.OnNext(i);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Drains synchronous teardown.</summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Cleanup();
    }

    /// <summary>Cached <see cref="Task"/>-returning helper used by <see cref="SubscribeSynchronous_TaskBridgedHandler"/>.</summary>
    /// <param name="value">The emitted value (ignored — the helper exercises the await machinery only).</param>
    /// <returns>A pre-completed task.</returns>
    private static async Task TaskHelper(int value)
    {
        _ = value;
        await _completedResult.ConfigureAwait(false);
    }

    /// <summary>Prevents the JIT from elide-ing the awaited value.</summary>
    /// <param name="value">The value to consume.</param>
    private static void BlackHole(int value) => _ = value;
}
