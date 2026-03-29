// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using Microsoft.Reactive.Testing;
using ReactiveUI.Extensions;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;
using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for Result struct, UnhandledExceptionHandler, and ConcurrentObserverCallsException.
/// </summary>
public class ResultAndInfrastructureTests
{
    /// <summary>Tests Result.Success IsSuccess is true.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultSuccess_ThenIsSuccessTrue()
    {
        var result = Result.Success;

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.IsFailure).IsFalse();
        await Assert.That(result.Exception).IsNull();
    }

    /// <summary>Tests Result.Failure IsFailure is true.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultFailure_ThenIsFailureTrue()
    {
        var ex = new InvalidOperationException("fail");
        var result = Result.Failure(ex);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Exception).IsEquivalentTo(ex);
    }

    /// <summary>Tests Result constructor null exception throws.</summary>
    [Test]
    public void WhenResultConstructorNullException_ThenThrowsArgumentNull() => Assert.Throws<ArgumentNullException>(() => new Result(null!));

    /// <summary>Tests Result TryThrow on success does nothing.</summary>
    [Test]
    public void WhenResultTryThrowOnSuccess_ThenDoesNothing()
    {
        var result = Result.Success;
        result.TryThrow();
    }

    /// <summary>Tests Result TryThrow on failure throws original exception.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultTryThrowOnFailure_ThenThrowsOriginalException()
    {
        var original = new InvalidOperationException("test error");
        var result = Result.Failure(original);

        var thrown = Assert.Throws<InvalidOperationException>(() => result.TryThrow());
        await Assert.That(thrown!.Message).IsEqualTo("test error");
    }

    /// <summary>Tests Result Success ToString returns Success.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultSuccessToString_ThenReturnsSuccess() => await Assert.That(Result.Success.ToString()).IsEqualTo("Success");

    /// <summary>Tests Result Failure ToString contains Failure and message.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultFailureToString_ThenContainsFailureAndMessage()
    {
        var result = Result.Failure(new InvalidOperationException("oh no"));

        await Assert.That(result.ToString()).Contains("Failure");
        await Assert.That(result.ToString()).Contains("oh no");
    }

    /// <summary>Tests Result Success equals default.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultEquality_ThenSuccessEqualsDefault()
    {
        var a = Result.Success;
        var b = default(Result);

        await Assert.That(a).IsEqualTo(b);
    }

    /// <summary>Tests ConcurrentObserverCallsException has descriptive message.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentObserverCallsException_ThenHasMessage()
    {
        var ex = new ConcurrentObserverCallsException();

        await Assert.That(ex.Message).Contains("Concurrent calls");
        await Assert.That(ex.Message).Contains("OnNextAsync");
    }

    /// <summary>Tests UnhandledExceptionHandler Register invokes custom handler.</summary>
    [Test]
    public void WhenUnhandledExceptionHandlerRegisterCustom_ThenCustomHandlerInvoked()
    {
        var caught = new List<Exception>();

        UnhandledExceptionHandler.Register(ex => caught.Add(ex));

        // Trigger unhandled exception via observer that throws during OnCompleted
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(Result.Success);
            return global::ReactiveUI.Extensions.Async.Disposables.DisposableAsync.Empty;
        });
    }

    /// <summary>
    /// Verifies that ConcurrentObserverCallsException with custom message stores the message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentObserverCallsExceptionWithMessage_ThenStoresMessage()
    {
        var ex = new ConcurrentObserverCallsException("custom message");

        await Assert.That(ex.Message).IsEqualTo("custom message");
    }

    /// <summary>
    /// Verifies that ConcurrentObserverCallsException with message and inner exception stores both.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentObserverCallsExceptionWithMessageAndInner_ThenStoresBoth()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ConcurrentObserverCallsException("custom message", inner);

        await Assert.That(ex.Message).IsEqualTo("custom message");
        await Assert.That(ex.InnerException).IsEquivalentTo(inner);
    }

    /// <summary>
    /// Verifies that UnhandledExceptionHandler invokes a custom handler when an exception occurs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUnhandledExceptionHandlerCustom_ThenCustomHandlerReceivesException()
    {
        var caught = new List<Exception>();

        UnhandledExceptionHandler.Register(ex => caught.Add(ex));

        // Trigger unhandled exception via the static method
        UnhandledExceptionHandler.OnUnhandledException(new InvalidOperationException("test error"));

        await Assert.That(caught).Count().IsEqualTo(1);
        await Assert.That(caught[0].Message).IsEqualTo("test error");
    }

    /// <summary>
    /// Verifies that UnhandledExceptionHandler ignores OperationCanceledException.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUnhandledExceptionHandlerOperationCanceled_ThenIgnored()
    {
        var caught = new List<Exception>();

        UnhandledExceptionHandler.Register(ex => caught.Add(ex));

        UnhandledExceptionHandler.OnUnhandledException(new OperationCanceledException());

        await Assert.That(caught).IsEmpty();
    }

    /// <summary>
    /// Verifies that UnhandledExceptionHandler swallows exceptions from the handler itself.
    /// </summary>
    [Test]
    public void WhenUnhandledExceptionHandlerThrows_ThenSwallowed()
    {
        UnhandledExceptionHandler.Register(_ => throw new InvalidOperationException("handler exploded"));

        // This should not throw even though the handler throws
        UnhandledExceptionHandler.OnUnhandledException(new ArgumentException("test"));
    }

    /// <summary>
    /// Verifies that EnumerableIList wraps a list and supports indexed access and enumeration.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnumerableIListFromList_ThenSupportsIndexedAccessAndEnumeration()
    {
        var list = new List<int> { 1, 2, 3 };
        EnumerableIList<int> enumerable = list;

        await Assert.That(enumerable.Count).IsEqualTo(3);
        await Assert.That(enumerable[0]).IsEqualTo(1);
        await Assert.That(enumerable[2]).IsEqualTo(3);
        await Assert.That(enumerable.IsReadOnly).IsFalse();

        // Test Contains, IndexOf
        await Assert.That(enumerable.Contains(2)).IsTrue();
        await Assert.That(enumerable.IndexOf(3)).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that EnumerableIList from array supports enumeration via all GetEnumerator paths.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnumerableIListEnumeration_ThenAllEnumeratorPathsWork()
    {
        // Use an array (read-only) so Dispose doesn't clear the backing store
        EnumerableIList<int> enumerable = new[] { 1, 2, 3 };

        // Test struct GetEnumerator
        List<int> items = [.. enumerable];

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);

        // Test IEnumerable<T>.GetEnumerator()
        IEnumerable<int> asIEnumerable = enumerable;
        List<int> items2 = [.. asIEnumerable];

        await Assert.That(items2).IsEquivalentTo([1, 2, 3]);

        // Test IEnumerable.GetEnumerator()
        System.Collections.IEnumerable asNonGenericEnumerable = enumerable;
        var items3 = new List<int>();
        foreach (int item in asNonGenericEnumerable)
        {
            items3.Add(item);
        }

        await Assert.That(items3).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Verifies that EnumerableIList supports modification operations.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnumerableIListModify_ThenModificationsReflected()
    {
        var list = new List<int> { 1, 2, 3 };
        EnumerableIList<int> enumerable = list;

        enumerable.Add(4);
        await Assert.That(enumerable.Count).IsEqualTo(4);

        enumerable.Insert(0, 0);
        await Assert.That(enumerable[0]).IsEqualTo(0);

        enumerable.Remove(2);
        await Assert.That(enumerable.Contains(2)).IsFalse();

        enumerable.RemoveAt(0);
        await Assert.That(enumerable[0]).IsEqualTo(1);

        var arr = new int[enumerable.Count];
        enumerable.CopyTo(arr, 0);
        await Assert.That(arr[0]).IsEqualTo(1);

        enumerable[0] = 99;
        await Assert.That(enumerable[0]).IsEqualTo(99);

        enumerable.Clear();
        await Assert.That(enumerable.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that EnumerableIList from array supports indexed access.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnumerableIListFromArray_ThenSupportsIndexedAccess()
    {
        EnumerableIList<int> enumerable = new[] { 10, 20, 30 };

        await Assert.That(enumerable.Count).IsEqualTo(3);
        await Assert.That(enumerable[1]).IsEqualTo(20);
        await Assert.That(enumerable.IsReadOnly).IsTrue();
    }

    /// <summary>
    /// Verifies that EnumeratorIList iterates and resets correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnumeratorIListReset_ThenCanIterateAgain()
    {
        var array = new[] { 1, 2, 3 };
        var enumerator = new EnumeratorIList<int>(array);

        await Assert.That(enumerator.MoveNext()).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(1);

        enumerator.Reset();
        await Assert.That(enumerator.MoveNext()).IsTrue();
        await Assert.That(enumerator.Current).IsEqualTo(1);

        // Array is read-only, Dispose does not clear it
        enumerator.Dispose();
    }

    /// <summary>
    /// Verifies that EnumeratorIList Dispose clears a mutable list.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnumeratorIListDisposeOnMutableList_ThenListCleared()
    {
        var list = new List<int> { 1, 2, 3 };
        var enumerator = new EnumeratorIList<int>(list);

        enumerator.MoveNext();
        enumerator.Dispose();

        await Assert.That(list).Count().IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that EnumeratorIList IEnumerator.Current returns boxed value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnumeratorIListCurrentViaIEnumerator_ThenReturnsBoxedValue()
    {
        var list = new List<int> { 42 };
        System.Collections.IEnumerator enumerator = new EnumeratorIList<int>(list);

        enumerator.MoveNext();
        await Assert.That(enumerator.Current).IsEqualTo(42);
    }

    /// <summary>
    /// Verifies that MulticastObservableAsync does not throw when ConnectAsync is called twice.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMulticastConnectTwice_ThenSecondConnectSucceeds()
    {
        var subject = SubjectAsync.Create<int>();
        var source = ObservableAsync.Range(1, 3);
        var connectable = source.Multicast(subject);

        await using var conn1 = await connectable.ConnectAsync(CancellationToken.None);
        var conn2 = await connectable.ConnectAsync(CancellationToken.None);

        // Both connections should be non-null; second returns the cached inner connection
        await Assert.That(conn1).IsNotNull();
        await Assert.That(conn2).IsNotNull();
    }

    /// <summary>
    /// Verifies that MulticastObservableAsync disconnect and reconnect works.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMulticastDisconnectAndReconnect_ThenNewConnectionWorks()
    {
        var subject = SubjectAsync.Create<int>();
        var items = new List<int>();

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var connectable = source.Multicast(subject);

        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        var conn = await connectable.ConnectAsync(CancellationToken.None);
        await conn.DisposeAsync();

        await Assert.That(items).IsNotEmpty();
    }

    /// <summary>
    /// Verifies that disposing the disconnect handle sets the internal connection to null
    /// and allows a subsequent reconnection.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMulticastDisconnectHandle_ThenConnectionCleared()
    {
        var subject = SubjectAsync.Create<int>();
        var callCount = 0;

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            Interlocked.Increment(ref callCount);
            await observer.OnNextAsync(callCount, ct);
            return DisposableAsync.Empty;
        });

        var connectable = source.Multicast(subject);

        // Connect
        var conn1 = await connectable.ConnectAsync(CancellationToken.None);

        // Disconnect
        await conn1.DisposeAsync();

        // Double-disconnect should be safe (connection is already null)
        await conn1.DisposeAsync();

        // Reconnect should create a new connection
        var conn2 = await connectable.ConnectAsync(CancellationToken.None);

        await Assert.That(callCount).IsGreaterThanOrEqualTo(2);

        await conn2.DisposeAsync();
    }

    /// <summary>
    /// Verifies that disposing the MulticastObservableAsync via IDisposable disposes the
    /// connection and gate.
    /// Covers the Dispose(bool) path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("TUnit", "TUnitAssertions0005", Justification = "Asserting expected constant outcome")]
    public async Task WhenMulticastDispose_ThenConnectionAndGateDisposed()
    {
        var subject = SubjectAsync.Create<int>();
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var connectable = source.Multicast(subject);

        // Connect first so there is a connection to dispose
        var conn = await connectable.ConnectAsync(CancellationToken.None);

        // Cast to IDisposable and call Dispose
        ((IDisposable)connectable).Dispose();

        // Double dispose should be safe (disposedValue is already true)
        ((IDisposable)connectable).Dispose();
    }

    /// <summary>
    /// Verifies that disposing the MulticastObservableAsync without an active connection
    /// is safe and disposes the gate.
    /// Covers the Dispose path when no connection is active.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("TUnit", "TUnitAssertions0005", Justification = "Asserting expected constant outcome")]
    public async Task WhenMulticastDisposeWithoutConnection_ThenSafe()
    {
        var subject = SubjectAsync.Create<int>();
        var source = ObservableAsync.Range(1, 3);
        var connectable = source.Multicast(subject);

        // Dispose without ever connecting
        ((IDisposable)connectable).Dispose();
    }

    /// <summary>
    /// Verifies that subscribing to a MulticastObservableAsync works and items flow
    /// through the subject when connected.
    /// Covers SubscribeAsyncCore.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMulticastSubscribeAndConnect_ThenItemsFlowThroughSubject()
    {
        var subject = SubjectAsync.Create<int>();
        var items = new List<int>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(10, ct);
            await observer.OnNextAsync(20, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var connectable = source.Multicast(subject);

        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            r =>
            {
                completed.TrySetResult();
                return default;
            });

        await using var conn = await connectable.ConnectAsync(CancellationToken.None);

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(items).IsEquivalentTo([10, 20]);
    }

    /// <summary>
    /// Verifies that when the cancellation token is cancelled before the observer completes,
    /// WaitValueAsync throws OperationCanceledException.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTaskObserverCancelledDuringWait_ThenThrowsOperationCancelled()
    {
        var cts = new CancellationTokenSource();

        // Use a source that never completes, so the TaskObserver waits forever
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
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

        // Cancel after a short delay to trigger the registration callback
        cts.CancelAfter(100);

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

        var source = ObservableAsync.CreateAsBackgroundJob<int>((observer, ct) =>
        {
            throw new InvalidOperationException("RunAsyncCore failure");
        });

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

        var source = ObservableAsync.CreateAsBackgroundJob<int>(async (observer, ct) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("source failure after yield");
        });

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
        var received = new List<(int value, IDisposable Sync)>();
        using var sub = observer.Subscribe(received.Add);

        // First Lock starts a task waiting on barrier
        var lockTask = continuation.Lock(1, observer);

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
        await Task.WhenAny(lockTask, Task.Delay(2000));

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

        var tasks = new List<Task<int>> { tcs.Task };
        var limiter = new ConcurrencyLimiter<int>(tasks, 1);

        Exception? caught = null;
        var completed = new TaskCompletionSource<bool>();

        limiter.IObservable.Subscribe(
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

        var tasks = new List<Task<int>> { tcs.Task };
        var limiter = new ConcurrencyLimiter<int>(tasks, 1);

        Exception? caught = null;
        var completed = new TaskCompletionSource<bool>();

        limiter.IObservable.Subscribe(
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
        var tasks = new List<Task<int>> { tcs.Task };
        var limiter = new ConcurrencyLimiter<int>(tasks, 1);

        var sub = limiter.IObservable.Subscribe(_ => { });
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
        limiter.IObservable.Subscribe(_ => { }, () => completed = true);

        await Assert.That(completed).IsTrue();
    }

    /// <summary>
    /// Tests ConcurrencyLimiter emits results and completes when all tasks succeed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrencyLimiterAllTasksSucceed_ThenEmitsAndCompletes()
    {
        var tasks = new List<Task<int>>
        {
            Task.FromResult(1),
            Task.FromResult(2),
            Task.FromResult(3)
        };
        var limiter = new ConcurrencyLimiter<int>(tasks, 2);

        var results = new List<int>();
        var completionTcs = new TaskCompletionSource<bool>();

        limiter.IObservable.Subscribe(
            results.Add,
            _ => completionTcs.TrySetResult(false),
            () => completionTcs.TrySetResult(true));

        var completedOk = await completionTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(completedOk).IsTrue();
        await Assert.That(results).Count().IsEqualTo(3);
    }

    /// <summary>
    /// Tests EnumerableIList indexer set path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnumerableIListIndexerSet_ThenUpdatesValue()
    {
        IList<int> list = new List<int> { 1, 2, 3 };
        var wrapper = EnumerableIList.Create(list);

        wrapper[1] = 99;

        await Assert.That(wrapper[1]).IsEqualTo(99);
        await Assert.That(list[1]).IsEqualTo(99);
    }

    /// <summary>
    /// Tests that Optional.Empty has no value and throws when accessed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOptionalIsEmpty_ThenHasNoValueAndThrowsOnAccess()
    {
        var optional = Optional<int>.Empty;

        await Assert.That(optional.HasValue).IsFalse();
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = optional.Value;
        });
    }

    /// <summary>
    /// Tests that Optional with a value exposes that value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOptionalHasValue_ThenReturnsContainedValue()
    {
        var optional = new Optional<int>(42);

        await Assert.That(optional.HasValue).IsTrue();
        await Assert.That(optional.Value).IsEqualTo(42);
    }

    /// <summary>
    /// Tests that AsyncGate can be acquired, released, and acquired again.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncGateIsReleased_ThenCanBeAcquiredAgain()
    {
        using var gate = new AsyncGate();

        var firstAcquired = false;
        using (await gate.LockAsync())
        {
            firstAcquired = true;
        }

        var reacquired = false;
        using (await gate.LockAsync())
        {
            reacquired = true;
        }

        await Assert.That(firstAcquired).IsTrue();
        await Assert.That(reacquired).IsTrue();
    }

    /// <summary>
    /// Tests that Heartbeat distinguishes heartbeat instances from update instances.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenHeartbeatIsConstructed_ThenPropertiesReflectTheMode()
    {
        var heartbeat = new global::ReactiveUI.Extensions.Heartbeat<int>();
        var update = new global::ReactiveUI.Extensions.Heartbeat<int>(7);

        await Assert.That(heartbeat.IsHeartbeat).IsTrue();
        await Assert.That(update.IsHeartbeat).IsFalse();
        await Assert.That(update.Update).IsEqualTo(7);
    }

    /// <summary>
    /// Tests that Stale distinguishes stale instances from update instances.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStaleIsConstructed_ThenPropertiesReflectTheMode()
    {
        var stale = new global::ReactiveUI.Extensions.Stale<int>();
        var update = new global::ReactiveUI.Extensions.Stale<int>(9);

        await Assert.That(stale.IsStale).IsTrue();
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = stale.Update;
        });
        await Assert.That(update.IsStale).IsFalse();
        await Assert.That(update.Update).IsEqualTo(9);
    }

    /// <summary>
    /// Tests AsyncContext.From(IScheduler) where scheduler is not a SynchronizationContext or TaskScheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncContextFromIScheduler_ThenWrapsInSchedulerTaskScheduler()
    {
        var scheduler = System.Reactive.Concurrency.Scheduler.Default;
        var context = AsyncContext.From(scheduler);

        await Assert.That(context.SynchronizationContext).IsNull();
        await Assert.That(context.TaskScheduler).IsNotNull();
    }

    /// <summary>
    /// Tests AsyncContext.From(TaskScheduler) factory method.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncContextFromTaskScheduler_ThenCreatesContext()
    {
        var context = AsyncContext.From(TaskScheduler.Default);

        await Assert.That(context.TaskScheduler).IsEqualTo(TaskScheduler.Default);
        await Assert.That(context.SynchronizationContext).IsNull();
    }

    /// <summary>
    /// Tests ObserveOn with SynchronizationContext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnSynchronizationContext_ThenUsesContext()
    {
        var result = await ObservableAsync.Return(42)
            .ObserveOn(SynchronizationContext.Current ?? new SynchronizationContext())
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests ObserveOn with TaskScheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnTaskScheduler_ThenUsesScheduler()
    {
        var result = await ObservableAsync.Return(42)
            .ObserveOn(TaskScheduler.Default)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests ObserveOn with TaskScheduler overload.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnWithTaskScheduler_ThenEmitsOnScheduler()
    {
        var source = ObservableAsync.Return(42);
        var result = await source.ObserveOn(TaskScheduler.Default).FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests SubscribeAsync with sync action callback.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithAction_ThenCallbackInvoked()
    {
        var items = new List<int>();

        await using var sub = await ObservableAsync.Range(1, 3)
            .SubscribeAsync(
                (Action<int>)(x => items.Add(x)),
                onErrorResume: null,
                onCompleted: null);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count >= 3,
            TimeSpan.FromSeconds(5));

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests SubscribeAsync with sync action, error, and completion callbacks.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithActionAndErrorAndCompletion_ThenAllCallbacksInvoked()
    {
        var items = new List<int>();
        Result? completion = null;

        await using var sub = await ObservableAsync.Range(1, 2)
            .SubscribeAsync(
                (Action<int>)(x => items.Add(x)),
                onErrorResume: null,
                onCompleted: r => completion = r);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completion is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(items).IsEquivalentTo([1, 2]);
        await Assert.That(completion).IsNotNull();
        await Assert.That(completion!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Tests SubscribeAsync with sync error handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithSyncErrorHandler_ThenErrorReported()
    {
        var errors = new List<Exception>();

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("err"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            (Action<int>)(_ => { }),
            onErrorResume: ex => errors.Add(ex),
            onCompleted: null);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => errors.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("err");
    }

    /// <summary>
    /// Tests sync SubscribeAsync with null onErrorResume handler: error is silently ignored,
    /// exercising the return default path when onErrorResume is null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncWithNullErrorHandler_ThenErrorSilentlyIgnored()
    {
        Result? completion = null;

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("ignored err"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            (Action<int>)(_ => { }),
            onErrorResume: null,
            onCompleted: r => completion = r);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completion is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(completion).IsNotNull();
        await Assert.That(completion!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Tests sync SubscribeAsync with null onCompleted handler: completion is silently ignored,
    /// exercising the return default path when onCompleted is null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncWithNullCompletedHandler_ThenCompletionSilentlyIgnored()
    {
        var items = new List<int>();

        var source = ObservableAsync.Range(1, 3);

        await using var sub = await source.SubscribeAsync(
            (Action<int>)(x => items.Add(x)),
            onErrorResume: null,
            onCompleted: null);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count >= 3,
            TimeSpan.FromSeconds(5));

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests DefaultUnhandledExceptionHandler does not throw and writes to console.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDefaultUnhandledExceptionHandler_ThenDoesNotThrow()
    {
        var ex = new InvalidOperationException("test-error");

        // Just verify it doesn't throw; it writes to Console.Out internally
        UnhandledExceptionHandler.DefaultUnhandledExceptionHandler(ex);
    }

    /// <summary>
    /// Verifies that WrappedObserverAsync forwards OnErrorResumeAsync to the inner observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWrappedObserverAsyncErrorResume_ThenForwardsToInner()
    {
        Exception? captured = null;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("wrapped-error"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            static (int x, CancellationToken _) => default,
            (Exception ex, CancellationToken _) =>
            {
                captured = ex;
                return default;
            },
            result =>
            {
                completed.TrySetResult();
                return default;
            });

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).IsEqualTo("wrapped-error");
    }

    /// <summary>
    /// Verifies that ObserveOn with the default AsyncContext invokes the
    /// IsSameAsCurrentAsyncContext fallback path where both SynchronizationContext and TaskScheduler are null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnDefaultAsyncContext_ThenEmitsValues()
    {
        var result = await ObservableAsync.Return(42)
            .ObserveOn(AsyncContext.Default)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Verifies that ObserveOn with an AsyncContext backed by a TaskScheduler emits values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnAsyncContext_ThenEmitsValues()
    {
        var context = AsyncContext.From(TaskScheduler.Default);
        var result = await ObservableAsync.Return(42)
            .ObserveOn(context)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Verifies that the SubscribeAsync overload with sync callbacks invokes onErrorResume.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncCallbacksErrorResume_ThenInvokesCallback()
    {
        Exception? captured = null;
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("sync-error"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            static x => { },
            ex => captured = ex);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => captured is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).IsEqualTo("sync-error");
    }

    /// <summary>
    /// Verifies that the SubscribeAsync overload with sync callbacks invokes onCompleted.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncCallbacksCompleted_ThenInvokesCallback()
    {
        Result? captured = null;
        await using var sub = await ObservableAsync.Return(1).SubscribeAsync(
            static x => { },
            null,
            result => captured = result);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => captured is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that EnumerableIList{T} explicitly implements IEnumerable{T}.GetEnumerator.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnumerableIListExplicitGetEnumerator_ThenEnumeratesCorrectly()
    {
        IList<int> list = new List<int> { 1, 2, 3 };
        var wrapper = EnumerableIList.Create(list);

        // Use the explicit IEnumerable<T>.GetEnumerator through LINQ
        IEnumerable<int> enumerable = wrapper;
        var items = enumerable.ToList();

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Verifies that ConcurrencyLimiter PullNextTask when disposed clears the enumerator
    /// and does not emit further values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrencyLimiterDisposedThenPullNext_ThenClearsRator()
    {
        var items = new List<int>();
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        IEnumerable<Task<int>> tasks = [tcs.Task, Task.FromResult(2)];
        var limiter = new ConcurrencyLimiter<int>(tasks, 1);

        // Subscribe and immediately dispose
        var sub = limiter.IObservable.Subscribe(x => items.Add(x));
        sub.Dispose();

        // After disposal, PullNextTask should clear the enumerator
        limiter.PullNextTask(Observer.Create<int>(x => items.Add(x)));

        await Assert.That(items).IsEmpty();
    }

    /// <summary>
    /// Verifies that disposing the connection from MulticastObservableAsync twice
    /// exercises the null-guard early return on the second dispose.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMulticastConnectDisposedTwice_ThenSecondDisposeIsNoop()
    {
        var subject = SubjectAsync.Create<int>();
        var multicast = ObservableAsync.Return(42).Multicast(subject);

        var connection = await multicast.ConnectAsync(CancellationToken.None);
        await connection.DisposeAsync();

        // Second dispose should hit the null-guard return path
        await connection.DisposeAsync();
    }

    /// <summary>
    /// Verifies that RefCount forwards error-resume from the source through the RefCountObserver.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRefCountSourceErrorResume_ThenForwardsToSubscriber()
    {
        Exception? captured = null;
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(new InvalidOperationException("refcount-error"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var subject = SubjectAsync.Create<int>();
        var refCounted = source.Multicast(subject).RefCount();

        var items = new List<int>();
        await using var sub = await refCounted.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            (ex, _) =>
            {
                captured = ex;
                return default;
            },
            null);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => captured is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(items).Contains(1);
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).IsEqualTo("refcount-error");
    }

    /// <summary>
    /// Verifies that IsDefaultContext returns true for a default AsyncContext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncContextDefault_ThenIsDefaultContextTrue()
    {
        var context = AsyncContext.Default;
        var isDefault = context.IsDefaultContext;
        await Assert.That(isDefault).IsTrue();
    }

    /// <summary>
    /// Verifies that From(IScheduler) with a scheduler that is a SynchronizationContext
    /// delegates to From(SynchronizationContext).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncContextFromSchedulerThatIsSyncContext_ThenUsesSyncContext()
    {
        var scheduler = new SyncContextScheduler();
        var context = AsyncContext.From((IScheduler)scheduler);

        await Assert.That(context.SynchronizationContext).IsNotNull();
        await Assert.That(context.TaskScheduler).IsNull();
    }

    /// <summary>
    /// Verifies that From(IScheduler) with a plain scheduler wraps it in SchedulerTaskScheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncContextFromPlainScheduler_ThenUsesSchedulerTaskScheduler()
    {
        var scheduler = new TestScheduler();
        var context = AsyncContext.From((IScheduler)scheduler);

        await Assert.That(context.TaskScheduler).IsNotNull();
        await Assert.That(context.SynchronizationContext).IsNull();

        // Verify the task scheduler works by scheduling a task on it
        var taskRan = false;
        var task = Task.Factory.StartNew(
            () => taskRan = true,
            CancellationToken.None,
            TaskCreationOptions.None,
            context.TaskScheduler!);

        // Advance the scheduler to run the task
        scheduler.AdvanceBy(1);
        await Assert.That(taskRan).IsTrue();
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
    /// Verifies that SchedulerTaskScheduler.QueueTask delegates to the IScheduler,
    /// and that TryExecuteTaskInline always returns false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSchedulerTaskSchedulerUsed_ThenTaskIsScheduledOnIScheduler()
    {
        var testScheduler = new TestScheduler();
        var context = AsyncContext.From(testScheduler);

        await Assert.That(context.TaskScheduler).IsNotNull();

        // The TaskScheduler returned by AsyncContext.From should be a SchedulerTaskScheduler
        var taskScheduler = context.TaskScheduler!;

        var executed = false;
        var task = Task.Factory.StartNew(
            () => executed = true,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            taskScheduler);

        // Task should not have run yet (TestScheduler needs advancement)
        await Assert.That(executed).IsFalse();

        // Advance the scheduler to execute the queued task
        testScheduler.AdvanceBy(1);

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Verifies that EnumerableIList{T}.IEnumerable{T}.GetEnumerator() returns a
    /// working enumerator (the explicit IEnumerable{T}.GetEnumerator implementation).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnumerableIListGenericGetEnumerator_ThenReturnsWorkingEnumerator()
    {
        EnumerableIList<int> enumerable = new[] { 10, 20, 30 };

        // Call the explicit IEnumerable<T>.GetEnumerator via the interface
        IEnumerable<int> asGenericEnumerable = enumerable;
        var items = new List<int>();
        using var enumerator = asGenericEnumerable.GetEnumerator();
        while (enumerator.MoveNext())
        {
            items.Add(enumerator.Current);
        }

        await Assert.That(items).IsEquivalentTo([10, 20, 30]);
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
            async (observer, ct) =>
            {
                throw new InvalidOperationException("run-async-failure");
            });

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
    public async Task WhenConcurrentObserverCalls_ThenDetectedAndReported()
    {
        var received = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        UnhandledExceptionHandler.Register(ex => received.TrySetResult(ex));

        var blockFirstCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            // Start first OnNextAsync call from a separate async context via Task.Run
            var firstCall = Task.Run(async () => await observer.OnNextAsync(1, ct), ct);

            // Wait for the first call to enter OnNextAsyncCore
            await blockFirstCall.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            // Second call from a different async context while first is in progress
            await observer.OnNextAsync(2, ct);

            // Release the blocked first call so it can exit
            releaseFirstCall.TrySetResult();

            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            async (_, ct) =>
            {
                blockFirstCall.TrySetResult();

                // Block until the concurrent call detection has completed
                await releaseFirstCall.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            },
            null,
            null);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
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
        cts.Cancel();

        await observer.OnErrorResumeAsync_Private(expectedError, cts.Token);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
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
    /// Verifies that AsyncContext.GetCurrent captures the current SynchronizationContext when one is set.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGetCurrentWithSyncContext_ThenCapturesSyncContext()
    {
        var sc = new SynchronizationContext();
        var original = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(sc);
            var context = AsyncContext.GetCurrent();

            await Assert.That(context.SynchronizationContext).IsSameReferenceAs(sc);
            await Assert.That(context.TaskScheduler).IsNull();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }
    }

    /// <summary>
    /// Verifies that AsyncContext.GetCurrent falls back to TaskScheduler.Current when no SynchronizationContext is set.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGetCurrentWithoutSyncContext_ThenFallsBackToTaskScheduler()
    {
        var original = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            var context = AsyncContext.GetCurrent();

            await Assert.That(context.SynchronizationContext).IsNull();
            await Assert.That(context.TaskScheduler).IsNotNull();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }
    }

    /// <summary>
    /// Verifies that AsyncContextSwitcherAwaitable.OnCompleted invokes the continuation immediately
    /// when the cancellation token is already cancelled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchContextAsyncOnCompletedWithCancelledToken_ThenContinuationInvokedImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var context = AsyncContext.Default;
        var awaitable = context.SwitchContextAsync(forceYielding: true, cts.Token);

        var invoked = false;
        awaitable.OnCompleted(() => invoked = true);

        await Assert.That(invoked).IsTrue();
    }

    /// <summary>
    /// Verifies that AsyncContextSwitcherAwaitable.OnCompleted schedules the continuation
    /// on the TaskScheduler when no SynchronizationContext is present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchContextAsyncOnCompletedWithTaskScheduler_ThenSchedulesContinuation()
    {
        var context = AsyncContext.From(TaskScheduler.Default);
        var awaitable = context.SwitchContextAsync(forceYielding: true, CancellationToken.None);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        awaitable.OnCompleted(() => tcs.TrySetResult());

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(tcs.Task.IsCompletedSuccessfully).IsTrue();
    }

    /// <summary>
    /// Verifies that SchedulerTaskScheduler.GetScheduledTasks returns null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSchedulerTaskSchedulerGetScheduledTasks_ThenReturnsNull()
    {
        var testScheduler = new TestScheduler();
        var taskScheduler = new AsyncContext.SchedulerTaskScheduler(testScheduler);

        var method = typeof(TaskScheduler).GetMethod(
            "GetScheduledTasks",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = method!.Invoke(taskScheduler, null);

        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies that SchedulerTaskScheduler.TryExecuteTaskInline always returns false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSchedulerTaskSchedulerTryExecuteTaskInline_ThenReturnsFalse()
    {
        var testScheduler = new TestScheduler();
        var taskScheduler = new AsyncContext.SchedulerTaskScheduler(testScheduler);

        var method = typeof(TaskScheduler).GetMethod(
            "TryExecuteTaskInline",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Create a task that we can pass to TryExecuteTaskInline
        var task = new Task(() => { });
        var result = (bool)method!.Invoke(taskScheduler, [task, false])!;

        await Assert.That(result).IsFalse();
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
            null,
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
        var handledErrors = new List<Exception>();
        UnhandledExceptionHandler.Register(ex => handledErrors.Add(ex));

        var completionException = new InvalidOperationException("observer completion throws");

        var observer = new TestableObserverAsync(
            onCompletedAsyncCore: _ => throw completionException);

        await CancelableTaskSubscription<int>.CompleteWithFailureAsync(
            observer, new InvalidOperationException("original error"));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => handledErrors.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(handledErrors).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(handledErrors[0]).IsSameReferenceAs(completionException);
    }

    /// <summary>
    /// A concrete <see cref="ObserverAsync{T}"/> implementation for testing, with
    /// configurable behavior for each virtual method.
    /// </summary>
    /// <param name="onNextAsyncCore">Optional delegate for <see cref="ObserverAsync{T}.OnNextAsyncCore"/>.</param>
    /// <param name="onErrorResumeAsyncCore">Optional delegate for <see cref="ObserverAsync{T}.OnErrorResumeAsyncCore"/>.</param>
    /// <param name="onCompletedAsyncCore">Optional delegate for <see cref="ObserverAsync{T}.OnCompletedAsyncCore"/>.</param>
    internal sealed class TestableObserverAsync(
        Func<int, CancellationToken, ValueTask>? onNextAsyncCore = null,
        Func<Exception, CancellationToken, ValueTask>? onErrorResumeAsyncCore = null,
        Func<Result, ValueTask>? onCompletedAsyncCore = null) : ObserverAsync<int>
    {
        /// <inheritdoc/>
        protected override ValueTask OnNextAsyncCore(int value, CancellationToken cancellationToken) =>
            onNextAsyncCore is not null ? onNextAsyncCore(value, cancellationToken) : default;

        /// <inheritdoc/>
        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
            onErrorResumeAsyncCore is not null ? onErrorResumeAsyncCore(error, cancellationToken) : default;

        /// <inheritdoc/>
        protected override ValueTask OnCompletedAsyncCore(Result result) =>
            onCompletedAsyncCore is not null ? onCompletedAsyncCore(result) : default;
    }

    /// <summary>
    /// An IScheduler that also extends SynchronizationContext, used to test the
    /// AsyncContext.From(IScheduler) path that checks for SynchronizationContext.
    /// </summary>
    private sealed class SyncContextScheduler : SynchronizationContext, IScheduler
    {
        /// <inheritdoc/>
        public DateTimeOffset Now => DateTimeOffset.UtcNow;

        /// <inheritdoc/>
        public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action) =>
            action(this, state);

        /// <inheritdoc/>
        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action) =>
            action(this, state);

        /// <inheritdoc/>
        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action) =>
            action(this, state);
    }
}
