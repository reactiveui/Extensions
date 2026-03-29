// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Subjects;
using AsyncObs = ReactiveUI.Extensions.Async.ObservableAsync;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for the bi-directional bridge between IObservable{T} and ObservableAsync{T},
/// including combined real-world scenarios.
/// </summary>
public class BridgeTests
{
    /// <summary>
    /// Tests that ToObservableAsync forwards all items from IObservable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenIObservableToObservableAsync_ThenForwardsAllItems()
    {
        var rxSource = Observable.Range(1, 5);
        var asyncObs = rxSource.ToObservableAsync();

        var result = await asyncObs.ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>
    /// Tests that ToObservableAsync forwards completion from IObservable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenIObservableToObservableAsync_ThenForwardsCompletion()
    {
        var rxSource = Observable.Empty<int>();
        var asyncObs = rxSource.ToObservableAsync();

        var result = await asyncObs.ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Tests that ToObservableAsync forwards errors from IObservable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenIObservableToObservableAsync_ThenForwardsError()
    {
        var ex = new InvalidOperationException("rx error");
        var rxSource = Observable.Throw<int>(ex);
        var asyncObs = rxSource.ToObservableAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await asyncObs.ToListAsync());
    }

    /// <summary>
    /// Tests that ToObservable forwards all items from ObservableAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObservableAsyncToObservable_ThenForwardsAllItems()
    {
        var asyncSource = AsyncObs.Range(1, 5);
        var rxObs = asyncSource.ToObservable();

        var result = await rxObs.ToList();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>
    /// Tests that ToObservable forwards completion from ObservableAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObservableAsyncToObservable_ThenForwardsCompletion()
    {
        var asyncSource = AsyncObs.Empty<int>();
        var rxObs = asyncSource.ToObservable();

        var result = await rxObs.ToList();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Tests that ToObservable forwards errors from ObservableAsync.
    /// </summary>
    [Test]
    public void WhenObservableAsyncToObservable_ThenForwardsError()
    {
        var asyncSource = AsyncObs.Throw<int>(new InvalidOperationException("async error"));
        var rxObs = asyncSource.ToObservable();

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await rxObs.ToList());
    }

    /// <summary>
    /// Tests that ToObservableAsync rejects null source.
    /// </summary>
    [Test]
    public void WhenToObservableAsyncNullSource_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IObservable<int>)null!).ToObservableAsync());
    }

    /// <summary>
    /// Tests that ToObservable rejects null source.
    /// </summary>
    [Test]
    public void WhenToObservableNullSource_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((ObservableAsync<int>)null!).ToObservable());
    }

    /// <summary>
    /// Tests round-trip IObservable through ObservableAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRoundTripIObservableThroughAsync_ThenPreservesSequence()
    {
        var rxSource = Observable.Range(1, 5);

        var roundTripped = rxSource
            .ToObservableAsync()
            .ToObservable();

        var result = await roundTripped.ToList();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>
    /// Tests round-trip ObservableAsync through IObservable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRoundTripAsyncThroughIObservable_ThenPreservesSequence()
    {
        var asyncSource = AsyncObs.Range(1, 5);

        var roundTripped = asyncSource
            .ToObservable()
            .ToObservableAsync();

        var result = await roundTripped.ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>
    /// Tests bridged IObservable with async operators.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBridgedObservableWithAsyncOperators_ThenPipelineWorks()
    {
        var rxSource = Observable.Range(1, 10);

        var result = await rxSource.ToObservableAsync()
            .Where(x => x % 2 == 0)
            .Select(x => x * 10)
            .Take(3)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([20, 40, 60]);
    }

    /// <summary>
    /// Tests async observable bridged to Rx with Rx operators.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncObservableWithRxOperators_ThenPipelineWorks()
    {
        var asyncSource = AsyncObs.Range(1, 10);

        var result = await asyncSource.ToObservable()
            .Where(x => x > 5)
            .Take(3)
            .Select(x => x * 2)
            .ToList();

        await Assert.That(result).IsEquivalentTo([12, 14, 16]);
    }

    /// <summary>
    /// Tests Rx Subject pushing data through async pipeline.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenIObservableSubjectToAsyncPipeline_ThenBidirectionalFlows()
    {
        var rxSubject = new Subject<int>();
        var asyncPipeline = rxSubject.ToObservableAsync()
            .Select(x => x * 2)
            .Where(x => x > 5);

        var items = new List<int>();
        await using var sub = await asyncPipeline.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        rxSubject.OnNext(1);  // 2 -> filtered
        rxSubject.OnNext(3);  // 6 -> passes
        rxSubject.OnNext(5);  // 10 -> passes
        rxSubject.OnNext(2);  // 4 -> filtered
        rxSubject.OnCompleted();

        await Task.Delay(200);

        await Assert.That(items).IsEquivalentTo([6, 10]);
    }

    /// <summary>
    /// Tests async subject bridged to Rx pipeline.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncSubjectToRxPipeline_ThenBidirectionalFlows()
    {
        var asyncSubject = SubjectAsync.Create<int>();
        var rxPipeline = asyncSubject.Values
            .Select(x => x + 100)
            .ToObservable();

        var items = new List<int>();
        using var sub = rxPipeline.Subscribe(x => items.Add(x));

        await asyncSubject.OnNextAsync(1, CancellationToken.None);
        await asyncSubject.OnNextAsync(2, CancellationToken.None);
        await asyncSubject.OnNextAsync(3, CancellationToken.None);
        await asyncSubject.OnCompletedAsync(Result.Success);

        await Task.Delay(200);

        await Assert.That(items).IsEquivalentTo([101, 102, 103]);
    }

    /// <summary>
    /// Tests merging bridged IObservable with native async observable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMixedRxAndAsyncMerge_ThenBothSourcesContribute()
    {
        var rxSource = Observable.Range(1, 3);
        var asyncSource = AsyncObs.Range(10, 3);

        var result = await rxSource.ToObservableAsync().Concat(asyncSource).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(6);
        await Assert.That(result).Contains(1);
        await Assert.That(result).Contains(10);
    }

    /// <summary>
    /// Tests concatenating bridged IObservable with async observable preserves order.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMixedConcatRxAndAsync_ThenOrderPreserved()
    {
        var rxSource = Observable.Range(1, 2);
        var asyncSource = AsyncObs.Range(3, 2);

        var result = await rxSource.ToObservableAsync()
            .Concat(asyncSource)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4]);
    }

    /// <summary>
    /// Tests SelectMany across bridged sources.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSelectManyBridgedSources_ThenFlattensCorrectly()
    {
        var rxSource = Observable.Range(1, 3);

        var result = await rxSource.ToObservableAsync()
            .SelectMany(x => AsyncObs.Return(x * 100))
            .ToListAsync();

        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result).Contains(100);
        await Assert.That(result).Contains(200);
        await Assert.That(result).Contains(300);
    }

    /// <summary>
    /// Tests Zip across bridged Rx and async sources.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipRxAndAsync_ThenPairsCorrectly()
    {
        var rxSource = Observable.Range(1, 3);
        var asyncSource = new[] { "a", "b", "c" }.ToObservableAsync();

        var result = await rxSource.ToObservableAsync()
            .Zip(asyncSource, (n, s) => $"{n}{s}")
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(["1a", "2b", "3c"]);
    }

    /// <summary>
    /// Tests that ProcessAsync swallows OperationCanceledException without routing to the handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenProcessAsyncThrowsOperationCanceled_ThenExceptionIsSwallowed()
    {
        var handlerCalled = false;
        var previousHandler = UnhandledExceptionHandler.CurrentHandler;
        UnhandledExceptionHandler.Register(_ => handlerCalled = true);

        try
        {
            ObservableBridgeExtensions.ObservableToObservableAsync<int>.BridgeObserver.ProcessAsync(
                static () => Task.FromException(new OperationCanceledException()));

            await Assert.That(handlerCalled).IsFalse();
        }
        finally
        {
            UnhandledExceptionHandler.Register(previousHandler);
        }
    }

    /// <summary>
    /// Tests that ProcessAsync routes general exceptions to the UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenProcessAsyncThrowsGeneralException_ThenRoutesToUnhandledHandler()
    {
        Exception? captured = null;
        var previousHandler = UnhandledExceptionHandler.CurrentHandler;
        UnhandledExceptionHandler.Register(e => captured = e);

        try
        {
            ObservableBridgeExtensions.ObservableToObservableAsync<int>.BridgeObserver.ProcessAsync(
                static () => Task.FromException(new InvalidOperationException("test error")));

            await Assert.That(captured).IsNotNull();
            await Assert.That(captured!.Message).IsEqualTo("test error");
        }
        finally
        {
            UnhandledExceptionHandler.Register(previousHandler);
        }
    }

    /// <summary>
    /// Tests that Enqueue returns early when the drain loop is already busy processing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnqueueCalledWhileDrainIsBusy_ThenSecondCallReturnsEarlyAndItemIsProcessed()
    {
        var items = new List<int>();
        var firstItemStarted = new TaskCompletionSource();
        var allowFirstToComplete = new TaskCompletionSource();

        var rxSource = new Subject<int>();
        var asyncObs = rxSource.ToObservableAsync();

        await using var sub = await asyncObs.SubscribeAsync(
            (x, _) =>
            {
                if (x == 1)
                {
                    firstItemStarted.TrySetResult();
                    allowFirstToComplete.Task.GetAwaiter().GetResult();
                }

                items.Add(x);
                return default;
            },
            null,
            null);

        // First OnNext starts drain loop and blocks inside the observer callback.
        // Must run on a background thread because DrainQueue blocks synchronously.
        _ = Task.Run(() => rxSource.OnNext(1));

        // Wait until observer callback is entered for item 1
        await firstItemStarted.Task;

        // Since the drain loop is blocked, Enqueue sees _busy==true and returns immediately,
        // so this call completes without blocking.
        var secondEnqueued = new TaskCompletionSource();
        _ = Task.Run(() =>
        {
            rxSource.OnNext(2);
            secondEnqueued.TrySetResult();
        });

        // Wait until the second item has been enqueued
        await secondEnqueued.Task;

        // Allow the first item to complete, which will drain item 2 as well
        allowFirstToComplete.TrySetResult();

        var conditionMet = await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count == 2,
            TimeSpan.FromSeconds(5));

        await Assert.That(conditionMet).IsTrue();
        await Assert.That(items).IsEquivalentTo([1, 2]);
    }

    /// <summary>
    /// Tests that ToObservable disposal catches OperationCanceledException when the subscription throws on cancel.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToObservableDisposalThrowsOperationCanceled_ThenExceptionIsSwallowed()
    {
        var source = AsyncObs.Create<int>(static (observer, ct) =>
            new ValueTask<IAsyncDisposable>(DisposableAsync.Create(
                static () => new ValueTask(Task.FromException(new OperationCanceledException())))));

        var handlerCalled = false;
        var previousHandler = UnhandledExceptionHandler.CurrentHandler;
        UnhandledExceptionHandler.Register(_ => handlerCalled = true);

        try
        {
            var rxObs = source.ToObservable();
            var sub = rxObs.Subscribe(_ => { });

            // Disposing triggers cancellation path that throws OperationCanceledException
            // This should be caught and swallowed
            sub.Dispose();

            await Assert.That(handlerCalled).IsFalse();
        }
        finally
        {
            UnhandledExceptionHandler.Register(previousHandler);
        }
    }

    /// <summary>
    /// Tests that ToObservable disposal catches general exceptions and routes them to the UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToObservableDisposalThrowsGeneralException_ThenRoutesToUnhandledHandler()
    {
        Exception? captured = null;
        var previousHandler = UnhandledExceptionHandler.CurrentHandler;
        UnhandledExceptionHandler.Register(e => captured = e);

        try
        {
            var source = AsyncObs.Create<int>(static (observer, ct) =>
                new ValueTask<IAsyncDisposable>(DisposableAsync.Create(
                    static () => new ValueTask(Task.FromException(new InvalidOperationException("dispose error"))))));

            var rxObs = source.ToObservable();
            var sub = rxObs.Subscribe(_ => { });

            // Disposing triggers disposal path that throws a general exception
            sub.Dispose();

            await Assert.That(captured).IsNotNull();
            await Assert.That(captured!.Message).IsEqualTo("dispose error");
        }
        finally
        {
            UnhandledExceptionHandler.Register(previousHandler);
        }
    }

    /// <summary>
    /// Tests that subscribing to a bridged IObservable with an already-cancelled token returns an empty disposable immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeWithCancelledToken_ThenReturnsEmptyDisposable()
    {
        var rxSource = Observable.Range(1, 5);
        var asyncObs = rxSource.ToObservableAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var items = new List<int>();
        await using var sub = await asyncObs.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null,
            cts.Token);

        await Assert.That(items).IsEmpty();
    }

    /// <summary>
    /// Tests that the BridgeObserver OnError method enqueues a failure completion on the async observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenIObservableEmitsError_ThenBridgeObserverForwardsAsFailureCompletion()
    {
        var rxSource = new Subject<int>();
        var asyncObs = rxSource.ToObservableAsync();
        var error = new InvalidOperationException("bridge error");

        var items = new List<int>();
        Result? completionResult = null;
        var completionReceived = new TaskCompletionSource();

        await using var sub = await asyncObs.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            result =>
            {
                completionResult = result;
                completionReceived.TrySetResult();
                return default;
            });

        RxSubjectOnNextThenError(rxSource, 42, error);

        var conditionMet = await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(conditionMet).IsTrue();
        await Assert.That(items).IsEquivalentTo([42]);
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
        await Assert.That(completionResult!.Value.Exception).IsSameReferenceAs(error);

        static void RxSubjectOnNextThenError(Subject<int> subject, int value, Exception ex)
        {
            subject.OnNext(value);
            subject.OnError(ex);
        }
    }

    /// <summary>
    /// Tests that disposing a ToObservable subscription before the async subscription task completes
    /// exercises the not-yet-completed disposal path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToObservableDisposedBeforeSubscriptionCompletes_ThenDisposalWaitsForSubscription()
    {
        var subscribeStarted = new TaskCompletionSource();
        var allowSubscribeToComplete = new TaskCompletionSource();

        var source = AsyncObs.Create<int>(async (observer, ct) =>
        {
            subscribeStarted.TrySetResult();
            await allowSubscribeToComplete.Task;
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var rxObs = source.ToObservable();
        var items = new List<int>();

        // Subscribe starts SubscribeAndCaptureAsync which will block
        var sub = rxObs.Subscribe(x => items.Add(x));

        // Wait until subscribe has started
        await subscribeStarted.Task;

        // Dispose on a background thread (it will block waiting for subscription to complete)
        var disposeCompleted = new TaskCompletionSource();
        _ = Task.Run(() =>
        {
            sub.Dispose();
            disposeCompleted.TrySetResult();
        });

        // Allow subscription to complete, which unblocks dispose
        allowSubscribeToComplete.TrySetResult();

        var conditionMet = await AsyncTestHelpers.WaitForConditionAsync(
            () => disposeCompleted.Task.IsCompleted,
            TimeSpan.FromSeconds(5));

        await Assert.That(conditionMet).IsTrue();
    }

    /// <summary>
    /// Tests that SubscribeAndCaptureAsync catches OperationCanceledException when subscription is cancelled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAndCaptureAsyncThrowsOperationCanceled_ThenReturnsNull()
    {
        var source = AsyncObs.Create<int>(static (observer, ct) =>
            throw new OperationCanceledException());

        var handlerCalled = false;
        var previousHandler = UnhandledExceptionHandler.CurrentHandler;
        UnhandledExceptionHandler.Register(_ => handlerCalled = true);

        try
        {
            var rxObs = source.ToObservable();
            using var sub = rxObs.Subscribe(_ => { });

            // The subscription should silently handle the OperationCanceledException
            // and not route to the unhandled exception handler
            await Assert.That(handlerCalled).IsFalse();
        }
        finally
        {
            UnhandledExceptionHandler.Register(previousHandler);
        }
    }

    /// <summary>
    /// Tests that SubscribeAndCaptureAsync catches general exceptions and routes them to the unhandled exception handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAndCaptureAsyncThrowsGeneralException_ThenRoutesToUnhandledHandler()
    {
        Exception? captured = null;
        var previousHandler = UnhandledExceptionHandler.CurrentHandler;
        UnhandledExceptionHandler.Register(e => captured = e);

        try
        {
            var source = AsyncObs.Create<int>(static (observer, ct) =>
                throw new InvalidOperationException("subscribe failed"));

            var rxObs = source.ToObservable();
            using var sub = rxObs.Subscribe(_ => { });

            var conditionMet = await AsyncTestHelpers.WaitForConditionAsync(
                () => captured is not null,
                TimeSpan.FromSeconds(5));

            await Assert.That(conditionMet).IsTrue();
            await Assert.That(captured!.Message).IsEqualTo("subscribe failed");
        }
        finally
        {
            UnhandledExceptionHandler.Register(previousHandler);
        }
    }

    /// <summary>
    /// Tests that the BridgeAsyncObserver forwards non-fatal errors to the synchronous observer via OnError.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncSourceEmitsErrorResume_ThenBridgeAsyncObserverForwardsToSyncOnError()
    {
        var directSource = AsyncTestHelpers.CreateDirectSource<int>();
        var rxObs = directSource.ToObservable();
        var error = new InvalidOperationException("resume error");

        var errorReceived = new TaskCompletionSource();
        Exception? receivedError = null;

        using var sub = rxObs.Subscribe(
            _ => { },
            ex =>
            {
                receivedError = ex;
                errorReceived.TrySetResult();
            });

        await directSource.EmitError(error);

        var conditionMet = await AsyncTestHelpers.WaitForConditionAsync(
            () => receivedError is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(conditionMet).IsTrue();
        await Assert.That(receivedError!.Message).IsEqualTo("resume error");
    }

    /// <summary>
    /// Tests that the BridgeAsyncObserver forwards failure completion to the synchronous observer via OnError.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncSourceCompletesWithFailure_ThenBridgeAsyncObserverForwardsToSyncOnError()
    {
        var directSource = AsyncTestHelpers.CreateDirectSource<int>();
        var rxObs = directSource.ToObservable();
        var error = new InvalidOperationException("completion failure");

        var errorReceived = new TaskCompletionSource();
        Exception? receivedError = null;

        using var sub = rxObs.Subscribe(
            _ => { },
            ex =>
            {
                receivedError = ex;
                errorReceived.TrySetResult();
            });

        await directSource.Complete(Result.Failure(error));

        var conditionMet = await AsyncTestHelpers.WaitForConditionAsync(
            () => receivedError is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(conditionMet).IsTrue();
        await Assert.That(receivedError!.Message).IsEqualTo("completion failure");
    }

    /// <summary>Tests ToObservable bridge disposal when subscription task is already completed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBridgeToObservableDisposedAfterCompletion_ThenCleansUp()
    {
        var source = new DirectSource<int>();
        var bridged = source.ToObservable();
        var items = new List<int>();

        var sub = bridged.Subscribe(x => items.Add(x));

        await source.EmitNext(42);
        await Task.Delay(50);

        sub.Dispose();

        await Assert.That(items).Contains(42);
    }
}
