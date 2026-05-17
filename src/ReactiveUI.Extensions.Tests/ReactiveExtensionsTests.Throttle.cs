// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Reactive.Testing;
using ReactiveUI.Extensions.Tests.Async;

namespace ReactiveUI.Extensions.Tests;

/// <summary>Tests for ReactiveExtensionsTests.</summary>
public partial class ReactiveExtensionsTests
{
    /// <summary>
    /// Tests DebounceImmediate emits first immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task DebounceImmediate_EmitsFirstImmediately()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();
        using var sub = subject.DebounceImmediate(TimeSpan.FromTicks(100), scheduler).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(SampleValue2);
        scheduler.AdvanceBy(SchedulerAdvancePastWindowTicks);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results[0]).IsEqualTo(1);
    }

    /// <summary>
    /// Tests ThrottleFirst emits first immediately, then ignores subsequent values within the throttle window.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ThrottleFirst_EmitsFirstImmediately_IgnoresSubsequentWithinWindow()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        // Throttle window of 100 ms
        subject.ThrottleFirst(TimeSpan.FromMilliseconds(100))
            .Subscribe(results.Add);

        subject.OnNext(1); // Should be emitted immediately
        subject.OnNext(SampleValue2); // Should be ignored (within throttle window)
        subject.OnNext(SampleValue3); // Should be ignored (within throttle window)
        await Task.Delay(ThrottleWaitMilliseconds); // Wait for throttle window to pass
        subject.OnNext(SampleValue4); // Should be emitted

        // Verify results
        await Assert.That(results).IsCollectionEqualTo([1, SampleValue4]);
    }

    /// <summary>
    /// Tests DropIfBusy drops values when busy.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DropIfBusy_DropsWhenBusy()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        var tcs = new TaskCompletionSource<object>();

        subject.DropIfBusy(async x =>
        {
            await tcs.Task;
            results.Add(x);
        }).Subscribe();

        subject.OnNext(1); // Should process
        subject.OnNext(SampleValue2); // Should drop
        subject.OnNext(SampleValue3); // Should drop

        tcs.SetResult(new object()); // Complete the async action

        await Task.Delay(SampleValue10); // Small delay to allow processing

        await Assert.That(results).IsCollectionEqualTo([1]);
    }

    /// <summary>
    /// Tests ThrottleDistinct throttles distinct values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ThrottleDistinct_ThrottlesDistinct()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.ThrottleDistinct(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(1); // Duplicate, ignored
        subject.OnNext(SampleValue2);
        scheduler.AdvanceBy(SchedulerAdvancePastWindowTicks);
        subject.OnNext(SampleValue2); // Duplicate after throttle

        await Assert.That(results).IsCollectionEqualTo([SampleValue2]);
    }

    /// <summary>
    /// Tests DebounceUntil emits immediately when condition true, delays when false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task DebounceUntil_EmitsImmediatelyWhenConditionTrue_DelaysWhenFalse()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.DebounceUntil(TimeSpan.FromTicks(100), x => x % SampleValue2 == 0, scheduler)
            .Subscribe(results.Add);

        subject.OnNext(1); // Odd, should be delayed
        scheduler.AdvanceBy(SchedulerHalfWindowTicks); // Advance less than debounce period
        subject.OnNext(SampleValue2); // Even, should emit immediately, cancelling delayed 1
        scheduler.AdvanceBy(SchedulerWindowTicks); // Advance past debounce period

        await Assert.That(results).IsCollectionEqualTo([SampleValue2]);
    }

    /// <summary>
    /// Tests ThrottleOnScheduler throttles on the specified scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleOnScheduler_ThenThrottlesOnScheduler()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.ThrottleOnScheduler(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(SampleValue2);
        scheduler.AdvanceBy(SchedulerAdvancePastWindowTicks);

        await Assert.That(results).IsCollectionEqualTo([SampleValue2]);
    }

    /// <summary>
    /// Tests ThrottleDistinct with scheduler throttles and deduplicates.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleDistinctWithScheduler_ThenThrottlesAndDeduplicates()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.ThrottleDistinct(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(1); // Duplicate, suppressed by DistinctUntilChanged
        subject.OnNext(SampleValue2);
        scheduler.AdvanceBy(SchedulerAdvancePastWindowTicks);

        await Assert.That(results).IsCollectionEqualTo([SampleValue2]);
    }

    /// <summary>
    /// Tests DebounceImmediate flushes pending value when source errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDebounceImmediateSourceErrors_ThenFlushesAndForwardsError()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();
        Exception? observedError = null;

        subject.DebounceImmediate(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add, ex => observedError = ex);

        subject.OnNext(1); // Emitted immediately (first)
        subject.OnNext(SampleValue2); // Buffered as pending
        subject.OnError(new InvalidOperationException("test"));

        using (Assert.Multiple())
        {
            await Assert.That(results).IsCollectionEqualTo([1, SampleValue2]);
            await Assert.That(observedError).IsNotNull();
        }
    }

    /// <summary>
    /// Tests DebounceImmediate flushes pending value when source completes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDebounceImmediateSourceCompletes_ThenFlushesAndCompletes()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();
        var completed = false;

        subject.DebounceImmediate(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add, () => completed = true);

        subject.OnNext(1); // Emitted immediately (first)
        subject.OnNext(SampleValue2); // Buffered as pending
        subject.OnCompleted();

        using (Assert.Multiple())
        {
            await Assert.That(results).IsCollectionEqualTo([1, SampleValue2]);
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Tests DebounceUntil with scheduler delays non-matching values using the scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDebounceUntilWithScheduler_ThenUsesSchedulerForDelay()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.DebounceUntil(TimeSpan.FromTicks(100), x => x % SampleValue2 == 0, scheduler)
            .Subscribe(results.Add);

        subject.OnNext(SampleValue2); // Even, emits immediately
        subject.OnNext(1); // Odd, delayed
        scheduler.AdvanceBy(SchedulerAdvancePastWindowTicks);

        await Assert.That(results).IsCollectionEqualTo([SampleValue2, 1]);
    }

    /// <summary>
    /// Tests ThrottleUntilTrue with predicate false path applies throttle.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleUntilTruePredicateFalse_ThenAppliesThrottle()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        using var sub = subject
            .ThrottleUntilTrue(TimeSpan.FromMilliseconds(100), x => x > 5)
            .Subscribe(results.Add);

        // Predicate true: immediate
        subject.OnNext(SampleValue10);
        await Task.Delay(SchedulerHalfWindowTicks);

        // Predicate false: throttled
        subject.OnNext(1);
        await Task.Delay(ThrottleWaitMilliseconds);

        await Assert.That(results).Contains(SampleValue10);
        await Assert.That(results).Contains(1);
    }

    /// <summary>
    /// Tests ThrottleDistinct without scheduler parameter.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleDistinctWithoutScheduler_ThenThrottlesAndDeduplicates()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        using var sub = subject
            .ThrottleDistinct(TimeSpan.FromMilliseconds(200))
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(1);
        subject.OnNext(SampleValue2);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Contains(SampleValue2),
            TimeSpan.FromSeconds(30));

        await Assert.That(results).Contains(SampleValue2);
    }

    /// <summary>
    /// Tests DebounceUntil with scheduler delays non-matching values and passes matching immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDebounceUntilWithScheduler_ThenUsesScheduler()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.DebounceUntil(TimeSpan.FromTicks(100), x => x % SampleValue2 == 0, scheduler)
            .Subscribe(results.Add);

        subject.OnNext(SampleValue2); // condition true -> immediate
        subject.OnNext(SampleValue3); // condition false -> delayed
        scheduler.AdvanceBy(SchedulerWindowTicks);

        await Assert.That(results).Contains(SampleValue2);
        await Assert.That(results).Contains(SampleValue3);
    }

    /// <summary>
    /// Tests DebounceImmediate with null scheduler uses Default scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDebounceImmediateNullScheduler_ThenUsesDefault()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        using var sub = subject
            .DebounceImmediate(TimeSpan.FromMilliseconds(200))
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(SampleValue2);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 2,
            TimeSpan.FromSeconds(30));

        await Assert.That(results).Contains(1);
        await Assert.That(results).Contains(SampleValue2);
    }

    /// <summary>
    /// Tests DebounceUntil without scheduler emits immediately when condition true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDebounceUntilWithoutScheduler_ThenEmitsImmediatelyWhenConditionTrue()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        using var sub = subject.DebounceUntil(TimeSpan.FromMilliseconds(500), x => x % 2 == 0)
            .Subscribe(results.Add);

        // Even values should emit immediately (condition true)
        subject.OnNext(SampleValue2);

        await Task.Delay(SchedulerHalfWindowTicks);
        await Assert.That(results).Contains(SampleValue2);
    }
}
