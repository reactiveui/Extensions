// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Subscribe-and-dispose cost for the scheduler-driven sync operators that don't yield meaningful
/// per-emission measurements without real wall-clock time: <c>Heartbeat</c>, <c>ThrottleFirst</c>,
/// <c>Conflate</c>, and <c>While</c>. The benchmark exercises only the per-subscription wrapper
/// allocation; steady-state timing-driven emission isn't covered because scheduler noise dominates.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class TimedSyncOperatorSubscribeBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int SmallInvocationCount = 100;

    /// <summary>High end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int LargeInvocationCount = 1_000;

    /// <summary>Long-enough scheduler window so no emission fires during the bench's subscribe-and-dispose lifetime.</summary>
    private static readonly TimeSpan _longWindow = TimeSpan.FromSeconds(60);

    /// <summary>Static condition delegate for the <c>While</c> bench; always returns <see langword="false"/> so the loop body never fires.</summary>
    private static readonly Func<bool> _falseCondition = static () => false;

    /// <summary>Static no-op action for the <c>While</c> bench.</summary>
    private static readonly Action _noopAction = static () => { };

    /// <summary>Source feeding the timed pipelines.</summary>
    private readonly Subject<int> _source = new();

    /// <summary>Reusable sinks for the per-pipeline subscribes.</summary>
    private readonly NoopObserver<int> _intSink = new();

    /// <summary>Reusable sink for <c>Heartbeat&lt;Heartbeat&lt;int&gt;&gt;</c>.</summary>
    private readonly NoopObserver<Heartbeat<int>> _heartbeatSink = new();

    /// <summary>Reusable sink for <c>Unit</c>-emitting While.</summary>
    private readonly NoopObserver<Unit> _unitSink = new();

    /// <summary>Gets or sets the number of subscribe-and-dispose cycles per benchmark invocation.</summary>
    [Params(SmallInvocationCount, LargeInvocationCount)]
    public int InvocationCount { get; set; }

    /// <summary>Loops <c>Heartbeat</c> subscribe → dispose with a 60s scheduler window (no emission fires).</summary>
    [Benchmark]
    public void Heartbeat_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.Heartbeat(_longWindow, Scheduler.Default).Subscribe(_heartbeatSink);
        }
    }

    /// <summary>Loops <c>ThrottleFirst</c> subscribe → dispose with a 60s scheduler window.</summary>
    [Benchmark]
    public void ThrottleFirst_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.ThrottleFirst(_longWindow, Scheduler.Default).Subscribe(_intSink);
        }
    }

    /// <summary>Loops <c>Conflate</c> subscribe → dispose with a 60s scheduler window.</summary>
    [Benchmark]
    public void Conflate_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.Conflate(_longWindow, Scheduler.Default).Subscribe(_intSink);
        }
    }

    /// <summary>Loops <c>While</c> subscribe → dispose with a false condition (loop body never fires).</summary>
    [Benchmark]
    public void While_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = ReactiveExtensions.While(_falseCondition, _noopAction, Scheduler.Default).Subscribe(_unitSink);
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

        _source.Dispose();
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
