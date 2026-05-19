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
}
