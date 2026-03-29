// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides async-native counterparts for high-value helper operators exposed by the synchronous reactive surface in
/// this repository.
/// </summary>
/// <remarks>These members intentionally compose existing async operators from this namespace so parity is achieved via
/// the library's own async primitives instead of by delegating to System.Reactive implementations.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> source)
    {
        /// <summary>
        /// Converts the source sequence into a signal sequence that emits <see cref="Unit.Default"/> for each source value.
        /// </summary>
        /// <returns>An observable sequence of <see cref="Unit"/> values.</returns>
        public IObservableAsync<Unit> AsSignal()
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

            return source.Select(static _ => Unit.Default);
        }

        /// <summary>
        /// Continues with an empty sequence when the source completes with a failure result.
        /// </summary>
        /// <returns>A sequence that suppresses terminal failures and completes instead.</returns>
        public IObservableAsync<T> CatchIgnore()
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

            return source.Catch(static _ => Empty<T>());
        }

        /// <summary>
        /// Continues with an empty sequence when the source fails with the specified exception type.
        /// </summary>
        /// <typeparam name="TException">The exception type to intercept.</typeparam>
        /// <param name="errorAction">The action to invoke when a matching exception is observed.</param>
        /// <returns>A sequence that suppresses matching terminal failures and completes instead.</returns>
        public IObservableAsync<T> CatchIgnore<TException>(Action<TException> errorAction)
            where TException : Exception
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(errorAction, nameof(errorAction));

            return source.Catch(ex =>
            {
                if (ex is TException typedException)
                {
                    errorAction(typedException);
                    return Empty<T>();
                }

                return Throw<T>(ex);
            });
        }

        /// <summary>
        /// Continues with a fallback value when the source completes with a failure result.
        /// </summary>
        /// <param name="fallback">The fallback value to emit.</param>
        /// <returns>A sequence that emits either the original values or the fallback value on terminal failure.</returns>
        public IObservableAsync<T> CatchAndReturn(T fallback)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

            return source.Catch(_ => Return(fallback));
        }

        /// <summary>
        /// Continues with a fallback value produced from a specific exception type.
        /// </summary>
        /// <typeparam name="TException">The exception type to intercept.</typeparam>
        /// <param name="fallbackFactory">The fallback value factory.</param>
        /// <returns>A sequence that emits either the original values or a fallback value on matching terminal failure.</returns>
        public IObservableAsync<T> CatchAndReturn<TException>(Func<TException, T> fallbackFactory)
            where TException : Exception
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(fallbackFactory, nameof(fallbackFactory));

            return source.Catch(ex => ex is TException typedException ? Return(fallbackFactory(typedException)) : Throw<T>(ex));
        }

        /// <summary>
        /// Registers a side effect that runs when the source is subscribed.
        /// </summary>
        /// <param name="action">The action to execute before subscription.</param>
        /// <returns>The original source sequence with the subscription side effect applied.</returns>
        public IObservableAsync<T> DoOnSubscribe(Action action)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(action, nameof(action));

            return Create<T>(async (observer, cancellationToken) =>
            {
                action();
                return await source.SubscribeAsync(observer.Wrap(), cancellationToken);
            });
        }

        /// <summary>
        /// Registers an asynchronous side effect that runs when the source is subscribed.
        /// </summary>
        /// <param name="action">The asynchronous action to execute before subscription.</param>
        /// <returns>The original source sequence with the subscription side effect applied.</returns>
        public IObservableAsync<T> DoOnSubscribe(Func<CancellationToken, ValueTask> action)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(action, nameof(action));

            return Create<T>(async (observer, cancellationToken) =>
            {
                await action(cancellationToken);
                return await source.SubscribeAsync(observer.Wrap(), cancellationToken);
            });
        }

        /// <summary>
        /// Drops source values while the previous asynchronous action is still running.
        /// </summary>
        /// <param name="asyncAction">The asynchronous action to execute for accepted values.</param>
        /// <returns>A sequence that emits only values that were accepted while the operator was idle.</returns>
        public IObservableAsync<T> DropIfBusy(Func<T, CancellationToken, ValueTask> asyncAction)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(asyncAction, nameof(asyncAction));

            return Create<T>(async (observer, subscribeToken) =>
            {
                var isBusy = 0;
                return await source.SubscribeAsync(
                    async (value, token) =>
                    {
                        if (Interlocked.CompareExchange(ref isBusy, 1, 0) != 0)
                        {
                            return;
                        }

                        try
                        {
                            await asyncAction(value, token);
                            await observer.OnNextAsync(value, token);
                        }
                        finally
                        {
                            Volatile.Write(ref isBusy, 0);
                        }
                    },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken);
            });
        }

        /// <summary>
        /// Emits the latest value or the provided default value before the source produces its first value.
        /// If the first source value equals <paramref name="defaultValue"/>, it will be suppressed by the
        /// distinct-until-changed filter.
        /// </summary>
        /// <param name="defaultValue">The default value to emit first.</param>
        /// <returns>A sequence that starts with the provided default value and then emits distinct source updates.</returns>
        public IObservableAsync<T> LatestOrDefault(T defaultValue)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

            return source.StartWith(defaultValue).DistinctUntilChanged();
        }

        /// <summary>
        /// Logs errors as they are observed without changing the source sequence.
        /// </summary>
        /// <param name="logger">The error logger.</param>
        /// <returns>The original sequence with error side effects attached.</returns>
        public IObservableAsync<T> LogErrors(Action<Exception> logger)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(logger, nameof(logger));

            return Create<T>(async (observer, subscribeToken) => await source.SubscribeAsync(
                observer.OnNextAsync,
                (exception, token) =>
                {
                    logger(exception);
                    return observer.OnErrorResumeAsync(exception, token);
                },
                observer.OnCompletedAsync,
                subscribeToken));
        }

        /// <summary>
        /// Emits the first value that satisfies the predicate and then completes.
        /// </summary>
        /// <param name="predicate">The predicate to satisfy.</param>
        /// <returns>A sequence containing the first matching value.</returns>
        public IObservableAsync<T> WaitUntil(Func<T, bool> predicate)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(predicate, nameof(predicate));

            return source.Where(predicate).Take(1);
        }

        /// <summary>
        /// Uses ObserveOn only when a context is provided.
        /// </summary>
        /// <param name="asyncContext">The target async context, or <see langword="null"/> to leave the sequence unchanged.</param>
        /// <param name="forceYielding">Whether to force yielding when switching context.</param>
        /// <returns>The source sequence, optionally observed on the provided context.</returns>
        public IObservableAsync<T> ObserveOnSafe(AsyncContext? asyncContext, bool forceYielding = false)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

            return asyncContext is null ? source : source.ObserveOn(asyncContext, forceYielding);
        }

        /// <summary>
        /// Uses ObserveOn only when a scheduler is provided.
        /// </summary>
        /// <param name="taskScheduler">The target scheduler, or <see langword="null"/> to leave the sequence unchanged.</param>
        /// <param name="forceYielding">Whether to force yielding when switching context.</param>
        /// <returns>The source sequence, optionally observed on the provided scheduler.</returns>
        public IObservableAsync<T> ObserveOnSafe(TaskScheduler? taskScheduler, bool forceYielding = false)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

            return taskScheduler is null ? source : source.ObserveOn(taskScheduler, forceYielding);
        }

        /// <summary>
        /// Observes the source on the provided context only when the condition is true.
        /// </summary>
        /// <param name="condition">A value indicating whether to switch context.</param>
        /// <param name="asyncContext">The target async context.</param>
        /// <param name="forceYielding">Whether to force yielding when switching context.</param>
        /// <returns>The source sequence, optionally observed on the provided context.</returns>
        public IObservableAsync<T> ObserveOnIf(bool condition, AsyncContext asyncContext, bool forceYielding = false)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(asyncContext, nameof(asyncContext));

            return condition ? source.ObserveOn(asyncContext, forceYielding) : source;
        }

        /// <summary>
        /// Observes the source on the provided scheduler only when the condition is true.
        /// </summary>
        /// <param name="condition">A value indicating whether to switch context.</param>
        /// <param name="taskScheduler">The target scheduler.</param>
        /// <param name="forceYielding">Whether to force yielding when switching context.</param>
        /// <returns>The source sequence, optionally observed on the provided scheduler.</returns>
        public IObservableAsync<T> ObserveOnIf(bool condition, TaskScheduler taskScheduler, bool forceYielding = false)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(taskScheduler, nameof(taskScheduler));

            return condition ? source.ObserveOn(taskScheduler, forceYielding) : source;
        }

        /// <summary>
        /// Emits adjacent pairs from the source sequence.
        /// </summary>
        /// <returns>A sequence of adjacent (previous, current) pairs.</returns>
        public IObservableAsync<(T Previous, T Current)> Pairwise()
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

            return Create<(T Previous, T Current)>(async (observer, subscribeToken) =>
            {
                var hasPrevious = false;
                T? previous = default;

                return await source.SubscribeAsync(
                    async (value, token) =>
                    {
                        if (!hasPrevious)
                        {
                            previous = value;
                            hasPrevious = true;
                            return;
                        }

                        var pair = (previous!, value);
                        previous = value;
                        await observer.OnNextAsync(pair, token);
                    },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken);
            });
        }

        /// <summary>
        /// Partitions the source sequence into values that satisfy the predicate and values that do not.
        /// The predicate is evaluated exactly once per element.
        /// </summary>
        /// <param name="predicate">The partition predicate.</param>
        /// <returns>A tuple of true and false partitions.</returns>
        public (IObservableAsync<T> True, IObservableAsync<T> False) Partition(Func<T, bool> predicate)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(predicate, nameof(predicate));

            var shared = source.Select(value => (value, matches: predicate(value))).Publish().RefCount();
            return (
                shared.Where(static pair => pair.matches).Select(static pair => pair.value),
                shared.Where(static pair => !pair.matches).Select(static pair => pair.value));
        }

        /// <summary>
        /// Replays the last observed value to new subscribers, starting with the provided initial value.
        /// </summary>
        /// <param name="initialValue">The initial replay value.</param>
        /// <returns>A replaying shared observable sequence.</returns>
        public IObservableAsync<T> ReplayLastOnSubscribe(T initialValue)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

            return source.Publish(initialValue).RefCount();
        }

        /// <summary>
        /// Emits only the first value in each throttle window and suppresses duplicates before and after throttling.
        /// </summary>
        /// <param name="throttle">The throttle duration.</param>
        /// <param name="timeProvider">The optional time provider.</param>
        /// <returns>A throttled distinct sequence.</returns>
        public IObservableAsync<T> ThrottleDistinct(TimeSpan throttle, TimeProvider? timeProvider = null)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

            return source.DistinctUntilChanged().Throttle(throttle, timeProvider).DistinctUntilChanged();
        }

        /// <summary>
        /// Performs a scan that emits the initial accumulator value before processing the source sequence.
        /// </summary>
        /// <typeparam name="TAccumulate">The accumulator type.</typeparam>
        /// <param name="initial">The initial accumulator value.</param>
        /// <param name="accumulator">The accumulator function.</param>
        /// <returns>A sequence beginning with the initial accumulator value followed by each intermediate accumulated value.</returns>
        public IObservableAsync<TAccumulate> ScanWithInitial<TAccumulate>(
            TAccumulate initial,
            Func<TAccumulate, T, TAccumulate> accumulator)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(accumulator, nameof(accumulator));

            return Return(initial).Concat(source.Scan(initial, accumulator));
        }

        /// <summary>
        /// Delays values until the condition becomes true immediately or the debounce interval elapses.
        /// </summary>
        /// <param name="debounce">The debounce interval.</param>
        /// <param name="condition">The condition that bypasses the delay when true.</param>
        /// <param name="timeProvider">The optional time provider.</param>
        /// <returns>A debounced observable sequence.</returns>
        public IObservableAsync<T> DebounceUntil(TimeSpan debounce, Func<T, bool> condition, TimeProvider? timeProvider = null)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(condition, nameof(condition));

            return source
                .Select(value => condition(value)
                    ? Return(value)
                    : Return(value).Delay(debounce, timeProvider))
                .Switch();
        }

        /// <summary>
        /// Performs an asynchronous scan that emits the initial accumulator value before processing the source sequence.
        /// </summary>
        /// <typeparam name="TAccumulate">The accumulator type.</typeparam>
        /// <param name="initial">The initial accumulator value.</param>
        /// <param name="accumulator">The asynchronous accumulator function.</param>
        /// <returns>A sequence beginning with the initial accumulator value followed by each intermediate accumulated value.</returns>
        public IObservableAsync<TAccumulate> ScanWithInitial<TAccumulate>(
            TAccumulate initial,
            Func<TAccumulate, T, CancellationToken, ValueTask<TAccumulate>> accumulator)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(accumulator, nameof(accumulator));

            return Return(initial).Concat(source.Scan(initial, accumulator));
        }
    }

    /// <summary>
    /// Returns the minimum of the latest values from the supplied source sequences.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The first source sequence.</param>
    /// <param name="sources">Additional source sequences.</param>
    /// <returns>A sequence that emits the minimum of the latest values.</returns>
    public static IObservableAsync<T> GetMin<T>(this IObservableAsync<T> source, params IObservableAsync<T>[] sources)
        where T : struct
    {
        ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
        ArgumentExceptionHelper.ThrowIfNull(sources, nameof(sources));

        var allSources = new IObservableAsync<T>[sources.Length + 1];
        allSources[0] = source;
        sources.CopyTo(allSources, 1);
        return allSources.CombineLatest(static values =>
        {
            var min = values[0];
            for (var i = 1; i < values.Count; i++)
            {
                if (Comparer<T>.Default.Compare(values[i], min) < 0)
                {
                    min = values[i];
                }
            }

            return min;
        });
    }

    /// <summary>
    /// Returns the maximum of the latest values from the supplied source sequences.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The first source sequence.</param>
    /// <param name="sources">Additional source sequences.</param>
    /// <returns>A sequence that emits the maximum of the latest values.</returns>
    public static IObservableAsync<T> GetMax<T>(this IObservableAsync<T> source, params IObservableAsync<T>[] sources)
        where T : struct
    {
        ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
        ArgumentExceptionHelper.ThrowIfNull(sources, nameof(sources));

        var allSources = new IObservableAsync<T>[sources.Length + 1];
        allSources[0] = source;
        sources.CopyTo(allSources, 1);
        return allSources.CombineLatest(static values =>
        {
            var max = values[0];
            for (var i = 1; i < values.Count; i++)
            {
                if (Comparer<T>.Default.Compare(values[i], max) > 0)
                {
                    max = values[i];
                }
            }

            return max;
        });
    }

    /// <summary>
    /// Emits <see langword="true"/> when the latest value from every source sequence is <see langword="false"/>.
    /// </summary>
    /// <param name="sources">The boolean source sequences.</param>
    /// <returns>A sequence of aggregate boolean states.</returns>
    public static IObservableAsync<bool> CombineLatestValuesAreAllFalse(this IEnumerable<IObservableAsync<bool>> sources)
    {
        ArgumentExceptionHelper.ThrowIfNull(sources, nameof(sources));

        var materializedSources = sources as IReadOnlyCollection<IObservableAsync<bool>> ?? sources.ToList();
        return materializedSources.Count == 0
            ? Return(true)
            : materializedSources.CombineLatest(static values =>
            {
                for (var i = 0; i < values.Count; i++)
                {
                    if (values[i])
                    {
                        return false;
                    }
                }

                return true;
            });
    }

    /// <summary>
    /// Emits <see langword="true"/> when the latest value from every source sequence is <see langword="true"/>.
    /// </summary>
    /// <param name="sources">The boolean source sequences.</param>
    /// <returns>A sequence of aggregate boolean states.</returns>
    public static IObservableAsync<bool> CombineLatestValuesAreAllTrue(this IEnumerable<IObservableAsync<bool>> sources)
    {
        ArgumentExceptionHelper.ThrowIfNull(sources, nameof(sources));

        var materializedSources = sources as IReadOnlyCollection<IObservableAsync<bool>> ?? sources.ToList();
        return materializedSources.Count == 0
            ? Return(true)
            : materializedSources.CombineLatest(static values =>
            {
                for (var i = 0; i < values.Count; i++)
                {
                    if (!values[i])
                    {
                        return false;
                    }
                }

                return true;
            });
    }

    /// <summary>
    /// Flattens each enumerable element emitted by the source into individual values.
    /// </summary>
    /// <typeparam name="T">The flattened element type.</typeparam>
    /// <param name="source">The source sequence of enumerables.</param>
    /// <returns>A flattened observable sequence.</returns>
    public static IObservableAsync<T> ForEach<T>(this IObservableAsync<IEnumerable<T>> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

        return source.SelectMany(values => values.ToObservableAsync());
    }

    /// <summary>
    /// Negates each boolean value emitted by the source sequence.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>A sequence of negated boolean values.</returns>
    public static IObservableAsync<bool> Not(this IObservableAsync<bool> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

        return source.Select(static value => !value);
    }

    /// <summary>
    /// Skips <see langword="null"/> values until the first non-null value appears.
    /// </summary>
    /// <typeparam name="T">The reference type element.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <returns>A sequence that starts with the first non-null value.</returns>
    public static IObservableAsync<T> SkipWhileNull<T>(this IObservableAsync<T?> source)
        where T : class
    {
        ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

        return source.SkipWhile(static value => value is null).Select(static value => value!);
    }

    /// <summary>
    /// Creates an observable sequence that executes the supplied action and emits <see cref="Unit.Default"/>.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="taskScheduler">An optional scheduler used to start the action.</param>
    /// <returns>An observable sequence that completes after the action has run.</returns>
    public static IObservableAsync<Unit> Start(Action action, TaskScheduler? taskScheduler = null)
    {
        ArgumentExceptionHelper.ThrowIfNull(action, nameof(action));

        return taskScheduler is null
            ? FromAsync(_ =>
            {
                action();
                return default;
            })
            : CreateAsBackgroundJob<Unit>(
                async (observer, cancellationToken) =>
                {
                    action();
                    await observer.OnNextAsync(Unit.Default, cancellationToken);
                    await observer.OnCompletedAsync(Async.Result.Success);
                },
                taskScheduler);
    }

    /// <summary>
    /// Creates an observable sequence that executes the supplied function and emits its result.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="function">The function to execute.</param>
    /// <param name="taskScheduler">An optional scheduler used to start the function.</param>
    /// <returns>An observable sequence that emits the function result and then completes.</returns>
    public static IObservableAsync<TResult> Start<TResult>(Func<TResult> function, TaskScheduler? taskScheduler = null)
    {
        ArgumentExceptionHelper.ThrowIfNull(function, nameof(function));

        return taskScheduler is null
            ? FromAsync(_ => new ValueTask<TResult>(function()))
            : CreateAsBackgroundJob<TResult>(
                async (observer, cancellationToken) =>
                {
                    await observer.OnNextAsync(function(), cancellationToken);
                    await observer.OnCompletedAsync(Async.Result.Success);
                },
                taskScheduler);
    }

    /// <summary>
    /// Filters the source sequence to <see langword="false"/> values.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>A sequence containing only <see langword="false"/> values.</returns>
    public static IObservableAsync<bool> WhereFalse(this IObservableAsync<bool> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

        return source.Where(static value => !value);
    }

    /// <summary>
    /// Filters the source sequence to non-null values and narrows the result type accordingly.
    /// </summary>
    /// <typeparam name="T">The reference type element.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <returns>A non-nullable observable sequence.</returns>
    public static IObservableAsync<T> WhereIsNotNull<T>(this IObservableAsync<T?> source)
        where T : class
    {
        ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

        return source.Where(static value => value is not null).Select(static value => value!);
    }

    /// <summary>
    /// Filters the source sequence to <see langword="true"/> values.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <returns>A sequence containing only <see langword="true"/> values.</returns>
    public static IObservableAsync<bool> WhereTrue(this IObservableAsync<bool> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

        return source.Where(static value => value);
    }
}
