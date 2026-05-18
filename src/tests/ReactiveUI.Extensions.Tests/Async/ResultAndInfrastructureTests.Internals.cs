// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// CancelableTaskSubscription, ConcurrencyLimiter, Continuation, and ObserverAsync internals tests.
/// </summary>
public partial class ResultAndInfrastructureTests
{
    /// <summary>
    /// Verifies that when the cancellation token is cancelled before the observer completes,
    /// WaitValueAsync throws OperationCanceledException.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTaskObserverCancelledDuringWait_ThenThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();

        // Use a source that never completes, so the TaskObserver waits forever
        var source = ObservableAsync.Create<int>(async (_, ct) =>
        {
            // Never complete - wait until cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            return DisposableAsync.Empty;
        });

        // FirstAsync internally creates a TaskObserverAsyncBase derivative
        var task = source.FirstAsync(cts.Token).AsTask();

        const int CancelDelayMilliseconds = 100;

        // Cancel after a short delay to trigger the registration callback
        cts.CancelAfter(CancelDelayMilliseconds);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
    }

    /// <summary>
    /// Verifies that when RunAsyncCore throws an exception during the catch block's
    /// OnCompletedAsync call, the secondary exception is forwarded to the
    /// UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRunAsyncCoreThrowsAndCompletedHandlerAlsoThrows_ThenSecondaryRoutedToUnhandled()
    {
        var received = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        UnhandledExceptionHandler.Register(ex => received.TrySetResult(ex));

        var completedError = new ArithmeticException("OnCompleted handler failed");

        var source = ObservableAsync.CreateAsBackgroundJob<int>(
            (_, _) =>
            throw new InvalidOperationException("RunAsyncCore failure"),
            NewThreadTaskScheduler.Instance);

        await using var sub = await source.SubscribeAsync(
            (_, _) => default,
            null,
            _ => throw completedError);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result).IsSameReferenceAs(completedError);
    }

    /// <summary>
    /// Verifies that when a background job source throws after yielding and the observer's
    /// OnCompletedAsync also throws, the inner exception from OnCompletedAsync is routed to
    /// the <see cref="UnhandledExceptionHandler"/>.
    /// Covers the catch block (lines 86-87) in <see cref="CancelableTaskSubscription{T}.CompleteWithFailureAsync"/>.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBackgroundJobThrowsAfterYieldAndOnCompletedThrows_ThenInnerExceptionRoutedToUnhandled()
    {
        var received = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        UnhandledExceptionHandler.Register(ex => received.TrySetResult(ex));

        var onCompletedError = new ArithmeticException("OnCompleted threw");

        var source = ObservableAsync.CreateAsBackgroundJob<int>(
            async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("source failure after yield");
        },
            NewThreadTaskScheduler.Instance);

        await using var sub = await source.SubscribeAsync(
            (_, _) => default,
            null,
            _ => throw onCompletedError);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result).IsSameReferenceAs(onCompletedError);
    }

    /// <summary>
    /// Tests Continuation.Lock when already locked returns immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenContinuationLockAlreadyLocked_ThenReturnsImmediately()
    {
        using var continuation = new Continuation();
        var observer = new Subject<(int value, IDisposable Sync)>();
        List<(int value, IDisposable Sync)> received = [];
        using var sub = observer.Subscribe(received.Add);

        // First Lock starts a task waiting on barrier
        _ = continuation.Lock(1, observer);

        // Second Lock while already locked should return completed immediately
        var secondLockTask = continuation.Lock(2, observer);
        await Assert.That(secondLockTask.IsCompleted).IsTrue();

        // Only one item should have been emitted
        await Assert.That(received).Count().IsEqualTo(1);
        await Assert.That(received[0].value).IsEqualTo(1);
    }

    /// <summary>
    /// Tests Continuation.Dispose when not locked calls UnLock which returns immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenContinuationDisposeNotLocked_ThenUnlockReturnsImmediately()
    {
        var continuation = new Continuation();
        continuation.Dispose();

        await Assert.That(continuation.CompletedPhases).IsEqualTo(0L);
    }

    /// <summary>
    /// Tests Continuation lock and unlock via Dispose completes the barrier cycle.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenContinuationLockThenDispose_ThenBarrierSignaled()
    {
        var continuation = new Continuation();
        var observer = new Subject<(int value, IDisposable Sync)>();

        var lockTask = continuation.Lock(1, observer);

        // Dispose triggers UnLock which signals the barrier
        continuation.Dispose();
        const int TimeoutMilliseconds = 2_000;
        await Task.WhenAny(lockTask, Task.Delay(TimeoutMilliseconds));

        await Assert.That(lockTask.IsCompleted).IsTrue();
    }

    /// <summary>
    /// Tests ConcurrencyLimiter ProcessTaskCompletion when task is faulted.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrencyLimiterTaskFaults_ThenForwardsError()
    {
        var tcs = new TaskCompletionSource<int>();
        tcs.SetException(new InvalidOperationException("boom"));

        List<Task<int>> tasks = [tcs.Task];
        var limiter = new ConcurrencyLimiter<int>(tasks, 1);

        Exception? caught = null;
        var completed = new TaskCompletionSource<bool>();

        limiter.Observable.Subscribe(
            _ => { },
            ex =>
            {
                caught = ex;
                completed.TrySetResult(true);
            },
            () => completed.TrySetResult(true));

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(caught).IsNotNull();
    }

    /// <summary>
    /// Tests ConcurrencyLimiter ProcessTaskCompletion when task is canceled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrencyLimiterTaskCanceled_ThenForwardsOperationCanceled()
    {
        var tcs = new TaskCompletionSource<int>();
        tcs.SetCanceled();

        List<Task<int>> tasks = [tcs.Task];
        var limiter = new ConcurrencyLimiter<int>(tasks, 1);

        Exception? caught = null;
        var completed = new TaskCompletionSource<bool>();

        limiter.Observable.Subscribe(
            _ => { },
            ex =>
            {
                caught = ex;
                completed.TrySetResult(true);
            },
            () => completed.TrySetResult(true));

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught).IsTypeOf<OperationCanceledException>();
    }

    /// <summary>
    /// Tests ConcurrencyLimiter PullNextTask when disposed clears the enumerator.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrencyLimiterDisposed_ThenClearsRator()
    {
        var tcs = new TaskCompletionSource<int>();
        List<Task<int>> tasks = [tcs.Task];
        var limiter = new ConcurrencyLimiter<int>(tasks, 1);

        var sub = limiter.Observable.Subscribe(_ => { });
        sub.Dispose();

        await Assert.That(limiter.Disposed).IsTrue();
    }

    /// <summary>
    /// Tests ConcurrencyLimiter completes when no outstanding tasks and enumerator exhausted.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrencyLimiterNoOutstandingAndExhausted_ThenCompletes()
    {
        Task<int>[] tasks = [];
        var limiter = new ConcurrencyLimiter<int>(tasks, 1);

        var completed = false;
        limiter.Observable.Subscribe(_ => { }, () => completed = true);

        await Assert.That(completed).IsTrue();
    }

    /// <summary>
    /// Tests ConcurrencyLimiter emits results and completes when all tasks succeed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrencyLimiterAllTasksSucceed_ThenEmitsAndCompletes()
    {
        List<Task<int>> tasks = [Task.FromResult(1), Task.FromResult(2), Task.FromResult(3)];
        var limiter = new ConcurrencyLimiter<int>(tasks, 2);

        List<int> results = [];
        var completionTcs = new TaskCompletionSource<bool>();

        limiter.Observable.Subscribe(
            results.Add,
            _ => completionTcs.TrySetResult(false),
            () => completionTcs.TrySetResult(true));

        var completedOk = await completionTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        const int ExpectedResultCount = 3;
        await Assert.That(completedOk).IsTrue();
        await Assert.That(results).Count().IsEqualTo(ExpectedResultCount);
    }

    /// <summary>
    /// Verifies that ConcurrencyLimiter PullNextTask when disposed clears the enumerator
    /// and does not emit further values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrencyLimiterDisposedThenPullNext_ThenClearsRator()
    {
        List<int> items = [];
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        IEnumerable<Task<int>> tasks = [tcs.Task, Task.FromResult(2)];
        var limiter = new ConcurrencyLimiter<int>(tasks, 1);

        // Subscribe and immediately dispose
        var sub = limiter.Observable.Subscribe(items.Add);
        sub.Dispose();

        // After disposal, PullNextTask should clear the enumerator
        limiter.PullNextTask(Observer.Create<int>(items.Add));

        await Assert.That(items).IsEmpty();
    }

    /// <summary>
    /// Verifies that when RunAsyncCore throws and OnCompletedAsync also throws,
    /// the exception is routed to UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCancelableTaskSubscriptionCompletionThrows_ThenRoutedToHandler()
    {
        Exception? unhandled = null;
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        // CancelableTaskSubscription.CompleteWithFailureAsync routes to UnhandledExceptionHandler
        // when OnCompletedAsync itself throws. Test it directly.
        var observer = new AnonymousObserverAsync<int>(
            (_, _) => default,
            null,
            _ => throw new InvalidOperationException("completion throws too"));

        await CancelableTaskSubscription<int>.CompleteWithFailureAsync(
            observer,
            new InvalidOperationException("original error"));

        await Assert.That(unhandled).IsNotNull();
        await Assert.That(unhandled!.Message).IsEqualTo("completion throws too");
        await Assert.That(unhandled!.Message).IsEqualTo("completion throws too");
    }

    /// <summary>
    /// Verifies that when RunAsyncCore throws, CompleteWithFailureAsync is called from
    /// the RunAsync catch block, which routes the error to the observer via
    /// OnCompletedAsync with Result.Failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCancelableTaskSubscriptionRunAsyncThrows_ThenCompletesWithFailure()
    {
        Result? completionResult = null;
        var completionReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Create a source that throws during execution (CreateAsBackgroundJob uses CancelableTaskSubscription)
        var source = ObservableAsync.CreateAsBackgroundJob<int>(
            (_, _) =>
        {
            try
            {
                throw new InvalidOperationException("run-async-failure");
            }
            catch (Exception exception)
            {
                return ValueTask.FromException(exception);
            }
        },
            NewThreadTaskScheduler.Instance);

        await using var sub = await source.SubscribeAsync(
            (_, _) => default,
            null,
            result =>
            {
                completionResult = result;
                completionReceived.TrySetResult();
                return default;
            });

        await completionReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
        await Assert.That(completionResult!.Value.Exception).IsTypeOf<InvalidOperationException>();
        await Assert.That(completionResult!.Value.Exception!.Message).IsEqualTo("run-async-failure");
    }

    /// <summary>
    /// Verifies that when OnNextAsyncCore throws OperationCanceledException the
    /// exception is silently swallowed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnNextAsyncCoreThrowsOce_ThenSwallowed()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            (_, _) => throw new OperationCanceledException(),
            null,
            _ =>
            {
                completed.TrySetResult();
                return default;
            });

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // If the OCE was not swallowed the test would have thrown before reaching here
        await Assert.That(completed.Task.IsCompletedSuccessfully).IsTrue();
    }

    /// <summary>
    /// Verifies that when the source subscription throws during DisposeAsync the
    /// exception is routed to UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSourceSubscriptionDisposeThrows_ThenRoutedToUnhandled()
    {
        var received = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        UnhandledExceptionHandler.Register(ex => received.TrySetResult(ex));

        var expectedError = new ArithmeticException("source-dispose-failure");
        var observer = new TestableObserverAsync();

        // Set a source subscription that throws on disposal
        await observer.SetSourceSubscriptionAsync(
            DisposableAsync.Create(() => throw expectedError));

        await observer.DisposeAsync();

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result).IsSameReferenceAs(expectedError);
    }

    /// <summary>
    /// Verifies that SetSourceSubscriptionAsync can be called to set a source
    /// subscription on an observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSetSourceSubscriptionAsync_ThenSubscriptionIsSet()
    {
        var disposed = false;
        var observer = new TestableObserverAsync();

        var disposable = DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        });

        await observer.SetSourceSubscriptionAsync(disposable);

        // Disposing the observer should also dispose the source subscription
        await observer.DisposeAsync();

        await Assert.That(disposed).IsTrue();
    }

    /// <summary>
    /// Verifies that TryEnterOnSomethingCall detects concurrent observer calls from
    /// different async contexts and reports via UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    [SuppressMessage(
        "Major Bug",
        "S4462:Calls to async methods should not be blocking",
        Justification = "Test drives OnNextAsync from a sync Thread body so the two callers have distinct managed thread IDs for concurrent-call detection.")]
    [SuppressMessage(
        "Major Bug",
        "S5034:'ValueTask' should be consumed correctly",
        Justification = "ValueTask is materialized once via AsTask() and consumed once via Wait().")]
    public async Task WhenConcurrentObserverCalls_ThenDetectedAndReported()
    {
        var received = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        UnhandledExceptionHandler.Register(ex => received.TrySetResult(ex));

        var observerCaptured = new TaskCompletionSource<IObserverAsync<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriberEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSubscriber = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = ObservableAsync.Create<int>((observer, _) =>
        {
            observerCaptured.TrySetResult(observer);
            return new ValueTask<IAsyncDisposable>(DisposableAsync.Empty);
        });

        await using var sub = await source.SubscribeAsync(
            async (_, ct) =>
            {
                subscriberEntered.TrySetResult();
                await releaseSubscriber.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            },
            null);

        var observer = await observerCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Concurrent detection keys on Environment.CurrentManagedThreadId. Drive each call from
        // its own dedicated Thread (not ThreadPool, where IDs can be reused) so the two callers
        // are guaranteed distinct — otherwise the second call is treated as reentrant and the
        // detection silently passes.
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstThread = new Thread(() =>
        {
            firstStarted.TrySetResult();
            observer.OnNextAsync(1, CancellationToken.None).AsTask().Wait();
        })
        { IsBackground = true, Name = "ConcurrentObserverTest-First" };
        firstThread.Start();

        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await subscriberEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondThread = new Thread(() =>
            observer.OnNextAsync(2, CancellationToken.None).AsTask().Wait())
        { IsBackground = true, Name = "ConcurrentObserverTest-Second" };
        secondThread.Start();

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        releaseSubscriber.TrySetResult();
        firstThread.Join(TimeSpan.FromSeconds(5));
        secondThread.Join(TimeSpan.FromSeconds(5));

        await Assert.That(result).IsTypeOf<ConcurrentObserverCallsException>();
    }

    /// <summary>
    /// Verifies that OnErrorResumeAsync_Private routes the original error to
    /// UnhandledExceptionHandler when the cancellation token is already cancelled
    /// and the token is already cancelled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorResumePrivateWithCancelledToken_ThenOriginalErrorRoutedToUnhandled()
    {
        var received = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        UnhandledExceptionHandler.Register(ex => received.TrySetResult(ex));

        var expectedError = new InvalidOperationException("original-error");
        var observer = new TestableObserverAsync();

        // Call the internal method directly with a pre-cancelled token
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await observer.OnErrorResumeAsync_Private(expectedError, cts.Token);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        await Assert.That(result).IsSameReferenceAs(expectedError);
    }

    /// <summary>
    /// Verifies that when OnErrorResumeAsyncCore throws OperationCanceledException the
    /// original error is routed to UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorResumeAsyncCoreThrowsOce_ThenOriginalErrorRoutedToUnhandled()
    {
        var received = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        UnhandledExceptionHandler.Register(ex => received.TrySetResult(ex));

        var originalError = new InvalidOperationException("original-error-oce-path");
        var observer = new TestableObserverAsync(
            onErrorResumeAsyncCore: (_, _) => throw new OperationCanceledException());

        await observer.OnErrorResumeAsync_Private(originalError, CancellationToken.None);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result).IsSameReferenceAs(originalError);
    }

    /// <summary>
    /// Verifies that when OnErrorResumeAsyncCore throws a non-cancellation exception
    /// the secondary exception is routed to UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorResumeAsyncCoreThrowsGenericException_ThenSecondaryRoutedToUnhandled()
    {
        var received = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        UnhandledExceptionHandler.Register(ex => received.TrySetResult(ex));

        var secondaryError = new ArithmeticException("handler-failed");
        var observer = new TestableObserverAsync(
            onErrorResumeAsyncCore: (_, _) => throw secondaryError);

        await observer.OnErrorResumeAsync_Private(new InvalidOperationException("original"), CancellationToken.None);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result).IsSameReferenceAs(secondaryError);
    }

    /// <summary>
    /// Verifies that disposing a CancelableTaskSubscription twice is safe; the second call returns immediately.
    /// Covers the early-return guard in CancelableTaskSubscription.DisposeAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCancelableTaskSubscriptionDisposedTwice_ThenSecondCallIsNoop()
    {
        var source = ObservableAsync.Return(42);
        var sub = await source.SubscribeAsync(
            (_, _) => default,
            null);

        await sub.DisposeAsync();

        // Second dispose should hit the early return guard
        await sub.DisposeAsync();
    }

    /// <summary>
    /// Verifies that CompleteWithFailureAsync routes to the unhandled exception handler
    /// when the observer's OnCompletedAsync itself throws.
    /// Covers the catch block in CancelableTaskSubscription.CompleteWithFailureAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCompleteWithFailureAsyncObserverThrows_ThenRoutedToUnhandledHandler()
    {
        List<Exception> handledErrors = [];
        UnhandledExceptionHandler.Register(handledErrors.Add);

        var completionException = new InvalidOperationException("observer completion throws");

        var observer = new RawThrowingOnCompletedObserver<int>(completionException);

        await CancelableTaskSubscription<int>.CompleteWithFailureAsync(
            observer,
            new InvalidOperationException("original error"));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => handledErrors.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(handledErrors).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(handledErrors[0]).IsSameReferenceAs(completionException);
    }
}
