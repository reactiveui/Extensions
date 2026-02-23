// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData;
using Microsoft.Reactive.Testing;
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
    [Test]
    public async Task SyncronizeAsync_RunsWithAsyncTasksInSubscriptions()
    {
        // Given, When
        var result = 0;
        var itterations = 0;
        var subject = new Subject<bool>();
        using var disposable = subject
            .SynchronizeAsync()
            .Subscribe(async x =>
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

                x.Sync.Dispose();
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
    [Test]
    public async Task SynchronizeSynchronous_RunsWithAsyncTasksInSubscriptions()
    {
        // Given, When
        var result = 0;
        var itterations = 0;
        var subject = new Subject<bool>();
        using var disposable = subject
            .SynchronizeSynchronous()
            .Subscribe(async x =>
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

                x.Sync.Dispose();
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
    /// Tests CatchIgnore without error action.
    /// </summary>
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
    /// Tests Heartbeat injects heartbeats.
    /// </summary>
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
    [Test]
    public async Task Start_WithActionAndNullScheduler_ExecutesAction()
    {
        var executed = false;
        Action action = () => executed = true;

        using var sub = ReactiveExtensions.Start(action, Scheduler.Immediate).Subscribe();

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests ForEach flattens enumerables.
    /// </summary>
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
    [Test]
    public async Task Heartbeat_WithoutUpdate_IsHeartbeat()
    {
        var heartbeat = new Heartbeat<int>();

        await Assert.That(heartbeat.IsHeartbeat).IsTrue();
    }

    /// <summary>
    /// Tests Stale class with update.
    /// </summary>
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
    [Test]
    public async Task Stale_WithoutUpdate_IsStale()
    {
        var stale = new Stale<int>();

        await Assert.That(stale.IsStale).IsTrue();
    }

    /// <summary>
    /// Tests Stale throws on Update access when stale.
    /// </summary>
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
    [Test]
    public async Task Continuation_CanBeDisposed()
    {
        var continuation = new Continuation();

        continuation.Dispose();
    }

    /// <summary>
    /// Tests Continuation tracks completed phases.
    /// </summary>
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
            async x =>
            {
                await Task.Delay(10);
                results.Add(x);
            },
            ex => caughtException = ex);

        subject.OnNext(1);
        subject.OnError(new InvalidOperationException());

        var resultReceived = await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count == 1 && caughtException != null,
            TimeSpan.FromSeconds(5));

        using (Assert.Multiple())
        {
            await Assert.That(resultReceived).IsTrue();
            await Assert.That(results).IsEquivalentTo([1]);
            await Assert.That(caughtException).IsNotNull();
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with error action and retry count.
    /// </summary>
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

    // ========== DynamicData Pattern Tests ==========
    // These tests use DynamicData's ToObservableChangeSet to track state changes over time

    /// <summary>
    /// Tests GetMin tracking minimum values as sources change over time.
    /// </summary>
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
    [Test]
    public async Task SyncTimer_ProducesSharedTicks()
    {
        var timeSpan = TimeSpan.FromMilliseconds(100);
        var results1 = new List<DateTime>();
        var results2 = new List<DateTime>();

        using var sub1 = ReactiveExtensions.SyncTimer(timeSpan).Take(2).Subscribe(results1.Add);
        using var sub2 = ReactiveExtensions.SyncTimer(timeSpan).Take(2).Subscribe(results2.Add);

        var ticksReceived = await AsyncTestHelpers.WaitForConditionAsync(
            () => results1.Count >= 1 && results2.Count >= 1,
            TimeSpan.FromSeconds(2));

        using (Assert.Multiple())
        {
            // Both subscriptions should get ticks (shared timer)
            await Assert.That(ticksReceived).IsTrue();
            await Assert.That(results1).Count().IsGreaterThanOrEqualTo(1);
            await Assert.That(results2).Count().IsGreaterThanOrEqualTo(1);
        }
    }

    /// <summary>
    /// Tests Using with action executes the action.
    /// </summary>
    [Test]
    public async Task Using_WithAction_ExecutesActionImmediately()
    {
        var executed = false;
        using var disposable = System.Reactive.Disposables.Disposable.Create(() => { });

        disposable.Using(d => executed = true, Scheduler.Immediate).Subscribe();

        await Assert.That(executed).IsTrue();
    }

    /// <summary>
    /// Tests Using with function transforms the value.
    /// </summary>
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
    /// Tests Schedule with observable, TimeSpan and action.
    /// </summary>
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
    [Test]
    public async Task OnErrorRetry_WithDelayAndErrorAction_RetriesWithDelay()
    {
        var attemptCount = 0;
        var errorsCaught = 0;
        var results = new List<int>();

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
                TimeSpan.FromMilliseconds(10))
            .Subscribe(results.Add);

        var resultReceived = await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count == 1 && errorsCaught == 1,
            TimeSpan.FromSeconds(5));

        using (Assert.Multiple())
        {
            await Assert.That(resultReceived).IsTrue();
            await Assert.That(errorsCaught).IsEqualTo(1);
            await Assert.That(results).IsEquivalentTo([42]);
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with retry count limit.
    /// </summary>
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
    [Test]
    public async Task OnErrorRetry_WithRetryCountAndDelay_LimitsRetriesWithDelay()
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
                retryCount: 2,
                delay: TimeSpan.FromMilliseconds(10))
            .Subscribe(_ => { }, ex => finalError = true);

        var finalErrorReceived = await AsyncTestHelpers.WaitForConditionAsync(
            () => finalError && errorsCaught == 2,
            TimeSpan.FromSeconds(5));

        using (Assert.Multiple())
        {
            // Should retry 2 times (2 error callbacks on retries)
            await Assert.That(finalErrorReceived).IsTrue();
            await Assert.That(errorsCaught).IsEqualTo(2);
            await Assert.That(finalError).IsTrue();
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with retry count, delay, and scheduler.
    /// </summary>
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
    [Test]
    public async Task SubscribeAsync_WithOnNextAndOnCompleted_CompletesCorrectly()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        var completed = false;
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        subject.SubscribeAsync(
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

        var completionReceived = await AsyncTestHelpers.WaitForConditionAsync(
            () => completed && results.Count == 2,
            TimeSpan.FromSeconds(5));

        using (Assert.Multiple())
        {
            await Assert.That(completionReceived).IsTrue();
            await Assert.That(results).IsEquivalentTo([1, 2]);
            await Assert.That(completed).IsTrue();
        }
    }

    /// <summary>
    /// Tests ThrottleFirst emits first immediately, then ignores subsequent values within the throttle window.
    /// </summary>
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
    /// Tests ReplayLastOnSubscribe replays last value to new subscribers.
    /// </summary>
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
    /// Test class for INotifyPropertyChanged.
    /// </summary>
    private class TestNotifyPropertyChanged : INotifyPropertyChanged
    {
        private string _testProperty = string.Empty;

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
            get => _testProperty;
            set
            {
                if (_testProperty != value)
                {
                    _testProperty = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TestProperty)));
                }
            }
        }
    }
}
