// Copyright (c) 2019-2023 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ReactiveMarbles.Extensions.Internal;

namespace ReactiveMarbles.Extensions;

/// <summary>
/// Extension methods for <see cref="System.Reactive"/>.
/// </summary>
public static class ReactiveExtensions
{
    // Thread-safe cache of timers keyed by TimeSpan. Ensures single shared timer per period.
    private static readonly ConcurrentDictionary<TimeSpan, Lazy<IConnectableObservable<DateTime>>> _timerList = new();

    /// <summary>
    /// Returns only values that are not null.
    /// Converts the nullability.
    /// </summary>
    /// <typeparam name="T">The type of value emitted by the observable.</typeparam>
    /// <param name="observable">The observable that can contain nulls.</param>
    /// <returns>A non nullable version of the observable that only emits valid values.</returns>
    public static IObservable<T> WhereIsNotNull<T>(this IObservable<T> observable) =>
        observable
            .Where(x => x is not null)!;

    /// <summary>
    /// Change the source observable type to <see cref="Unit"/>.
    /// This allows us to be notified when the observable emits a value.
    /// </summary>
    /// <typeparam name="T">The current type of the observable.</typeparam>
    /// <param name="observable">The observable to convert.</param>
    /// <returns>The signal.</returns>
    public static IObservable<Unit> AsSignal<T>(this IObservable<T> observable) =>
        observable
            .Select(_ => Unit.Default);

    /// <summary>
    /// Synchronized timer all instances of this with the same TimeSpan use the same timer.
    /// </summary>
    /// <param name="timeSpan">The time span.</param>
    /// <returns>An observable sequence producing the shared DateTime ticks.</returns>
    public static IObservable<DateTime> SyncTimer(TimeSpan timeSpan)
    {
        var lazy = _timerList.GetOrAdd(
            timeSpan,
            ts => new Lazy<IConnectableObservable<DateTime>>(
                () =>
                {
                    var published = Observable
                        .Timer(TimeSpan.Zero, ts)
                        .Timestamp()
                        .Select(x => x.Timestamp.DateTime)
                        .Publish();

                    // Connect immediately so subsequent subscribers share.
                    published.Connect();

                    return published;
                }));
        return lazy.Value;
    }

    /// <summary>
    /// Buffers until Start char and End char are found.
    /// </summary>
    /// <param name="this">The source observable of characters.</param>
    /// <param name="startsWith">The starting delimiter.</param>
    /// <param name="endsWith">The ending delimiter.</param>
    /// <returns>A sequence of buffered strings including the start and end delimiters.</returns>
    public static IObservable<string> BufferUntil(this IObservable<char> @this, char startsWith, char endsWith) =>
        Observable.Create<string>(o =>
        {
            StringBuilder sb = new();
            var startFound = false;
            var sub = @this.Subscribe(
                s =>
                {
                    if (startFound || s == startsWith)
                    {
                        startFound = true;
                        sb.Append(s);
                        if (s == endsWith)
                        {
                            o.OnNext(sb.ToString());
                            startFound = false;
                            sb.Clear();
                        }
                    }
                },
                o.OnError,
                () =>
                {
                    if (startFound && sb.Length > 0)
                    {
                        o.OnNext(sb.ToString());
                    }

                    o.OnCompleted();
                });
            return new CompositeDisposable(sub);
        });

    /// <summary>
    /// Catch exception and return Observable.Empty.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>A sequence that ignores errors and completes.</returns>
    public static IObservable<TSource?> CatchIgnore<TSource>(this IObservable<TSource?> source) =>
        source.Catch(Observable.Empty<TSource?>());

    /// <summary>
    /// Catch exception and return Observable.Empty.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="errorAction">The error action.</param>
    /// <returns>A sequence that invokes <paramref name="errorAction"/> on error and completes.</returns>
    public static IObservable<TSource?> CatchIgnore<TSource, TException>(this IObservable<TSource?> source, Action<TException> errorAction)
        where TException : Exception =>
            source.Catch((TException ex) =>
            {
                errorAction(ex);
                return Observable.Empty<TSource?>();
            });

    /// <summary>
    /// Latest values of each sequence are all false.
    /// </summary>
    /// <param name="sources">The sources.</param>
    /// <returns>A sequence that emits true when all latest booleans are false.</returns>
    public static IObservable<bool> CombineLatestValuesAreAllFalse(this IEnumerable<IObservable<bool>> sources) =>
        sources.CombineLatest(xs => xs.All(x => !x));

    /// <summary>
    /// Latest values of each sequence are all true.
    /// </summary>
    /// <param name="sources">The sources.</param>
    /// <returns>A sequence that emits true when all latest booleans are true.</returns>
    public static IObservable<bool> CombineLatestValuesAreAllTrue(this IEnumerable<IObservable<bool>> sources) =>
        sources.CombineLatest(xs => xs.All(x => x));

    /// <summary>
    /// Gets the maximum from all sources.
    /// </summary>
    /// <typeparam name="T">The Value Type.</typeparam>
    /// <param name="this">The first observable.</param>
    /// <param name="sources">Other sources.</param>
    /// <returns>A sequence emitting the maximum of the latest values.</returns>
    public static IObservable<T?> GetMax<T>(this IObservable<T?> @this, params IObservable<T?>[] sources)
        where T : struct
    {
        List<IObservable<T?>> source = [@this, .. sources];
        return source.CombineLatest().Select(x => x.Max());
    }

    /// <summary>
    /// Gets the minimum from all sources.
    /// </summary>
    /// <typeparam name="T">The Value Type.</typeparam>
    /// <param name="this">The first observable.</param>
    /// <param name="sources">Other sources.</param>
    /// <returns>A sequence emitting the minimum of the latest values.</returns>
    public static IObservable<T?> GetMin<T>(this IObservable<T?> @this, params IObservable<T?>[] sources)
        where T : struct
    {
        List<IObservable<T?>> source = [@this, .. sources];
        return source.CombineLatest().Select(x => x.Min());
    }

    /// <summary>
    /// Detects when a stream becomes inactive for some period of time.
    /// </summary>
    /// <typeparam name="T">update type.</typeparam>
    /// <param name="source">source stream.</param>
    /// <param name="stalenessPeriod">If source stream does not OnNext any update during this period, it is declared stale.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>Observable stale markers or updates.</returns>
    public static IObservable<IStale<T>> DetectStale<T>(this IObservable<T> source, TimeSpan stalenessPeriod, IScheduler scheduler) =>
        Observable.Create<IStale<T>>(observer =>
        {
            SerialDisposable timerSubscription = new();
            object observerLock = new();

            void ScheduleStale() =>
                    timerSubscription!.Disposable = Observable.Timer(stalenessPeriod, scheduler)
                    .Subscribe(_ =>
                    {
                        lock (observerLock)
                        {
                            observer.OnNext(new Stale<T>());
                        }
                    });

            var sourceSubscription = source.Subscribe(
                x =>
                {
                    (timerSubscription?.Disposable)?.Dispose();

                    lock (observerLock)
                    {
                        observer.OnNext(new Stale<T>(x));
                    }

                    ScheduleStale();
                },
                observer.OnError,
                observer.OnCompleted);

            ScheduleStale();

            return new CompositeDisposable
            {
                sourceSubscription,
                timerSubscription,
            };
        });

    /// <summary>
    /// Applies a conflation algorithm to an observable stream. Anytime the stream OnNext twice
    /// below minimumUpdatePeriod, the second update gets delayed to respect the
    /// minimumUpdatePeriod. If more than 2 updates happen, only the last update is pushed.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="source">The stream.</param>
    /// <param name="minimumUpdatePeriod">Minimum delay between two updates.</param>
    /// <param name="scheduler">Scheduler to publish updates.</param>
    /// <returns>The conflated stream.</returns>
    public static IObservable<T> Conflate<T>(this IObservable<T> source, TimeSpan minimumUpdatePeriod, IScheduler scheduler) =>
        Observable.Create<T>(observer =>
        {
            var lastUpdateTime = DateTimeOffset.MinValue;
            MultipleAssignmentDisposable updateScheduled = new();
            var completionRequested = false;
            object gate = new();

            return source.ObserveOn(scheduler)
                .Subscribe(
                x =>
                {
                    var currentUpdateTime = scheduler.Now;

                    bool scheduleRequired;
                    lock (gate)
                    {
                        scheduleRequired = currentUpdateTime - lastUpdateTime < minimumUpdatePeriod;
                        if (scheduleRequired && updateScheduled.Disposable != null)
                        {
                            updateScheduled.Disposable.Dispose();
                            updateScheduled.Disposable = null;
                        }
                    }

                    if (scheduleRequired)
                    {
                        updateScheduled.Disposable = scheduler.Schedule(
                            lastUpdateTime + minimumUpdatePeriod,
                            () =>
                            {
                                observer.OnNext(x);

                                lock (gate)
                                {
                                    lastUpdateTime = scheduler.Now;
                                    updateScheduled.Disposable = null;
                                    if (completionRequested)
                                    {
                                        observer.OnCompleted();
                                    }
                                }
                            });
                    }
                    else
                    {
                        observer.OnNext(x);
                        lock (gate)
                        {
                            lastUpdateTime = scheduler.Now;
                        }
                    }
                },
                observer.OnError,
                () =>
                {
                    if (updateScheduled.Disposable != null)
                    {
                        lock (gate)
                        {
                            completionRequested = true;
                        }
                    }
                    else
                    {
                        observer.OnCompleted();
                    }
                });
        });

    /// <summary>
    /// Injects heartbeats in a stream when the source stream becomes quiet.
    /// </summary>
    /// <typeparam name="T">Update type.</typeparam>
    /// <param name="source">Source stream.</param>
    /// <param name="heartbeatPeriod">Period between heartbeats.</param>
    /// <param name="scheduler">Scheduler.</param>
    /// <returns>Observable heartbeat values.</returns>
    public static IObservable<IHeartbeat<T>> Heartbeat<T>(this IObservable<T> source, TimeSpan heartbeatPeriod, IScheduler scheduler) =>
        Observable.Create<IHeartbeat<T>>(observer =>
        {
            MultipleAssignmentDisposable heartbeatTimerSubscription = new();
            object gate = new();

            void ScheduleHeartbeats()
            {
                var disposable = Observable.Timer(heartbeatPeriod, heartbeatPeriod, scheduler)
                    .Subscribe(_ => observer.OnNext(new Heartbeat<T>()));

                lock (gate)
                {
                    heartbeatTimerSubscription!.Disposable = disposable;
                }
            }

            var sourceSubscription = source.Subscribe(
                x =>
                {
                    lock (gate)
                    {
                        heartbeatTimerSubscription?.Disposable?.Dispose();
                    }

                    observer.OnNext(new Heartbeat<T>(x));
                    ScheduleHeartbeats();
                },
                observer.OnError,
                observer.OnCompleted);

            ScheduleHeartbeats();

            return new CompositeDisposable
            {
                sourceSubscription,
                heartbeatTimerSubscription,
            };
        });

    /// <summary>
    /// Executes with limited concurrency.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="taskFunctions">Tasks to execute.</param>
    /// <param name="maxConcurrency">Maximum concurrency.</param>
    /// <returns>A sequence of task results.</returns>
    public static IObservable<T> WithLimitedConcurrency<T>(this IEnumerable<Task<T>> taskFunctions, int maxConcurrency) =>
        new ConcurrencyLimiter<T>(taskFunctions, maxConcurrency).IObservable;

    /// <summary>
    /// Pushes multiple values to an observer.
    /// </summary>
    /// <typeparam name="T">Type of value.</typeparam>
    /// <param name="observer">Observer to push to.</param>
    /// <param name="events">Values to push.</param>
    public static void OnNext<T>(this IObserver<T?> observer, params T?[] events) =>
        FastForEach(observer, events!);

    /// <summary>
    /// If the scheduler is not null observes on that scheduler.
    /// </summary>
    /// <typeparam name="TSource">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="scheduler">Scheduler to notify observers on (optional).</param>
    /// <returns>The source sequence whose callbacks happen on the specified scheduler.</returns>
    public static IObservable<TSource> ObserveOnSafe<TSource>(this IObservable<TSource> source, IScheduler? scheduler) =>
        scheduler == null ? source : source.ObserveOn(scheduler);

    /// <summary>
    /// Invokes the action asynchronously surfacing the result through a Unit observable.
    /// </summary>
    /// <param name="action">Action to run.</param>
    /// <param name="scheduler">Scheduler (optional).</param>
    /// <returns>A sequence producing Unit upon completion.</returns>
    public static IObservable<Unit> Start(Action action, IScheduler? scheduler) =>
        scheduler == null ? Observable.Start(action) : Observable.Start(action, scheduler);

    /// <summary>
    /// Invokes the specified function asynchronously surfacing the result.
    /// </summary>
    /// <typeparam name="TResult">Result type.</typeparam>
    /// <param name="function">Function to run.</param>
    /// <param name="scheduler">Scheduler.</param>
    /// <returns>A sequence producing the function result.</returns>
    public static IObservable<TResult> Start<TResult>(Func<TResult> function, IScheduler? scheduler) =>
        scheduler == null ? Observable.Start(function) : Observable.Start(function, scheduler);

    /// <summary>
    /// Flattens a sequence of enumerables into individual values.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source of enumerables.</param>
    /// <param name="scheduler">Scheduler (optional).</param>
    /// <returns>A flattened observable.</returns>
    public static IObservable<T> ForEach<T>(this IObservable<IEnumerable<T>> source, IScheduler? scheduler = null) =>
        Observable.Create<T>(observer => source.ObserveOnSafe(scheduler).Subscribe(values => FastForEach(observer, values)));

    /// <summary>
    /// Schedules an action immediately if scheduler null, else on scheduler.
    /// </summary>
    /// <param name="scheduler">Scheduler.</param>
    /// <param name="action">Action.</param>
    /// <returns>Disposable for the scheduled action.</returns>
    public static IDisposable ScheduleSafe(this IScheduler? scheduler, Action action)
    {
        if (scheduler == null)
        {
            action();
            return Disposable.Empty;
        }

        return scheduler.Schedule(action);
    }

    /// <summary>
    /// Schedules an action after a due time.
    /// </summary>
    /// <param name="scheduler">Scheduler.</param>
    /// <param name="dueTime">Delay.</param>
    /// <param name="action">Action.</param>
    /// <returns>Disposable for the scheduled action.</returns>
    public static IDisposable ScheduleSafe(this IScheduler? scheduler, TimeSpan dueTime, Action action)
    {
        if (scheduler == null)
        {
            Thread.Sleep(dueTime);
            action();
            return Disposable.Empty;
        }

        return scheduler.Schedule(dueTime, action);
    }

    /// <summary>
    /// Emits each element of an IEnumerable.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source enumerable.</param>
    /// <param name="scheduler">Scheduler (optional).</param>
    /// <returns>Observable of elements.</returns>
    public static IObservable<T> FromArray<T>(this IEnumerable<T> source, IScheduler? scheduler = null) =>
        Observable.Create<T>(observer => scheduler.ScheduleSafe(() => FastForEach(observer, source)));

    /// <summary>
    /// Using helper with Action.
    /// </summary>
    /// <typeparam name="T">Disposable type.</typeparam>
    /// <param name="obj">Object to use.</param>
    /// <param name="action">Action to run.</param>
    /// <param name="scheduler">Scheduler.</param>
    /// <returns>Completion signal.</returns>
    public static IObservable<Unit> Using<T>(this T obj, Action<T> action, IScheduler? scheduler = null)
        where T : IDisposable =>
        Observable.Using(() => obj, id => Start(() => action?.Invoke(id), scheduler));

    /// <summary>
    /// Using helper with Func.
    /// </summary>
    /// <typeparam name="T">Disposable type.</typeparam>
    /// <typeparam name="TResult">Result type.</typeparam>
    /// <param name="obj">Object to use.</param>
    /// <param name="function">Function to invoke.</param>
    /// <param name="scheduler">Scheduler.</param>
    /// <returns>Observable of result.</returns>
    public static IObservable<TResult> Using<T, TResult>(this T obj, Func<T, TResult> function, IScheduler? scheduler = null)
        where T : IDisposable =>
        Observable.Using(() => obj, id => Start(() => function.Invoke(id), scheduler));

    /// <summary>
    /// While construct.
    /// </summary>
    /// <param name="condition">Condition to evaluate.</param>
    /// <param name="action">Action to execute.</param>
    /// <param name="scheduler">Scheduler.</param>
    /// <returns>Observable representing the loop.</returns>
    public static IObservable<Unit> While(Func<bool> condition, Action action, IScheduler? scheduler = null) =>
        Observable.While(condition, Start(action, scheduler));

    /// <summary>
    /// Schedules a single value after a delay.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="value">Value.</param>
    /// <param name="dueTime">Delay.</param>
    /// <param name="scheduler">Scheduler.</param>
    /// <returns>Observable that emits the value.</returns>
    public static IObservable<T> Schedule<T>(this T value, TimeSpan dueTime, IScheduler scheduler) =>
        Observable.Create<T>(observer => scheduler.ScheduleSafe(dueTime, () => observer.OnNext(value)));

    /// <summary>
    /// Schedules the specified due time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="source">The value.</param>
    /// <param name="dueTime">The due time.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An IObservable of T.</returns>
    public static IObservable<T> Schedule<T>(this IObservable<T> source, TimeSpan dueTime, IScheduler scheduler) =>
        Observable.Create<T>(observer => source.Subscribe(value => scheduler.ScheduleSafe(dueTime, () => observer.OnNext(value))));

    /// <summary>
    /// Schedules the specified due time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="dueTime">The due time.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An IObservable of T.</returns>
    public static IObservable<T> Schedule<T>(this T value, DateTimeOffset dueTime, IScheduler scheduler) =>
        Observable.Create<T>(observer => scheduler.Schedule(dueTime, () => observer.OnNext(value)));

    /// <summary>
    /// Schedules the specified due time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="source">The value.</param>
    /// <param name="dueTime">The due time.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An IObservable of T.</returns>
    public static IObservable<T> Schedule<T>(this IObservable<T> source, DateTimeOffset dueTime, IScheduler scheduler) =>
        Observable.Create<T>(observer => source.Subscribe(value => scheduler.Schedule(dueTime, () => observer.OnNext(value))));

    /// <summary>
    /// Schedules the specified due time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="dueTime">The due time.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="action">The action.</param>
    /// <returns>An IObservable of T.</returns>
    public static IObservable<T> Schedule<T>(this T value, TimeSpan dueTime, IScheduler scheduler, Action<T> action) =>
        Observable.Create<T>(observer => scheduler.ScheduleSafe(dueTime, () =>
        {
            action(value);
            observer.OnNext(value);
        }));

    /// <summary>
    /// Schedules the specified due time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="source">The value.</param>
    /// <param name="dueTime">The due time.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="action">The action.</param>
    /// <returns>An IObservable of T.</returns>
    public static IObservable<T> Schedule<T>(this IObservable<T> source, TimeSpan dueTime, IScheduler scheduler, Action<T> action) =>
        Observable.Create<T>(observer => source.Subscribe(value => scheduler.ScheduleSafe(dueTime, () =>
        {
            action(value);
            observer.OnNext(value);
        })));

    /// <summary>
    /// Schedules the specified due time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="dueTime">The due time.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="action">The action.</param>
    /// <returns>An IObservable of T.</returns>
    public static IObservable<T> Schedule<T>(this T value, DateTimeOffset dueTime, IScheduler scheduler, Action<T> action) =>
        Observable.Create<T>(observer => scheduler.Schedule(dueTime, () =>
        {
            action(value);
            observer.OnNext(value);
        }));

    /// <summary>
    /// Schedules the specified due time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="source">The value.</param>
    /// <param name="dueTime">The due time.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="action">The action.</param>
    /// <returns>An IObservable of T.</returns>
    public static IObservable<T> Schedule<T>(this IObservable<T> source, DateTimeOffset dueTime, IScheduler scheduler, Action<T> action) =>
        Observable.Create<T>(observer => source.Subscribe(value => scheduler.Schedule(dueTime, () =>
        {
            action(value);
            observer.OnNext(value);
        })));

    /// <summary>
    /// Schedules the specified due time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="function">The function.</param>
    /// <returns>An IObservable of T.</returns>
    public static IObservable<T> Schedule<T>(this T value, IScheduler scheduler, Func<T, T> function) =>
        Observable.Create<T>(observer => scheduler.Schedule(() => observer.OnNext(function(value))));

    /// <summary>
    /// Schedules the specified due time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="source">The value.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="function">The function.</param>
    /// <returns>An IObservable of T.</returns>
    public static IObservable<T> Schedule<T>(this IObservable<T> source, IScheduler scheduler, Func<T, T> function) =>
        Observable.Create<T>(observer => source.Subscribe(value => scheduler.Schedule(() => observer.OnNext(function(value)))));

    /// <summary>
    /// Schedules the specified due time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="dueTime">The due time.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="function">The function.</param>
    /// <returns>An IObservable of T.</returns>
    public static IObservable<T> Schedule<T>(this T value, TimeSpan dueTime, IScheduler scheduler, Func<T, T> function) =>
        Observable.Create<T>(observer => scheduler.Schedule(dueTime, () => observer.OnNext(function(value))));

    /// <summary>
    /// Schedules the specified due time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="source">The value.</param>
    /// <param name="dueTime">The due time.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="function">The function.</param>
    /// <returns>An IObservable of T.</returns>
    public static IObservable<T> Schedule<T>(this IObservable<T> source, TimeSpan dueTime, IScheduler scheduler, Func<T, T> function) =>
        Observable.Create<T>(observer => source.Subscribe(value => scheduler.Schedule(dueTime, () => observer.OnNext(function(value)))));

    /// <summary>
    /// Filters strings by regex.
    /// </summary>
    /// <param name="source">Source sequence.</param>
    /// <param name="regexPattern">Regex pattern.</param>
    /// <returns>Filtered sequence.</returns>
    public static IObservable<string> Filter(this IObservable<string> source, string regexPattern) =>
        source.Where(f => Regex.IsMatch(f, regexPattern));

    /// <summary>
    /// Randomly shuffles arrays emitted by the source.
    /// </summary>
    /// <typeparam name="T">Array element type.</typeparam>
    /// <param name="source">Source array sequence.</param>
    /// <returns>Sequence of shuffled arrays (in-place).</returns>
    public static IObservable<T[]> Shuffle<T>(this IObservable<T[]> source) =>
        Observable.Create<T[]>(observer => source.Subscribe(array =>
        {
            Random random = new(unchecked(Environment.TickCount * 31));
            var n = array.Length;
            while (n > 1)
            {
                n--;
                var k = random.Next(n + 1);
                (array[n], array[k]) = (array[k], array[n]);
            }

            observer.OnNext(array);
        }));

    /// <summary>
    /// Repeats the source until it terminates successfully (alias of Retry).
    /// </summary>
    /// <typeparam name="TSource">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <returns>Retried sequence.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource>(this IObservable<TSource?> source) => source.Retry();

    /// <summary>
    /// When caught exception, do onError action and repeat observable sequence.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="onError">The on error.</param>
    /// <returns>A sequence that retries on error with optional delay.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource, TException>(this IObservable<TSource?> source, Action<TException> onError)
        where TException : Exception => source.OnErrorRetry(onError, TimeSpan.Zero);

    /// <summary>
    /// When caught exception, do onError action and repeat observable sequence after delay time.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="onError">The on error.</param>
    /// <param name="delay">The delay.</param>
    /// <returns>A sequence that retries on error with optional delay.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource, TException>(this IObservable<TSource?> source, Action<TException> onError, TimeSpan delay)
        where TException : Exception => source.OnErrorRetry(onError, int.MaxValue, delay);

    /// <summary>
    /// When caught exception, do onError action and repeat observable sequence during within retryCount.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="onError">The on error.</param>
    /// <param name="retryCount">The retry count.</param>
    /// <returns>A sequence that retries on error with optional delay.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource, TException>(this IObservable<TSource?> source, Action<TException> onError, int retryCount)
        where TException : Exception => source.OnErrorRetry(onError, retryCount, TimeSpan.Zero);

    /// <summary>
    /// When caught exception, do onError action and repeat observable sequence after delay time
    /// during within retryCount.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="onError">The on error.</param>
    /// <param name="retryCount">The retry count.</param>
    /// <param name="delay">The delay.</param>
    /// <returns>A sequence that retries on error with optional delay.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource, TException>(this IObservable<TSource?> source, Action<TException> onError, int retryCount, TimeSpan delay)
        where TException : Exception => source.OnErrorRetry(onError, retryCount, delay, Scheduler.Default);

    /// <summary>
    /// When caught exception, do onError action and repeat observable sequence after delay
    /// time(work on delayScheduler) during within retryCount.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="onError">The on error.</param>
    /// <param name="retryCount">The retry count.</param>
    /// <param name="delay">The delay.</param>
    /// <param name="delayScheduler">The delay scheduler.</param>
    /// <returns>A sequence that retries on error with optional delay.</returns>
    public static IObservable<TSource?> OnErrorRetry<TSource, TException>(this IObservable<TSource?> source, Action<TException> onError, int retryCount, TimeSpan delay, IScheduler delayScheduler)
        where TException : Exception => Observable.Defer(() =>
        {
            var dueTime = (delay.Ticks < 0) ? TimeSpan.Zero : delay;
            var empty = Observable.Empty<TSource?>();
            var count = 0;
            IObservable<TSource?>? self = null;
            self = source.Catch((TException ex) =>
            {
                onError(ex);

                return (++count < retryCount)
                        ? (dueTime == TimeSpan.Zero)
                            ? self!.SubscribeOn(Scheduler.CurrentThread)
                            : empty.Delay(dueTime, delayScheduler).Concat(self!).SubscribeOn(Scheduler.CurrentThread)
                        : Observable.Throw<TSource?>(ex);
            });
            return self;
        });

    /// <summary>
    /// Takes elements until predicate returns true for an element (inclusive) then completes.
    /// </summary>
    /// <typeparam name="TSource">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="predicate">Predicate for completion.</param>
    /// <returns>Sequence that completes when predicate satisfied.</returns>
    public static IObservable<TSource> TakeUntil<TSource>(this IObservable<TSource> source, Func<TSource, bool> predicate) =>
        Observable.Create<TSource>(observer =>
            source.Subscribe(
                item =>
                {
                    observer.OnNext(item);
                    if (predicate?.Invoke(item) ?? default)
                    {
                        observer.OnCompleted();
                    }
                },
                observer.OnError,
                observer.OnCompleted));

    /// <summary>
    /// Wraps values with a synchronization disposable that completes when disposed.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <returns>Sequence of (value, sync handle).</returns>
    public static IObservable<(T Value, IDisposable Sync)> SynchronizeSynchronous<T>(this IObservable<T> source) =>
        Observable.Create<(T Value, IDisposable Sync)>(observer => source.Subscribe(item => new Continuation().Lock(item, observer).Wait()));

    /// <summary>
    /// Subscribes to the specified source synchronously.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="onNext">The on next.</param>
    /// <param name="onError">The on error.</param>
    /// <param name="onCompleted">The on completed.</param>
    /// <returns><see cref="IDisposable"/> object used to unsubscribe from the observable sequence.</returns>
    public static IDisposable SubscribeSynchronous<T>(this IObservable<T> source, Func<T, Task> onNext, Action<Exception> onError, Action onCompleted) =>
        source.SynchronizeSynchronous().Subscribe(
            async observer =>
            {
                await onNext(observer.Value);
                observer.Sync.Dispose();
            },
            onError,
            onCompleted);

    /// <summary>
    /// Subscribes an element handler and an exception handler to an observable sequence synchronously.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">Observable sequence to subscribe to.</param>
    /// <param name="onNext">Action to invoke for each element in the observable sequence.</param>
    /// <param name="onError">Action to invoke upon exceptional termination of the observable sequence.</param>
    /// <returns><see cref="IDisposable"/> object used to unsubscribe from the observable sequence.</returns>
    public static IDisposable SubscribeSynchronous<T>(this IObservable<T> source, Func<T, Task> onNext, Action<Exception> onError) =>
        source.SynchronizeSynchronous().Subscribe(
            async observer =>
            {
                await onNext(observer.Value);
                observer.Sync.Dispose();
            },
            onError);

    /// <summary>
    /// Subscribes an element handler and a completion handler to an observable sequence synchronously.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">Observable sequence to subscribe to.</param>
    /// <param name="onNext">Action to invoke for each element in the observable sequence.</param>
    /// <param name="onCompleted">Action to invoke upon graceful termination of the observable sequence.</param>
    /// <returns><see cref="IDisposable"/> object used to unsubscribe from the observable sequence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="onNext"/> or <paramref name="onCompleted"/> is <c>null</c>.</exception>
    public static IDisposable SubscribeSynchronous<T>(this IObservable<T> source, Func<T, Task> onNext, Action onCompleted) =>
        source.SynchronizeSynchronous().Subscribe(
            async observer =>
            {
                await onNext(observer.Value);
                observer.Sync.Dispose();
            },
            onCompleted);

    /// <summary>
    /// Subscribes an element handler to an observable sequence synchronously.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">Observable sequence to subscribe to.</param>
    /// <param name="onNext">Action to invoke for each element in the observable sequence.</param>
    /// <returns><see cref="IDisposable"/> object used to unsubscribe from the observable sequence.</returns>
    public static IDisposable SubscribeSynchronous<T>(this IObservable<T> source, Func<T, Task> onNext) =>
        source.SynchronizeSynchronous().Subscribe(
             async observer =>
             {
                 await onNext(observer.Value);
                 observer.Sync.Dispose();
             });

    /// <summary>
    /// Synchronizes the asynchronous operations in downstream operations.
    /// Use SubscribeSynchronus instead for a simpler version.
    /// Call Sync.Dispose() to release the lock in the downstream methods.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An Observable of T and a release mechanism.</returns>
    public static IObservable<(T Value, IDisposable Sync)> SynchronizeAsync<T>(this IObservable<T> source) =>
        Observable.Create<(T Value, IDisposable Sync)>(observer => source.Select(item => Observable.FromAsync(() => new Continuation().Lock(item, observer))).Concat().Subscribe());

    /// <summary>
    /// Subscribes allowing asynchronous operations to be executed without blocking the source.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">Observable sequence to subscribe to.</param>
    /// <param name="onNext">Action to invoke for each element in the observable sequence.</param>
    /// <returns><see cref="IDisposable"/> object used to unsubscribe from the observable sequence.</returns>
    public static IDisposable SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNext) =>
            source.Select(o => Observable.FromAsync(() => onNext(o))).Concat().Subscribe();

    /// <summary>
    /// Subscribes allowing asynchronous operations to be executed without blocking the source.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">Observable sequence to subscribe to.</param>
    /// <param name="onNext">Action to invoke for each element in the observable sequence.</param>
    /// <param name="onCompleted">The on completed.</param>
    /// <returns>
    ///   <see cref="IDisposable" /> object used to unsubscribe from the observable sequence.
    /// </returns>
    public static IDisposable SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNext, Action onCompleted) =>
            source.Select(o => Observable.FromAsync(() => onNext(o))).Concat().Subscribe(_ => { }, onCompleted);

    /// <summary>
    /// Subscribes allowing asynchronous operations to be executed without blocking the source.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">Observable sequence to subscribe to.</param>
    /// <param name="onNext">Action to invoke for each element in the observable sequence.</param>
    /// <param name="onError">The on error.</param>
    /// <returns>
    ///   <see cref="IDisposable" /> object used to unsubscribe from the observable sequence.
    /// </returns>
    public static IDisposable SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNext, Action<Exception> onError) =>
            source.Select(o => Observable.FromAsync(() => onNext(o))).Concat().Subscribe(_ => { }, onError);

    /// <summary>
    /// Subscribes allowing asynchronous operations to be executed without blocking the source.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">Observable sequence to subscribe to.</param>
    /// <param name="onNext">Action to invoke for each element in the observable sequence.</param>
    /// <param name="onError">The on error.</param>
    /// <param name="onCompleted">The on completed.</param>
    /// <returns>
    ///   <see cref="IDisposable" /> object used to unsubscribe from the observable sequence.
    /// </returns>
    public static IDisposable SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNext, Action<Exception> onError, Action onCompleted) =>
            source.Select(o => Observable.FromAsync(() => onNext(o))).Concat().Subscribe(_ => { }, onError, onCompleted);

    /// <summary>
    /// Emits the boolean negation of the source sequence.
    /// </summary>
    /// <param name="source">Boolean source.</param>
    /// <returns>Negated boolean sequence.</returns>
    public static IObservable<bool> Not(this IObservable<bool> source) => source.Select(b => !b);

    /// <summary>
    /// Filters to true values only.
    /// </summary>
    /// <param name="source">Boolean source.</param>
    /// <returns>Sequence of true values.</returns>
    public static IObservable<bool> WhereTrue(this IObservable<bool> source) => source.Where(b => b);

    /// <summary>
    /// Filters to false values only.
    /// </summary>
    /// <param name="source">Boolean source.</param>
    /// <returns>Sequence of false values.</returns>
    public static IObservable<bool> WhereFalse(this IObservable<bool> source) => source.Where(b => !b);

    /// <summary>
    /// Catches any error and returns a fallback value then completes.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="fallback">Fallback value.</param>
    /// <returns>Sequence producing either original values or fallback on error then completing.</returns>
    public static IObservable<T> CatchAndReturn<T>(this IObservable<T> source, T fallback) =>
        source.Catch(Observable.Return(fallback));

    /// <summary>
    /// Catches a specific exception type mapping it to a fallback value.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <typeparam name="TException">Exception type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="fallbackFactory">Factory producing fallback from the exception.</param>
    /// <returns>Recovered sequence.</returns>
    public static IObservable<T> CatchAndReturn<T, TException>(this IObservable<T> source, Func<TException, T> fallbackFactory)
        where TException : Exception =>
        source.Catch<T, TException>(ex => Observable.Return(fallbackFactory(ex)));

    /// <summary>
    /// Retries with exponential backoff.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="maxRetries">Maximum number of retries.</param>
    /// <param name="initialDelay">Initial backoff delay.</param>
    /// <param name="backoffFactor">Multiplier for each retry (default 2).</param>
    /// <param name="maxDelay">Optional maximum delay.</param>
    /// <param name="scheduler">Scheduler (optional).</param>
    /// <returns>Retried sequence with backoff.</returns>
    public static IObservable<T> RetryWithBackoff<T>(this IObservable<T> source, int maxRetries, TimeSpan initialDelay, double backoffFactor = 2.0, TimeSpan? maxDelay = null, IScheduler? scheduler = null) =>
        Observable.Defer(() =>
        {
            scheduler ??= Scheduler.Default;
            var attempt = 0;
            return source.Catch<T, Exception>(ex =>
            {
                if (attempt++ >= maxRetries)
                {
                    return Observable.Throw<T>(ex);
                }

                var nextDelay = TimeSpan.FromMilliseconds(initialDelay.TotalMilliseconds * Math.Pow(backoffFactor, attempt - 1));
                if (maxDelay.HasValue && nextDelay > maxDelay.Value)
                {
                    nextDelay = maxDelay.Value;
                }

                return Observable.Timer(nextDelay, scheduler).Select(_ => default(T)!).IgnoreElements().Concat(source).RetryWithBackoff(maxRetries - attempt, initialDelay, backoffFactor, maxDelay, scheduler);
            });
        });

    /// <summary>
    /// Emits only the first value in each time window.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="window">Time window.</param>
    /// <param name="scheduler">Scheduler (optional).</param>
    /// <returns>Throttle-first sequence.</returns>
    public static IObservable<T> ThrottleFirst<T>(this IObservable<T> source, TimeSpan window, IScheduler? scheduler = null)
    {
        scheduler ??= Scheduler.Default;
        return Observable.Create<T>(obs =>
        {
            object gate = new();
            DateTimeOffset last = DateTimeOffset.MinValue;
            return source.Subscribe(
                x =>
                {
                    var now = scheduler.Now;
                    bool emit;
                    lock (gate)
                    {
                        emit = now - last >= window;
                        if (emit)
                        {
                            last = now;
                        }
                    }

                    if (emit)
                    {
                        obs.OnNext(x);
                    }
                },
                obs.OnError,
                obs.OnCompleted);
        });
    }

    /// <summary>
    /// Debounces with an immediate first emission then standard debounce behavior.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="dueTime">Debounce time.</param>
    /// <param name="scheduler">Scheduler (optional).</param>
    /// <returns>Debounced sequence.</returns>
    public static IObservable<T> DebounceImmediate<T>(this IObservable<T> source, TimeSpan dueTime, IScheduler? scheduler = null)
    {
        scheduler ??= Scheduler.Default;
        return Observable.Create<T>(obs =>
        {
            SerialDisposable timer = new();
            object gate = new();
            var isFirst = true;
            T? lastValue = default;
            bool hasValue = false;

            void Emit()
            {
                if (hasValue)
                {
                    obs.OnNext(lastValue!);
                    hasValue = false;
                }
            }

            var subscription = source.Subscribe(
                v =>
                {
                    lock (gate)
                    {
                        if (isFirst)
                        {
                            isFirst = false;
                            obs.OnNext(v);
                            return;
                        }

                        lastValue = v;
                        hasValue = true;
                        timer.Disposable = scheduler.Schedule(dueTime, Emit);
                    }
                },
                ex =>
                {
                    lock (gate)
                    {
                        Emit();
                    }

                    obs.OnError(ex);
                },
                () =>
                {
                    lock (gate)
                    {
                        Emit();
                    }

                    obs.OnCompleted();
                });
            return new CompositeDisposable(subscription, timer);
        });
    }

    /// <summary>
    /// Projects each element to a task executed sequentially.
    /// </summary>
    /// <typeparam name="TSource">Source element type.</typeparam>
    /// <typeparam name="TResult">Result type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="selector">Task selector.</param>
    /// <returns>Sequence of results preserving order.</returns>
    public static IObservable<TResult> SelectAsyncSequential<TSource, TResult>(this IObservable<TSource> source, Func<TSource, Task<TResult>> selector) =>
        source.Select(x => Observable.FromAsync(() => selector(x))).Concat();

    /// <summary>
    /// Projects each element to a task but only latest result is emitted.
    /// </summary>
    /// <typeparam name="TSource">Source type.</typeparam>
    /// <typeparam name="TResult">Result type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="selector">Task selector.</param>
    /// <returns>Sequence of latest task results.</returns>
    public static IObservable<TResult> SelectLatestAsync<TSource, TResult>(this IObservable<TSource> source, Func<TSource, Task<TResult>> selector) =>
        source.Select(x => Observable.FromAsync(() => selector(x))).Switch();

    /// <summary>
    /// Projects each element to a task with limited concurrency.
    /// </summary>
    /// <typeparam name="TSource">Source type.</typeparam>
    /// <typeparam name="TResult">Result type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="selector">Task selector.</param>
    /// <param name="maxConcurrency">Max concurrency.</param>
    /// <returns>Merged sequence of task results.</returns>
    public static IObservable<TResult> SelectAsyncConcurrent<TSource, TResult>(this IObservable<TSource> source, Func<TSource, Task<TResult>> selector, int maxConcurrency) =>
        source.Select(x => Observable.FromAsync(() => selector(x))).Merge(maxConcurrency);

    /// <summary>
    /// Partitions a sequence into two based on predicate.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="predicate">Predicate.</param>
    /// <returns>Tuple of (trueSequence, falseSequence).</returns>
    public static (IObservable<T> True, IObservable<T> False) Partition<T>(this IObservable<T> source, Func<T, bool> predicate)
    {
        var published = source.Publish().RefCount();
        return (published.Where(predicate), published.Where(x => !predicate(x)));
    }

    /// <summary>
    /// Buffers items until inactivity period elapses then emits and resets buffer.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="inactivityPeriod">Inactivity period.</param>
    /// <param name="scheduler">Scheduler.</param>
    /// <returns>Sequence of buffered lists.</returns>
    public static IObservable<IList<T>> BufferUntilInactive<T>(this IObservable<T> source, TimeSpan inactivityPeriod, IScheduler? scheduler = null)
    {
        scheduler ??= Scheduler.Default;
        return Observable.Create<IList<T>>(observer =>
        {
            object gate = new();
            List<T> buffer = new();
            SerialDisposable timer = new();

            void Flush()
            {
                List<T>? toEmit = null;
                lock (gate)
                {
                    if (buffer.Count > 0)
                    {
                        toEmit = buffer;
                        buffer = new();
                    }
                }

                if (toEmit != null)
                {
                    observer.OnNext(toEmit);
                }
            }

            void ScheduleFlush() => timer.Disposable = scheduler.Schedule(inactivityPeriod, Flush);

            var subscription = source.Subscribe(
                x =>
                {
                    lock (gate)
                    {
                        buffer.Add(x);
                        ScheduleFlush();
                    }
                },
                ex =>
                {
                    Flush();
                    observer.OnError(ex);
                },
                () =>
                {
                    Flush();
                    observer.OnCompleted();
                });

            return new CompositeDisposable(subscription, timer);
        });
    }

    /// <summary>
    /// Emits the first element matching predicate then completes.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="predicate">Predicate.</param>
    /// <returns>Sequence with first matching element.</returns>
    public static IObservable<T> WaitUntil<T>(this IObservable<T> source, Func<T, bool> predicate) =>
        source.Where(predicate).Take(1);

    /// <summary>
    /// Executes an action at subscription time.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="action">Action to run on subscribe.</param>
    /// <returns>Original sequence with subscribe side-effect.</returns>
    public static IObservable<T> DoOnSubscribe<T>(this IObservable<T> source, Action action) =>
        Observable.Create<T>(o =>
        {
            action();
            return source.Subscribe(o);
        });

    /// <summary>
    /// Executes an action when subscription is disposed.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Source sequence.</param>
    /// <param name="disposeAction">Action to run on dispose.</param>
    /// <returns>Original sequence with dispose side-effect.</returns>
    public static IObservable<T> DoOnDispose<T>(this IObservable<T> source, Action disposeAction) =>
        Observable.Create<T>(o =>
        {
            var disp = source.Subscribe(o);
            return Disposable.Create(() =>
            {
                try
                {
                    disp.Dispose();
                }
                finally
                {
                    disposeAction();
                }
            });
        });

    private static void FastForEach<T>(IObserver<T> observer, IEnumerable<T> source)
    {
        if (source is List<T> fullList)
        {
            foreach (var item in fullList)
            {
                observer.OnNext(item);
            }
        }
        else if (source is IList<T> list)
        {
            foreach (var item in EnumerableIList.Create(list))
            {
                observer.OnNext(item);
            }
        }
        else if (source is T[] array)
        {
            foreach (var item in array)
            {
                observer.OnNext(item);
            }
        }
        else
        {
            foreach (var item in source)
            {
                observer.OnNext(item);
            }
        }
    }
}
