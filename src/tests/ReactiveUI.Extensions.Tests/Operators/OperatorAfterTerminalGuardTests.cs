// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Reactive.Testing;

namespace ReactiveUI.Extensions.Tests.Operators;

/// <summary>Covers the consistent <c>if (_done) return;</c> after-terminal guards on the
/// remaining sync operators that share the pattern but lacked dedicated coverage —
/// <c>RetryWithDelay</c>, <c>OnErrorRetry</c>, <c>TakeUntilInclusive</c>, <c>SwitchIfEmpty</c>,
/// <c>ThrottleOnScheduler</c>, <c>BufferUntilIdle</c>, <c>ObserveOnIf</c>. Each test drives a
/// <see cref="SyncDirectSource{T}"/> through one terminal event, then pushes additional
/// notifications past the terminal to verify the guard silently drops them.</summary>
public class OperatorAfterTerminalGuardTests
{
    /// <summary>Settle window used to let scheduler-marshalled tests fire any racing emission.</summary>
    private const int SettleDelayMilliseconds = 50;

    /// <summary>Tick window for fast-scheduler tests.</summary>
    private const int TickWindow = 100;

    /// <summary>Multiplier used to advance past the tick window in settle assertions.</summary>
    private const int SettleMultiplier = 2;

    /// <summary>Second sentinel value used in after-terminal pushes.</summary>
    private const int SecondValue = 2;

    /// <summary>Verifies <c>OnErrorRetry</c>'s sink silently drops events after a downstream
    /// completion has set the <c>_disposed</c> latch.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryForeverEventsAfterDispose_ThenDropped()
    {
        var source = new SyncDirectSource<int>();
        var values = new List<int>();
        var completed = false;

        var sub = source.OnErrorRetry().Subscribe(values.Add, () => completed = true);
        source.Observer.OnCompleted();

        // Dispose latches _disposed in the retry sink.
        sub.Dispose();
        source.Observer.OnNext(1);
        source.Observer.OnError(new InvalidOperationException("late"));

        await Assert.That(completed).IsTrue();
        await Assert.That(values).IsEmpty();
    }

    /// <summary>Verifies that <c>RetryWithDelay</c>'s sink silently drops a source error
    /// arriving after dispose — exercises the <c>if (_disposed) return;</c> guard in OnError.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithDelaySourceErrorAfterDispose_ThenDropped()
    {
        var source = new SyncDirectSource<int>();
        Exception? caught = null;

        var sub = source.RetryForeverWithDelay(TimeSpan.FromMilliseconds(SettleDelayMilliseconds))
            .Subscribe(static _ => { }, ex => caught = ex);

        sub.Dispose();
        source.Observer.OnError(new InvalidOperationException("after-dispose"));

        // The sink's _disposed guard short-circuits, so the downstream onError handler is not invoked
        // (no retry, no terminal forwarded).
        await Assert.That(caught).IsNull();
    }

    /// <summary>Verifies <c>TakeUntilInclusive</c>'s after-terminal sink guard.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilInclusiveEventsAfterTerminated_ThenDropped()
    {
        var source = new SyncDirectSource<int>();
        var values = new List<int>();
        var completedCount = 0;

        using var sub = source.TakeUntil(static x => x > 0)
            .Subscribe(values.Add, () => completedCount++);

        // Predicate triggers on the first positive value, sets _done.
        source.Observer.OnNext(1);
        source.Observer.OnNext(SecondValue);
        source.Observer.OnError(new InvalidOperationException("late"));
        source.Observer.OnCompleted();

        await Assert.That(completedCount).IsEqualTo(1);
        await Assert.That(values).IsCollectionEqualTo([1]);
    }

    /// <summary>Verifies <c>SwitchIfEmpty</c>'s after-terminal sink guard.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchIfEmptyEventsAfterTerminated_ThenDropped()
    {
        var source = new SyncDirectSource<int>();
        var fallback = new Subject<int>();
        var values = new List<int>();
        var completedCount = 0;

        using var sub = source.SwitchIfEmpty(fallback)
            .Subscribe(values.Add, () => completedCount++);

        source.Observer.OnNext(1);
        source.Observer.OnCompleted();
        source.Observer.OnNext(SecondValue);
        source.Observer.OnError(new InvalidOperationException("late"));
        source.Observer.OnCompleted();

        await Assert.That(completedCount).IsEqualTo(1);
        await Assert.That(values).IsCollectionEqualTo([1]);
    }

    /// <summary>Verifies <c>ThrottleOnScheduler</c>'s post-completion sink guard.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleOnSchedulerEventsAfterCompleted_ThenDropped()
    {
        var scheduler = new TestScheduler();
        var source = new SyncDirectSource<int>();
        var values = new List<int>();
        var completedCount = 0;

        using var sub = source.ThrottleOnScheduler(TimeSpan.FromTicks(TickWindow), scheduler)
            .Subscribe(values.Add, () => completedCount++);

        source.Observer.OnCompleted();
        source.Observer.OnNext(1);
        source.Observer.OnError(new InvalidOperationException("late"));
        source.Observer.OnCompleted();
        scheduler.AdvanceBy(TickWindow * SettleMultiplier);

        await Assert.That(completedCount).IsEqualTo(1);
        await Assert.That(values).IsEmpty();
    }

    /// <summary>Verifies <c>DetectStale</c>'s post-completion <c>OnNext</c> guard — values
    /// arriving after the upstream completed are dropped at the <c>_state.Done</c> check.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDetectStaleEventsAfterCompleted_ThenDropped()
    {
        var scheduler = new TestScheduler();
        var source = new SyncDirectSource<int>();
        var values = new List<Stale<int>>();
        var completedCount = 0;

        using var sub = source.DetectStale(TimeSpan.FromTicks(TickWindow), scheduler)
            .Subscribe(values.Add, () => completedCount++);

        source.Observer.OnCompleted();
        source.Observer.OnNext(1);
        scheduler.AdvanceBy(TickWindow * SettleMultiplier);

        await Assert.That(completedCount).IsEqualTo(1);
        await Assert.That(values).IsEmpty();
    }

    /// <summary>Verifies <c>DropIfBusy</c>'s post-completion <c>OnNext</c> guard.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDropIfBusyEventsAfterCompleted_ThenDropped()
    {
        var source = new SyncDirectSource<int>();
        var values = new List<int>();
        var completedCount = 0;

        using var sub = source.DropIfBusy(static _ => Task.CompletedTask)
            .Subscribe(values.Add, () => completedCount++);

        source.Observer.OnCompleted();
        source.Observer.OnNext(1);
        source.Observer.OnError(new InvalidOperationException("late"));

        await Assert.That(completedCount).IsEqualTo(1);
        await Assert.That(values).IsEmpty();
    }

    /// <summary>Verifies <c>SampleLatest</c>'s post-completion <c>Sample</c> guard.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSampleLatestSampledAfterCompleted_ThenNoOp()
    {
        var source = new SyncDirectSource<int>();
        var sampler = new Subject<object>();
        var values = new List<int>();
        var completedCount = 0;

        using var sub = source.SampleLatest(sampler)
            .Subscribe(values.Add, () => completedCount++);

        source.Observer.OnNext(1);
        source.Observer.OnCompleted();
        sampler.OnNext(new object());

        await Assert.That(completedCount).IsEqualTo(1);
    }

    /// <summary>Verifies <c>Heartbeat</c>'s post-completion <c>OnNext</c> guard.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenHeartbeatEventsAfterCompleted_ThenDropped()
    {
        var scheduler = new TestScheduler();
        var source = new SyncDirectSource<int>();
        var completedCount = 0;

        using var sub = source.Heartbeat(TimeSpan.FromTicks(TickWindow), scheduler)
            .Subscribe(static _ => { }, () => completedCount++);

        source.Observer.OnCompleted();
        source.Observer.OnNext(1);
        scheduler.AdvanceBy(TickWindow * SettleMultiplier);

        await Assert.That(completedCount).IsEqualTo(1);
    }

    /// <summary>Verifies <c>DebounceUntil</c>'s post-completion sink guard — values arriving
    /// after the upstream has already completed are dropped at the <c>_state.Done</c> check
    /// inside <c>OnNext</c>.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDebounceUntilEventsAfterCompleted_ThenDropped()
    {
        var scheduler = new TestScheduler();
        var source = new SyncDirectSource<int>();
        var values = new List<int>();
        var completedCount = 0;

        using var sub = source.DebounceUntil(TimeSpan.FromTicks(TickWindow), static _ => true, scheduler)
            .Subscribe(values.Add, () => completedCount++);

        source.Observer.OnCompleted();
        source.Observer.OnNext(1);
        source.Observer.OnError(new InvalidOperationException("late"));
        scheduler.AdvanceBy(TickWindow * SettleMultiplier);

        await Assert.That(completedCount).IsEqualTo(1);
        await Assert.That(values).IsEmpty();
    }

    /// <summary>Verifies <c>BufferUntilIdle</c>'s post-completion sink guard.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBufferUntilIdleEventsAfterCompleted_ThenDropped()
    {
        var scheduler = new TestScheduler();
        var source = new SyncDirectSource<int>();
        var batches = new List<IList<int>>();
        var completedCount = 0;

        using var sub = source.BufferUntilIdle(TimeSpan.FromTicks(TickWindow), scheduler)
            .Subscribe(batches.Add, () => completedCount++);

        source.Observer.OnCompleted();
        source.Observer.OnNext(1);
        source.Observer.OnError(new InvalidOperationException("late"));
        scheduler.AdvanceBy(TickWindow * SettleMultiplier);

        await Assert.That(completedCount).IsEqualTo(1);
    }

    /// <summary>Verifies <c>ObserveOnIf</c>'s post-completion sink guard on the condition observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnIfConditionEventsAfterCompleted_ThenDropped()
    {
        var source = new Subject<int>();
        var condition = new SyncDirectSource<bool>();
        var trueScheduler = ImmediateScheduler.Instance;
        var falseScheduler = ImmediateScheduler.Instance;
        var values = new List<int>();
        var completedCount = 0;

        using var sub = source.ObserveOnIf(condition, trueScheduler, falseScheduler)
            .Subscribe(values.Add, () => completedCount++);

        // Drive the condition observer terminal, then push more events to hit the after-terminal guard.
        condition.Observer.OnCompleted();
        condition.Observer.OnNext(true);
        condition.Observer.OnError(new InvalidOperationException("late"));
        condition.Observer.OnCompleted();

        // Source still works because the operator multicasts via condition.
        source.OnNext(1);
        source.OnCompleted();

        await Assert.That(completedCount).IsEqualTo(1);
    }

    /// <summary>Verifies <c>RetryWithBackoff</c>'s sink silently drops a source error after dispose.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithBackoffSourceErrorAfterDispose_ThenDropped()
    {
        var source = new SyncDirectSource<int>();
        Exception? caught = null;

        var sub = source.RetryWithBackoff(maxRetries: 1, TimeSpan.FromMilliseconds(SettleDelayMilliseconds))
            .Subscribe(static _ => { }, ex => caught = ex);

        sub.Dispose();
        source.Observer.OnError(new InvalidOperationException("after-dispose"));

        await Assert.That(caught).IsNull();
    }

    /// <summary>Verifies <c>WhileObservable</c>'s after-dispose guard.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWhileDisposedTwice_ThenSecondIsNoOp()
    {
        var ran = 0;
        var condition = true;
        var sub = ReactiveExtensions.While(
                () =>
                {
                    if (!condition)
                    {
                        return false;
                    }

                    condition = false;
                    return true;
                },
                () => Interlocked.Increment(ref ran))
            .Subscribe(static _ => { });

        sub.Dispose();
        sub.Dispose();

        await Assert.That(ran).IsEqualTo(1);
    }

    /// <summary>Verifies <c>ScheduledSource</c>'s emit catch — when the side-effect action throws,
    /// the exception is forwarded as <c>OnError</c> on the downstream observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScheduledSourceActionThrows_ThenForwardsError()
    {
        var scheduler = new TestScheduler();
        var source = new Subject<int>();
        var expected = new InvalidOperationException("action-failed");
        Exception? caught = null;

        using var sub = source.Schedule(TimeSpan.FromTicks(TickWindow), scheduler, _ => throw expected)
            .Subscribe(static _ => { }, ex => caught = ex);

        source.OnNext(1);
        scheduler.AdvanceBy(TickWindow * SettleMultiplier);

        await Assert.That(caught).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies the <c>SubscribeSynchronous</c> sink's null-callback branches —
    /// omitting <c>onError</c> and <c>onCompleted</c> covers the null-coalescing fast paths.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeSynchronousOmitsErrorAndCompletedCallbacks_ThenNullPathsTaken()
    {
        var subject = new Subject<int>();
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = subject.SubscribeSynchronous<int>(_ =>
        {
            processed.TrySetResult();
            return Task.CompletedTask;
        });

        subject.OnNext(1);
        await processed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Subject silently terminates without invoking the optional callbacks.
        subject.OnError(new InvalidOperationException("ignored"));

        var second = new Subject<int>();
        using var sub2 = second.SubscribeSynchronous<int>(static _ => Task.CompletedTask);
        second.OnCompleted();
    }
}
