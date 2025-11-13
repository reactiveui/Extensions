// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
    /// Tests SyncTimer creates shared timer for same TimeSpan.
    /// </summary>
    [Test]
    public void SyncTimer_WithSameTimeSpan_SharesTimer()
    {
        var timeSpan = TimeSpan.FromMilliseconds(100);
        var values1 = new List<DateTime>();
        var values2 = new List<DateTime>();

        using var sub1 = ReactiveExtensions.SyncTimer(timeSpan).Take(2).Subscribe(values1.Add);
        using var sub2 = ReactiveExtensions.SyncTimer(timeSpan).Take(2).Subscribe(values2.Add);
        Thread.Sleep(250);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values1, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(values2, Has.Count.GreaterThanOrEqualTo(2));
        }
    }

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
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0], Is.EqualTo(1));
            Assert.That(results[1], Is.EqualTo(3));
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

        var tasks = Enumerable.Range(1, 10).Select(async i =>
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

            return i;
        }).Select(t => t).ToList();

        var results = await tasks.WithLimitedConcurrency(3).ToList();

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

        using var sub = ReactiveExtensions.Start(action, null).Subscribe();
        Thread.Sleep(100);

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
        using var sub = source.OnErrorRetry().Take(1).Subscribe(results.Add);
        Thread.Sleep(100);

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
    /// Tests ThrottleFirst emits first in window.
    /// </summary>
    [Test]
    public void ThrottleFirst_EmitsFirstInWindow()
    {
        var scheduler = new TestScheduler();
        var subject = new Subject<int>();
        var results = new List<int>();
        using var sub = subject.ThrottleFirst(TimeSpan.FromTicks(100), scheduler).Subscribe(results.Add);

        subject.OnNext(1);
        scheduler.AdvanceBy(50);
        subject.OnNext(2);
        scheduler.AdvanceBy(51);
        subject.OnNext(3);

        Assert.That(results, Is.EquivalentTo(new[] { 1, 3 }));
    }

    /// <summary>
    /// Tests Partition splits sequence.
    /// </summary>
    [Test]
    public void Partition_SplitsSequence()
    {
        var source = Observable.Range(1, 10);
        var trueResults = new List<int>();
        var falseResults = new List<int>();

        var (trueObs, falseObs) = source.Partition(x => x % 2 == 0);
        using var trueSub = trueObs.Subscribe(trueResults.Add);
        using var falseSub = falseObs.Subscribe(falseResults.Add);

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
}
