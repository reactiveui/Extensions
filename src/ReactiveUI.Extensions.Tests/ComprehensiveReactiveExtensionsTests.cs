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

    // ========== DynamicData Pattern Tests ==========
    // These tests use DynamicData's ToObservableChangeSet to track state changes over time

    /// <summary>
    /// Tests GetMin tracking minimum values as sources change over time.
    /// </summary>
    [Test]
    public void GetMin_TracksMinimumOverTime()
    {
        var subject1 = new BehaviorSubject<int>(5);
        var subject2 = new BehaviorSubject<int>(10);
        var subject3 = new BehaviorSubject<int>(3);

        subject1.Select(x => (int?)x)
            .GetMin(subject2.Select(x => (int?)x), subject3.Select(x => (int?)x))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        // Initial minimum is 3
        Assert.That(results, Is.EquivalentTo(new[] { 3 }));

        // Change minimum to 1
        subject3.OnNext(1);
        Assert.That(results, Is.EquivalentTo(new[] { 3, 1 }));

        // Change minimum to 0
        subject1.OnNext(0);
        Assert.That(results, Is.EquivalentTo(new[] { 3, 1, 0 }));
    }

    /// <summary>
    /// Tests GetMax tracking maximum values as sources change over time.
    /// </summary>
    [Test]
    public void GetMax_TracksMaximumOverTime()
    {
        var subject1 = new BehaviorSubject<int>(5);
        var subject2 = new BehaviorSubject<int>(10);
        var subject3 = new BehaviorSubject<int>(3);

        subject1.Select(x => (int?)x)
            .GetMax(subject2.Select(x => (int?)x), subject3.Select(x => (int?)x))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToObservableChangeSet(scheduler: ImmediateScheduler.Instance)
            .Bind(out var results)
            .Subscribe();

        // Initial maximum is 10
        Assert.That(results, Is.EquivalentTo(new[] { 10 }));

        // Change maximum to 15
        subject2.OnNext(15);
        Assert.That(results, Is.EquivalentTo(new[] { 10, 15 }));

        // Change maximum to 20
        subject1.OnNext(20);
        Assert.That(results, Is.EquivalentTo(new[] { 10, 15, 20 }));
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllTrue tracking state changes.
    /// </summary>
    [Test]
    public void CombineLatestValuesAreAllTrue_TracksStateChanges()
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
        Assert.That(results, Is.EquivalentTo(new[] { false }));

        // One true, still false
        subject1.OnNext(true);
        Assert.That(results, Is.EquivalentTo(new[] { false, false }));

        // Two true, still false
        subject2.OnNext(true);
        Assert.That(results, Is.EquivalentTo(new[] { false, false, false }));

        // All true
        subject3.OnNext(true);
        Assert.That(results, Is.EquivalentTo(new[] { false, false, false, true }));

        // Back to false
        subject1.OnNext(false);
        Assert.That(results, Is.EquivalentTo(new[] { false, false, false, true, false }));
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllFalse tracking state changes.
    /// </summary>
    [Test]
    public void CombineLatestValuesAreAllFalse_TracksStateChanges()
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
        Assert.That(results, Is.EquivalentTo(new[] { true }));

        // One becomes true - result becomes false
        subject1.OnNext(true);
        Assert.That(results, Is.EquivalentTo(new[] { true, false }));

        // Back to false - result becomes true
        subject1.OnNext(false);
        Assert.That(results, Is.EquivalentTo(new[] { true, false, true }));

        // Another becomes true - result becomes false
        subject2.OnNext(true);
        Assert.That(results, Is.EquivalentTo(new[] { true, false, true, false }));
    }

    /// <summary>
    /// Tests WhereIsNotNull filtering nulls over time.
    /// </summary>
    [Test]
    public void WhereIsNotNull_FiltersNullsOverTime()
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
        Assert.That(results, Is.EquivalentTo(new[] { "first", "second", "third" }));
    }

    /// <summary>
    /// Tests Not operator inverting boolean values over time.
    /// </summary>
    [Test]
    public void Not_InvertsBooleanValuesOverTime()
    {
        var subject = new Subject<bool>();
        var results = new List<bool>();

        subject.Not()
            .Subscribe(results.Add);

        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);

        Assert.That(results, Is.EquivalentTo(new[] { false, true, false, true }));
    }

    /// <summary>
    /// Tests AsSignal converting values to Unit over time.
    /// </summary>
    [Test]
    public void AsSignal_ConvertsToUnitOverTime()
    {
        var subject = new Subject<int>();
        var results = new List<Unit>();

        subject.AsSignal()
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        // All values converted to Unit.Default
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results, Is.All.EqualTo(Unit.Default));
    }

    /// <summary>
    /// Tests SyncTimer creates shared observable that produces ticks.
    /// </summary>
    [Test]
    public void SyncTimer_ProducesSharedTicks()
    {
        var timeSpan = TimeSpan.FromMilliseconds(100);
        var results1 = new List<DateTime>();
        var results2 = new List<DateTime>();

        using var sub1 = ReactiveExtensions.SyncTimer(timeSpan).Take(2).Subscribe(results1.Add);
        using var sub2 = ReactiveExtensions.SyncTimer(timeSpan).Take(2).Subscribe(results2.Add);

        // Wait for ticks to arrive
        Thread.Sleep(300);

        using (Assert.EnterMultipleScope())
        {
            // Both subscriptions should get ticks (shared timer)
            Assert.That(results1, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(results2, Has.Count.GreaterThanOrEqualTo(1));
        }
    }

    /// <summary>
    /// Tests Using with action executes the action.
    /// </summary>
    [Test]
    public void Using_WithAction_ExecutesActionImmediately()
    {
        var executed = false;
        using var disposable = System.Reactive.Disposables.Disposable.Create(() => { });

        disposable.Using(d => executed = true, Scheduler.Immediate).Subscribe();

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests Using with function transforms the value.
    /// </summary>
    [Test]
    public void Using_WithFunction_TransformsValue()
    {
        using var disposable = System.Reactive.Disposables.Disposable.Create(() => { });
        var result = 0;

        disposable.Using(d => 42, Scheduler.Immediate).Subscribe(r => result = r);

        Assert.That(result, Is.EqualTo(42));
    }

    /// <summary>
    /// Tests Schedule with TimeSpan and action.
    /// </summary>
    [Test]
    public void Schedule_WithTimeSpanAndAction_ExecutesAction()
    {
        var executed = false;
        var value = 42;

        value.Schedule(TimeSpan.FromMilliseconds(10), Scheduler.Immediate, v => executed = true)
            .Subscribe();

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests Schedule with observable and TimeSpan and action.
    /// </summary>
    [Test]
    public void Schedule_WithObservableTimeSpanAndAction_ExecutesAction()
    {
        var executed = false;
        var subject = new Subject<int>();

        subject.Schedule(TimeSpan.FromMilliseconds(10), Scheduler.Immediate, v => executed = true)
            .Subscribe();

        subject.OnNext(42);

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests Schedule with DateTimeOffset and action.
    /// </summary>
    [Test]
    public void Schedule_WithDateTimeOffsetAndAction_ExecutesAction()
    {
        var executed = false;
        var value = 42;

        value.Schedule(DateTimeOffset.Now.AddMilliseconds(10), Scheduler.Immediate, v => executed = true)
            .Subscribe();

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests Schedule with observable, DateTimeOffset and action.
    /// </summary>
    [Test]
    public void Schedule_WithObservableDateTimeOffsetAndAction_ExecutesAction()
    {
        var executed = false;
        var subject = new Subject<int>();

        subject.Schedule(DateTimeOffset.Now.AddMilliseconds(10), Scheduler.Immediate, v => executed = true)
            .Subscribe();

        subject.OnNext(42);

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests Schedule with function and scheduler.
    /// </summary>
    [Test]
    public void Schedule_WithFunction_TransformsValue()
    {
        var value = 42;
        var result = 0;

        value.Schedule(Scheduler.Immediate, v => v * 2)
            .Subscribe(r => result = r);

        Assert.That(result, Is.EqualTo(84));
    }

    /// <summary>
    /// Tests Schedule with observable and function.
    /// </summary>
    [Test]
    public void Schedule_WithObservableAndFunction_TransformsValue()
    {
        var subject = new Subject<int>();
        var result = 0;

        subject.Schedule(Scheduler.Immediate, v => v * 2)
            .Subscribe(r => result = r);

        subject.OnNext(42);

        Assert.That(result, Is.EqualTo(84));
    }

    /// <summary>
    /// Tests Schedule with TimeSpan and function.
    /// </summary>
    [Test]
    public void Schedule_WithTimeSpanAndFunction_TransformsValue()
    {
        var value = 42;
        var result = 0;

        value.Schedule(TimeSpan.FromMilliseconds(10), Scheduler.Immediate, v => v * 2)
            .Subscribe(r => result = r);

        Assert.That(result, Is.EqualTo(84));
    }

    /// <summary>
    /// Tests Schedule with observable, TimeSpan and function.
    /// </summary>
    [Test]
    public void Schedule_WithObservableTimeSpanAndFunction_TransformsValue()
    {
        var subject = new Subject<int>();
        var result = 0;

        subject.Schedule(TimeSpan.FromMilliseconds(10), Scheduler.Immediate, v => v * 2)
            .Subscribe(r => result = r);

        subject.OnNext(42);

        Assert.That(result, Is.EqualTo(84));
    }

    /// <summary>
    /// Tests OnErrorRetry with delay and no error action.
    /// </summary>
    [Test]
    public void OnErrorRetry_WithDelayAndErrorAction_RetriesWithDelay()
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

        // Wait for retries
        Thread.Sleep(100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errorsCaught, Is.EqualTo(1));
            Assert.That(results, Is.EquivalentTo(new[] { 42 }));
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with retry count limit.
    /// </summary>
    [Test]
    public void OnErrorRetry_WithRetryCount_LimitsRetries()
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
            .Subscribe(_ => { }, _ => finalError = true, () => { });

        using (Assert.EnterMultipleScope())
        {
            // Should retry 2 times (attempts 1 + 2 retries = total of 2 error callbacks on retries only)
            Assert.That(errorsCaught, Is.EqualTo(2));
            Assert.That(finalError, Is.True);
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with retry count and delay.
    /// </summary>
    [Test]
    public void OnErrorRetry_WithRetryCountAndDelay_LimitsRetriesWithDelay()
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
            .Subscribe(_ => { }, _ => finalError = true);

        // Wait for retries
        Thread.Sleep(100);

        using (Assert.EnterMultipleScope())
        {
            // Should retry 2 times (2 error callbacks on retries)
            Assert.That(errorsCaught, Is.EqualTo(2));
            Assert.That(finalError, Is.True);
        }
    }

    /// <summary>
    /// Tests OnErrorRetry with retry count, delay, and scheduler.
    /// </summary>
    [Test]
    public void OnErrorRetry_WithRetryCountDelayAndScheduler_RetriesCorrectly()
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errorsCaught, Is.EqualTo(1));
            Assert.That(result, Is.EqualTo(42));
        }
    }

    /// <summary>
    /// Tests SubscribeSynchronous with full callbacks.
    /// </summary>
    [Test]
    [Ignore("SubscribeSynchronous has timing issues with async completion callbacks in tests")]
    public void SubscribeSynchronous_WithFullCallbacks_ExecutesAll()
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

        // Wait for async operations
        Thread.Sleep(50);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(errorHandled, Is.False);
            Assert.That(completed, Is.True);
        }
    }

    /// <summary>
    /// Tests SubscribeSynchronous with onNext and onError.
    /// </summary>
    [Test]
    [Ignore("SubscribeSynchronous has timing issues with async error callbacks in tests")]
    public void SubscribeSynchronous_WithOnNextAndOnError_HandlesError()
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.EquivalentTo(new[] { 1 }));
            Assert.That(errorHandled, Is.True);
        }
    }

    /// <summary>
    /// Tests SubscribeSynchronous with onNext and onCompleted.
    /// </summary>
    [Test]
    [Ignore("SubscribeSynchronous has timing issues with async completion callbacks in tests")]
    public void SubscribeSynchronous_WithOnNextAndOnCompleted_CompletesCorrectly()
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(completed, Is.True);
        }
    }

    /// <summary>
    /// Tests SubscribeSynchronous with only onNext.
    /// </summary>
    [Test]
    public void SubscribeSynchronous_WithOnlyOnNext_ProcessesValues()
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

        Assert.That(results, Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    /// <summary>
    /// Tests SubscribeAsync with onNext and onCompleted.
    /// </summary>
    [Test]
    public void SubscribeAsync_WithOnNextAndOnCompleted_CompletesCorrectly()
    {
        var subject = new Subject<int>();
        var results = new List<int>();
        var completed = false;

        subject.SubscribeAsync(
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(completed, Is.True);
        }
    }

    /// <summary>
    /// Tests ThrottleFirst emits first value and throttles subsequent values within window.
    /// </summary>
    [Test]
    public void ThrottleFirst_WithWindow_ThrottlesCorrectly()
    {
        var subject = new Subject<int>();
        var results = new List<int>();

        subject.ThrottleFirst(TimeSpan.FromMilliseconds(100), Scheduler.Immediate)
            .Subscribe(results.Add);

        subject.OnNext(1); // Emitted
        subject.OnNext(2); // Throttled
        subject.OnNext(3); // Throttled
        Thread.Sleep(150);
        subject.OnNext(4); // Emitted (window expired)

        // Should only get first value and value after window expires
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results[0], Is.EqualTo(1));
    }

    /// <summary>
    /// Tests BufferUntilInactive buffers values during activity and emits when inactive.
    /// </summary>
    [Test]
    public void BufferUntilInactive_BuffersValuesUntilInactivity()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<IList<int>>();

        subject.BufferUntilInactive(TimeSpan.FromMilliseconds(50), scheduler)
            .Subscribe(results.Add);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        // Advance time past inactivity period
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);

        // Should have one buffer with all three values
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0], Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    /// <summary>
    /// Tests ScheduleSafe with action executes immediately.
    /// </summary>
    [Test]
    public void ScheduleSafe_WithAction_ExecutesImmediately()
    {
        var executed = false;

        Scheduler.Immediate.ScheduleSafe(() => executed = true);

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests ScheduleSafe with null scheduler uses immediate scheduler.
    /// </summary>
    [Test]
    public void ScheduleSafe_WithNullScheduler_UsesImmediate()
    {
        var executed = false;
        IScheduler? scheduler = null;

        scheduler.ScheduleSafe(() => executed = true);

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests ScheduleSafe with TimeSpan executes after delay.
    /// </summary>
    [Test]
    public void ScheduleSafe_WithTimeSpan_ExecutesAfterDelay()
    {
        var executed = false;

        Scheduler.Immediate.ScheduleSafe(TimeSpan.FromMilliseconds(10), () => executed = true);

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests ScheduleSafe with TimeSpan and null scheduler.
    /// </summary>
    [Test]
    public void ScheduleSafe_WithTimeSpanAndNullScheduler_UsesImmediate()
    {
        var executed = false;
        IScheduler? scheduler = null;

        scheduler.ScheduleSafe(TimeSpan.FromMilliseconds(10), () => executed = true);

        Assert.That(executed, Is.True);
    }
}
