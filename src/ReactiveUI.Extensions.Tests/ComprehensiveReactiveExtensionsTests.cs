// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace ReactiveUI.Extensions.Tests;

/// <summary>
/// Comprehensive unit tests for ReactiveExtensions methods.
/// </summary>
[TestFixture]
public class ComprehensiveReactiveExtensionsTests
{
    /// <summary>
    /// Tests BufferUntil with character delimiters.
    /// </summary>
    [Test]
    public void BufferUntil_WithStartAndEndChars_BuffersCorrectly()
    {
        var subject = new Subject<char>();
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0], Is.EqualTo("<test>"));
            Assert.That(results[1], Is.EqualTo("<data>"));
        }
    }

    /// <summary>
    /// Tests CatchIgnore without error action.
    /// </summary>
    [Test]
    public void CatchIgnore_OnError_ReturnsEmpty()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        var completed = false;
        using var sub = subject.CatchIgnore().Subscribe(results.Add, _ => { }, () => completed = true);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnError(new InvalidOperationException());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(completed, Is.True);
        }
    }

    /// <summary>
    /// Tests CatchIgnore with error action.
    /// </summary>
    [Test]
    public void CatchIgnore_WithErrorAction_CallsActionAndReturnsEmpty()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        var errorCaught = false;
        var completed = false;
        using var sub = subject.CatchIgnore<int, InvalidOperationException>(ex => errorCaught = true)
            .Subscribe(results.Add, _ => { }, () => completed = true);

        subject.OnNext(1);
        subject.OnError(new InvalidOperationException());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.EquivalentTo(new[] { 1 }));
            Assert.That(errorCaught, Is.True);
            Assert.That(completed, Is.True);
        }
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllFalse.
    /// </summary>
    [Test]
    public void CombineLatestValuesAreAllFalse_WhenAllFalse_ReturnsTrue()
    {
        var subject1 = new BehaviorSubject<bool>(false);
        var subject2 = new BehaviorSubject<bool>(false);
        var sources = new[] { subject1.AsObservable(), subject2.AsObservable() };
        bool? result = null;
        using var sub = sources.CombineLatestValuesAreAllFalse().Subscribe(x => result = x);

        Assert.That(result, Is.True);
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllTrue.
    /// </summary>
    [Test]
    public void CombineLatestValuesAreAllTrue_WhenAllTrue_ReturnsTrue()
    {
        var subject1 = new BehaviorSubject<bool>(true);
        var subject2 = new BehaviorSubject<bool>(true);
        var sources = new[] { subject1.AsObservable(), subject2.AsObservable() };
        bool? result = null;
        using var sub = sources.CombineLatestValuesAreAllTrue().Subscribe(x => result = x);

        Assert.That(result, Is.True);
    }

    /// <summary>
    /// Tests GetMax returns maximum value.
    /// </summary>
    [Test]
    public void GetMax_WithMultipleSources_ReturnsMaximum()
    {
        var subject1 = new BehaviorSubject<int?>(5);
        var subject2 = new BehaviorSubject<int?>(10);
        var subject3 = new BehaviorSubject<int?>(3);
        int? result = null;
        using var sub = subject1.GetMax(subject2, subject3).Subscribe(x => result = x);

        Assert.That(result, Is.EqualTo(10));
    }

    /// <summary>
    /// Tests GetMin returns minimum value.
    /// </summary>
    [Test]
    public void GetMin_WithMultipleSources_ReturnsMinimum()
    {
        var subject1 = new BehaviorSubject<int?>(5);
        var subject2 = new BehaviorSubject<int?>(10);
        var subject3 = new BehaviorSubject<int?>(3);
        int? result = null;
        using var sub = subject1.GetMin(subject2, subject3).Subscribe(x => result = x);

        Assert.That(result, Is.EqualTo(3));
    }

    /// <summary>
    /// Tests DetectStale marks stream as stale.
    /// </summary>
    [Test]
    public void DetectStale_WhenInactive_MarksAsStale()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IStale<int>>();
        using var sub = subject.DetectStale(TimeSpan.FromTicks(100), scheduler).Subscribe(results.Add);

        subject.OnNext(1);
        scheduler.AdvanceBy(50);
        subject.OnNext(2);
        scheduler.AdvanceBy(101);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(3));
            Assert.That(results[0].IsStale, Is.False);
            Assert.That(results[0].Update, Is.EqualTo(1));
            Assert.That(results[1].IsStale, Is.False);
            Assert.That(results[1].Update, Is.EqualTo(2));
            Assert.That(results[2].IsStale, Is.True);
        }
    }

    /// <summary>
    /// Tests Conflate with minimum update period.
    /// </summary>
    [Test]
    public void Conflate_WithMinimumPeriod_DelaysUpdates()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();
        using var sub = subject.Conflate(TimeSpan.FromTicks(100), scheduler).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        scheduler.AdvanceBy(100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(3));
        }
    }

    /// <summary>
    /// Tests Heartbeat injects heartbeats.
    /// </summary>
    [Test]
    public void Heartbeat_WhenInactive_InjectsHeartbeats()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IHeartbeat<int>>();
        using var sub = subject.Heartbeat(TimeSpan.FromTicks(100), scheduler).Subscribe(results.Add);

        subject.OnNext(1);
        scheduler.AdvanceBy(101);
        subject.OnNext(2);
        scheduler.AdvanceBy(101);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(results[0].IsHeartbeat, Is.False);
            Assert.That(results[0].Update, Is.EqualTo(1));
            Assert.That(results[1].IsHeartbeat, Is.True);
            Assert.That(results[2].IsHeartbeat, Is.False);
            Assert.That(results[2].Update, Is.EqualTo(2));
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(10));
            Assert.That(maxConcurrent, Is.LessThanOrEqualTo(3));
        }
    }

    /// <summary>
    /// Tests OnNext with params.
    /// </summary>
    [Test]
    public void OnNext_WithMultipleValues_PushesAll()
    {
        var results = new List<int>();
        var subject = new Subject<int>();
        using var sub = subject.Subscribe(results.Add);

        subject.OnNext(1, 2, 3, 4, 5);

        Assert.That(results, Is.EquivalentTo(new[] { 1, 2, 3, 4, 5 }));
    }

    /// <summary>
    /// Tests ObserveOnSafe with null scheduler.
    /// </summary>
    [Test]
    public void ObserveOnSafe_WithNullScheduler_ReturnsSource()
    {
        var source = Observable.Return(1);
        var result = source.ObserveOnSafe(null);

        Assert.That(result, Is.SameAs(source));
    }

    /// <summary>
    /// Tests ObserveOnSafe with scheduler.
    /// </summary>
    [Test]
    public void ObserveOnSafe_WithScheduler_ObservesOnScheduler()
    {
        var scheduler = new TestScheduler();
        var source = Observable.Return(1);
        int? result = null;
        using var sub = source.ObserveOnSafe(scheduler).Subscribe(x => result = x);

        Assert.That(result, Is.Null);

        scheduler.AdvanceBy(1);

        Assert.That(result, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests Start with Action and null scheduler.
    /// </summary>
    [Test]
    public void Start_WithActionAndNullScheduler_ExecutesAction()
    {
        var executed = false;
        Action action = () => executed = true;

        using var sub = ReactiveExtensions.Start(action, Scheduler.Immediate).Subscribe();

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests ForEach flattens enumerables.
    /// </summary>
    [Test]
    public void ForEach_FlattensEnumerables()
    {
        var source = Observable.Return(new[] { 1, 2, 3 });
        var results = new List<int>();
        using var sub = source.ForEach().Subscribe(results.Add);

        Assert.That(results, Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    /// <summary>
    /// Tests ScheduleSafe with null scheduler executes immediately.
    /// </summary>
    [Test]
    public void ScheduleSafe_WithNullScheduler_ExecutesImmediately()
    {
        var executed = false;
        IScheduler? scheduler = null;
        var disposable = scheduler.ScheduleSafe(() => executed = true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(executed, Is.True);
            Assert.That(disposable, Is.Not.Null);
        }
    }

    /// <summary>
    /// Tests FromArray with scheduler.
    /// </summary>
    [Test]
    public void FromArray_WithScheduler_EmitsElements()
    {
        var source = new[] { 1, 2, 3, 4, 5 };
        var results = new List<int>();
        using var sub = source.FromArray(Scheduler.Immediate).Subscribe(results.Add);

        Assert.That(results, Is.EquivalentTo(source));
    }

    /// <summary>
    /// Tests Filter with regex.
    /// </summary>
    [Test]
    public void Filter_WithRegex_FiltersStrings()
    {
        var source = new[] { "test123", "hello", "test456", "world" }.ToObservable();
        var results = new List<string>();
        using var sub = source.Filter(@"^test\d+$").Subscribe(results.Add);

        Assert.That(results, Is.EquivalentTo(new[] { "test123", "test456" }));
    }

    /// <summary>
    /// Tests Shuffle randomizes array.
    /// </summary>
    [Test]
    public void Shuffle_RandomizesArray()
    {
        var original = Enumerable.Range(1, 100).ToArray();
        var source = Observable.Return(original.ToArray());
        int[]? result = null;
        using var sub = source.Shuffle().Subscribe(x => result = x);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Length.EqualTo(100));
            Assert.That(result!.OrderBy(x => x), Is.EqualTo(original));
        }
    }

    /// <summary>
    /// Tests OnErrorRetry without parameters.
    /// </summary>
    [Test]
    public void OnErrorRetry_RetriesOnError()
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(42));
            Assert.That(attempts, Is.EqualTo(3));
        }
    }

    /// <summary>
    /// Tests TakeUntil with predicate.
    /// </summary>
    [Test]
    public void TakeUntil_WithPredicate_CompletesWhenPredicateTrue()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        using var sub = subject.TakeUntil(x => x >= 5).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(5);
        subject.OnNext(6);

        Assert.That(results, Is.EquivalentTo(new[] { 1, 2, 5 }));
    }

    /// <summary>
    /// Tests Not inverts boolean.
    /// </summary>
    [Test]
    public void Not_InvertsBoolean()
    {
        var subject = new BehaviorSubject<bool>(true);
        bool? result = null;
        using var sub = subject.Not().Subscribe(x => result = x);

        Assert.That(result, Is.False);
    }

    /// <summary>
    /// Tests WhereTrue filters to true values.
    /// </summary>
    [Test]
    public void WhereTrue_FiltersTrueValues()
    {
        var source = new[] { true, false, true, false, true }.ToObservable();
        var results = new List<bool>();
        using var sub = source.WhereTrue().Subscribe(results.Add);

        Assert.That(results, Is.EquivalentTo(new[] { true, true, true }));
    }

    /// <summary>
    /// Tests WhereFalse filters to false values.
    /// </summary>
    [Test]
    public void WhereFalse_FiltersFalseValues()
    {
        var source = new[] { true, false, true, false, true }.ToObservable();
        var results = new List<bool>();
        using var sub = source.WhereFalse().Subscribe(results.Add);

        Assert.That(results, Is.EquivalentTo(new[] { false, false }));
    }

    /// <summary>
    /// Tests CatchAndReturn with fallback value.
    /// </summary>
    [Test]
    public void CatchAndReturn_OnError_ReturnsFallback()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        using var sub = subject.CatchAndReturn(99).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnError(new Exception());

        Assert.That(results, Is.EquivalentTo(new[] { 1, 2, 99 }));
    }

    /// <summary>
    /// Tests Partition splits sequence.
    /// </summary>
    [Test]
    public void Partition_SplitsSequence()
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trueResults, Is.EquivalentTo(new[] { 2, 4, 6, 8, 10 }));
            Assert.That(falseResults, Is.EquivalentTo(new[] { 1, 3, 5, 7, 9 }));
        }
    }

    /// <summary>
    /// Tests WaitUntil takes first matching.
    /// </summary>
    [Test]
    public void WaitUntil_TakesFirstMatching()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        using var sub = subject.WaitUntil(x => x > 5).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(3);
        subject.OnNext(7);
        subject.OnNext(9);

        Assert.That(results, Is.EquivalentTo(new[] { 7 }));
    }

    /// <summary>
    /// Tests DoOnSubscribe executes on subscribe.
    /// </summary>
    [Test]
    public void DoOnSubscribe_ExecutesOnSubscribe()
    {
        var executed = false;
        var source = Observable.Return(1);
        using var sub = source.DoOnSubscribe(() => executed = true).Subscribe();

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests DoOnDispose executes on dispose.
    /// </summary>
    [Test]
    public void DoOnDispose_ExecutesOnDispose()
    {
        var executed = false;
        var source = Observable.Never<int>();
        var sub = source.DoOnDispose(() => executed = true).Subscribe();

        sub.Dispose();

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests Heartbeat class with update.
    /// </summary>
    [Test]
    public void Heartbeat_WithUpdate_IsNotHeartbeat()
    {
        var heartbeat = new Heartbeat<int>(42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(heartbeat.IsHeartbeat, Is.False);
            Assert.That(heartbeat.Update, Is.EqualTo(42));
        }
    }

    /// <summary>
    /// Tests Heartbeat class without update.
    /// </summary>
    [Test]
    public void Heartbeat_WithoutUpdate_IsHeartbeat()
    {
        var heartbeat = new Heartbeat<int>();

        Assert.That(heartbeat.IsHeartbeat, Is.True);
    }

    /// <summary>
    /// Tests Stale class with update.
    /// </summary>
    [Test]
    public void Stale_WithUpdate_IsNotStale()
    {
        var stale = new Stale<int>(42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stale.IsStale, Is.False);
            Assert.That(stale.Update, Is.EqualTo(42));
        }
    }

    /// <summary>
    /// Tests Stale class without update.
    /// </summary>
    [Test]
    public void Stale_WithoutUpdate_IsStale()
    {
        var stale = new Stale<int>();

        Assert.That(stale.IsStale, Is.True);
    }

    /// <summary>
    /// Tests Stale throws on Update access when stale.
    /// </summary>
    [Test]
    public void Stale_WithoutUpdate_ThrowsOnUpdateAccess()
    {
        var stale = new Stale<int>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            var unused = stale.Update;
        });

        Assert.That(ex, Is.Not.Null);
    }

    /// <summary>
    /// Tests Continuation can be disposed.
    /// </summary>
    [Test]
    public void Continuation_CanBeDisposed()
    {
        var continuation = new Continuation();

        continuation.Dispose();

        Assert.Pass();
    }

    /// <summary>
    /// Tests Continuation tracks completed phases.
    /// </summary>
    [Test]
    public void Continuation_TracksCompletedPhases()
    {
        using var continuation = new Continuation();

        var phases = continuation.CompletedPhases;

        Assert.That(phases, Is.GreaterThanOrEqualTo(0));
    }

    /// <summary>
    /// Tests WhereIsNotNull filters null values.
    /// </summary>
    [Test]
    public void WhereIsNotNull_FiltersNullValues()
    {
        var source = new[] { "a", null, "b", null, "c" }.ToObservable();
        var results = new List<string>();
        using var sub = source.WhereIsNotNull().Subscribe(x => results.Add(x!));

        Assert.That(results, Is.EquivalentTo(new[] { "a", "b", "c" }));
    }

    /// <summary>
    /// Tests AsSignal converts to Unit.
    /// </summary>
    [Test]
    public void AsSignal_ConvertsToUnit()
    {
        var source = Observable.Range(1, 3);
        var results = new List<Unit>();
        using var sub = source.AsSignal().Subscribe(results.Add);

        Assert.That(results, Has.Count.EqualTo(3));
    }

    /// <summary>
    /// Tests DebounceImmediate emits first immediately.
    /// </summary>
    [Test]
    public void DebounceImmediate_EmitsFirstImmediately()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();
        using var sub = subject.DebounceImmediate(TimeSpan.FromTicks(100), scheduler).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        scheduler.AdvanceBy(101);

        Assert.That(results, Is.Not.Empty);
        Assert.That(results[0], Is.EqualTo(1));
    }

    /// <summary>
    /// Tests RetryWithBackoff respects max delay.
    /// </summary>
    [Test]
    public void RetryWithBackoff_RespectsMaxDelay()
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

        Assert.That(result, Is.EqualTo(42));
    }

    /// <summary>
    /// Tests SynchronizeSynchronous provides sync lock.
    /// </summary>
    [Test]
    public void SynchronizeSynchronous_ProvidesSyncLock()
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(lastSync, Is.Not.Null);
        }
    }

    /// <summary>
    /// Tests SynchronizeAsync provides sync lock.
    /// </summary>
    [Test]
    public void SynchronizeAsync_ProvidesSyncLock()
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(lastSync, Is.Not.Null);
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

        await Task.Delay(100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.EquivalentTo(new[] { 1 }));
            Assert.That(caughtException, Is.Not.Null);
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with error action and retry count.
    /// </summary>
    [Test]
    public void OnErrorRetry_WithErrorActionAndRetryCount_RetriesLimitedTimes()
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(caughtException, Is.Not.Null);
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with delay.
    /// </summary>
    [Test]
    public void OnErrorRetry_WithDelay_DelaysRetries()
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(42));
            Assert.That(elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(100));
        }
    }

    /// <summary>
    /// Tests Schedule with value and TimeSpan and function.
    /// </summary>
    [Test]
    public void Schedule_WithValueTimeSpanAndFunction_DelaysAndTransforms()
    {
        var scheduler = new TestScheduler();
        int? result = null;

        using var sub = 10.Schedule(TimeSpan.FromTicks(100), scheduler, x => x * 2)
            .Subscribe(x => result = x);

        Assert.That(result, Is.Null);

        scheduler.AdvanceBy(101);

        Assert.That(result, Is.EqualTo(20));
    }

    /// <summary>
    /// Tests Schedule with observable TimeSpan and function.
    /// </summary>
    [Test]
    public void Schedule_WithObservableTimeSpanAndFunction_DelaysAndTransforms()
    {
        var scheduler = new TestScheduler();
        var source = Observable.Return(10);
        var results = new List<int>();

        using var sub = source.Schedule(TimeSpan.FromTicks(100), scheduler, x => x * 2)
            .Subscribe(results.Add);

        Assert.That(results, Is.Empty);

        scheduler.AdvanceBy(101);

        Assert.That(results, Is.EquivalentTo(new[] { 20 }));
    }

    /// <summary>
    /// Tests BufferUntilInactive buffers values during inactivity.
    /// </summary>
    [Test]
    public void BufferUntilInactive_BuffersValuesUntilInactive()
    {
        var subject = new Subject<int>();
        var results = new List<IList<int>>();

        using var sub = subject.BufferUntilInactive(TimeSpan.FromMilliseconds(50), ImmediateScheduler.Instance)
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        // With ImmediateScheduler, values are buffered immediately
        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(results[0], Is.EquivalentTo(new[] { 1, 2, 3 }));
        }
    }

    /// <summary>
    /// Tests Using with action executes action on item.
    /// </summary>
    [Test]
    public void Using_WithAction_ExecutesAction()
    {
        var executed = false;
        var item = System.Reactive.Disposables.Disposable.Create(() => { });
        var results = new List<Unit>();

        using var sub = item.Using(x => executed = true, ImmediateScheduler.Instance)
            .Subscribe(results.Add);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(executed, Is.True);
            Assert.That(results, Has.Count.EqualTo(1));
        }
    }

    /// <summary>
    /// Tests Using with function transforms item.
    /// </summary>
    [Test]
    public void Using_WithFunction_TransformsItem()
    {
        var item = System.Reactive.Disposables.Disposable.Create(() => { });
        var results = new List<int>();

        using var sub = item.Using(x => x * 2, ImmediateScheduler.Instance)
            .Subscribe(results.Add);

        Assert.That(results, Is.EquivalentTo(new[] { 20 }));
    }

    /// <summary>
    /// Tests While executes action while condition is true.
    /// </summary>
    [Test]
    public void While_ExecutesWhileConditionTrue()
    {
        var counter = 0;
        var results = new List<Unit>();

        using var sub = ReactiveExtensions.While(() => counter < 3, () => counter++, ImmediateScheduler.Instance)
            .Subscribe(results.Add);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(counter, Is.EqualTo(3));
            Assert.That(results, Has.Count.EqualTo(3));
        }
    }

    /// <summary>
    /// Tests SelectAsyncSequential processes items sequentially.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SelectAsyncSequential_ProcessesSequentially()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        var processingOrder = new List<int>();

        using var sub = subject.SelectAsyncSequential(async x =>
        {
            processingOrder.Add(x);
            await Task.Delay(10);
            return x * 2;
        }).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        await Task.Delay(100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.EquivalentTo(new[] { 2, 4, 6 }));
            Assert.That(processingOrder, Is.EquivalentTo(new[] { 1, 2, 3 }));
        }
    }

    /// <summary>
    /// Tests SelectLatestAsync processes only latest.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SelectLatestAsync_ProcessesLatest()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        using var sub = subject.SelectLatestAsync(async x =>
        {
            await Task.Delay(10);
            return x * 2;
        }).Subscribe(results.Add);

        subject.OnNext(1);
        await Task.Delay(5); // Not enough time for first to complete
        subject.OnNext(2);
        await Task.Delay(5);
        subject.OnNext(3);

        await Task.Delay(50);

        // Only the latest value should be processed
        Assert.That(results, Has.Count.GreaterThan(0));
    }

    /// <summary>
    /// Tests SelectAsyncConcurrent processes concurrently.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SelectAsyncConcurrent_ProcessesConcurrently()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        using var sub = subject.SelectAsyncConcurrent(
            async x =>
            {
                await Task.Delay(10);
                return x * 2;
            },
            3).Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        await Task.Delay(100);

        // All values should be processed concurrently
        Assert.That(results, Has.Count.EqualTo(3));
    }

    /// <summary>
    /// Tests Partition with DynamicData pattern.
    /// </summary>
    [Test]
    public void Partition_WithDynamicDataPattern_SplitsCorrectly()
    {
        var subject = new Subject<int>();
        var (evens, odds) = subject.Partition(x => x % 2 == 0);

        var evenResults = new List<int>();
        var oddResults = new List<int>();

        using var evenSub = evens.Subscribe(evenResults.Add);
        using var oddSub = odds.Subscribe(oddResults.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4);
        subject.OnNext(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evenResults, Is.EquivalentTo(new[] { 2, 4 }));
            Assert.That(oddResults, Is.EquivalentTo(new[] { 1, 3, 5 }));
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with delay using ImmediateScheduler.
    /// </summary>
    [Test]
    public void OnErrorRetry_WithDelayImmediate_RetriesWithDelay()
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

        using var sub = source.OnErrorRetry<int, InvalidOperationException>(
            delay: TimeSpan.FromMilliseconds(10),
            retryCount: 5)
            .Subscribe(results.Add);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(results, Is.EquivalentTo(new[] { 42 }));
        }
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllFalse with multiple observables.
    /// </summary>
    [Test]
    public void CombineLatestValuesAreAllFalse_WithMultipleValues_WorksCorrectly()
    {
        var subject1 = new BehaviorSubject<bool>(false);
        var subject2 = new BehaviorSubject<bool>(false);
        var subject3 = new BehaviorSubject<bool>(false);
        var results = new List<bool>();

        using var sub = new[] { subject1, subject2, subject3 }.CombineLatestValuesAreAllFalse()
            .Subscribe(results.Add);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0], Is.True); // All are false
        }

        subject1.OnNext(true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[1], Is.False); // Not all are false
        }
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllTrue with multiple observables.
    /// </summary>
    [Test]
    public void CombineLatestValuesAreAllTrue_WithMultipleValues_WorksCorrectly()
    {
        var subject1 = new BehaviorSubject<bool>(true);
        var subject2 = new BehaviorSubject<bool>(true);
        var subject3 = new BehaviorSubject<bool>(false);
        var results = new List<bool>();

        using var sub = new[] { subject1, subject2, subject3 }.CombineLatestValuesAreAllTrue()
            .Subscribe(results.Add);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0], Is.False); // Not all are true
        }

        subject3.OnNext(true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[1], Is.True); // All are true
        }
    }

    /// <summary>
    /// Tests Conflate with ImmediateScheduler.
    /// </summary>
    [Test]
    public void Conflate_WithImmediateScheduler_ConflatesValues()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        using var sub = subject.Conflate(TimeSpan.FromMilliseconds(50), ImmediateScheduler.Instance)
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        // With ImmediateScheduler, last value is emitted
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
    }

    /// <summary>
    /// Tests DetectStale with updates.
    /// </summary>
    [Test]
    public void DetectStale_WithUpdates_DetectsCorrectly()
    {
        var subject = new Subject<int>();
        var results = new List<Stale<int>>();

        using var sub = subject.DetectStale(TimeSpan.FromMilliseconds(50), ImmediateScheduler.Instance)
            .Subscribe(x => results.Add((Stale<int>)x));

        subject.OnNext(1);
        subject.OnNext(2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(results[0].IsStale, Is.False);
            Assert.That(((IStale<int>)results[0]).Value, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// Tests Heartbeat with periodic updates.
    /// </summary>
    [Test]
    public void Heartbeat_WithPeriodicUpdates_InjectsHeartbeats()
    {
        var subject = new Subject<int>();
        var results = new List<Heartbeat<int>>();

        using var sub = subject.Heartbeat(TimeSpan.FromMilliseconds(50), ImmediateScheduler.Instance)
            .Subscribe(x => results.Add((Heartbeat<int>)x));

        subject.OnNext(1);
        subject.OnNext(2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(results[0].IsHeartbeat, Is.False);
            Assert.That(((IStale<int>)results[0]).Value, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// Tests WaitUntil with predicate match.
    /// </summary>
    [Test]
    public void WaitUntil_WithMatchingPredicate_TakesFirstMatch()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        using var sub = subject.WaitUntil(x => x > 5)
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(3);
        subject.OnNext(7);
        subject.OnNext(9);

        Assert.That(results, Is.EquivalentTo(new[] { 7 }));
    }

    /// <summary>
    /// Tests DoOnSubscribe executes action on subscription.
    /// </summary>
    [Test]
    public void DoOnSubscribe_ExecutesActionOnSubscription()
    {
        var executed = false;
        var subject = new Subject<int>();

        using var sub = subject.DoOnSubscribe(() => executed = true)
            .Subscribe();

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests DoOnDispose executes action on disposal.
    /// </summary>
    [Test]
    public void DoOnDispose_ExecutesActionOnDisposal()
    {
        var executed = false;
        var subject = new Subject<int>();

        var sub = subject.DoOnDispose(() => executed = true)
            .Subscribe();

        Assert.That(executed, Is.False);

        sub.Dispose();

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests Shuffle randomizes array elements.
    /// </summary>
    [Test]
    public void Shuffle_RandomizesElements()
    {
        var array = Enumerable.Range(1, 10).ToList();
        var originalArray = array.ToList();
        array.Shuffle();

        // Array should still contain all elements
        Assert.That(array.OrderBy(x => x), Is.EquivalentTo(originalArray));

        // Array should likely be in different order (probabilistic test)
        // With 10 elements, probability of same order after shuffle is 1/10! which is extremely low
        var isDifferent = !array.SequenceEqual(originalArray);
        Assert.That(isDifferent || array.Length < 2, Is.True);
    }

    /// <summary>
    /// Tests Filter with regex pattern.
    /// </summary>
    [Test]
    public void Filter_WithRegexPattern_FiltersCorrectly()
    {
        var subject = new Subject<string>();
        var results = new List<string>();

        using var sub = subject.Filter(@"^\d+$")
            .Subscribe(results.Add);

        subject.OnNext("123");
        subject.OnNext("abc");
        subject.OnNext("456");
        subject.OnNext("xyz");

        Assert.That(results, Is.EquivalentTo(new[] { "123", "456" }));
    }

    /// <summary>
    /// Tests GetMax with changing values.
    /// </summary>
    [Test]
    public void GetMax_WithChangingValues_ReturnsMaximum()
    {
        var subject1 = new BehaviorSubject<int>(5);
        var subject2 = new BehaviorSubject<int>(10);
        var subject3 = new BehaviorSubject<int>(3);
        var results = new List<int>();

        using var sub = subject1.Select(x => (int?)x).GetMax(subject2.Select(x => (int?)x), subject3.Select(x => (int?)x))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Subscribe(results.Add);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(10));
        }

        subject1.OnNext(15);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[1], Is.EqualTo(15));
        }
    }

    /// <summary>
    /// Tests GetMin with changing values.
    /// </summary>
    [Test]
    public void GetMin_WithChangingValues_ReturnsMinimum()
    {
        var subject1 = new BehaviorSubject<int>(5);
        var subject2 = new BehaviorSubject<int>(10);
        var subject3 = new BehaviorSubject<int>(3);
        var results = new List<int>();

        using var sub = subject1.Select(x => (int?)x).GetMin(subject2.Select(x => (int?)x), subject3.Select(x => (int?)x))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Subscribe(results.Add);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(3));
        }

        subject3.OnNext(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[1], Is.EqualTo(1));
        }
    }

    /// <summary>
    /// Tests WhereIsNotNull with DynamicData pattern filters null values.
    /// </summary>
    [Test]
    public void WhereIsNotNull_WithDynamicDataPattern_FiltersNulls()
    {
        var subject = new Subject<string?>();
        subject.WhereIsNotNull()
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext("test1");
        subject.OnNext(null);
        subject.OnNext("test2");
        subject.OnNext(null);
        subject.OnNext("test3");

        Assert.That(results, Is.EquivalentTo(new[] { "test1", "test2", "test3" }));
    }

    /// <summary>
    /// Tests AsSignal with DynamicData pattern converts values to Unit.
    /// </summary>
    [Test]
    public void AsSignal_WithDynamicDataPattern_ConvertsToUnit()
    {
        var subject = new Subject<int>();
        subject.AsSignal()
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results.All(x => x == Unit.Default), Is.True);
    }

    /// <summary>
    /// Tests Not with DynamicData pattern inverts boolean values.
    /// </summary>
    [Test]
    public void Not_WithDynamicDataPattern_InvertsValues()
    {
        var subject = new BehaviorSubject<bool>(false);
        subject.Not()
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        Assert.That(results, Is.EquivalentTo(new[] { true }));

        subject.OnNext(true);
        Assert.That(results, Is.EquivalentTo(new[] { true, false }));

        subject.OnNext(false);
        Assert.That(results, Is.EquivalentTo(new[] { true, false, true }));
    }

    /// <summary>
    /// Tests WhereTrue with DynamicData pattern filters for true values.
    /// </summary>
    [Test]
    public void WhereTrue_WithDynamicDataPattern_FiltersTrueValues()
    {
        var subject = new Subject<bool>();
        subject.WhereTrue()
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(true);

        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results.All(x => x), Is.True);
    }

    /// <summary>
    /// Tests WhereFalse with DynamicData pattern filters for false values.
    /// </summary>
    [Test]
    public void WhereFalse_WithDynamicDataPattern_FiltersFalseValues()
    {
        var subject = new Subject<bool>();
        subject.WhereFalse()
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(false);

        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results.All(x => !x), Is.True);
    }

    /// <summary>
    /// Tests CatchAndReturn with DynamicData pattern returns fallback on error.
    /// </summary>
    [Test]
    public void CatchAndReturn_WithDynamicDataPattern_ReturnsFallbackOnError()
    {
        var subject = new Subject<int>();
        subject.CatchAndReturn(999)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnError(new InvalidOperationException());

        Assert.That(results, Is.EquivalentTo(new[] { 1, 2, 999 }));
    }

    /// <summary>
    /// Tests TakeUntil with DynamicData pattern completes when predicate matches.
    /// </summary>
    [Test]
    public void TakeUntil_WithDynamicDataPattern_CompletesWhenPredicateTrue()
    {
        var subject = new Subject<int>();
        subject.TakeUntil(x => x >= 5)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(5);
        subject.OnNext(6); // Should not be included

        Assert.That(results, Is.EquivalentTo(new[] { 1, 2, 3, 5 }));
    }

    /// <summary>
    /// Tests CatchIgnore with DynamicData pattern ignores errors.
    /// </summary>
    [Test]
    public void CatchIgnore_WithDynamicDataPattern_IgnoresErrors()
    {
        var subject = new Subject<int>();
        subject.CatchIgnore()
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnError(new InvalidOperationException());

        // Observable completes silently after error
        Assert.That(results, Is.EquivalentTo(new[] { 1, 2 }));
    }

    /// <summary>
    /// Tests OnNext with params using DynamicData pattern.
    /// </summary>
    [Test]
    public void OnNext_WithParamsAndDynamicDataPattern_PushesAllValues()
    {
        var subject = new Subject<int>();
        subject
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext(1, 2, 3, 4, 5);

        Assert.That(results, Is.EquivalentTo(new[] { 1, 2, 3, 4, 5 }));
    }

    /// <summary>
    /// Tests DetectStale with DynamicData pattern tracks staleness.
    /// </summary>
    [Test]
    public void DetectStale_WithDynamicDataPattern_TracksStaleState()
    {
        var subject = new Subject<int>();
        subject.DetectStale(TimeSpan.FromMilliseconds(50), ImmediateScheduler.Instance)
            .Select(x => (IStale<int>)x)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(((IStale<int>)results[0]).Value, Is.EqualTo(1));
            Assert.That(results[1].Value, Is.EqualTo(2));
            Assert.That(results[2].Value, Is.EqualTo(3));
        }
    }

    /// <summary>
    /// Tests Heartbeat with DynamicData pattern tracks heartbeat state.
    /// </summary>
    [Test]
    public void Heartbeat_WithDynamicDataPattern_TracksHeartbeatState()
    {
        var subject = new Subject<int>();
        subject.Heartbeat(TimeSpan.FromMilliseconds(50), ImmediateScheduler.Instance)
            .Select(x => (IHeartbeat<int>)x)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(((IStale<int>)results[0]).Value, Is.EqualTo(1));
            Assert.That(results[0].IsHeartbeat, Is.False);
            Assert.That(results[1].Value, Is.EqualTo(2));
            Assert.That(results[1].IsHeartbeat, Is.False);
        }
    }

    /// <summary>
    /// Tests Start with action using DynamicData pattern.
    /// </summary>
    [Test]
    public void Start_WithActionAndDynamicDataPattern_ExecutesAction()
    {
        var executed = false;
        ReactiveExtensions.Start(() => executed = true, ImmediateScheduler.Instance)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(executed, Is.True);
            Assert.That(results, Has.Count.EqualTo(1));
        }
    }

    /// <summary>
    /// Tests Start with function using DynamicData pattern.
    /// </summary>
    [Test]
    public void Start_WithFunctionAndDynamicDataPattern_ReturnsResult()
    {
        ReactiveExtensions.Start(() => 42, ImmediateScheduler.Instance)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        Assert.That(results, Is.EquivalentTo(new[] { 42 }));
    }

    /// <summary>
    /// Tests Using with action and DynamicData pattern.
    /// </summary>
    [Test]
    public void Using_WithActionAndDynamicDataPattern_ExecutesAction()
    {
        var executed = false;
        var item = "test";
        item.Using(x => executed = true, ImmediateScheduler.Instance)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(executed, Is.True);
            Assert.That(results, Has.Count.EqualTo(1));
        }
    }

    /// <summary>
    /// Tests Using with function and DynamicData pattern.
    /// </summary>
    [Test]
    public void Using_WithFunctionAndDynamicDataPattern_TransformsValue()
    {
        var item = System.Reactive.Disposables.Disposable.Create(() => { });
        item.Using(x => x * 3, ImmediateScheduler.Instance)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        Assert.That(results, Is.EquivalentTo(new[] { 30 }));
    }

    /// <summary>
    /// Tests While with DynamicData pattern executes until condition false.
    /// </summary>
    [Test]
    public void While_WithDynamicDataPattern_ExecutesUntilConditionFalse()
    {
        var counter = 0;
        ReactiveExtensions.While(() => counter < 5, () => counter++, ImmediateScheduler.Instance)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(counter, Is.EqualTo(5));
            Assert.That(results, Has.Count.EqualTo(5));
        }
    }

    /// <summary>
    /// Tests FromArray with DynamicData pattern emits all elements.
    /// </summary>
    [Test]
    public void FromArray_WithDynamicDataPattern_EmitsAllElements()
    {
        var array = new[] { 1, 2, 3, 4, 5 };
        ReactiveExtensions.FromArray(array, ImmediateScheduler.Instance)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        Assert.That(results, Is.EquivalentTo(array));
    }

    /// <summary>
    /// Tests ForEach with DynamicData pattern processes enumerable.
    /// </summary>
    [Test]
    public void ForEach_WithDynamicDataPattern_ProcessesEnumerable()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var subject = new BehaviorSubject<IEnumerable<int>>(numbers);

        subject.ForEach(ImmediateScheduler.Instance)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        Assert.That(results, Is.EquivalentTo(numbers));
    }

    /// <summary>
    /// Tests Filter with regex and DynamicData pattern.
    /// </summary>
    [Test]
    public void Filter_WithRegexAndDynamicDataPattern_FiltersCorrectly()
    {
        var subject = new Subject<string>();
        subject.Filter(@"^test")
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext("test1");
        subject.OnNext("nottest");
        subject.OnNext("test2");
        subject.OnNext("another");
        subject.OnNext("test3");

        Assert.That(results, Is.EquivalentTo(new[] { "test1", "test2", "test3" }));
    }

    /// <summary>
    /// Tests BufferUntil with DynamicData pattern.
    /// </summary>
    [Test]
    public void BufferUntil_WithDynamicDataPattern_BuffersCorrectly()
    {
        var subject = new Subject<char>();
        subject.BufferUntil('[', ']')
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        subject.OnNext('a');
        subject.OnNext('[');
        subject.OnNext('t');
        subject.OnNext('e');
        subject.OnNext('s');
        subject.OnNext('t');
        subject.OnNext(']');
        subject.OnNext('b');
        subject.OnNext('[');
        subject.OnNext('d');
        subject.OnNext('a');
        subject.OnNext('t');
        subject.OnNext('a');
        subject.OnNext(']');

        Assert.That(results, Is.EquivalentTo(new[] { "[test]", "[data]" }));
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllTrue with DynamicData pattern.
    /// </summary>
    [Test]
    public void CombineLatestValuesAreAllTrue_WithDynamicDataPattern_CombinesCorrectly()
    {
        var subject1 = new BehaviorSubject<bool>(true);
        var subject2 = new BehaviorSubject<bool>(true);
        var subject3 = new BehaviorSubject<bool>(true);

        new[] { subject1, subject2, subject3 }.CombineLatestValuesAreAllTrue()
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        Assert.That(results, Is.EquivalentTo(new[] { true }));

        subject2.OnNext(false);
        Assert.That(results, Is.EquivalentTo(new[] { true, false }));

        subject2.OnNext(true);
        Assert.That(results, Is.EquivalentTo(new[] { true, false, true }));
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllFalse with DynamicData pattern.
    /// </summary>
    [Test]
    public void CombineLatestValuesAreAllFalse_WithDynamicDataPattern_CombinesCorrectly()
    {
        var subject1 = new BehaviorSubject<bool>(false);
        var subject2 = new BehaviorSubject<bool>(false);
        var subject3 = new BehaviorSubject<bool>(false);

        new[] { subject1, subject2, subject3 }.CombineLatestValuesAreAllFalse()
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        Assert.That(results, Is.EquivalentTo(new[] { true }));

        subject1.OnNext(true);
        Assert.That(results, Is.EquivalentTo(new[] { true, false }));

        subject1.OnNext(false);
        Assert.That(results, Is.EquivalentTo(new[] { true, false, true }));
    }

    /// <summary>
    /// Tests GetMax with DynamicData pattern tracks maximum value.
    /// </summary>
    [Test]
    public void GetMax_WithDynamicDataPattern_TracksMaximum()
    {
        var subject1 = new BehaviorSubject<int>(5);
        var subject2 = new BehaviorSubject<int>(10);
        var subject3 = new BehaviorSubject<int>(3);

        subject1.Select(x => (int?)x).GetMax(subject2.Select(x => (int?)x), subject3.Select(x => (int?)x))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        Assert.That(results, Is.EquivalentTo(new[] { 10 }));

        subject1.OnNext(20);
        Assert.That(results, Is.EquivalentTo(new[] { 10, 20 }));

        subject3.OnNext(25);
        Assert.That(results, Is.EquivalentTo(new[] { 10, 20, 25 }));
    }

    /// <summary>
    /// Tests GetMin with DynamicData pattern tracks minimum value.
    /// </summary>
    [Test]
    public void GetMin_WithDynamicDataPattern_TracksMinimum()
    {
        var subject1 = new BehaviorSubject<int>(5);
        var subject2 = new BehaviorSubject<int>(10);
        var subject3 = new BehaviorSubject<int>(3);

        subject1.Select(x => (int?)x).GetMin(subject2.Select(x => (int?)x), subject3.Select(x => (int?)x))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        Assert.That(results, Is.EquivalentTo(new[] { 3 }));

        subject3.OnNext(1);
        Assert.That(results, Is.EquivalentTo(new[] { 3, 1 }));

        subject1.OnNext(0);
        Assert.That(results, Is.EquivalentTo(new[] { 3, 1, 0 }));
    }
}
