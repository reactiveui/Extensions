// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for factory observables: Return, Empty, Throw, Never, Range, FromAsync, Defer, Create, Timer, Interval, ToAsyncObservable.
/// </summary>
public class FactoryObservableTests
{
    /// <summary>
    /// Tests Return emits single value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenReturnSingleValue_ThenEmitsValueAndCompletes()
    {
        var result = await ObservableAsync.Return(42).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(42);
    }

    /// <summary>
    /// Tests Return emits string.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenReturnString_ThenEmitsStringAndCompletes()
    {
        var result = await ObservableAsync.Return("hello").ToListAsync();

        await Assert.That(result).IsEquivalentTo(["hello"]);
    }

    /// <summary>
    /// Tests Empty completes with no items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEmpty_ThenCompletesWithNoItems()
    {
        var result = await ObservableAsync.Empty<int>().ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Tests Throw completes with exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrow_ThenCompletesWithException()
    {
        var ex = new InvalidOperationException("test error");
        var source = ObservableAsync.Throw<int>(ex);

        InvalidOperationException? thrown = null;
        try
        {
            await source.ToListAsync();
        }
        catch (InvalidOperationException caught)
        {
            thrown = caught;
        }

        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown!.Message).IsEqualTo("test error");
    }

    /// <summary>
    /// Tests Throw rejects null exception.
    /// </summary>
    [Test]
    public void WhenThrowNullException_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => ObservableAsync.Throw<int>(null!));
    }

    /// <summary>
    /// Tests Never does not complete within timeout.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenNever_ThenDoesNotCompleteWithinTimeout()
    {
        using var cts = new CancellationTokenSource(200);
        var items = new List<int>();
        var completed = false;

        await using var sub = await ObservableAsync.Never<int>().SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            _ =>
            {
                completed = true;
                return default;
            },
            cts.Token);

        await Task.Delay(250);

        await Assert.That(items).IsEmpty();
        await Assert.That(completed).IsFalse();
    }

    /// <summary>
    /// Tests Range from zero emits sequential integers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRangeFromZero_ThenEmitsSequentialIntegers()
    {
        var result = await ObservableAsync.Range(0, 5).ToListAsync();

        await Assert.That(result).IsEquivalentTo([0, 1, 2, 3, 4]);
    }

    /// <summary>
    /// Tests Range from non-zero emits correct range.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRangeFromNonZero_ThenEmitsCorrectRange()
    {
        var result = await ObservableAsync.Range(10, 3).ToListAsync();

        await Assert.That(result).IsEquivalentTo([10, 11, 12]);
    }

    /// <summary>
    /// Tests Range with count zero emits nothing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRangeCountZero_ThenEmitsNothing()
    {
        var result = await ObservableAsync.Range(0, 0).ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Tests FromAsync with value emits single value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFromAsyncWithValue_ThenEmitsSingleValue()
    {
        var source = ObservableAsync.FromAsync(async ct =>
        {
            await Task.Yield();
            return 99;
        });

        var result = await source.ToListAsync();

        await Assert.That(result).IsEquivalentTo([99]);
    }

    /// <summary>
    /// Tests FromAsync void executes action.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFromAsyncVoid_ThenEmitsUnit()
    {
        var executed = false;
        var source = ObservableAsync.FromAsync(async ct =>
        {
            await Task.Yield();
            executed = true;
        });

        await source.WaitCompletionAsync();

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests Defer creates new sequence per subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDefer_ThenCreatesNewSequencePerSubscription()
    {
        var counter = 0;
        var source = ObservableAsync.Defer(() =>
        {
            counter++;
            return ObservableAsync.Return(counter);
        });

        var first = await source.FirstAsync();
        var second = await source.FirstAsync();

        await Assert.That(first).IsEqualTo(1);
        await Assert.That(second).IsEqualTo(2);
    }

    /// <summary>
    /// Tests async Defer creates new sequence per subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDeferAsync_ThenCreatesNewSequencePerSubscription()
    {
        var counter = 0;
        var source = ObservableAsync.Defer(async ct =>
        {
            await Task.Yield();
            counter++;
            return ObservableAsync.Return(counter);
        });

        var first = await source.FirstAsync();
        var second = await source.FirstAsync();

        await Assert.That(first).IsEqualTo(1);
        await Assert.That(second).IsEqualTo(2);
    }

    /// <summary>
    /// Tests Create with custom subscription logic.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCreate_ThenCustomSubscriptionLogicRuns()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnNextAsync(2, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var result = await source.ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2]);
    }

    /// <summary>
    /// Tests Create with null subscribe function.
    /// </summary>
    [Test]
    public void WhenCreateWithNullSubscribeFunc_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Create<int>(null!));
    }

    /// <summary>
    /// Tests CreateAsBackgroundJob runs on background.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCreateAsBackgroundJob_ThenRunsOnBackground()
    {
        var source = ObservableAsync.CreateAsBackgroundJob<int>(async (observer, ct) =>
        {
            await Task.Yield();
            await observer.OnNextAsync(42, ct);
            await observer.OnCompletedAsync(Result.Success);
        });

        var result = await source.ToListAsync();

        await Assert.That(result).IsEquivalentTo([42]);
    }

    /// <summary>
    /// Tests Timer single shot emits one value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTimerSingleShot_ThenEmitsSingleValueAfterDelay()
    {
        var source = ObservableAsync.Timer(TimeSpan.FromMilliseconds(50));

        var result = await source.ToListAsync();

        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(0L);
    }

    /// <summary>
    /// Tests Timer periodic emits multiple values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTimerPeriodic_ThenEmitsMultipleValues()
    {
        var source = ObservableAsync.Timer(
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(50));

        var items = new List<long>();
        await using var sub = await source.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count >= 2,
            TimeSpan.FromSeconds(5));

        await Assert.That(items.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(items[0]).IsEqualTo(0L);
    }

    /// <summary>
    /// Tests Timer negative due time.
    /// </summary>
    [Test]
    public void WhenTimerNegativeDueTime_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Timer(TimeSpan.FromMilliseconds(-1)));
    }

    /// <summary>
    /// Tests Timer periodic with non-positive period.
    /// </summary>
    [Test]
    public void WhenTimerPeriodicNonPositivePeriod_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Timer(TimeSpan.Zero, TimeSpan.Zero));
    }

    /// <summary>
    /// Tests IEnumerable to ObservableAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEnumerableToObservableAsync_ThenEmitsAllItems()
    {
        var source = new[] { 1, 2, 3 }.ToObservableAsync();

        var result = await source.ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests IAsyncEnumerable to ObservableAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncEnumerableToObservableAsync_ThenEmitsAllItems()
    {
        var source = AsyncEnumerable().ToObservableAsync();

        var result = await source.ToListAsync();

        await Assert.That(result).IsEquivalentTo([10, 20, 30]);

        static async IAsyncEnumerable<int> AsyncEnumerable()
        {
            yield return 10;
            await Task.Yield();
            yield return 20;
            await Task.Yield();
            yield return 30;
        }
    }

    /// <summary>
    /// Tests Task to ObservableAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTaskToObservableAsync_ThenEmitsTaskResult()
    {
        var task = Task.FromResult(7);
        var source = task.ToObservableAsync();

        var result = await source.FirstAsync();

        await Assert.That(result).IsEqualTo(7);
    }

    /// <summary>
    /// Tests void Task to ObservableAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenVoidTaskToObservableAsync_ThenEmitsUnit()
    {
        var task = Task.CompletedTask;
        var source = task.ToObservableAsync();

        await source.WaitCompletionAsync();
    }

    /// <summary>
    /// Tests Interval emits periodic values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenIntervalWithCancellation_ThenEmitsPeriodicValues()
    {
        using var cts = new CancellationTokenSource();
        var source = ObservableAsync.Interval(TimeSpan.FromMilliseconds(50));

        var items = new List<long>();
        var received = false;
        try
        {
            await using var sub = await source.SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null,
                cts.Token);
            received = await AsyncTestHelpers.WaitForConditionAsync(
                () => items.Count >= 2,
                TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        }

        await Assert.That(received).IsTrue();
        await Assert.That(items.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(items[0]).IsEqualTo(1L);
    }

    /// <summary>
    /// Tests that EmitEnumerableAsync returns early when the cancellation token is already cancelled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenEmitEnumerableAsyncWithCancelledToken_ThenReturnsEarly()
    {
        var items = new List<int>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var observer = new AnonymousObserverAsync<int>((x, _) =>
        {
            items.Add(x);
            return default;
        });

        await ObservableAsync.EmitEnumerableAsync(Enumerable.Range(0, 100), observer, cts.Token);

        await Assert.That(items).IsEmpty();
    }

    /// <summary>
    /// Tests that the parameterless SubscribeAsync overload subscribes and disposes without error.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncParameterless_ThenSubscribesAndDisposes()
    {
        await using var sub = await ObservableAsync.Return(1).SubscribeAsync();

        await Assert.That(sub).IsNotNull();
    }

    /// <summary>
    /// Tests that SubscribeAsync with only an async onNext delegate and no cancellation token subscribes correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithOnNextAsyncOnly_ThenReceivesItems()
    {
        var items = new List<int>();
        var received = new TaskCompletionSource();

        await using var sub = await ObservableAsync.Return(77).SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                received.TrySetResult();
                return default;
            });

        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(items).IsEquivalentTo([77]);
    }

    /// <summary>
    /// Tests that SubscribeAsync with an async onNext delegate and explicit cancellation token subscribes correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithOnNextAsyncAndCancellationToken_ThenReceivesItems()
    {
        var items = new List<int>();
        var received = new TaskCompletionSource();
        using var cts = new CancellationTokenSource();

        await using var sub = await ObservableAsync.Return(55).SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                received.TrySetResult();
                return default;
            },
            cts.Token);

        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(items).IsEquivalentTo([55]);
    }

    /// <summary>
    /// Tests that the sync SubscribeAsync overload invokes the onErrorResume action when an error occurs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncOverloadWithError_ThenInvokesOnErrorResume()
    {
        var errorReceived = new TaskCompletionSource<Exception>();
        var source = ObservableAsync.Throw<int>(new InvalidOperationException("sync error"));

        await using var sub = await source.SubscribeAsync(
            _ => { },
            onErrorResume: ex => errorReceived.TrySetResult(ex),
            onCompleted: null);

        var error = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(error).IsTypeOf<InvalidOperationException>();
        await Assert.That(error.Message).IsEqualTo("sync error");
    }

    /// <summary>
    /// Tests that the sync SubscribeAsync overload invokes the onCompleted action when the sequence completes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncOverloadWithCompletion_ThenInvokesOnCompleted()
    {
        var completedResult = new TaskCompletionSource<Result>();

        await using var sub = await ObservableAsync.Return(1).SubscribeAsync(
            _ => { },
            onErrorResume: null,
            onCompleted: r => completedResult.TrySetResult(r));

        var result = await completedResult.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Tests that SubscribeAsync with null onErrorResume completes normally.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithNullOnErrorResume_ThenCompletesNormally()
    {
        var items = new List<int>();
        var completed = new TaskCompletionSource();

        await using var sub = await ObservableAsync.Return(42).SubscribeAsync(
            x => items.Add(x),
            onErrorResume: null,
            onCompleted: _ => completed.TrySetResult());

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(items).IsEquivalentTo([42]);
    }

    /// <summary>
    /// Tests that SubscribeAsync with null onCompleted completes normally.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithNullOnCompleted_ThenCompletesNormally()
    {
        var items = new List<int>();
        var received = new TaskCompletionSource();

        await using var sub = await ObservableAsync.Return(42).SubscribeAsync(
            x =>
            {
                items.Add(x);
                received.TrySetResult();
            },
            onErrorResume: _ => { },
            onCompleted: null);

        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(items).IsEquivalentTo([42]);
    }

    /// <summary>
    /// Verifies that FromAsync(Func of CancellationToken, ValueTask) throws ArgumentNullException when the factory is null.
    /// Covers the null guard in ObservableAsync.FromAsync.
    /// </summary>
    [Test]
    public void WhenFromAsyncWithNullFactory_ThenThrowsArgumentNull()
    {
        Func<CancellationToken, ValueTask> factory = null!;
        Assert.Throws<ArgumentNullException>(() => ObservableAsync.FromAsync(factory));
    }
}
