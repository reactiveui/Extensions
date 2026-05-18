// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Tests.Operators;

/// <summary>Edge-case coverage for the reactive-condition <c>ObserveOnIf</c> overload
/// backed by <c>ObserveOnIfObservable&lt;T&gt;</c> — condition switching, error forwarding,
/// and completion forwarding.</summary>
public class ObserveOnIfObservableTests
{
    /// <summary>Synthetic error message attached to source errors.</summary>
    private const string SourceErrorMessage = "source error";

    /// <summary>Verifies that values dispatch on the false-scheduler before any condition arrives.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnIfNoCondition_ThenUsesFalseScheduler()
    {
        const int Value = 11;
        var source = new Subject<int>();
        var condition = new Subject<bool>();
        var trueScheduler = new RecordingScheduler();
        var falseScheduler = new RecordingScheduler();
        var emitted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = source.ObserveOnIf(condition, trueScheduler, falseScheduler)
            .Subscribe(v => emitted.TrySetResult(v));

        source.OnNext(Value);

        var v2 = await emitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(v2).IsEqualTo(Value);
        await Assert.That(falseScheduler.ScheduleCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(trueScheduler.ScheduleCount).IsEqualTo(0);
    }

    /// <summary>Verifies that emitting after the condition becomes true dispatches on the true-scheduler.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnIfConditionTrue_ThenUsesTrueScheduler()
    {
        const int Value = 22;
        var source = new Subject<int>();
        var condition = new Subject<bool>();
        var trueScheduler = new RecordingScheduler();
        var falseScheduler = new RecordingScheduler();
        var emitted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = source.ObserveOnIf(condition, trueScheduler, falseScheduler)
            .Subscribe(v => emitted.TrySetResult(v));

        condition.OnNext(true);
        source.OnNext(Value);

        var v2 = await emitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(v2).IsEqualTo(Value);
        await Assert.That(trueScheduler.ScheduleCount).IsGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies that <c>ObserveOnIf</c> forwards source errors without scheduler dispatch.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnIfSourceErrors_ThenForwardsError()
    {
        var source = new Subject<int>();
        var condition = new Subject<bool>();
        Exception? caught = null;
        var expected = new InvalidOperationException(SourceErrorMessage);

        using var sub = source.ObserveOnIf(condition, TaskPoolScheduler.Default, ImmediateScheduler.Instance)
            .Subscribe(static _ => { }, ex => caught = ex);

        source.OnError(expected);

        await Assert.That(caught).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies that <c>ObserveOnIf</c> forwards source completion.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnIfSourceCompletes_ThenForwardsCompletion()
    {
        var source = new Subject<int>();
        var condition = new Subject<bool>();
        var completed = false;

        using var sub = source.ObserveOnIf(condition, TaskPoolScheduler.Default, ImmediateScheduler.Instance)
            .Subscribe(static _ => { }, () => completed = true);

        source.OnCompleted();

        await Assert.That(completed).IsTrue();
    }

    /// <summary>Verifies that the single-scheduler overload defaults the false branch to
    /// <see cref="ImmediateScheduler"/> by emitting synchronously when the condition is false.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnIfSingleSchedulerConditionFalse_ThenImmediate()
    {
        const int Value = 33;
        var source = new Subject<int>();
        var condition = new Subject<bool>();
        var trueScheduler = new RecordingScheduler();
        var results = new List<int>();

        using var sub = source.ObserveOnIf(condition, trueScheduler)
            .Subscribe(results.Add);

        condition.OnNext(false);
        source.OnNext(Value);

        await Assert.That(results).IsCollectionEqualTo([Value]);
        await Assert.That(trueScheduler.ScheduleCount).IsEqualTo(0);
    }

    /// <summary>Scheduler that delegates to the default thread-pool scheduler but records
    /// each call to <see cref="IScheduler.Schedule{TState}(TState, Func{IScheduler, TState, IDisposable})"/>.</summary>
    private sealed class RecordingScheduler : IScheduler
    {
        /// <summary>Backing scheduler used to actually dispatch work.</summary>
        private readonly TaskPoolScheduler _inner = TaskPoolScheduler.Default;

        /// <summary>Gets the number of recorded schedule calls.</summary>
        public int ScheduleCount { get; private set; }

        /// <inheritdoc/>
        public DateTimeOffset Now => _inner.Now;

        /// <inheritdoc/>
        public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
        {
            ScheduleCount++;
            return _inner.Schedule(state, action);
        }

        /// <inheritdoc/>
        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            ScheduleCount++;
            return _inner.Schedule(state, dueTime, action);
        }

        /// <inheritdoc/>
        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            ScheduleCount++;
            return _inner.Schedule(state, dueTime, action);
        }
    }
}
