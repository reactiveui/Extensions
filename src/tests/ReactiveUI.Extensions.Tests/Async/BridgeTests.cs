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
    /// <summary>Sentinel value (42) used by tests.</summary>
    private const int SentinelValue = 42;

    /// <summary>Sequence item two (2).</summary>
    private const int SequenceItemTwo = 2;

    /// <summary>Sequence item three (3).</summary>
    private const int SequenceItemThree = 3;

    /// <summary>Sequence item four (4).</summary>
    private const int SequenceItemFour = 4;

    /// <summary>Sequence item five (5).</summary>
    private const int SequenceItemFive = 5;

    /// <summary>Hoisted source array used by tests (was inline literal).</summary>
    private static readonly string[] SequenceABC = ["a", "b", "c"];

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

        await Assert.That(result).IsCollectionEqualTo([1, SequenceItemTwo, SequenceItemThree, SequenceItemFour, SequenceItemFive]);
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

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await asyncObs.ToListAsync());
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

        var result = rxObs.ToList().WaitForValue();

        await Assert.That(result).IsCollectionEqualTo([1, SequenceItemTwo, SequenceItemThree, SequenceItemFour, SequenceItemFive]);
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

        var result = rxObs.ToList().WaitForValue();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Tests that ToObservable forwards errors from ObservableAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObservableAsyncToObservable_ThenForwardsError()
    {
        var asyncSource = AsyncObs.Throw<int>(new InvalidOperationException("async error"));
        var rxObs = asyncSource.ToObservable();

        var error = rxObs.ToList().WaitForError();

        await Assert.That(error).IsTypeOf<InvalidOperationException>();
    }

    /// <summary>
    /// Tests that ToObservableAsync rejects null source.
    /// </summary>
    [Test]
    public void WhenToObservableAsyncNullSource_ThenThrowsArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() =>
            ((IObservable<int>)null!).ToObservableAsync());

    /// <summary>
    /// Tests that ToObservable rejects null source.
    /// </summary>
    [Test]
    public void WhenToObservableNullSource_ThenThrowsArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() =>
            ((ObservableAsync<int>)null!).ToObservable());

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

        var result = roundTripped.ToList().WaitForValue();

        await Assert.That(result).IsCollectionEqualTo([1, SequenceItemTwo, SequenceItemThree, SequenceItemFour, SequenceItemFive]);
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

        await Assert.That(result).IsCollectionEqualTo([1, SequenceItemTwo, SequenceItemThree, SequenceItemFour, SequenceItemFive]);
    }

    /// <summary>
    /// Tests bridged IObservable with async operators.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBridgedObservableWithAsyncOperators_ThenPipelineWorks()
    {
        const int ExpectedFirst = 20;
        const int ExpectedSecond = 40;
        const int ExpectedThird = 60;
        var rxSource = Observable.Range(1, 10);

        var result = await rxSource.ToObservableAsync()
            .Where(x => x % 2 == 0)
            .Select(x => x * 10)
            .Take(3)
            .ToListAsync();

        await Assert.That(result).IsCollectionEqualTo([ExpectedFirst, ExpectedSecond, ExpectedThird]);
    }

    /// <summary>
    /// Tests async observable bridged to Rx with Rx operators.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncObservableWithRxOperators_ThenPipelineWorks()
    {
        const int ExpectedFirst = 12;
        const int ExpectedSecond = 14;
        const int ExpectedThird = 16;
        var asyncSource = AsyncObs.Range(1, 10);

        var result = await asyncSource.ToObservable()
            .Where(x => x > 5)
            .Take(3)
            .Select(x => x * 2)
            .ToList();

        await Assert.That(result).IsCollectionEqualTo([ExpectedFirst, ExpectedSecond, ExpectedThird]);
    }

    /// <summary>
    /// Regression test for the re-entrant-dispose deadlock between <c>ToObservable()</c> and a
    /// downstream synchronous operator (e.g. <c>Take</c>) that calls <c>Dispose</c> from inside
    /// an <c>OnNext</c> while the producer pump is still emitting. If the bridge's
    /// <see cref="IDisposable.Dispose"/> blocks synchronously on the underlying
    /// <see cref="IAsyncDisposable"/>, the producer thread ends up waiting for itself to
    /// finish and the test hangs. Wrapped in a 10s deadline so a regression fails fast with a
    /// clear timeout instead of timing out the whole suite.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncObservableTakeDisposesFromInsideOnNext_ThenDoesNotDeadlock()
    {
        const int FirstItemsToConsume = 3;
        const int RangeLength = 10;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await AsyncObs.Range(1, RangeLength)
            .ToObservable()
            .Take(FirstItemsToConsume)
            .ToList()
            .ToTask(deadline.Token);

        await Assert.That(result).IsCollectionEqualTo([1, SequenceItemTwo, SequenceItemThree]);
    }

    /// <summary>
    /// Tests Rx Subject pushing data through async pipeline.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenIObservableSubjectToAsyncPipeline_ThenBidirectionalFlows()
    {
        const int Input1 = 3;
        const int Input2 = 5;
        const int Input3 = 2;
        const int ExpectedFirst = 6;
        const int ExpectedSecond = 10;
        var rxSubject = new Subject<int>();
        var asyncPipeline = rxSubject.ToObservableAsync()
            .Select(x => x * 2)
            .Where(x => x > 5);

        var items = new List<int>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await asyncPipeline.SubscribeAsync(
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

        rxSubject.OnNext(1); // 2 -> filtered
        rxSubject.OnNext(Input1); // 6 -> passes
        rxSubject.OnNext(Input2); // 10 -> passes
        rxSubject.OnNext(Input3); // 4 -> filtered
        rxSubject.OnCompleted();

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(items).IsCollectionEqualTo([ExpectedFirst, ExpectedSecond]);
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
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sub = rxPipeline.Subscribe(items.Add, () => completed.TrySetResult());

        const int ExpectedFirst = 101;
        const int ExpectedSecond = 102;
        const int ExpectedThird = 103;
        await asyncSubject.OnNextAsync(1, CancellationToken.None);
        await asyncSubject.OnNextAsync(SequenceItemTwo, CancellationToken.None);
        await asyncSubject.OnNextAsync(SequenceItemThree, CancellationToken.None);
        await asyncSubject.OnCompletedAsync(Result.Success);

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(items).IsCollectionEqualTo([ExpectedFirst, ExpectedSecond, ExpectedThird]);
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

        const int ExpectedTotalCount = 6;
        const int ExpectedAsyncStart = 10;
        var result = await rxSource.ToObservableAsync().Concat(asyncSource).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(ExpectedTotalCount);
        await Assert.That(result).Contains(1);
        await Assert.That(result).Contains(ExpectedAsyncStart);
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

        await Assert.That(result).IsCollectionEqualTo([1, SequenceItemTwo, SequenceItemThree, SequenceItemFour]);
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

        const int ExpectedCount = 3;
        const int ExpectedFirst = 100;
        const int ExpectedSecond = 200;
        const int ExpectedThird = 300;
        await Assert.That(result).Count().IsEqualTo(ExpectedCount);
        await Assert.That(result).Contains(ExpectedFirst);
        await Assert.That(result).Contains(ExpectedSecond);
        await Assert.That(result).Contains(ExpectedThird);
    }

    /// <summary>
    /// Tests Zip across bridged Rx and async sources.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipRxAndAsync_ThenPairsCorrectly()
    {
        var rxSource = Observable.Range(1, 3);
        var asyncSource = SequenceABC.ToObservableAsync();

        var result = await rxSource.ToObservableAsync()
            .Zip(asyncSource, (n, s) => $"{n}{s}")
            .ToListAsync();

        await Assert.That(result).IsCollectionEqualTo(["1a", "2b", "3c"]);
    }

    /// <summary>
    /// Tests that the bridge dispatcher swallows OperationCanceledException raised by the downstream async observer
    /// without routing it to the unhandled-exception handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBridgeDispatchThrowsOperationCanceled_ThenExceptionIsSwallowed()
    {
        var handlerCalled = false;
        var previousHandler = UnhandledExceptionHandler.CurrentHandler;
        UnhandledExceptionHandler.Register(_ => handlerCalled = true);

        try
        {
            var throwing = new ThrowingObserverAsync<int>(new OperationCanceledException());
            var bridge = new ObservableBridgeExtensions.ObservableToObservableAsync<int>.BridgeObserver(throwing, CancellationToken.None);

            bridge.OnNext(SentinelValue);

            await Assert.That(handlerCalled).IsFalse();
        }
        finally
        {
            UnhandledExceptionHandler.Register(previousHandler);
        }
    }

    /// <summary>
    /// Tests that the bridge dispatcher routes general exceptions raised by the downstream async observer to the
    /// unhandled-exception handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBridgeDispatchThrowsGeneralException_ThenRoutesToUnhandledHandler()
    {
        Exception? captured = null;
        var previousHandler = UnhandledExceptionHandler.CurrentHandler;
        UnhandledExceptionHandler.Register(e => captured = e);

        try
        {
            var throwing = new ThrowingObserverAsync<int>(new InvalidOperationException("test error"));
            var bridge = new ObservableBridgeExtensions.ObservableToObservableAsync<int>.BridgeObserver(throwing, CancellationToken.None);

            bridge.OnNext(SentinelValue);

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
        const int SecondItem = 2;
        var items = new List<int>();
        var firstItemStarted = new TaskCompletionSource();
        var allowFirstToComplete = new TaskCompletionSource();

        var rxSource = new Subject<int>();
        var asyncObs = rxSource.ToObservableAsync();

        await using var sub = await asyncObs.SubscribeAsync(
            async (x, _) =>
            {
                if (x == 1)
                {
                    firstItemStarted.TrySetResult();
                    await allowFirstToComplete.Task.ConfigureAwait(false);
                }

                items.Add(x);
            },
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
            rxSource.OnNext(SecondItem);
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
        await Assert.That(items).IsCollectionEqualTo([1, SecondItem]);
    }

    /// <summary>
    /// Tests that ToObservable disposal catches OperationCanceledException when the subscription throws on cancel.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToObservableDisposalThrowsOperationCanceled_ThenExceptionIsSwallowed()
    {
        var source = AsyncObs.Create<int>(static (_, _) =>
            new(DisposableAsync.Create(static () => new(Task.FromException(new OperationCanceledException())))));

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
            var source = AsyncObs.Create<int>(static (_, _) =>
                new(DisposableAsync.Create(static () =>
                    new(Task.FromException(new InvalidOperationException("dispose error"))))));

            var rxObs = source.ToObservable();
            var sub = rxObs.Subscribe(_ => { });

            // Disposing triggers disposal path that throws a general exception. The cleanup
            // (and therefore the routed unhandled exception) runs on a background task so the
            // calling thread is never blocked on async work, so the assertion has to wait for
            // the handler to be invoked.
            sub.Dispose();
            await AsyncTestHelpers.WaitForConditionAsync(
                () => captured is not null,
                TimeSpan.FromSeconds(5));

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
        await cts.CancelAsync();

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

        RxSubjectOnNextThenError(rxSource, SentinelValue, error);

        var conditionMet = await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(conditionMet).IsTrue();
        await Assert.That(items).IsCollectionEqualTo([SentinelValue]);
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

        var source = AsyncObs.Create<int>(async (observer, _) =>
        {
            subscribeStarted.TrySetResult();
            await allowSubscribeToComplete.Task;
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var rxObs = source.ToObservable();
        var items = new List<int>();

        // Subscribe starts SubscribeAndCaptureAsync which will block
        var sub = rxObs.Subscribe(items.Add);

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
        var source = AsyncObs.Create<int>(static (_, _) =>
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
            var source = AsyncObs.Create<int>(static (_, _) =>
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
        var directSource = new DirectSource<int>();
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
        var directSource = new DirectSource<int>();
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
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var sub = bridged.Subscribe(x =>
        {
            items.Add(x);
            received.TrySetResult();
        });

        await source.EmitNext(SentinelValue);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        sub.Dispose();

        await Assert.That(items).Contains(SentinelValue);
    }

    /// <summary>
    /// Async observer whose <see cref="OnNextAsync"/> returns a faulted ValueTask carrying a preconfigured
    /// exception. Used by the bridge dispatch tests to verify exception routing. Implements
    /// <see cref="IObserverAsync{T}"/> directly (not via <see cref="ObserverAsync{T}"/>) so the framework's
    /// internal exception-handling pipeline doesn't intercept the throw before it reaches the bridge.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="toThrow">The exception to throw on every <c>OnNext</c>.</param>
    private sealed class ThrowingObserverAsync<T>(Exception toThrow) : IObserverAsync<T>
    {
        /// <inheritdoc/>
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken) =>
            ValueTask.FromException(toThrow);

        /// <inheritdoc/>
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken) => default;

        /// <inheritdoc/>
        public ValueTask OnCompletedAsync(Result result) => default;

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => default;
    }
}
