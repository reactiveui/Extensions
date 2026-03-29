// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData;
using Microsoft.Reactive.Testing;
using ReactiveUI.Extensions;
using ReactiveUI.Extensions.Tests.Async;

namespace ReactiveUI.Extensions.Tests;

/// <summary>
/// Tests Reactive Extensions.
/// </summary>
public class ReactiveExtensionsTests
{
    /// <summary>
    /// Tests the WhereIsNotNull extension.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task GivenNull_WhenWhereIsNotNull_ThenNoNotification()
    {
        // Given, When
        bool? result = null;
        using var disposable = Observable.Return<bool?>(null).WhereIsNotNull().Subscribe(x => result = x);

        // Then
        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Tests the WhereIsNotNull extension.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task GivenValue_WhenWhereIsNotNull_ThenNotification()
    {
        // Given, When
        bool? result = null;
        using var disposable = Observable.Return<bool?>(false).WhereIsNotNull().Subscribe(x => result = x);

        // Then
        await Assert.That(result).IsFalse();
    }

    /// <summary>
    /// Tests the AsSignal extension.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task GivenObservable_WhenAsSignal_ThenNotifiesUnit()
    {
        // Given, When
        Unit? result = null;
        using var disposable = Observable.Return<bool?>(false).AsSignal().Subscribe(x => result = x);

        // Then
        await Assert.That(result).IsEqualTo(Unit.Default);
    }

    /// <summary>
    /// Syncronizes the asynchronous runs with asynchronous tasks in subscriptions.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SubscribeSynchronus_RunsWithAsyncTasksInSubscriptions()
    {
        // Given, When
        var result = 0;
        var itterations = 0;
        var subject = new Subject<bool>();
        using var disposable = subject
            .SubscribeSynchronous(async x =>
            {
                if (x)
                {
                    await Task.Delay(1000);
                    result++;
                }
                else
                {
                    await Task.Delay(500);
                    result--;
                }

                itterations++;
            });

        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);

        while (itterations < 6)
        {
            Thread.Yield();
        }

        // Then
        await Assert.That(result).IsZero();
    }

    /// <summary>
    /// Syncronizes the asynchronous runs with asynchronous tasks in subscriptions.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SyncronizeAsync_RunsWithAsyncTasksInSubscriptions()
    {
        // Given, When
        var result = 0;
        var itterations = 0;
        var subject = new Subject<bool>();
        var tasks = new List<Task>();
        using var disposable = subject
            .SynchronizeAsync()
            .Subscribe(x => tasks.Add(HandleAsync(x)));

        async Task HandleAsync((bool Value, IDisposable Sync) x)
        {
            try
            {
                if (x.Value)
                {
                    await Task.Delay(1000);
                    result++;
                }
                else
                {
                    await Task.Delay(500);
                    result--;
                }
            }
            finally
            {
                x.Sync.Dispose();
                itterations++;
            }
        }

        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);

        await Task.WhenAll(tasks);

        while (itterations < 6)
        {
            Thread.Yield();
        }

        // Then
        await Assert.That(result).IsZero();
    }

    /// <summary>
    /// Syncronizes the asynchronous runs with asynchronous tasks in subscriptions.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SynchronizeSynchronous_RunsWithAsyncTasksInSubscriptions()
    {
        // Given, When
        var result = 0;
        var itterations = 0;
        var subject = new Subject<bool>();
        var tasks = new List<Task>();
        using var disposable = subject
            .SynchronizeSynchronous()
            .Subscribe(x => tasks.Add(HandleAsync(x)));

        async Task HandleAsync((bool Value, IDisposable Sync) x)
        {
            try
            {
                if (x.Value)
                {
                    await Task.Delay(1000);
                    result++;
                }
                else
                {
                    await Task.Delay(500);
                    result--;
                }
            }
            finally
            {
                x.Sync.Dispose();
                itterations++;
            }
        }

        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);

        await Task.WhenAll(tasks);

        // Then
        await Assert.That(result).IsZero();
    }

    /// <summary>
    /// Syncronizes the asynchronous runs with asynchronous tasks in subscriptions.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SubscribeAsync_RunsWithAsyncTasksInSubscriptions()
    {
        // Given, When
        var result = 0;
        var itterations = 0;
        var subject = new Subject<bool>();
        using var disposable = subject
            .SubscribeAsync(async x =>
            {
                if (x)
                {
                    await Task.Delay(1000);
                    result++;
                }
                else
                {
                    await Task.Delay(500);
                    result--;
                }

                itterations++;
            });

        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);

        while (itterations < 6)
        {
            Thread.Yield();
        }

        // Then
        await Assert.That(result).IsZero();
    }

    /// <summary>
    /// Tests BufferUntil with character delimiters.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task BufferUntil_WithStartAndEndChars_BuffersCorrectly()
    {
        using var subject = new Subject<char>();
        var results = new List<string>();
        using var sub = subject.BufferUntil('<', '>').Subscribe(results.Add);

        subject.OnNext('a');
        subject.OnNext('<');
        subject.OnNext('t');
        subject.OnNext('e');
        subject.OnNext('s');
        subject.OnNext('t');
        subject.OnNext('>');
        subject.OnNext('b');
        subject.OnNext('<');
        subject.OnNext('d');
        subject.OnNext('a');
        subject.OnNext('t');
        subject.OnNext('a');
        subject.OnNext('>');
        subject.OnCompleted();

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(2);
            await Assert.That(results[0]).IsEqualTo("<test>");
            await Assert.That(results[1]).IsEqualTo("<data>");
        }
    }

    /// <summary>
    /// Tests BufferUntil emits remaining buffered content when the source completes before the end delimiter.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task BufferUntil_WhenSourceCompletesWithPartialBuffer_EmitsRemainingContent()
    {
        using var subject = new Subject<char>();
        var results = new List<string>();
        var completed = false;
        using var sub = subject.BufferUntil('<', '>').Subscribe(results.Add, () => completed = true);

        subject.OnNext('x');
        subject.OnNext('<');
        subject.OnNext('a');
        subject.OnNext('b');
        subject.OnCompleted();

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(1);
            await Assert.That(results[0]).IsEqualTo("<ab");
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Tests CatchIgnore without error action.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task CatchIgnore_OnError_ReturnsEmpty()
    {
        using var subject = new Subject<int>();
        var results = new List<int>();
        var completed = false;
        using var sub = subject.CatchIgnore().Subscribe(results.Add, _ => { }, () => completed = true);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnError(new InvalidOperationException());

        using (Assert.Multiple())
        {
            await Assert.That(results).IsEquivalentTo([1, 2]);
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Tests CatchIgnore with error action.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task CatchIgnore_WithErrorAction_CallsActionAndReturnsEmpty()
    {
        using var subject = new Subject<int>();
        var results = new List<int>();
        var errorCaught = false;
        var completed = false;
        using var sub = subject.CatchIgnore<int, InvalidOperationException>(ex => errorCaught = true)
            .Subscribe(results.Add, _ => { }, () => completed = true);

        subject.OnNext(1);
        subject.OnError(new InvalidOperationException());

        using (Assert.Multiple())
        {
            await Assert.That(results).IsEquivalentTo([1]);
            await Assert.That(errorCaught).IsTrue();
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllFalse.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task CombineLatestValuesAreAllFalse_WhenAllFalse_ReturnsTrue()
    {
        var subject1 = new BehaviorSubject<bool>(false);
        var subject2 = new BehaviorSubject<bool>(false);
        var sources = new[] { subject1.AsObservable(), subject2.AsObservable() };
        bool? result = null;
        using var sub = sources.CombineLatestValuesAreAllFalse().Subscribe(x => result = x);

        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllTrue.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task CombineLatestValuesAreAllTrue_WhenAllTrue_ReturnsTrue()
    {
        var subject1 = new BehaviorSubject<bool>(true);
        var subject2 = new BehaviorSubject<bool>(true);
        var sources = new[] { subject1.AsObservable(), subject2.AsObservable() };
        bool? result = null;
        using var sub = sources.CombineLatestValuesAreAllTrue().Subscribe(x => result = x);

        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Tests GetMax returns maximum value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task GetMax_WithMultipleSources_ReturnsMaximum()
    {
        var subject1 = new BehaviorSubject<int>(5);
        var subject2 = new BehaviorSubject<int>(10);
        var subject3 = new BehaviorSubject<int>(3);
        int? result = null;
        using var sub = subject1.GetMax(subject2, subject3).Subscribe(x => result = x);

        await Assert.That(result).IsEqualTo(10);
    }

    /// <summary>
    /// Tests GetMin returns minimum value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task GetMin_WithMultipleSources_ReturnsMinimum()
    {
        var subject1 = new BehaviorSubject<int>(5);
        var subject2 = new BehaviorSubject<int>(10);
        var subject3 = new BehaviorSubject<int>(3);
        int? result = null;
        using var sub = subject1.GetMin(subject2, subject3).Subscribe(x => result = x);

        await Assert.That(result).IsEqualTo(3);
    }

    /// <summary>
    /// Tests DetectStale marks stream as stale.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task DetectStale_WhenInactive_MarksAsStale()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IStale<int>>();
        using var sub = subject.DetectStale(TimeSpan.FromTicks(100), scheduler).Subscribe(results.Add);

        subject.OnNext(1);
        scheduler.AdvanceBy(50);
        subject.OnNext(2);
        scheduler.AdvanceBy(101);

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(3);
            await Assert.That(results[0].IsStale).IsFalse();
            await Assert.That(results[0].Update).IsEqualTo(1);
            await Assert.That(results[1].IsStale).IsFalse();
            await Assert.That(results[1].Update).IsEqualTo(2);
            await Assert.That(results[2].IsStale).IsTrue();
        }
    }

    /// <summary>
    /// Tests Conflate with minimum update period.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Conflate_WithMinimumPeriod_DelaysUpdates()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();
        using var sub = subject.Conflate(TimeSpan.FromTicks(100), scheduler).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        scheduler.AdvanceBy(100);

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(1);
            await Assert.That(results[0]).IsEqualTo(3);
        }
    }

    /// <summary>
    /// Tests Conflate completes after a pending delayed update has been emitted.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Conflate_WithPendingDelayedUpdate_CompletesAfterFlush()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();
        var completed = false;
        using var sub = subject.Conflate(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add, () => completed = true);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();

        await Assert.That(completed).IsFalse();

        scheduler.AdvanceBy(100);

        using (Assert.Multiple())
        {
            await Assert.That(results).IsEquivalentTo([2]);
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Tests Heartbeat injects heartbeats.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Heartbeat_WhenInactive_InjectsHeartbeats()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IHeartbeat<int>>();
        using var sub = subject.Heartbeat(TimeSpan.FromTicks(100), scheduler).Subscribe(results.Add);

        subject.OnNext(1);
        scheduler.AdvanceBy(101);
        subject.OnNext(2);
        scheduler.AdvanceBy(101);

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsGreaterThanOrEqualTo(3);
            await Assert.That(results[0].IsHeartbeat).IsFalse();
            await Assert.That(results[0].Update).IsEqualTo(1);
            await Assert.That(results[1].IsHeartbeat).IsTrue();
            await Assert.That(results[2].IsHeartbeat).IsFalse();
            await Assert.That(results[2].Update).IsEqualTo(2);
        }
    }

    /// <summary>
    /// Tests WithLimitedConcurrency.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task WithLimitedConcurrency_LimitsConcurrentTasks()
    {
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        IEnumerable<Task<int>> CreateTasks()
        {
            for (int i = 1; i <= 10; i++)
            {
                var value = i;
                yield return Task.Run(async () =>
                {
                    lock (lockObj)
                    {
                        currentConcurrent++;
                        maxConcurrent = Math.Max(maxConcurrent, currentConcurrent);
                    }

                    await Task.Delay(10);

                    lock (lockObj)
                    {
                        currentConcurrent--;
                    }

                    return value;
                });
            }
        }

        var results = await CreateTasks().WithLimitedConcurrency(3).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(10);
            await Assert.That(maxConcurrent).IsLessThanOrEqualTo(3);
        }
    }

    /// <summary>
    /// Tests OnNext with params.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task OnNext_WithMultipleValues_PushesAll()
    {
        var results = new List<int>();
        var subject = new Subject<int>();
        using var sub = subject.Subscribe(results.Add);

        subject.OnNext(1, 2, 3, 4, 5);

        await Assert.That(results).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>
    /// Tests ObserveOnSafe with null scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ObserveOnSafe_WithNullScheduler_ReturnsSource()
    {
        var source = Observable.Return(1);
        var result = source.ObserveOnSafe(null);

        await Assert.That(result).IsEquivalentTo(source);
    }

    /// <summary>
    /// Tests ObserveOnSafe with scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ObserveOnSafe_WithScheduler_ObservesOnScheduler()
    {
        var scheduler = new TestScheduler();
        var source = Observable.Return(1);
        int? result = null;
        using var sub = source.ObserveOnSafe(scheduler).Subscribe(x => result = x);

        await Assert.That(result).IsNull();

        scheduler.AdvanceBy(1);

        await Assert.That(result).IsEqualTo(1);
    }

    /// <summary>
    /// Tests Start with Action and null scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Start_WithActionAndNullScheduler_ExecutesAction()
    {
        var executed = false;
        Action action = () => executed = true;

        using var sub = ReactiveExtensions.Start(action, Scheduler.Immediate).Subscribe();

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests Start with function and null scheduler executes immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Start_WithFunctionAndNullScheduler_ReturnsComputedValue()
    {
        var result = await ReactiveExtensions.Start(() => 21 * 2, scheduler: null).ToTask();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests ForEach flattens enumerables.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ForEach_FlattensEnumerables()
    {
        var source = Observable.Return(new[] { 1, 2, 3 });
        var results = new List<int>();
        using var sub = source.ForEach().Subscribe(results.Add);

        await Assert.That(results).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests ScheduleSafe with null scheduler executes immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ScheduleSafe_WithNullScheduler_ExecutesImmediately()
    {
        var executed = false;
        IScheduler? scheduler = null;
        var disposable = scheduler.ScheduleSafe(() => executed = true);

        using (Assert.Multiple())
        {
            await Assert.That(executed).IsTrue();
            await Assert.That(disposable).IsNotNull();
        }
    }

    /// <summary>
    /// Tests FromArray with scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task FromArray_WithScheduler_EmitsElements()
    {
        var source = new[] { 1, 2, 3, 4, 5 };
        var results = new List<int>();
        using var sub = source.FromArray(Scheduler.Immediate).Subscribe(results.Add);

        await Assert.That(results).IsEquivalentTo(source);
    }

    /// <summary>
    /// Tests Filter with regex.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Filter_WithRegex_FiltersStrings()
    {
        var source = new[] { "test123", "hello", "test456", "world" }.ToObservable();
        var results = new List<string>();
        using var sub = source.Filter(@"^test\d+$").Subscribe(results.Add);

        await Assert.That(results).IsEquivalentTo(["test123", "test456"]);
    }

    /// <summary>
    /// Tests Shuffle randomizes array.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Shuffle_RandomizesArray()
    {
        var original = Enumerable.Range(1, 100).ToArray();
        var source = Observable.Return(original.ToArray());
        int[]? result = null;
        using var sub = source.Shuffle().Subscribe(x => result = x);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsNotNull();
            await Assert.That(result).Count().IsEqualTo(100);
            var sorted = result!.ToArray();
            Array.Sort(sorted);
            await Assert.That(sorted).IsEquivalentTo(original);
        }
    }

    /// <summary>
    /// Tests OnErrorRetry without parameters.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task OnErrorRetry_RetriesOnError()
    {
        var attempts = 0;
        var source = Observable.Create<int>(observer =>
        {
            attempts++;
            if (attempts < 3)
            {
                observer.OnError(new InvalidOperationException());
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        var results = new List<int>();
        using var sub = source.OnErrorRetry().Subscribe(results.Add);

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(1);
            await Assert.That(results[0]).IsEqualTo(42);
            await Assert.That(attempts).IsEqualTo(3);
        }
    }

    /// <summary>
    /// Tests TakeUntil with predicate.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task TakeUntil_WithPredicate_CompletesWhenPredicateTrue()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        using var sub = subject.TakeUntil(x => x >= 5).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(5);
        subject.OnNext(6);

        await Assert.That(results).IsEquivalentTo([1, 2, 5]);
    }

    /// <summary>
    /// Tests Not inverts boolean.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Not_InvertsBoolean()
    {
        var subject = new BehaviorSubject<bool>(true);
        bool? result = null;
        using var sub = subject.Not().Subscribe(x => result = x);

        await Assert.That(result).IsFalse();
    }

    /// <summary>
    /// Tests WhereTrue filters to true values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhereTrue_FiltersTrueValues()
    {
        var source = new[] { true, false, true, false, true }.ToObservable();
        var results = new List<bool>();
        using var sub = source.WhereTrue().Subscribe(results.Add);

        await Assert.That(results).IsEquivalentTo([true, true, true]);
    }

    /// <summary>
    /// Tests WhereFalse filters to false values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhereFalse_FiltersFalseValues()
    {
        var source = new[] { true, false, true, false, true }.ToObservable();
        var results = new List<bool>();
        using var sub = source.WhereFalse().Subscribe(results.Add);

        await Assert.That(results).IsEquivalentTo([false, false]);
    }

    /// <summary>
    /// Tests CatchAndReturn with fallback value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task CatchAndReturn_OnError_ReturnsFallback()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        using var sub = subject.CatchAndReturn(99).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnError(new Exception());

        await Assert.That(results).IsEquivalentTo([1, 2, 99]);
    }

    /// <summary>
    /// Tests Partition splits sequence.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Partition_SplitsSequence()
    {
        var subject = new Subject<int>();
        var trueResults = new List<int>();
        var falseResults = new List<int>();

        var (trueObs, falseObs) = subject.Partition(x => x % 2 == 0);

        using var trueSub = trueObs.Subscribe(trueResults.Add);
        using var falseSub = falseObs.Subscribe(falseResults.Add);

        for (int i = 1; i <= 10; i++)
        {
            subject.OnNext(i);
        }

        subject.OnCompleted();

        using (Assert.Multiple())
        {
            await Assert.That(trueResults).IsEquivalentTo([2, 4, 6, 8, 10]);
            await Assert.That(falseResults).IsEquivalentTo([1, 3, 5, 7, 9]);
        }
    }

    /// <summary>
    /// Tests WaitUntil takes first matching.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WaitUntil_TakesFirstMatching()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        using var sub = subject.WaitUntil(x => x > 5).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(3);
        subject.OnNext(7);
        subject.OnNext(9);

        await Assert.That(results).IsEquivalentTo([7]);
    }

    /// <summary>
    /// Tests DoOnSubscribe executes on subscribe.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task DoOnSubscribe_ExecutesOnSubscribe()
    {
        var executed = false;
        var source = Observable.Return(1);
        using var sub = source.DoOnSubscribe(() => executed = true).Subscribe();

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests DoOnDispose executes on dispose.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task DoOnDispose_ExecutesOnDispose()
    {
        var executed = false;
        var source = Observable.Never<int>();
        var sub = source.DoOnDispose(() => executed = true).Subscribe();

        sub.Dispose();

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests Heartbeat class with update.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Heartbeat_WithUpdate_IsNotHeartbeat()
    {
        var heartbeat = new Heartbeat<int>(42);

        using (Assert.Multiple())
        {
            await Assert.That(heartbeat.IsHeartbeat).IsFalse();
            await Assert.That(heartbeat.Update).IsEqualTo(42);
        }
    }

    /// <summary>
    /// Tests Heartbeat class without update.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Heartbeat_WithoutUpdate_IsHeartbeat()
    {
        var heartbeat = new Heartbeat<int>();

        await Assert.That(heartbeat.IsHeartbeat).IsTrue();
    }

    /// <summary>
    /// Tests Stale class with update.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Stale_WithUpdate_IsNotStale()
    {
        var stale = new Stale<int>(42);

        using (Assert.Multiple())
        {
            await Assert.That(stale.IsStale).IsFalse();
            await Assert.That(stale.Update).IsEqualTo(42);
        }
    }

    /// <summary>
    /// Tests Stale class without update.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Stale_WithoutUpdate_IsStale()
    {
        var stale = new Stale<int>();

        await Assert.That(stale.IsStale).IsTrue();
    }

    /// <summary>
    /// Tests Stale throws on Update access when stale.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Stale_WithoutUpdate_ThrowsOnUpdateAccess()
    {
        var stale = new Stale<int>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = stale.Update;
        });

        await Assert.That(ex).IsNotNull();
    }

    /// <summary>
    /// Tests Continuation can be disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Continuation_CanBeDisposed()
    {
        var continuation = new Continuation();

        continuation.Dispose();
    }

    /// <summary>
    /// Tests Continuation tracks completed phases.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Continuation_TracksCompletedPhases()
    {
        using var continuation = new Continuation();

        var phases = continuation.CompletedPhases;

        await Assert.That(phases).IsGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Tests WhereIsNotNull filters null values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhereIsNotNull_FiltersNullValues()
    {
        var source = new[] { "a", null, "b", null, "c" }.ToObservable();
        var results = new List<string>();
        using var sub = source.WhereIsNotNull().Subscribe(x => results.Add(x!));

        await Assert.That(results).IsEquivalentTo(["a", "b", "c"]);
    }

    /// <summary>
    /// Tests AsSignal converts to Unit.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task AsSignal_ConvertsToUnit()
    {
        var source = Observable.Range(1, 3);
        var results = new List<Unit>();
        using var sub = source.AsSignal().Subscribe(results.Add);

        await Assert.That(results).Count().IsEqualTo(3);
    }

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
        subject.OnNext(2);
        scheduler.AdvanceBy(101);

        await Assert.That(results).IsNotEmpty();
        await Assert.That(results[0]).IsEqualTo(1);
    }

    /// <summary>
    /// Tests RetryWithBackoff respects max delay.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task RetryWithBackoff_RespectsMaxDelay()
    {
        var attempts = 0;
        var source = Observable.Create<int>(observer =>
        {
            attempts++;
            if (attempts < 5)
            {
                observer.OnError(new InvalidOperationException());
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        var result = source.RetryWithBackoff(
            maxRetries: 10,
            initialDelay: TimeSpan.FromMilliseconds(10),
            backoffFactor: 2.0,
            maxDelay: TimeSpan.FromMilliseconds(50))
            .Wait();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests SynchronizeSynchronous provides sync lock.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SynchronizeSynchronous_ProvidesSyncLock()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        IDisposable? lastSync = null;

        using var sub = subject.SynchronizeSynchronous().Subscribe(tuple =>
        {
            results.Add(tuple.Value);
            lastSync = tuple.Sync;
            tuple.Sync.Dispose(); // Must dispose sync lock to allow next item to process
        });

        subject.OnNext(1);

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(1);
            await Assert.That(lastSync).IsNotNull();
        }
    }

    /// <summary>
    /// Tests SynchronizeAsync provides sync lock.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SynchronizeAsync_ProvidesSyncLock()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        IDisposable? lastSync = null;

        using var sub = subject.SynchronizeAsync().Subscribe(tuple =>
        {
            results.Add(tuple.Value);
            lastSync = tuple.Sync;
        });

        subject.OnNext(1);

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(1);
            await Assert.That(lastSync).IsNotNull();
        }
    }

    /// <summary>
    /// Tests SubscribeAsync with onNext and onError.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SubscribeAsync_WithOnNextAndOnError_HandlesError()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        Exception? caughtException = null;

        using var sub = subject.SubscribeAsync(
            x =>
            {
                results.Add(x);
                return Task.CompletedTask;
            },
            ex => caughtException = ex);

        subject.OnNext(1);

        var onNextProcessed = await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count == 1,
            TimeSpan.FromSeconds(5));

        subject.OnError(new InvalidOperationException());

        var errorReceived = await AsyncTestHelpers.WaitForConditionAsync(
            () => caughtException != null,
            TimeSpan.FromSeconds(5));

        using (Assert.Multiple())
        {
            await Assert.That(onNextProcessed).IsTrue();
            await Assert.That(errorReceived).IsTrue();
            await Assert.That(results).IsEquivalentTo([1]);
            await Assert.That(caughtException).IsNotNull();
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with error action and retry count.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task OnErrorRetry_WithErrorActionAndRetryCount_RetriesLimitedTimes()
    {
        var attempts = 0;
        var errorCount = 0;
        var source = Observable.Create<int>(observer =>
        {
            attempts++;
            observer.OnError(new InvalidOperationException());
            return System.Reactive.Disposables.Disposable.Empty;
        });

        Exception? caughtException = null;

        using var sub = source.OnErrorRetry<int, InvalidOperationException>(ex => errorCount++, retryCount: 3)
            .Subscribe(_ => { }, ex => caughtException = ex);

        using (Assert.Multiple())
        {
            await Assert.That(attempts).IsEqualTo(3);
            await Assert.That(caughtException).IsNotNull();
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with delay.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task OnErrorRetry_WithDelay_DelaysRetries()
    {
        var attempts = 0;
        var source = Observable.Create<int>(observer =>
        {
            attempts++;
            if (attempts < 3)
            {
                observer.OnError(new InvalidOperationException());
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        var startTime = DateTime.Now;

        var result = source.OnErrorRetry<int, InvalidOperationException>(
            ex => { },
            retryCount: 5,
            delay: TimeSpan.FromMilliseconds(50))
            .Wait();

        var elapsed = DateTime.Now - startTime;

        using (Assert.Multiple())
        {
            await Assert.That(result).IsEqualTo(42);
            await Assert.That(elapsed.TotalMilliseconds).IsGreaterThanOrEqualTo(90);
        }
    }

    /// <summary>
    /// Tests Schedule with value and TimeSpan and function.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Schedule_WithValueTimeSpanAndFunction_DelaysAndTransforms()
    {
        var scheduler = new TestScheduler();
        int? result = null;

        using var sub = 10.Schedule(TimeSpan.FromTicks(100), scheduler, x => x * 2)
            .Subscribe(x => result = x);

        await Assert.That(result).IsNull();

        scheduler.AdvanceBy(101);

        await Assert.That(result).IsEqualTo(20);
    }

    /// <summary>
    /// Tests Schedule with observable, TimeSpan and function.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Schedule_WithObservableTimeSpanAndFunction_DelaysAndTransforms()
    {
        var scheduler = new TestScheduler();
        var source = Observable.Return(10);
        var results = new List<int>();

        using var sub = source.Schedule(TimeSpan.FromTicks(100), scheduler, x => x * 2)
            .Subscribe(results.Add);

        await Assert.That(results).IsEmpty();

        scheduler.AdvanceBy(101);

        await Assert.That(results).IsEquivalentTo([20]);
    }

    /// <summary>
    /// Tests GetMin tracking minimum values as sources change over time.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task GetMin_TracksMinimumOverTime()
    {
        var subject1 = new BehaviorSubject<int>(5);
        var subject2 = new BehaviorSubject<int>(10);
        var subject3 = new BehaviorSubject<int>(3);

        subject1
            .GetMin(subject2, subject3)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        // Initial minimum is 3
        await Assert.That(results).IsEquivalentTo([3]);

        // Change minimum to 1
        subject3.OnNext(1);
        await Assert.That(results).IsEquivalentTo([3, 1]);

        // Change minimum to 0
        subject1.OnNext(0);
        await Assert.That(results).IsEquivalentTo([3, 1, 0]);
    }

    /// <summary>
    /// Tests GetMax tracking maximum values as sources change over time.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task GetMax_TracksMaximumOverTime()
    {
        var subject1 = new BehaviorSubject<int>(5);
        var subject2 = new BehaviorSubject<int>(10);
        var subject3 = new BehaviorSubject<int>(3);

        subject1
            .GetMax(subject2, subject3)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        // Initial maximum is 10
        await Assert.That(results).IsEquivalentTo([10]);

        // Change maximum to 15
        subject2.OnNext(15);
        await Assert.That(results).IsEquivalentTo([10, 15]);

        // Change maximum to 20
        subject1.OnNext(20);
        await Assert.That(results).IsEquivalentTo([10, 15, 20]);
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllTrue tracking state changes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task CombineLatestValuesAreAllTrue_TracksStateChanges()
    {
        var subject1 = new BehaviorSubject<bool>(false);
        var subject2 = new BehaviorSubject<bool>(false);
        var subject3 = new BehaviorSubject<bool>(false);

        new[] { subject1, subject2, subject3 }
            .CombineLatestValuesAreAllTrue()
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        // Initially all false
        await Assert.That(results).IsEquivalentTo([false]);

        // One true, still false
        subject1.OnNext(true);
        await Assert.That(results).IsEquivalentTo([false, false]);

        // Two true, still false
        subject2.OnNext(true);
        await Assert.That(results).IsEquivalentTo([false, false, false]);

        // All true
        subject3.OnNext(true);
        await Assert.That(results).IsEquivalentTo([false, false, false, true]);

        // Back to false
        subject1.OnNext(false);
        await Assert.That(results).IsEquivalentTo([false, false, false, true, false]);
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllFalse tracking state changes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task CombineLatestValuesAreAllFalse_TracksStateChanges()
    {
        var subject1 = new BehaviorSubject<bool>(false);
        var subject2 = new BehaviorSubject<bool>(false);
        var subject3 = new BehaviorSubject<bool>(false);

        new[] { subject1, subject2, subject3 }
            .CombineLatestValuesAreAllFalse()
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        // Initially all false - result is true
        await Assert.That(results).IsEquivalentTo([true]);

        // One becomes true - result becomes false
        subject1.OnNext(true);
        await Assert.That(results).IsEquivalentTo([true, false]);

        // Back to false - result becomes true
        subject1.OnNext(false);
        await Assert.That(results).IsEquivalentTo([true, false, true]);

        // Another becomes true - result becomes false
        subject2.OnNext(true);
        await Assert.That(results).IsEquivalentTo([true, false, true, false]);
    }

    /// <summary>
    /// Tests WhereIsNotNull filtering nulls over time.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhereIsNotNull_FiltersNullsOverTime()
    {
        var subject = new Subject<string?>();
        var results = new List<string?>();

        subject.WhereIsNotNull()
            .Subscribe(x => results.Add(x));

        subject.OnNext("first");
        subject.OnNext(null);
        subject.OnNext("second");
        subject.OnNext(null);
        subject.OnNext("third");

        // Only non-null values collected
        await Assert.That(results).IsEquivalentTo(new string?[] { "first", "second", "third" });
    }

    /// <summary>
    /// Tests Not operator inverting boolean values over time.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Not_InvertsBooleanValuesOverTime()
    {
        var subject = new Subject<bool>();
        var results = new List<bool>();

        subject.Not()
            .Subscribe(results.Add);

        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);

        await Assert.That(results).IsEquivalentTo([false, true, false, true]);
    }

    /// <summary>
    /// Tests AsSignal converting values to Unit over time.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task AsSignal_ConvertsToUnitOverTime()
    {
        var subject = new Subject<int>();
        var results = new List<Unit>();

        subject.AsSignal()
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        // All values converted to Unit.Default
        await Assert.That(results).Count().IsEqualTo(3);
        await Assert.That(results).All(x => x == Unit.Default);
    }

    /// <summary>
    /// Tests SyncTimer creates shared observable that produces ticks.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SyncTimer_ProducesSharedTicks()
    {
        var timeSpan = TimeSpan.FromMilliseconds(100);
        var scheduler = new TestScheduler();
        var results1 = new List<DateTime>();
        var results2 = new List<DateTime>();

        using var sub1 = ReactiveExtensions.SyncTimer(timeSpan, scheduler).Take(2).Subscribe(results1.Add);
        using var sub2 = ReactiveExtensions.SyncTimer(timeSpan, scheduler).Take(2).Subscribe(results2.Add);

        scheduler.AdvanceBy(timeSpan.Ticks * 2);

        using (Assert.Multiple())
        {
            // Both subscriptions should get ticks (shared timer)
            await Assert.That(results1).Count().IsGreaterThanOrEqualTo(1);
            await Assert.That(results2).Count().IsGreaterThanOrEqualTo(1);
        }
    }

    /// <summary>
    /// Tests Using with action executes the action.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Using_WithAction_ExecutesActionImmediately()
    {
        var executed = false;
        using var disposable = System.Reactive.Disposables.Disposable.Create(() => { });

        disposable.Using(d => executed = true, Scheduler.Immediate).Subscribe();

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests Using with action and null scheduler executes immediately.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Using_WithActionAndNullScheduler_ExecutesActionImmediately()
    {
        var executed = false;
        using var disposable = System.Reactive.Disposables.Disposable.Create(() => { });

        await disposable.Using(d => executed = true, scheduler: null).ToTask();

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests Using with function transforms the value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Using_WithFunction_TransformsValue()
    {
        using var disposable = System.Reactive.Disposables.Disposable.Create(() => { });
        var result = 0;

        disposable.Using(d => 42, Scheduler.Immediate).Subscribe(r => result = r);

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests Schedule with TimeSpan and action.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Schedule_WithTimeSpanAndAction_ExecutesAction()
    {
        var executed = false;
        var value = 42;

        value.Schedule(TimeSpan.FromMilliseconds(10), Scheduler.Immediate, v => executed = true)
            .Subscribe();

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests Schedule with observable, DateTimeOffset and action delays emission until the scheduler advances.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Schedule_WithObservableDateTimeOffsetAndAction_DelaysAndExecutesAction()
    {
        var scheduler = new TestScheduler();
        var dueTime = scheduler.Now.AddTicks(100);
        var subject = new Subject<int>();
        var executed = false;
        var results = new List<int>();

        using var sub = subject.Schedule(dueTime, scheduler, value => executed = value == 42)
            .Subscribe(results.Add);

        subject.OnNext(42);

        using (Assert.Multiple())
        {
            await Assert.That(executed).IsFalse();
            await Assert.That(results).IsEmpty();
        }

        scheduler.AdvanceBy(100);

        using (Assert.Multiple())
        {
            await Assert.That(executed).IsTrue();
            await Assert.That(results).IsEquivalentTo([42]);
        }
    }

    /// <summary>
    /// Tests Schedule with observable, TimeSpan and action.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Schedule_WithObservableTimeSpanAndAction_ExecutesAction()
    {
        var executed = false;
        var subject = new Subject<int>();

        subject.Schedule(TimeSpan.FromMilliseconds(10), Scheduler.Immediate, v => executed = true)
            .Subscribe();

        subject.OnNext(42);

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests Schedule with DateTimeOffset and action.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Schedule_WithDateTimeOffsetAndAction_ExecutesAction()
    {
        var executed = false;
        var value = 42;

        value.Schedule(DateTimeOffset.Now.AddMilliseconds(10), Scheduler.Immediate, v => executed = true)
            .Subscribe();

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests Schedule with observable, DateTimeOffset and action.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Schedule_WithObservableDateTimeOffsetAndAction_ExecutesAction()
    {
        var executed = false;
        var subject = new Subject<int>();

        subject.Schedule(DateTimeOffset.Now.AddMilliseconds(10), Scheduler.Immediate, v => executed = true)
            .Subscribe();

        subject.OnNext(42);

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests Schedule with function and scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Schedule_WithFunction_TransformsValue()
    {
        var value = 42;
        var result = 0;

        value.Schedule(Scheduler.Immediate, v => v * 2)
            .Subscribe(r => result = r);

        await Assert.That(result).IsEqualTo(84);
    }

    /// <summary>
    /// Tests Schedule with observable and function.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Schedule_WithObservableAndFunction_TransformsValue()
    {
        var subject = new Subject<int>();
        var result = 0;

        subject.Schedule(Scheduler.Immediate, v => v * 2)
            .Subscribe(r => result = r);

        subject.OnNext(42);

        await Assert.That(result).IsEqualTo(84);
    }

    /// <summary>
    /// Tests Schedule with TimeSpan and function.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Schedule_WithTimeSpanAndFunction_TransformsValue()
    {
        var value = 42;
        var result = 0;

        value.Schedule(TimeSpan.FromMilliseconds(10), Scheduler.Immediate, v => v * 2)
            .Subscribe(r => result = r);

        await Assert.That(result).IsEqualTo(84);
    }

    /// <summary>
    /// Tests Schedule with observable, TimeSpan and function.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Schedule_WithObservableTimeSpanAndFunction_TransformsValue()
    {
        var subject = new Subject<int>();
        var result = 0;

        subject.Schedule(TimeSpan.FromMilliseconds(10), Scheduler.Immediate, v => v * 2)
            .Subscribe(r => result = r);

        subject.OnNext(42);

        await Assert.That(result).IsEqualTo(84);
    }

    /// <summary>
    /// Tests OnErrorRetry with delay and no error action.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task OnErrorRetry_WithDelayAndErrorAction_RetriesWithDelay()
    {
        var attemptCount = 0;
        var errorsCaught = 0;
        var results = new List<int>();
        var scheduler = new TestScheduler();

        var source = Observable.Create<int>(observer =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                observer.OnError(new InvalidOperationException($"Attempt {attemptCount}"));
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        source.OnErrorRetry<int, InvalidOperationException>(
                ex => errorsCaught++,
                retryCount: int.MaxValue,
                delay: TimeSpan.FromMilliseconds(10),
                delayScheduler: scheduler)
            .Subscribe(results.Add);

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        using (Assert.Multiple())
        {
            await Assert.That(errorsCaught).IsEqualTo(1);
            await Assert.That(results).IsEquivalentTo([42]);
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with retry count limit.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task OnErrorRetry_WithRetryCount_LimitsRetries()
    {
        var attemptCount = 0;
        var errorsCaught = 0;
        var finalError = false;

        var source = Observable.Create<int>(observer =>
        {
            attemptCount++;
            observer.OnError(new InvalidOperationException($"Attempt {attemptCount}"));
            return System.Reactive.Disposables.Disposable.Empty;
        });

        source.OnErrorRetry<int, InvalidOperationException>(
                ex => errorsCaught++,
                retryCount: 2)
            .Subscribe(_ => { }, ex => finalError = true);

        var finalErrorReceived = await AsyncTestHelpers.WaitForConditionAsync(
            () => finalError,
            TimeSpan.FromSeconds(2));

        using (Assert.Multiple())
        {
            // Should retry 2 times (attempts 1 + 2 retries = total of 2 error callbacks on retries only)
            await Assert.That(finalErrorReceived).IsTrue();
            await Assert.That(errorsCaught).IsEqualTo(2);
            await Assert.That(finalError).IsTrue();
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with retry count and delay.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task OnErrorRetry_WithRetryCountAndDelay_LimitsRetriesWithDelay()
    {
        var attemptCount = 0;
        var errorsCaught = 0;
        var finalError = false;
        var scheduler = new TestScheduler();

        var source = Observable.Create<int>(observer =>
        {
            attemptCount++;
            observer.OnError(new InvalidOperationException($"Attempt {attemptCount}"));
            return System.Reactive.Disposables.Disposable.Empty;
        });

        source.OnErrorRetry<int, InvalidOperationException>(
                ex => errorsCaught++,
                retryCount: 2,
                delay: TimeSpan.FromMilliseconds(10),
                delayScheduler: scheduler)
            .Subscribe(_ => { }, ex => finalError = true);

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        using (Assert.Multiple())
        {
            // Should retry 2 times (2 error callbacks on retries)
            await Assert.That(errorsCaught).IsEqualTo(2);
            await Assert.That(finalError).IsTrue();
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with retry count, delay, and scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task OnErrorRetry_WithRetryCountDelayAndScheduler_RetriesCorrectly()
    {
        var attemptCount = 0;
        var errorsCaught = 0;

        var source = Observable.Create<int>(observer =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                observer.OnError(new InvalidOperationException($"Attempt {attemptCount}"));
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        var result = 0;
        source.OnErrorRetry<int, InvalidOperationException>(
                ex => errorsCaught++,
                retryCount: 3,
                delay: TimeSpan.FromMilliseconds(10),
                delayScheduler: Scheduler.Immediate)
            .Subscribe(r => result = r);

        using (Assert.Multiple())
        {
            await Assert.That(errorsCaught).IsEqualTo(1);
            await Assert.That(result).IsEqualTo(42);
        }
    }

    /// <summary>
    /// Tests SubscribeSynchronous with full callbacks.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SubscribeSynchronous_WithFullCallbacks_ExecutesAll()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        var errorHandled = false;
        var completed = false;

        subject.SubscribeSynchronous(
            async v =>
            {
                await Task.Delay(1);
                results.Add(v);
            },
            ex => errorHandled = true,
            () => completed = true);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();

        // Wait for completion callback
        var timeout = 0;
        while (!completed && timeout < 1000)
        {
            Thread.Sleep(10);
            timeout += 10;
        }

        using (Assert.Multiple())
        {
            await Assert.That(results).IsEquivalentTo([1, 2]);
            await Assert.That(errorHandled).IsFalse();
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Tests SubscribeSynchronous with onNext and onError.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SubscribeSynchronous_WithOnNextAndOnError_HandlesError()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        var errorHandled = false;

        subject.SubscribeSynchronous(
            async v =>
            {
                await Task.Delay(1);
                results.Add(v);
            },
            ex => errorHandled = true);

        subject.OnNext(1);
        subject.OnError(new InvalidOperationException());

        // Wait for async operations
        Thread.Sleep(50);

        using (Assert.Multiple())
        {
            await Assert.That(results).IsEquivalentTo([1]);
            await Assert.That(errorHandled).IsTrue();
        }
    }

    /// <summary>
    /// Tests SubscribeSynchronous with onNext and onCompleted.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SubscribeSynchronous_WithOnNextAndOnCompleted_CompletesCorrectly()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        var completed = false;

        subject.SubscribeSynchronous(
            async v =>
            {
                await Task.Delay(1);
                results.Add(v);
            },
            () => completed = true);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();

        // Wait for async operations
        Thread.Sleep(50);

        using (Assert.Multiple())
        {
            await Assert.That(results).IsEquivalentTo([1, 2]);
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Tests SubscribeSynchronous with only onNext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SubscribeSynchronous_WithOnlyOnNext_ProcessesValues()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.SubscribeSynchronous(
            async v =>
            {
                await Task.Delay(1);
                results.Add(v);
            });

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        // Wait for async operations
        Thread.Sleep(50);

        await Assert.That(results).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests SubscribeAsync with onNext and onCompleted.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SubscribeAsync_WithOnNextAndOnCompleted_CompletesCorrectly()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        var completed = false;
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = subject.SubscribeAsync(
            async v =>
            {
                await Task.Delay(1);
                results.Add(v);
            },
            () =>
            {
                completed = true;
                completionSource.TrySetResult(true);
            });

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();

        await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using (Assert.Multiple())
        {
            await Assert.That(results).IsEquivalentTo([1, 2]);
            await Assert.That(completed).IsTrue();
        }
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
        subject.OnNext(2); // Should be ignored (within throttle window)
        subject.OnNext(3); // Should be ignored (within throttle window)
        Thread.Sleep(150); // Wait for throttle window to pass
        subject.OnNext(4); // Should be emitted

        // Verify results
        await Assert.That(results).IsEquivalentTo([1, 4]);
    }

    /// <summary>
    /// Tests BufferUntilIdle buffers values until idle period.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task BufferUntilIdle_BuffersUntilIdle()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IList<int>>();

        subject.BufferUntilIdle(TimeSpan.FromMilliseconds(100), scheduler)
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
        subject.OnNext(3);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks); // Wait for idle period

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0]).IsEquivalentTo([1, 2, 3]);
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
        subject.OnNext(2); // Should drop
        subject.OnNext(3); // Should drop

        tcs.SetResult(new object()); // Complete the async action

        await Task.Delay(10); // Small delay to allow processing

        await Assert.That(results).IsEquivalentTo([1]);
    }

    /// <summary>
    /// Tests Pairwise emits previous and current pairs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task Pairwise_EmitsPairs()
    {
        var subject = new Subject<int>();
        var results = new List<(int Previous, int Current)>();

        subject.Pairwise().Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(2);
            await Assert.That(results[0]).IsEqualTo((1, 2));
            await Assert.That(results[1]).IsEqualTo((2, 3));
        }
    }

    /// <summary>
    /// Tests ScanWithInitial starts with initial value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ScanWithInitial_StartsWithInitial()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.ScanWithInitial(10, (acc, x) => acc + x).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);

        await Assert.That(results).IsEquivalentTo([10, 11, 13]);
    }

    /// <summary>
    /// Tests SampleLatest samples latest on trigger.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SampleLatest_SamplesLatestOnTrigger()
    {
        var subject = new Subject<int>();
        var trigger = new Subject<object>();
        var results = new List<int>();

        subject.SampleLatest(trigger).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        trigger.OnNext(new object()); // Should emit 2
        subject.OnNext(3);
        trigger.OnNext(new object()); // Should emit 3

        await Assert.That(results).IsEquivalentTo([2, 3]);
    }

    /// <summary>
    /// Tests SwitchIfEmpty switches to fallback when empty.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SwitchIfEmpty_SwitchesWhenEmpty()
    {
        var emptySubject = new Subject<int>();
        var fallbackSubject = new Subject<int>();
        var results = new List<int>();

        emptySubject.SwitchIfEmpty(fallbackSubject).Subscribe(results.Add);

        emptySubject.OnCompleted(); // Empty completes
        fallbackSubject.OnNext(42);
        fallbackSubject.OnCompleted();

        await Assert.That(results).IsEquivalentTo([42]);
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
        subject.OnNext(2);
        scheduler.AdvanceBy(101);
        subject.OnNext(2); // Duplicate after throttle

        await Assert.That(results).IsEquivalentTo([2]);
    }

    /// <summary>
    /// Tests ToReadOnlyBehavior creates read-only behavior.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ToReadOnlyBehavior_CreatesReadOnly()
    {
        var (observable, observer) = ReactiveExtensions.ToReadOnlyBehavior(10);
        var results = new List<int>();

        observable.Subscribe(results.Add);

        observer.OnNext(20);
        observer.OnNext(30);

        await Assert.That(results).IsEquivalentTo([10, 20, 30]);
    }

    /// <summary>
    /// Tests ToHotTask converts to hot task.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ToHotTask_ConvertsToTask()
    {
        var subject = new Subject<int>();
        var task = subject.ToHotTask();

        subject.OnNext(42);

        await Assert.That(task.Result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests ToPropertyObservable observes property changes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ToPropertyObservable_ObservesProperty()
    {
        var obj = new TestNotifyPropertyChanged { TestProperty = "initial" };
        var results = new List<string>();

        obj.ToPropertyObservable(x => x.TestProperty).Subscribe(results.Add);

        obj.TestProperty = "changed";

        await Assert.That(results).IsEquivalentTo(["initial", "changed"]);
    }

    /// <summary>
    /// Tests ObserveOnIf with bool condition and single scheduler when true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ObserveOnIf_WithBoolConditionTrue_ObservesOnScheduler()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.ObserveOnIf(true, scheduler).Subscribe(results.Add);

        subject.OnNext(1);
        await Assert.That(results).IsEmpty();

        scheduler.AdvanceBy(1);

        await Assert.That(results).IsEquivalentTo([1]);
    }

    /// <summary>
    /// Tests ObserveOnIf with bool condition and single scheduler when false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ObserveOnIf_WithBoolConditionFalse_DoesNotObserveOnScheduler()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.ObserveOnIf(false, scheduler).Subscribe(results.Add);

        subject.OnNext(1);
        await Assert.That(results).IsEquivalentTo([1]);
    }

    /// <summary>
    /// Tests ObserveOnIf with bool condition and two schedulers when true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ObserveOnIf_WithBoolConditionTrue_ObservesOnTrueScheduler()
    {
        var trueScheduler = new TestScheduler();
        var falseScheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.ObserveOnIf(true, trueScheduler, falseScheduler).Subscribe(results.Add);

        subject.OnNext(1);
        await Assert.That(results).IsEmpty();

        trueScheduler.AdvanceBy(1);
        await Assert.That(results).IsEquivalentTo([1]);
    }

    /// <summary>
    /// Tests ObserveOnIf with bool condition and two schedulers when false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ObserveOnIf_WithBoolConditionFalse_ObservesOnFalseScheduler()
    {
        var trueScheduler = new TestScheduler();
        var falseScheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.ObserveOnIf(false, trueScheduler, falseScheduler).Subscribe(results.Add);

        subject.OnNext(1);
        await Assert.That(results).IsEmpty();

        falseScheduler.AdvanceBy(1);
        await Assert.That(results).IsEquivalentTo([1]);
    }

    /// <summary>
    /// Tests ObserveOnIf with reactive condition routes notifications to both schedulers as the condition changes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ObserveOnIf_WithReactiveCondition_ObservesOnMatchingScheduler()
    {
        var trueScheduler = new TestScheduler();
        var falseScheduler = new TestScheduler();
        var source = new Subject<int>();
        var condition = new BehaviorSubject<bool>(false);
        var results = new List<int>();

        using var sub = source.ObserveOnIf(condition, trueScheduler, falseScheduler).Subscribe(results.Add);

        source.OnNext(1);
        await Assert.That(results).IsEmpty();

        falseScheduler.AdvanceBy(1);
        await Assert.That(results).IsEquivalentTo([1]);

        condition.OnNext(true);
        source.OnNext(2);

        await Assert.That(results).IsEquivalentTo([1]);

        trueScheduler.AdvanceBy(1);
        await Assert.That(results).IsEquivalentTo([1, 2]);
    }

    /// <summary>
    /// Tests ObserveOnIf with reactive condition and single scheduler observes only when condition is true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ObserveOnIf_WithReactiveConditionAndSingleScheduler_ObservesOnlyWhenEnabled()
    {
        var scheduler = new TestScheduler();
        var source = new Subject<int>();
        var condition = new BehaviorSubject<bool>(false);
        var results = new List<int>();

        using var sub = source.ObserveOnIf(condition, scheduler).Subscribe(results.Add);

        source.OnNext(1);
        await Assert.That(results).IsEquivalentTo([1]);

        condition.OnNext(true);
        source.OnNext(2);

        await Assert.That(results).IsEquivalentTo([1]);

        scheduler.AdvanceBy(1);
        await Assert.That(results).IsEquivalentTo([1, 2]);
    }

    /// <summary>
    /// Tests SkipWhileNull emits values after the first non-null value, including later nulls.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task SkipWhileNull_WhenFirstValueArrives_EmitsRemainingValues()
    {
        IObservable<string> source = Observable.Create<string>(observer =>
        {
            observer.OnNext(null!);
            observer.OnNext(null!);
            observer.OnNext("first");
            observer.OnNext(null!);
            observer.OnNext("second");
            observer.OnCompleted();
            return Disposable.Empty;
        });
        var results = new List<string>();

        using var sub = source.SkipWhileNull().Subscribe(results.Add);

        await Assert.That(results).IsEquivalentTo(["first", null, "second"]);
    }

    /// <summary>
    /// Tests LogErrors invokes the logger when the source faults.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task LogErrors_WhenSourceErrors_InvokesLogger()
    {
        Exception? logged = null;
        Exception? observed = null;
        using var subject = new Subject<int>();
        using var sub = subject.LogErrors(ex => logged = ex)
            .Subscribe(_ => { }, ex => observed = ex);

        var exception = new InvalidOperationException("boom");
        subject.OnError(exception);

        using (Assert.Multiple())
        {
            await Assert.That(logged).IsSameReferenceAs(exception);
            await Assert.That(observed).IsSameReferenceAs(exception);
        }
    }

    /// <summary>
    /// Tests ReplayLastOnSubscribe replays last value to new subscribers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task ReplayLastOnSubscribe_ReplaysLastValueToNewSubscribers()
    {
        var subject = new Subject<int>();
        var replayed = subject.ReplayLastOnSubscribe(99);

        var results1 = new List<int>();
        using var sub1 = replayed.Subscribe(results1.Add);

        // First subscriber gets initial
        await Assert.That(results1).IsEquivalentTo([99]);

        subject.OnNext(1);
        await Assert.That(results1).IsEquivalentTo([99, 1]);

        var results2 = new List<int>();
        using var sub2 = replayed.Subscribe(results2.Add);

        // Second subscriber gets last value
        await Assert.That(results2).IsEquivalentTo([1]);

        subject.OnNext(2);
        using (Assert.Multiple())
        {
            await Assert.That(results1).IsEquivalentTo([99, 1, 2]);
            await Assert.That(results2).IsEquivalentTo([1, 2]);
        }
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

        subject.DebounceUntil(TimeSpan.FromTicks(100), x => x % 2 == 0, scheduler)
            .Subscribe(results.Add);

        subject.OnNext(1); // Odd, should be delayed
        scheduler.AdvanceBy(50); // Advance less than debounce period
        subject.OnNext(2); // Even, should emit immediately, cancelling delayed 1
        scheduler.AdvanceBy(100); // Advance past debounce period

        await Assert.That(results).IsEquivalentTo([2]);
    }

    /// <summary>
    /// Tests SyncTimer without scheduler uses default scheduler and produces ticks.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSyncTimerCalledWithoutScheduler_ThenProducesTicks()
    {
        var results = new List<DateTime>();
        using var sub = ReactiveExtensions.SyncTimer(TimeSpan.FromMilliseconds(50)).Take(2).Subscribe(results.Add);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Tests BufferUntilIdle with scheduler forwards source error after flushing buffered items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBufferUntilIdleWithSchedulerSourceErrors_ThenFlushesAndForwardsError()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IList<int>>();
        Exception? observedError = null;

        subject.BufferUntilIdle(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add, ex => observedError = ex);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnError(new InvalidOperationException("test error"));

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(1);
            await Assert.That(results[0]).IsEquivalentTo([1, 2]);
            await Assert.That(observedError).IsNotNull();
        }
    }

    /// <summary>
    /// Tests BufferUntilIdle with scheduler flushes buffered items on source completion.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBufferUntilIdleWithSchedulerSourceCompletes_ThenFlushesAndCompletes()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IList<int>>();
        var completed = false;

        subject.BufferUntilIdle(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add, () => completed = true);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(1);
            await Assert.That(results[0]).IsEquivalentTo([1, 2]);
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Tests BufferUntilIdle without scheduler uses the Publish+Buffer+Throttle path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBufferUntilIdleCalledWithoutScheduler_ThenBuffersUntilIdle()
    {
        var subject = new Subject<int>();
        var results = new List<IList<int>>();

        subject.BufferUntilIdle(TimeSpan.FromMilliseconds(100))
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnCompleted();

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Tests LatestOrDefault emits default first then distinct values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLatestOrDefault_ThenEmitsDefaultThenDistinctValues()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.LatestOrDefault(42).Subscribe(results.Add);

        subject.OnNext(42); // Same as default, should be suppressed by DistinctUntilChanged
        subject.OnNext(1);
        subject.OnNext(1); // Duplicate, suppressed
        subject.OnNext(2);

        await Assert.That(results).IsEquivalentTo([42, 1, 2]);
    }

    /// <summary>
    /// Tests OnNext with null observer throws ArgumentNullException.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnNextWithNullObserver_ThenThrowsArgumentNullException()
    {
        IObserver<int>? observer = null;

        var ex = Assert.Throws<ArgumentNullException>(() => observer!.OnNext(1, 2, 3));

        await Assert.That(ex).IsNotNull();
    }

    /// <summary>
    /// Tests OnNext with null events array throws ArgumentNullException.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnNextWithNullEvents_ThenThrowsArgumentNullException()
    {
        var subject = new Subject<int>();

        var ex = Assert.Throws<ArgumentNullException>(() => subject.OnNext(null!));

        await Assert.That(ex).IsNotNull();
    }

    /// <summary>
    /// Tests Start with null scheduler executes the action directly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStartActionWithNullScheduler_ThenExecutesAction()
    {
        var executed = false;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = ReactiveExtensions.Start(() => executed = true, scheduler: null)
            .Subscribe(_ => { }, () => completed.SetResult());

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests Start with function and null scheduler returns computed result.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStartFuncWithNullScheduler_ThenReturnsResult()
    {
        var result = await ReactiveExtensions.Start(() => 42, scheduler: null).ToTask();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests ScheduleSafe with TimeSpan and null scheduler uses Thread.Sleep path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScheduleSafeWithTimeSpanAndNullScheduler_ThenSleepsAndExecutes()
    {
        var executed = false;
        IScheduler? scheduler = null;

        var disposable = scheduler.ScheduleSafe(TimeSpan.FromMilliseconds(10), () => executed = true);

        using (Assert.Multiple())
        {
            await Assert.That(executed).IsTrue();
            await Assert.That(disposable).IsNotNull();
        }
    }

    /// <summary>
    /// Tests Using with Action invokes the action and disposes the resource.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUsingWithActionAndNoScheduler_ThenExecutesAndDisposes()
    {
        var actionExecuted = false;
        using var disposable = System.Reactive.Disposables.Disposable.Create(() => { });

        await disposable.Using(d => actionExecuted = true).ToTask();

        await Assert.That(actionExecuted).IsTrue();
    }

    /// <summary>
    /// Tests Schedule with value and TimeSpan emits value after delay.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScheduleValueWithTimeSpan_ThenEmitsAfterDelay()
    {
        var scheduler = new TestScheduler();
        int? result = null;

        using var sub = 42.Schedule(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(x => result = x);

        await Assert.That(result).IsNull();

        scheduler.AdvanceBy(101);

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests Schedule with observable and TimeSpan delays each emitted value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScheduleObservableWithTimeSpan_ThenDelaysValues()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();

        using var sub = ((IObservable<int>)subject).Schedule(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(x => results.Add(x));

        subject.OnNext(10);
        await Assert.That(results).IsEmpty();

        scheduler.AdvanceBy(101);

        await Assert.That(results).IsEquivalentTo([10]);
    }

    /// <summary>
    /// Tests Schedule with value and DateTimeOffset emits value at due time.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScheduleValueWithDateTimeOffset_ThenEmitsAtDueTime()
    {
        var scheduler = new TestScheduler();
        int? result = null;
        var dueTime = scheduler.Now.AddTicks(100);

        using var sub = 42.Schedule(dueTime, scheduler)
            .Subscribe(x => result = x);

        await Assert.That(result).IsNull();

        scheduler.AdvanceBy(100);

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests Schedule with observable and DateTimeOffset delays values to due time.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScheduleObservableWithDateTimeOffset_ThenDelaysValues()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();
        var dueTime = scheduler.Now.AddTicks(100);

        using var sub = ((IObservable<int>)subject).Schedule(dueTime, scheduler)
            .Subscribe(x => results.Add(x));

        subject.OnNext(10);
        await Assert.That(results).IsEmpty();

        scheduler.AdvanceBy(100);

        await Assert.That(results).IsEquivalentTo([10]);
    }

    /// <summary>
    /// Tests Schedule with action and TimeSpan executes action then emits value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScheduleValueWithTimeSpanAndAction_ThenExecutesActionAndEmits()
    {
        var scheduler = new TestScheduler();
        var actionExecuted = false;
        int? result = null;

        using var sub = 42.Schedule(TimeSpan.FromTicks(100), scheduler, v => actionExecuted = v == 42)
            .Subscribe(x => result = x);

        await Assert.That(actionExecuted).IsFalse();

        scheduler.AdvanceBy(101);

        using (Assert.Multiple())
        {
            await Assert.That(actionExecuted).IsTrue();
            await Assert.That(result).IsEqualTo(42);
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with action only uses zero-delay retry.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorRetryWithActionOnly_ThenRetriesImmediately()
    {
        var attempts = 0;
        var errorCount = 0;
        var source = Observable.Create<int>(observer =>
        {
            attempts++;
            if (attempts < 3)
            {
                observer.OnError(new InvalidOperationException());
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        var results = new List<int>();
        using var sub = source.OnErrorRetry<int, InvalidOperationException>(ex => errorCount++)
            .Subscribe(results.Add);

        using (Assert.Multiple())
        {
            await Assert.That(results).IsEquivalentTo([42]);
            await Assert.That(errorCount).IsEqualTo(2);
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with delay retries after specified delay.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorRetryWithDelay_ThenRetriesAfterDelay()
    {
        var attempts = 0;
        var source = Observable.Create<int>(observer =>
        {
            attempts++;
            if (attempts < 2)
            {
                observer.OnError(new InvalidOperationException());
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        var result = source.OnErrorRetry<int, InvalidOperationException>(
            ex => { },
            TimeSpan.FromMilliseconds(10))
            .Wait();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests SubscribeAsync with all three handlers (onNext, onError, onCompleted).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithAllHandlers_ThenInvokesAll()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        var completed = false;
        Exception? caughtError = null;
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = subject.SubscribeAsync(
            async x =>
            {
                await Task.Delay(1);
                results.Add(x);
            },
            ex => caughtError = ex,
            () =>
            {
                completed = true;
                completionSource.TrySetResult(true);
            });

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();

        await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using (Assert.Multiple())
        {
            await Assert.That(results).IsEquivalentTo([1, 2]);
            await Assert.That(completed).IsTrue();
            await Assert.That(caughtError).IsNull();
        }
    }

    /// <summary>
    /// Tests RetryWithBackoff rethrows after max retries exceeded.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithBackoffExceedsMaxRetries_ThenRethrows()
    {
        var source = Observable.Throw<int>(new InvalidOperationException("fail"));
        Exception? caughtError = null;

        source.RetryWithBackoff(
            maxRetries: 2,
            initialDelay: TimeSpan.FromMilliseconds(1),
            scheduler: Scheduler.Immediate)
            .Subscribe(_ => { }, ex => caughtError = ex);

        await Assert.That(caughtError).IsNotNull();
    }

    /// <summary>
    /// Tests RetryWithBackoff caps delay at maxDelay.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithBackoffDelayExceedsMax_ThenCapsDelay()
    {
        var attempts = 0;
        var source = Observable.Create<int>(observer =>
        {
            attempts++;
            if (attempts < 4)
            {
                observer.OnError(new InvalidOperationException());
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        var result = source.RetryWithBackoff(
            maxRetries: 5,
            initialDelay: TimeSpan.FromMilliseconds(5),
            backoffFactor: 10.0,
            maxDelay: TimeSpan.FromMilliseconds(20),
            scheduler: Scheduler.Immediate)
            .Wait();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests RetryWithDelay retries with custom delay selector.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithDelay_ThenRetriesWithCustomDelay()
    {
        var attempts = 0;
        var source = Observable.Create<int>(observer =>
        {
            attempts++;
            if (attempts < 3)
            {
                observer.OnError(new InvalidOperationException());
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        var result = source.RetryWithDelay(5, attempt => TimeSpan.FromMilliseconds(1)).Wait();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests RetryForeverWithDelay retries indefinitely with delay.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryForeverWithDelay_ThenRetriesIndefinitely()
    {
        var attempts = 0;
        var source = Observable.Create<int>(observer =>
        {
            attempts++;
            if (attempts < 4)
            {
                observer.OnError(new InvalidOperationException());
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        var result = source.RetryForeverWithDelay(TimeSpan.FromMilliseconds(1)).Wait();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests RetryWithFixedDelay retries with constant delay between retries.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithFixedDelay_ThenRetriesWithConstantDelay()
    {
        var attempts = 0;
        var source = Observable.Create<int>(observer =>
        {
            attempts++;
            if (attempts < 3)
            {
                observer.OnError(new InvalidOperationException());
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        var result = source.RetryWithFixedDelay(5, TimeSpan.FromMilliseconds(1)).Wait();

        await Assert.That(result).IsEqualTo(42);
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
        subject.OnNext(2);
        scheduler.AdvanceBy(101);

        await Assert.That(results).IsEquivalentTo([2]);
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
        subject.OnNext(2);
        scheduler.AdvanceBy(101);

        await Assert.That(results).IsEquivalentTo([2]);
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
        subject.OnNext(2); // Buffered as pending
        subject.OnError(new InvalidOperationException("test"));

        using (Assert.Multiple())
        {
            await Assert.That(results).IsEquivalentTo([1, 2]);
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
        subject.OnNext(2); // Buffered as pending
        subject.OnCompleted();

        using (Assert.Multiple())
        {
            await Assert.That(results).IsEquivalentTo([1, 2]);
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

        subject.DebounceUntil(TimeSpan.FromTicks(100), x => x % 2 == 0, scheduler)
            .Subscribe(results.Add);

        subject.OnNext(2); // Even, emits immediately
        subject.OnNext(1); // Odd, delayed
        scheduler.AdvanceBy(101);

        await Assert.That(results).IsEquivalentTo([2, 1]);
    }

    /// <summary>
    /// Tests SelectAsync with CancellationToken projects values asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSelectAsyncWithCancellationToken_ThenProjectsValues()
    {
        var source = new[] { 1, 2, 3 }.ToObservable();
        var results = new List<int>();
        var tcs = new TaskCompletionSource<bool>();

        source.SelectAsync((x, ct) => Task.FromResult(x * 2))
            .Subscribe(
            results.Add,
            () => tcs.TrySetResult(true));

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(results).IsEquivalentTo([2, 4, 6]);
    }

    /// <summary>
    /// Tests SelectAsync simple overload projects values asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSelectAsyncSimple_ThenProjectsValues()
    {
        var source = new[] { 1, 2, 3 }.ToObservable();
        var results = new List<int>();
        var tcs = new TaskCompletionSource<bool>();

        source.SelectAsync(x => Task.FromResult(x * 2))
            .Subscribe(
            results.Add,
            () => tcs.TrySetResult(true));

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(results).IsEquivalentTo([2, 4, 6]);
    }

    /// <summary>
    /// Tests SelectAsyncSequential processes tasks in order.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSelectAsyncSequential_ThenProcessesInOrder()
    {
        var source = new[] { 1, 2, 3 }.ToObservable();
        var results = new List<int>();
        var tcs = new TaskCompletionSource<bool>();

        source.SelectAsyncSequential(x => Task.FromResult(x * 2))
            .Subscribe(
            results.Add,
            () => tcs.TrySetResult(true));

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(results).IsEquivalentTo([2, 4, 6]);
    }

    /// <summary>
    /// Tests SelectLatestAsync emits only the latest async result.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSelectLatestAsync_ThenEmitsLatestResult()
    {
        var source = new[] { 1, 2, 3 }.ToObservable();
        var results = new List<int>();
        var tcs = new TaskCompletionSource<bool>();

        source.SelectLatestAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        }).Subscribe(
            results.Add,
            () => tcs.TrySetResult(true));

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Switch means only latest may survive, but at minimum last value should be present
        await Assert.That(results).IsNotEmpty();
    }

    /// <summary>
    /// Tests SelectAsyncConcurrent processes tasks concurrently up to max concurrency.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSelectAsyncConcurrent_ThenProcessesConcurrently()
    {
        var source = new[] { 1, 2, 3 }.ToObservable();
        var results = new List<int>();
        var tcs = new TaskCompletionSource<bool>();

        source.SelectAsyncConcurrent(
            async x =>
            {
                await Task.Delay(1);
                return x * 2;
            },
            maxConcurrency: 2).Subscribe(results.Add, () => tcs.TrySetResult(true));

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        results.Sort();
        await Assert.That(results).IsEquivalentTo([2, 4, 6]);
    }

    /// <summary>
    /// Tests BufferUntilInactive buffers items and flushes after inactivity period.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBufferUntilInactive_ThenBuffersAndFlushesOnInactivity()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IList<int>>();

        subject.BufferUntilInactive(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        scheduler.AdvanceBy(50);
        subject.OnNext(3);
        scheduler.AdvanceBy(101);

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0]).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests BufferUntilInactive flushes remaining items on error.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBufferUntilInactiveSourceErrors_ThenFlushesAndForwardsError()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IList<int>>();
        Exception? observedError = null;

        subject.BufferUntilInactive(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add, ex => observedError = ex);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnError(new InvalidOperationException("test"));

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(1);
            await Assert.That(results[0]).IsEquivalentTo([1, 2]);
            await Assert.That(observedError).IsNotNull();
        }
    }

    /// <summary>
    /// Tests BufferUntilInactive flushes remaining items on completion.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenBufferUntilInactiveSourceCompletes_ThenFlushesAndCompletes()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IList<int>>();
        var completed = false;

        subject.BufferUntilInactive(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add, () => completed = true);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();

        using (Assert.Multiple())
        {
            await Assert.That(results).Count().IsEqualTo(1);
            await Assert.That(results[0]).IsEquivalentTo([1, 2]);
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Tests FastForEach with List source emits all items via OnNext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFastForEachWithList_ThenEmitsAllItems()
    {
        var source = Observable.Return(new List<int> { 1, 2, 3 } as IEnumerable<int>);
        var results = new List<int>();
        using var sub = source.ForEach().Subscribe(results.Add);

        await Assert.That(results).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests FastForEach with array source emits all items via OnNext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFastForEachWithArray_ThenEmitsAllItems()
    {
        var arr = new[] { 1, 2, 3 };
        var results = new List<int>();
        using var sub = arr.FromArray().Subscribe(results.Add);

        await Assert.That(results).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests FastForEach with generic IEnumerable source emits all items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFastForEachWithEnumerable_ThenEmitsAllItems()
    {
        IEnumerable<int> Enumerate()
        {
            yield return 1;
            yield return 2;
            yield return 3;
        }

        var source = Observable.Return(Enumerate());
        var results = new List<int>();
        using var sub = source.ForEach().Subscribe(results.Add);

        await Assert.That(results).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests CatchAndReturn with factory recovers from a specific exception type.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchAndReturnWithFactory_ThenRecoverFromException()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.CatchAndReturn<int, InvalidOperationException>(ex => -1)
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnError(new InvalidOperationException("boom"));

        await Assert.That(results).IsEquivalentTo([1, -1]);
    }

    /// <summary>
    /// Tests Conflate non-throttled path emits immediately when update period has passed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConflateNonThrottledPath_ThenEmitsImmediately()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();
        var completed = false;

        subject.Conflate(TimeSpan.FromTicks(100), scheduler)
            .Subscribe(results.Add, _ => { }, () => completed = true);

        // Emit first value - goes through non-throttled path (line 353)
        subject.OnNext(1);
        scheduler.AdvanceBy(1);

        // Advance past the minimum update period
        scheduler.AdvanceBy(200);

        // Emit second value - also non-throttled since enough time passed
        subject.OnNext(2);
        scheduler.AdvanceBy(1);

        // Complete with no scheduled update pending (line 372)
        subject.OnCompleted();
        scheduler.AdvanceBy(1);

        await Assert.That(results).IsEquivalentTo([1, 2]);
        await Assert.That(completed).IsTrue();
    }

    /// <summary>
    /// Tests Using with Func overload returns the function result.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUsingWithFunc_ThenReturnsFunctionResult()
    {
        var disposable = new CompositeDisposable();
        var result = await disposable.Using(d => 42).FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests While with scheduler repeatedly executes action while condition holds.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWhileWithScheduler_ThenExecutesActionOnScheduler()
    {
        var executedOnScheduler = false;
        var scheduler = new TestScheduler();

        // Test that the scheduler parameter is passed through to Observable.Start
        var obs = ReactiveExtensions.While(
            () => !executedOnScheduler,
            () => executedOnScheduler = true,
            scheduler);

        var results = new List<Unit>();
        using var sub = obs.Subscribe(results.Add);

        // Nothing should execute until the scheduler advances
        await Assert.That(executedOnScheduler).IsFalse();

        scheduler.Start();

        await Assert.That(executedOnScheduler).IsTrue();
        await Assert.That(results).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Tests Schedule with action, observable source, and TimeSpan overload.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScheduleWithActionAndTimeSpan_ThenExecutesActionAndEmits()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();
        var actionValues = new List<int>();

        subject.Schedule(TimeSpan.FromTicks(50), scheduler, v => actionValues.Add(v))
            .Subscribe(results.Add);

        subject.OnNext(10);
        scheduler.AdvanceBy(50);

        await Assert.That(actionValues).IsEquivalentTo([10]);
        await Assert.That(results).IsEquivalentTo([10]);
    }

    /// <summary>
    /// Tests RetryWithBackoff inner retry with max delay cap path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithBackoffInnerRetry_ThenRetriesAndCapsDelay()
    {
        var scheduler = new TestScheduler();
        var attempt = 0;
        var source = Observable.Defer(() =>
        {
            attempt++;
            return attempt < 3
                ? Observable.Throw<int>(new InvalidOperationException("retry"))
                : Observable.Return(42);
        });

        var results = new List<int>();
        Exception? error = null;

        source.RetryWithBackoff(
            maxRetries: 5,
            initialDelay: TimeSpan.FromTicks(10),
            backoffFactor: 2.0,
            maxDelay: TimeSpan.FromTicks(15),
            scheduler: scheduler)
            .Subscribe(results.Add, ex => error = ex);

        // Advance through retry delays
        scheduler.AdvanceBy(1000);

        await Assert.That(results).IsEquivalentTo([42]);
        await Assert.That(error).IsNull();
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
        subject.OnNext(10);
        await Task.Delay(50);

        // Predicate false: throttled
        subject.OnNext(1);
        await Task.Delay(150);

        await Assert.That(results).Contains(10);
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
        subject.OnNext(2);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Contains(2),
            TimeSpan.FromSeconds(30));

        await Assert.That(results).Contains(2);
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

        subject.DebounceUntil(TimeSpan.FromTicks(100), x => x % 2 == 0, scheduler)
            .Subscribe(results.Add);

        subject.OnNext(2); // condition true -> immediate
        subject.OnNext(3); // condition false -> delayed
        scheduler.AdvanceBy(100);

        await Assert.That(results).Contains(2);
        await Assert.That(results).Contains(3);
    }

    /// <summary>
    /// Tests FastForEach with IList branch using ArraySegment which implements IList but not List or T[].
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFastForEachWithIList_ThenEmitsAllItems()
    {
        IList<int> ilist = new ArraySegment<int>([10, 20, 30]);
        var source = Observable.Return((IEnumerable<int>)ilist);
        var results = new List<int>();
        using var sub = source.ForEach().Subscribe(results.Add);

        await Assert.That(results).IsEquivalentTo([10, 20, 30]);
    }

    /// <summary>
    /// Tests OnErrorRetry with negative delay ticks sets dueTime to zero.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorRetryNegativeDelay_ThenUsesZeroDelay()
    {
        var attempt = 0;
        var source = Observable.Defer(() =>
        {
            attempt++;
            return attempt < 3
                ? Observable.Throw<int>(new InvalidOperationException("fail"))
                : Observable.Return(42);
        });

        var results = new List<int>();
        Exception? error = null;

        source.OnErrorRetry<int, InvalidOperationException>(
            _ => { },
            retryCount: 5,
            delay: TimeSpan.FromTicks(-1))
            .Subscribe(results.Add, ex => error = ex);

        await Task.Delay(500);

        await Assert.That(results).Contains(42);
    }

    /// <summary>
    /// Tests OnErrorRetry with retry count check rethrows after exceeding count.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorRetryExceedsRetryCount_ThenRethrows()
    {
        var source = Observable.Throw<int>(new InvalidOperationException("fail"));
        Exception? caught = null;

        source.OnErrorRetry<int, InvalidOperationException>(
            _ => { },
            retryCount: 2,
            delay: TimeSpan.Zero)
            .Subscribe(_ => { }, ex => caught = ex);

        await Task.Delay(500);

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
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
        subject.OnNext(2);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 2,
            TimeSpan.FromSeconds(30));

        await Assert.That(results).Contains(1);
        await Assert.That(results).Contains(2);
    }

    /// <summary>
    /// Tests Heartbeat timer subscription null check path by quickly disposing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenHeartbeatSourceEmits_ThenTimerSubscriptionDisposed()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IHeartbeat<int>>();

        using var sub = subject.Heartbeat(TimeSpan.FromTicks(100), scheduler).Subscribe(results.Add);

        // Emit value, which disposes the current timer
        subject.OnNext(1);
        scheduler.AdvanceBy(1);

        // Emit another quickly, before heartbeat timer fires
        subject.OnNext(2);
        scheduler.AdvanceBy(1);

        await Assert.That(results.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(results[0].Update).IsEqualTo(1);
        await Assert.That(results[1].Update).IsEqualTo(2);
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
        subject.OnNext(2);

        await Task.Delay(50);
        await Assert.That(results).Contains(2);
    }

    /// <summary>
    /// Tests RetryWithBackoff caps delay at maxDelay when backoff exceeds it.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithBackoffExceedsMaxDelay_ThenCapsAtMaxDelay()
    {
        var scheduler = new TestScheduler();
        var attempt = 0;
        var source = Observable.Create<int>(obs =>
        {
            attempt++;
            if (attempt <= 3)
            {
                obs.OnError(new InvalidOperationException($"fail {attempt}"));
            }
            else
            {
                obs.OnNext(42);
                obs.OnCompleted();
            }

            return Disposable.Empty;
        });

        var results = new List<int>();
        using var sub = source
            .RetryWithBackoff(
                5,
                initialDelay: TimeSpan.FromMilliseconds(100),
                backoffFactor: 10.0,
                maxDelay: TimeSpan.FromMilliseconds(200),
                scheduler: scheduler)
            .Subscribe(results.Add);

        // Advance through retry delays
        for (var i = 0; i < 5; i++)
        {
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(250).Ticks);
        }

        await Assert.That(results).Contains(42);
    }

    /// <summary>
    /// Tests Using with Action and null scheduler disposes the object after executing the action.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUsingWithActionAndNullScheduler_ThenDisposesObject()
    {
        var executed = false;
        using var stream = new MemoryStream();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = stream.Using<MemoryStream>(
            _ => executed = true,
            null).Subscribe(
            _ => { },
            () => completed.SetResult());

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests FastForEach with an array passed through ForEach (Observable of IEnumerable).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFastForEachWithArrayViaForEach_ThenEmitsAllItems()
    {
        var arr = new[] { 10, 20, 30 };
        var source = Observable.Return(arr as IEnumerable<int>);
        var results = new List<int>();
        using var sub = source.ForEach().Subscribe(results.Add);

        await Assert.That(results).IsEquivalentTo([10, 20, 30]);
    }

    /// <summary>
    /// Tests RetryWithBackoff maxDelay cap is applied when computed delay exceeds it,
    /// exercising line 1240 of ReactiveExtensions.cs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithBackoffDelayExceedsMaxDelay_ThenCappedAtMaxDelay()
    {
        var scheduler = new TestScheduler();
        var attemptCount = 0;

        var source = Observable.Create<int>(observer =>
        {
            attemptCount++;
            if (attemptCount <= 3)
            {
                observer.OnError(new InvalidOperationException($"attempt {attemptCount}"));
            }
            else
            {
                observer.OnNext(99);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        // initialDelay=1ms, backoffFactor=100 => attempt 2 delay = 1*100^1 = 100ms, exceeds maxDelay=5ms
        var results = new List<int>();
        source.RetryWithBackoff(
            maxRetries: 5,
            initialDelay: TimeSpan.FromMilliseconds(1),
            backoffFactor: 100.0,
            maxDelay: TimeSpan.FromMilliseconds(5),
            scheduler: scheduler)
            .Subscribe(results.Add);

        // Advance past the capped delays
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        await Assert.That(results).Contains(99);
    }

    /// <summary>
    /// Tests ToPropertyObservable unsubscribes from PropertyChanged when disposed,
    /// exercising line 1428 of ReactiveExtensions.cs (the -= handler).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToPropertyObservableDisposed_ThenUnsubscribesFromPropertyChanged()
    {
        var obj = new TestNotifyPropertyChanged { TestProperty = "initial" };
        var results = new List<string>();

        var sub = obj.ToPropertyObservable(x => x.TestProperty).Subscribe(results.Add);

        obj.TestProperty = "changed";

        // Dispose the subscription, triggering the -= handler
        sub.Dispose();

        // Changes after dispose should not be observed
        obj.TestProperty = "afterDispose";

        await Assert.That(results).IsEquivalentTo(["initial", "changed"]);
    }

    /// <summary>
    /// Tests FastForEach with an IList source.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFastForEach_GivenIList_ThenAllItemsEmitted()
    {
        // Given
        IList<int> source = new List<int> { 1, 2, 3 };
        var received = new List<int>();
        var observer = Observer.Create<int>(x => received.Add(x));

        // When
        ReactiveExtensions.FastForEach(observer, source);

        // Then
        await Assert.That(received).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests FastForEach with an array source.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFastForEach_GivenArray_ThenAllItemsEmitted()
    {
        // Given
        var source = new[] { 10, 20, 30 };
        var received = new List<int>();
        var observer = Observer.Create<int>(x => received.Add(x));

        // When
        ReactiveExtensions.FastForEach(observer, source);

        // Then
        await Assert.That(received).IsEquivalentTo([10, 20, 30]);
    }

    /// <summary>
    /// Tests FastForEach with a plain IEnumerable source.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFastForEach_GivenIEnumerable_ThenAllItemsEmitted()
    {
        // Given
        static IEnumerable<int> Generate()
        {
            yield return 100;
            yield return 200;
        }

        var received = new List<int>();
        var observer = Observer.Create<int>(x => received.Add(x));

        // When
        ReactiveExtensions.FastForEach(observer, Generate());

        // Then
        await Assert.That(received).IsEquivalentTo([100, 200]);
    }

    /// <summary>
    /// Verifies that RetryWithBackoff caps the computed delay at maxDelay when the
    /// exponential backoff exceeds it. Uses Scheduler.Immediate so the cap is exercised
    /// synchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithBackoffComputedDelayExceedsMaxDelay_ThenCappedToMaxDelay()
    {
        var attemptCount = 0;
        var source = Observable.Create<int>(observer =>
        {
            attemptCount++;

            // Fail on first two attempts, succeed on third
            if (attemptCount <= 2)
            {
                observer.OnError(new InvalidOperationException($"attempt {attemptCount}"));
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return System.Reactive.Disposables.Disposable.Empty;
        });

        // initialDelay=1ms, backoffFactor=1000 => computed delay = 1000ms >> maxDelay=2ms
        // This ensures the cap path at line 1240 is hit
        var result = source.RetryWithBackoff(
            maxRetries: 5,
            initialDelay: TimeSpan.FromMilliseconds(1),
            backoffFactor: 1000.0,
            maxDelay: TimeSpan.FromMilliseconds(2),
            scheduler: Scheduler.Immediate)
            .Wait();

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(attemptCount).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that FastForEach handles a T[] array correctly. Since T[] implements
    /// IList{T} and that branch is checked before T[], arrays are handled by the
    /// IList{T} path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFastForEachWithArrayAsIEnumerable_ThenHandledByIListBranch()
    {
        var arr = new[] { 5, 10, 15 };
        var received = new List<int>();
        var observer = Observer.Create<int>(x => received.Add(x));

        ReactiveExtensions.FastForEach(observer, arr);

        await Assert.That(received).IsEquivalentTo([5, 10, 15]);
    }

    /// <summary>
    /// Test class for INotifyPropertyChanged.
    /// </summary>
    private class TestNotifyPropertyChanged : INotifyPropertyChanged
    {
        /// <summary>
        /// Property changed event.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets or sets the test property.
        /// </summary>
        /// <returns>The test property value.</returns>
        public string TestProperty
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TestProperty)));
                }
            }
        }

        = string.Empty;
    }
}
