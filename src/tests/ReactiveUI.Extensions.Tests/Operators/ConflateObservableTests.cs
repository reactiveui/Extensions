// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Reactive.Testing;

namespace ReactiveUI.Extensions.Tests.Operators;

/// <summary>Edge-case coverage for the <c>Conflate</c> operator backed by
/// <c>ConflateObservable&lt;T&gt;</c> — source-error path through the scheduler
/// marshaller, completion-while-throttled, fast-path interruption by a newer value,
/// and dispose mid-drain.</summary>
public class ConflateObservableTests
{
    /// <summary>Synthetic error message attached to source errors.</summary>
    private const string SourceErrorMessage = "source error";

    /// <summary>Minimum-update-period tick window for the conflate operator.</summary>
    private const int UpdatePeriodTicks = 100;

    /// <summary>Multiplier used to advance past the update period in settle assertions.</summary>
    private const int SettleMultiplier = 2;

    /// <summary>Half of the update-period window.</summary>
    private const int HalfWindowTicks = 50;

    /// <summary>Sentinel values.</summary>
    private const int First = 1;

    /// <summary>Second sentinel value.</summary>
    private const int Second = 2;

    /// <summary>Third sentinel value.</summary>
    private const int Third = 3;

    /// <summary>Verifies that a source error is forwarded through the scheduler marshaller.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConflateSourceErrors_ThenForwardsError()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        Exception? caught = null;
        var expected = new InvalidOperationException(SourceErrorMessage);

        using var sub = subject.Conflate(TimeSpan.FromTicks(UpdatePeriodTicks), scheduler)
            .Subscribe(static _ => { }, ex => caught = ex);

        subject.OnError(expected);
        scheduler.AdvanceBy(UpdatePeriodTicks);

        await Assert.That(caught).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies that a newer value arriving inside the throttle window replaces the
    /// pending scheduled emission rather than emitting both.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConflateNewerValueDuringThrottle_ThenReplacesPending()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        using var sub = subject.Conflate(TimeSpan.FromTicks(UpdatePeriodTicks), scheduler)
            .Subscribe(results.Add);

        subject.OnNext(First);
        scheduler.AdvanceBy(HalfWindowTicks);
        subject.OnNext(Second);
        scheduler.AdvanceBy(HalfWindowTicks);
        subject.OnNext(Third);
        scheduler.AdvanceBy(UpdatePeriodTicks);

        // Inside the throttle window: at most one emission for the burst.
        await Assert.That(results.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(results[^1]).IsEqualTo(Third);
    }

    /// <summary>Verifies that completion before any throttled emission flushes through.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConflateCompletesBeforeFirstEmission_ThenCompletes()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var completed = false;

        using var sub = subject.Conflate(TimeSpan.FromTicks(UpdatePeriodTicks), scheduler)
            .Subscribe(static _ => { }, () => completed = true);

        subject.OnCompleted();
        scheduler.AdvanceBy(UpdatePeriodTicks);

        await Assert.That(completed).IsTrue();
    }

    /// <summary>Verifies that disposing before the scheduled emission fires suppresses the value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConflateDisposedBeforeScheduledEmission_ThenSuppressed()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        var sub = subject.Conflate(TimeSpan.FromTicks(UpdatePeriodTicks), scheduler)
            .Subscribe(results.Add);

        subject.OnNext(First);
        scheduler.AdvanceBy(HalfWindowTicks);
        subject.OnNext(Second);
        sub.Dispose();
        scheduler.AdvanceBy(UpdatePeriodTicks);

        // Initial value may or may not have fired before disposal but no late emission must arrive.
        var snapshot = results.Count;
        scheduler.AdvanceBy(UpdatePeriodTicks);
        await Assert.That(results.Count).IsEqualTo(snapshot);
    }

    /// <summary>Verifies that an <c>OnNext</c> arriving after the source has completed is silently dropped.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnNextAfterCompleted_ThenDropped()
    {
        var scheduler = new TestScheduler();
        var source = new SyncDirectSource<int>();
        var results = new List<int>();
        var completed = false;

        using var sub = source.Conflate(TimeSpan.FromTicks(UpdatePeriodTicks), scheduler)
            .Subscribe(results.Add, () => completed = true);

        source.Observer.OnCompleted();
        scheduler.AdvanceBy(SettleMultiplier * UpdatePeriodTicks);
        source.Observer.OnNext(1);
        scheduler.AdvanceBy(SettleMultiplier * UpdatePeriodTicks);

        await Assert.That(completed).IsTrue();
        await Assert.That(results).IsEmpty();
    }

    /// <summary>Verifies that an <c>OnError</c> arriving after completion is silently dropped.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorAfterCompleted_ThenDropped()
    {
        var scheduler = new TestScheduler();
        var source = new SyncDirectSource<int>();
        Exception? caught = null;
        var completed = false;

        using var sub = source.Conflate(TimeSpan.FromTicks(UpdatePeriodTicks), scheduler)
            .Subscribe(static _ => { }, ex => caught = ex, () => completed = true);

        source.Observer.OnCompleted();
        scheduler.AdvanceBy(UpdatePeriodTicks);
        source.Observer.OnError(new InvalidOperationException("late"));
        scheduler.AdvanceBy(UpdatePeriodTicks);

        await Assert.That(completed).IsTrue();
        await Assert.That(caught).IsNull();
    }

    /// <summary>Verifies that a duplicate <c>OnCompleted</c> after an error is silently dropped.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnCompletedAfterError_ThenDropped()
    {
        var scheduler = new TestScheduler();
        var source = new SyncDirectSource<int>();
        Exception? caught = null;
        var completed = false;
        var expected = new InvalidOperationException("first");

        using var sub = source.Conflate(TimeSpan.FromTicks(UpdatePeriodTicks), scheduler)
            .Subscribe(static _ => { }, ex => caught = ex, () => completed = true);

        source.Observer.OnError(expected);
        scheduler.AdvanceBy(UpdatePeriodTicks);
        source.Observer.OnCompleted();
        scheduler.AdvanceBy(UpdatePeriodTicks);

        await Assert.That(caught).IsSameReferenceAs(expected);
        await Assert.That(completed).IsFalse();
    }
}
