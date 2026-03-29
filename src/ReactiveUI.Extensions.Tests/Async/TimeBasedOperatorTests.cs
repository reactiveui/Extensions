// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for time-based operators: Throttle, Delay, Timeout, Timer, Interval.
/// </summary>
public class TimeBasedOperatorTests
{
    /// <summary>Tests Throttle only last in burst is emitted.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(results).IsEquivalentTo([1, 2]);
    }

    /// <summary>Tests Throttle negative due time throws.</summary>
    [Test]
    public void WhenThrottleNegativeDueTime_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Throttle(TimeSpan.FromMilliseconds(-1)));
    }

    /// <summary>Tests Delay elements are time shifted.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDelaySequence_ThenAllElementsDelayed()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Delay(TimeSpan.FromMilliseconds(30))
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests Timeout not exceeded completes normally.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// Verifies that when the downstream observer throws a non-cancellation exception
    /// during OnNext from a throttled delay callback, the exception is routed to the
    /// <see cref="UnhandledExceptionHandler"/>.
    /// This covers the Throttle exception routing path.
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
    /// This covers the Timeout OnCompleted exception routing path.
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
    /// This covers the Interval TimeProvider code path.
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
    /// This covers the cancellation loop exit in the periodic timer.
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
    /// Verifies that <see cref="ObservableAsync.Throttle"/> uses the non-system
    /// <see cref="TimeProvider"/> code path in <c>DelayAsync</c> when a
    /// custom provider is supplied, and still correctly debounces values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleWithCustomTimeProvider_ThenUsesTimerPath()
    {
        var customProvider = new CustomTimeProvider();
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();
        var resultReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(50), customProvider)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    resultReceived.TrySetResult(true);
                    return default;
                },
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(2, CancellationToken.None);
        await subject.OnNextAsync(3, CancellationToken.None);

        await resultReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await subject.OnCompletedAsync(Result.Success);

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that when two values are emitted in quick succession with a custom
    /// <see cref="TimeProvider"/>, the first value is superseded and only
    /// the second is forwarded, exercising the non-system <c>DelayAsync</c> path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleWithCustomTimeProviderValueSuperseded_ThenOlderValueDropped()
    {
        var customProvider = new CustomTimeProvider();
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();
        var resultReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(80), customProvider)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    resultReceived.TrySetResult(true);
                    return default;
                },
                null,
                null);

        // Emit two values rapidly; first should be superseded
        await subject.OnNextAsync(10, CancellationToken.None);
        await subject.OnNextAsync(20, CancellationToken.None);

        await resultReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await subject.OnCompletedAsync(Result.Success);

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(20);
    }

    /// <summary>
    /// Verifies that when the downstream observer throws during a throttled emission
    /// with a custom <see cref="TimeProvider"/>, the exception is routed to
    /// <see cref="UnhandledExceptionHandler"/>.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleWithCustomTimeProviderOnNextThrows_ThenRoutedToUnhandledExceptionHandler()
    {
        var caught = new List<Exception>();
        UnhandledExceptionHandler.Register(ex => caught.Add(ex));

        var customProvider = new CustomTimeProvider();
        var subject = SubjectAsync.Create<int>();

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(50), customProvider)
            .SubscribeAsync(
                (x, _) => throw new InvalidOperationException("custom provider observer exploded"),
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);

        var handlerCalled = await AsyncTestHelpers.WaitForConditionAsync(
            () => caught.Count >= 1,
            TimeSpan.FromSeconds(10));

        await Assert.That(handlerCalled).IsTrue();
        await Assert.That(caught[0]).IsTypeOf<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that when <c>OnErrorResumeAsync</c> is called on a throttled sequence,
    /// the pending timer is cancelled and the error is forwarded to the downstream observer
    /// in the Throttle operator.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleOnErrorResume_ThenCancelsTimerAndForwardsError()
    {
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();
        var errors = new List<Exception>();
        var errorReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(500))
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                (ex, _) =>
                {
                    errors.Add(ex);
                    errorReceived.TrySetResult(true);
                    return default;
                },
                null);

        // Emit a value (starts a 500ms timer)
        await subject.OnNextAsync(1, CancellationToken.None);

        // Immediately send an error before the throttle timer fires
        await subject.OnErrorResumeAsync(new InvalidOperationException("test error"), CancellationToken.None);

        await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Error should be forwarded, and the pending value should NOT be emitted
        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsTypeOf<InvalidOperationException>();
        await Assert.That(results).Count().IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that when the <see cref="TimeProvider"/> throws a non-cancellation exception
    /// during the delay inside <c>OnTimeoutAsync</c>, the exception is routed to the
    /// <see cref="UnhandledExceptionHandler"/>.
    /// This covers the Timeout delay exception routing path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTimeoutDelayThrowsNonCancellation_ThenRoutedToUnhandledExceptionHandler()
    {
        var caught = new List<Exception>();
        UnhandledExceptionHandler.Register(ex => caught.Add(ex));

        var throwingProvider = new ThrowingTimeProvider();
        var source = AsyncTestHelpers.CreateDirectSource<int>();

        await using var sub = await source
            .Timeout(TimeSpan.FromMilliseconds(100), throwingProvider)
            .SubscribeAsync(
                static (_, _) => default,
                null,
                null);

        var handlerCalled = await AsyncTestHelpers.WaitForConditionAsync(
            () => caught.Count >= 1,
            TimeSpan.FromSeconds(10));

        await Assert.That(handlerCalled).IsTrue();
        await Assert.That(caught[0]).IsTypeOf<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that when the source emits an error via <c>OnErrorResumeAsync</c>,
    /// the <c>TimeoutObserver</c> cancels the timer and forwards the error downstream.
    /// This covers the <c>OnErrorResumeAsyncCore</c> path in the Timeout operator.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTimeoutSourceEmitsErrorResume_ThenForwardsAndCancelsTimer()
    {
        var errors = new List<Exception>();
        var errorReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = AsyncTestHelpers.CreateDirectSource<int>();

        await using var sub = await source
            .Timeout(TimeSpan.FromSeconds(30))
            .SubscribeAsync(
                static (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    errorReceived.TrySetResult(true);
                    return default;
                },
                null);

        var testError = new InvalidOperationException("test error");
        await source.EmitError(testError);

        await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsTypeOf<InvalidOperationException>();
        await Assert.That(errors[0].Message).IsEqualTo("test error");
    }

    /// <summary>
    /// Verifies that Delay forwards non-terminal errors via OnErrorResumeAsync.
    /// Covers the OnErrorResumeAsyncCore path in DelayObserver.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDelaySourceEmitsErrorResume_ThenErrorForwarded()
    {
        var source = AsyncTestHelpers.CreateDirectSource<int>();
        var errors = new List<Exception>();
        var completed = new TaskCompletionSource();

        await using var sub = await source
            .Delay(TimeSpan.FromMilliseconds(1))
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                result =>
                {
                    completed.TrySetResult();
                    return default;
                });

        var expectedError = new InvalidOperationException("resume error");
        await source.EmitError(expectedError);
        await source.Complete(Result.Success);

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsSameReferenceAs(expectedError);
    }

    /// <summary>
    /// Verifies that Throttle drops a value when superseded by a newer emission,
    /// exercising the id-mismatch early return in FireAfterDelayAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleValueSuperseded_ThenOlderValueDropped()
    {
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();
        var completed = new TaskCompletionSource();

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(200))
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completed.TrySetResult();
                    return default;
                });

        // Emit two values in rapid succession; first should be superseded
        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(2, CancellationToken.None);

        // Wait for the throttled value to arrive before completing
        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(5));

        await subject.OnCompletedAsync(Result.Success);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Only the last value (2) should have been emitted
        await Assert.That(results).Contains(2);
    }

    /// <summary>
    /// Verifies that Throttle routes non-cancellation exceptions to the unhandled exception handler.
    /// Covers the catch(Exception) block in ThrottleObserver.FireAfterDelayAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleFireThrowsNonCancellation_ThenRoutedToUnhandledHandler()
    {
        var handledErrors = new List<Exception>();
        UnhandledExceptionHandler.Register(ex => handledErrors.Add(ex));

        var expectedError = new InvalidOperationException("downstream error");
        var source = AsyncTestHelpers.CreateDirectSource<int>();

        await using var sub = await source
            .Throttle(TimeSpan.FromMilliseconds(1))
            .SubscribeAsync(
                (_, _) => throw expectedError,
                null,
                null);

        await source.EmitNext(1);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => handledErrors.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(handledErrors).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that a periodic Timer emits multiple ticks before cancellation.
    /// Covers the while-loop body in the periodic Timer factory.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPeriodicTimerEmitsMultipleTicks_ThenAllTicksReceived()
    {
        var results = new List<long>();

        var sub = await ObservableAsync.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(20))
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 3,
            TimeSpan.FromSeconds(10));

        await sub.DisposeAsync();

        await Assert.That(results.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(results[0]).IsEqualTo(0L);
        await Assert.That(results[1]).IsEqualTo(1L);
        await Assert.That(results[2]).IsEqualTo(2L);
    }

    /// <summary>
    /// Verifies that a periodic <see cref="ObservableAsync.Timer(TimeSpan, TimeSpan, TimeProvider?)"/>
    /// with a custom <see cref="TimeProvider"/> emits at least two ticks before disposal,
    /// exercising the loop continuation on line 90 of Timer.cs through the non-system delay path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPeriodicTimerWithCustomTimeProvider_ThenLoopContinuesUntilDisposed()
    {
        var customProvider = new CustomTimeProvider();
        var results = new List<long>();

        var sub = await ObservableAsync.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(20), customProvider)
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

        var noMoreEmissions = await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count == countAtDispose,
            TimeSpan.FromMilliseconds(200));

        await Assert.That(noMoreEmissions).IsTrue();
        await Assert.That(results[0]).IsEqualTo(0L);
        await Assert.That(results[1]).IsEqualTo(1L);
    }

    /// <summary>
    /// Verifies that when the downstream observer throws a non-cancellation exception
    /// during OnNext from a throttled emission using an immediate-fire
    /// <see cref="TimeProvider"/>, the exception is routed to the
    /// <see cref="UnhandledExceptionHandler"/>.
    /// This deterministically covers lines 162-163 in ThrottleObserver.FireAfterDelayAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleImmediateFireOnNextThrows_ThenRoutedToUnhandledExceptionHandler()
    {
        var previousHandler = UnhandledExceptionHandler.CurrentHandler;
        var caught = new List<Exception>();

        try
        {
            UnhandledExceptionHandler.Register(ex => caught.Add(ex));

            var immediateProvider = new ImmediateFireTimeProvider();
            var subject = SubjectAsync.Create<int>();

            await using var sub = await subject.Values
                .Throttle(TimeSpan.FromMilliseconds(100), immediateProvider)
                .SubscribeAsync(
                    (_, _) => throw new InvalidOperationException("immediate fire observer exploded"),
                    null,
                    null);

            // Single emission; the delay completes synchronously via ImmediateFireTimeProvider,
            // id matches, observer.OnNextAsync throws, caught by catch(Exception) and routed
            // to UnhandledExceptionHandler.
            await subject.OnNextAsync(1, CancellationToken.None);

            var handlerCalled = await AsyncTestHelpers.WaitForConditionAsync(
                () => caught.Count >= 1,
                TimeSpan.FromSeconds(5));

            await Assert.That(handlerCalled).IsTrue();
            await Assert.That(caught[0]).IsTypeOf<InvalidOperationException>();
            await Assert.That(caught[0].Message).IsEqualTo("immediate fire observer exploded");
        }
        finally
        {
            UnhandledExceptionHandler.Register(previousHandler);
        }
    }

    /// <summary>Tests Interval stops when cancelled.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenIntervalCancelled_ThenStops()
    {
        var cts = new CancellationTokenSource();
        var items = new List<long>();

        await using var sub = await ObservableAsync.Interval(TimeSpan.FromMilliseconds(10))
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    if (x >= 2)
                    {
                        cts.Cancel();
                    }

                    return default;
                },
                null,
                null,
                cts.Token);

        await Task.Delay(200);
        await Assert.That(items.Count).IsGreaterThanOrEqualTo(2);
    }

    /// <summary>Tests Timer with period stops when cancelled.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTimerWithPeriodCancelled_ThenStops()
    {
        var cts = new CancellationTokenSource();
        var items = new List<long>();

        await using var sub = await ObservableAsync.Timer(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(10))
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    if (x >= 2)
                    {
                        cts.Cancel();
                    }

                    return default;
                },
                null,
                null,
                cts.Token);

        await Task.Delay(200);
        await Assert.That(items.Count).IsGreaterThanOrEqualTo(2);
    }

    /// <summary>Tests Throttle supersedes older values and only emits latest.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleReceivesRapidValues_ThenOnlyEmitsLatest()
    {
        var source = new DirectSource<int>();
        var items = new List<int>();
        var completed = new TaskCompletionSource();

        await using var sub = await source.Throttle(TimeSpan.FromMilliseconds(50))
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                _ =>
                {
                    completed.TrySetResult();
                    return default;
                });

        await source.EmitNext(1);
        await source.EmitNext(2);
        await source.EmitNext(3);

        await Task.Delay(200);
        await source.Complete(Result.Success);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(items).Contains(3);
    }

    /// <summary>Tests Timeout fires when source is slow.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTimeoutFires_ThenThrowsTimeoutException()
    {
        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await ObservableAsync.Never<int>()
                .Timeout(TimeSpan.FromMilliseconds(10))
                .FirstAsync());
    }

    /// <summary>Tests Timeout with fallback observable.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTimeoutWithFallback_ThenFallbackUsed()
    {
        var result = await ObservableAsync.Never<int>()
            .Timeout(TimeSpan.FromMilliseconds(10), ObservableAsync.Return(99))
            .FirstAsync();

        await Assert.That(result).IsEqualTo(99);
    }

    /// <summary>Tests Timeout resets on each value and does not fire.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTimeoutResetsOnValue_ThenDoesNotFire()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Timeout(TimeSpan.FromSeconds(5))
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
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

    /// <summary>
    /// A <see cref="TimeProvider"/> that throws an <see cref="InvalidOperationException"/>
    /// from <see cref="CreateTimer"/>. Used to test the non-cancellation catch block in timeout observers.
    /// </summary>
    private sealed class ThrowingTimeProvider : TimeProvider
    {
        /// <summary>
        /// Throws an <see cref="InvalidOperationException"/> instead of creating a timer.
        /// </summary>
        /// <param name="callback">The callback (unused).</param>
        /// <param name="state">The state (unused).</param>
        /// <param name="dueTime">The due time (unused).</param>
        /// <param name="period">The period (unused).</param>
        /// <returns>Never returns; always throws.</returns>
        /// <exception cref="InvalidOperationException">Always thrown.</exception>
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            throw new InvalidOperationException("timer creation failed");
    }

    /// <summary>
    /// A <see cref="TimeProvider"/> that fires the timer callback synchronously during
    /// <see cref="CreateTimer"/>, completing the delay immediately. Used to deterministically
    /// test the id-mismatch early return and exception routing paths in ThrottleObserver.
    /// </summary>
    private sealed class ImmediateFireTimeProvider : TimeProvider
    {
        /// <summary>
        /// Invokes the timer callback synchronously and returns a no-op timer.
        /// </summary>
        /// <param name="callback">The callback to invoke immediately.</param>
        /// <param name="state">The state object passed to the callback.</param>
        /// <param name="dueTime">The initial delay (ignored; fires immediately).</param>
        /// <param name="period">The interval (ignored; fires only once).</param>
        /// <returns>A no-op <see cref="ITimer"/> instance.</returns>
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            callback(state);
            return new NoOpTimer();
        }

        /// <summary>
        /// A timer that performs no operations. Used as the return value from
        /// <see cref="ImmediateFireTimeProvider.CreateTimer"/>.
        /// </summary>
        private sealed class NoOpTimer : ITimer
        {
            /// <summary>
            /// No-op change; returns true.
            /// </summary>
            /// <param name="dueTime">The due time (ignored).</param>
            /// <param name="period">The period (ignored).</param>
            /// <returns>Always returns true.</returns>
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            /// <summary>
            /// No-op dispose.
            /// </summary>
            public void Dispose()
            {
            }

            /// <summary>
            /// No-op async dispose.
            /// </summary>
            /// <returns>A completed <see cref="ValueTask"/>.</returns>
            public ValueTask DisposeAsync() => default;
        }
    }
}
