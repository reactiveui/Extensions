// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for time-based operators: Throttle, Delay, Timeout, Timer, Interval.
/// </summary>
[NotInParallel(nameof(UnhandledExceptionHandler))]
[TestExecutor<UnhandledExceptionTestExecutor>]
public class TimeBasedOperatorTests
{
    /// <summary>Tests Throttle only last in burst is emitted.</summary>
    [Test]
    public async Task WhenThrottle_ThenOnlyLastInBurstIsEmitted()
    {
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(100))
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(2, CancellationToken.None);
        await subject.OnNextAsync(3, CancellationToken.None);

        var resultReceived = await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count == 1,
            TimeSpan.FromSeconds(10));

        await subject.OnCompletedAsync(Result.Success);

        await Assert.That(resultReceived).IsTrue();
        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(3);
    }

    /// <summary>Tests Throttle with spaced items all are emitted.</summary>
    [Test]
    public async Task WhenThrottleWithSpacedItems_ThenAllAreEmitted()
    {
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();
        var firstReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(50))
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);

                    if (results.Count == 1)
                    {
                        firstReceived.TrySetResult(true);
                    }
                    else if (results.Count == 2)
                    {
                        secondReceived.TrySetResult(true);
                    }

                    return default;
                },
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);
        await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Task.Delay(75);

        await subject.OnNextAsync(2, CancellationToken.None);
        await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(results).IsEquivalentTo(new[] { 1, 2 });
    }

    /// <summary>Tests Throttle negative due time throws.</summary>
    [Test]
    public void WhenThrottleNegativeDueTime_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Throttle(TimeSpan.FromMilliseconds(-1)));
    }

    /// <summary>Tests Delay elements are time shifted.</summary>
    [Test]
    public async Task WhenDelay_ThenElementsAreTimeShifted()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await ObservableAsync.Return(42)
            .Delay(TimeSpan.FromMilliseconds(100))
            .FirstAsync();
        stopwatch.Stop();

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(stopwatch.ElapsedMilliseconds).IsGreaterThanOrEqualTo(80);
    }

    /// <summary>Tests Delay zero causes no delay.</summary>
    [Test]
    public async Task WhenDelayZero_ThenNoDelay()
    {
        var result = await ObservableAsync.Return(42)
            .Delay(TimeSpan.Zero)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>Tests Delay negative throws.</summary>
    [Test]
    public void WhenDelayNegative_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Delay(TimeSpan.FromMilliseconds(-1)));
    }

    /// <summary>Tests Delay sequence delays all elements.</summary>
    [Test]
    public async Task WhenDelaySequence_ThenAllElementsDelayed()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Delay(TimeSpan.FromMilliseconds(30))
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests Timeout not exceeded completes normally.</summary>
    [Test]
    public async Task WhenTimeoutNotExceeded_ThenCompletesNormally()
    {
        var result = await ObservableAsync.Return(42)
            .Timeout(TimeSpan.FromSeconds(5))
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>Tests Timeout exceeded throws TimeoutException.</summary>
    [Test]
    public void WhenTimeoutExceeded_ThenThrowsTimeoutException()
    {
        var source = ObservableAsync.Never<int>()
            .Timeout(TimeSpan.FromMilliseconds(100));

        Assert.ThrowsAsync<TimeoutException>(
            async () => await source.FirstAsync());
    }

    /// <summary>Tests Timeout with fallback switches to fallback.</summary>
    [Test]
    public async Task WhenTimeoutWithFallback_ThenSwitchesToFallback()
    {
        var source = ObservableAsync.Never<int>()
            .Timeout(TimeSpan.FromMilliseconds(100), ObservableAsync.Return(99));

        var result = await source.FirstAsync();

        await Assert.That(result).IsEqualTo(99);
    }

    /// <summary>Tests Timeout zero duration throws.</summary>
    [Test]
    public void WhenTimeoutZeroDuration_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Timeout(TimeSpan.Zero));
    }

    /// <summary>Tests Timeout negative duration throws.</summary>
    [Test]
    public void WhenTimeoutNegativeDuration_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Timeout(TimeSpan.FromMilliseconds(-1)));
    }

    /// <summary>Tests Timeout with null fallback throws.</summary>
    [Test]
    public void WhenTimeoutWithFallbackNull_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).Timeout(TimeSpan.FromSeconds(1), (ObservableAsync<int>)null!));
    }

    /// <summary>
    /// Verifies that when two values are emitted quickly to a throttled sequence,
    /// only the last value is forwarded after the debounce period (the first value is superseded).
    /// This covers the early return at Throttle.cs line 150 where <c>_id != id</c>.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleValueSuperseded_ThenOlderValueDropped()
    {
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(100))
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        // Emit two values in quick succession; the first should be superseded
        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(2, CancellationToken.None);

        var resultReceived = await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(10));

        await subject.OnCompletedAsync(Result.Success);

        await Assert.That(resultReceived).IsTrue();
        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that when the downstream observer throws a non-cancellation exception
    /// during OnNext from a throttled delay callback, the exception is routed to the
    /// <see cref="UnhandledExceptionHandler"/>.
    /// This covers Throttle.cs lines 162-163.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleOnNextThrows_ThenRoutedToUnhandledExceptionHandler()
    {
        var caught = new List<Exception>();
        UnhandledExceptionHandler.Register(ex => caught.Add(ex));

        var subject = SubjectAsync.Create<int>();

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(50))
            .SubscribeAsync(
                (x, _) => throw new InvalidOperationException("observer exploded"),
                null,
                null);

        await subject.OnNextAsync(42, CancellationToken.None);

        var handlerCalled = await AsyncTestHelpers.WaitForConditionAsync(
            () => caught.Count >= 1,
            TimeSpan.FromSeconds(10));

        await Assert.That(handlerCalled).IsTrue();
        await Assert.That(caught[0]).IsTypeOf<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that when the downstream observer throws a non-cancellation exception
    /// during OnCompleted from a timeout callback, the exception is routed to the
    /// <see cref="UnhandledExceptionHandler"/>.
    /// This covers Timeout.cs lines 197-198.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTimeoutOnCompletedThrows_ThenRoutedToUnhandledExceptionHandler()
    {
        var caught = new List<Exception>();
        UnhandledExceptionHandler.Register(ex => caught.Add(ex));

        var source = ObservableAsync.Never<int>()
            .Timeout(TimeSpan.FromMilliseconds(50));

        await using var sub = await source.SubscribeAsync(
            static (_, _) => default,
            null,
            _ => throw new InvalidOperationException("completion handler exploded"));

        var handlerCalled = await AsyncTestHelpers.WaitForConditionAsync(
            () => caught.Count >= 1,
            TimeSpan.FromSeconds(10));

        await Assert.That(handlerCalled).IsTrue();
        await Assert.That(caught[0]).IsTypeOf<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that <see cref="ObservableAsync.Interval(TimeSpan, TimeProvider?)"/>
    /// uses the custom <see cref="TimeProvider"/> path when a non-system provider is supplied.
    /// This covers Interval.cs lines 39-43.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenIntervalWithNonSystemTimeProvider_ThenUsesTimerPath()
    {
        var customProvider = new CustomTimeProvider();
        var results = new List<long>();

        await using var sub = await ObservableAsync.Interval(TimeSpan.FromMilliseconds(50), customProvider)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        var receivedTwo = await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 2,
            TimeSpan.FromSeconds(10));

        await Assert.That(receivedTwo).IsTrue();
        await Assert.That(results[0]).IsEqualTo(1L);
        await Assert.That(results[1]).IsEqualTo(2L);
    }

    /// <summary>
    /// Verifies that a periodic <see cref="ObservableAsync.Timer(TimeSpan, TimeSpan, TimeProvider?)"/>
    /// stops emitting values once the subscription is disposed.
    /// This covers Timer.cs line 90 (the cancellation loop exit).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPeriodicTimerCancelled_ThenStopsEmitting()
    {
        var results = new List<long>();

        var sub = await ObservableAsync.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(50))
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        var receivedTwo = await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 2,
            TimeSpan.FromSeconds(10));

        await Assert.That(receivedTwo).IsTrue();

        var countAtDispose = results.Count;
        await sub.DisposeAsync();

        // Allow a brief window to confirm no further emissions
        var noMoreEmissions = await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count == countAtDispose,
            TimeSpan.FromMilliseconds(200));

        await Assert.That(noMoreEmissions).IsTrue();
    }

    /// <summary>
    /// A custom <see cref="TimeProvider"/> that delegates timer creation to the system provider.
    /// Used to exercise the non-system <see cref="TimeProvider"/> code paths in Interval and Timer operators.
    /// </summary>
    private sealed class CustomTimeProvider : TimeProvider
    {
        /// <summary>
        /// Creates a timer by delegating to the system <see cref="TimeProvider"/>.
        /// </summary>
        /// <param name="callback">The callback to invoke when the timer fires.</param>
        /// <param name="state">The state object passed to the callback.</param>
        /// <param name="dueTime">The initial delay before the first invocation.</param>
        /// <param name="period">The interval between subsequent invocations.</param>
        /// <returns>An <see cref="ITimer"/> instance.</returns>
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            System.CreateTimer(callback, state, dueTime, period);
    }
}
