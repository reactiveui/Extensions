// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides Zip extension methods for asynchronous observable sequences.
/// </summary>
/// <remarks>Zip combines elements from two observable sequences pair-wise. The nth element from
/// each source is paired together. The resulting sequence completes when either source completes.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Combines two observable sequences element-by-element using the specified result selector.
    /// </summary>
    /// <typeparam name="T1">The type of elements in the first source sequence.</typeparam>
    /// <typeparam name="T2">The type of elements in the second source sequence.</typeparam>
    /// <typeparam name="TResult">The type of elements in the result sequence.</typeparam>
    /// <param name="first">The first observable sequence. Cannot be null.</param>
    /// <param name="second">The second observable sequence. Cannot be null.</param>
    /// <param name="resultSelector">A function to apply to each pair of elements. Cannot be null.</param>
    /// <returns>An observable sequence whose elements are the result of pair-wise combining the source
    /// elements using the result selector.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
    public static IObservableAsync<TResult> Zip<T1, T2, TResult>(
        this IObservableAsync<T1> first,
        IObservableAsync<T2> second,
        Func<T1, T2, TResult> resultSelector)
    {
        if (first is null)
        {
            throw new ArgumentNullException(nameof(first));
        }

        if (second is null)
        {
            throw new ArgumentNullException(nameof(second));
        }

        if (resultSelector is null)
        {
            throw new ArgumentNullException(nameof(resultSelector));
        }

        return new ZipObservable<T1, T2, TResult>(first, second, resultSelector);
    }

    /// <summary>
    /// Combines two observable sequences element-by-element into pairs.
    /// </summary>
    /// <typeparam name="T1">The type of elements in the first source sequence.</typeparam>
    /// <typeparam name="T2">The type of elements in the second source sequence.</typeparam>
    /// <param name="first">The first observable sequence. Cannot be null.</param>
    /// <param name="second">The second observable sequence. Cannot be null.</param>
    /// <returns>An observable sequence of tuples pairing elements from each source.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
    public static IObservableAsync<(T1 First, T2 Second)> Zip<T1, T2>(
        this IObservableAsync<T1> first,
        IObservableAsync<T2> second) => Zip(first, second, static (a, b) => (a, b));

    /// <summary>
    /// Represents an observable sequence that combines the latest values from two asynchronous observable sequences
    /// into a single result sequence using a specified selector function.
    /// </summary>
    /// <remarks>The resulting sequence produces a value each time both source sequences have produced an
    /// element, pairing elements in the order they are received. The sequence completes when either source sequence
    /// completes and there are no more pairs to combine. If either source sequence signals an error, the resulting
    /// sequence will propagate that error.</remarks>
    /// <typeparam name="T1">The type of the elements in the first source sequence.</typeparam>
    /// <typeparam name="T2">The type of the elements in the second source sequence.</typeparam>
    /// <typeparam name="TResult">The type of the elements in the resulting sequence produced by the selector function.</typeparam>
    /// <param name="first">The first asynchronous observable sequence to combine.</param>
    /// <param name="second">The second asynchronous observable sequence to combine.</param>
    /// <param name="resultSelector">A function that specifies how to combine elements from the first and second sequences into a result element.</param>
    private sealed class ZipObservable<T1, T2, TResult>(
        IObservableAsync<T1> first,
        IObservableAsync<T2> second,
        Func<T1, T2, TResult> resultSelector) : ObservableAsync<TResult>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<TResult> observer, CancellationToken cancellationToken)
        {
            var state = new ZipState(observer, resultSelector);

            var sub1 = await first.SubscribeAsync(
                new FirstObserver(state),
                cancellationToken);

            var sub2 = await second.SubscribeAsync(
                new SecondObserver(state),
                cancellationToken);

            return new CompositeDisposableAsync(sub1, sub2);
        }

        private sealed class ZipState(IObserverAsync<TResult> observer, Func<T1, T2, TResult> resultSelector)
        {
            private readonly object _gate = new();
            private readonly Queue<T1> _queue1 = new();
            private readonly Queue<T2> _queue2 = new();
            private bool _completed1;
            private bool _completed2;
            private bool _done;

            public async ValueTask OnNext1Async(T1 value, CancellationToken cancellationToken)
            {
                T2 second;
                lock (_gate)
                {
                    if (_done)
                    {
                        return;
                    }

                    if (_queue2.Count > 0)
                    {
                        second = _queue2.Dequeue();
                    }
                    else
                    {
                        _queue1.Enqueue(value);
                        return;
                    }
                }

                var result = resultSelector(value, second);
                await observer.OnNextAsync(result, cancellationToken);
            }

            public async ValueTask OnNext2Async(T2 value, CancellationToken cancellationToken)
            {
                T1 firstVal;
                lock (_gate)
                {
                    if (_done)
                    {
                        return;
                    }

                    if (_queue1.Count > 0)
                    {
                        firstVal = _queue1.Dequeue();
                    }
                    else
                    {
                        _queue2.Enqueue(value);
                        return;
                    }
                }

                var result = resultSelector(firstVal, value);
                await observer.OnNextAsync(result, cancellationToken);
            }

            public async ValueTask OnCompleted1Async(Result result)
            {
                bool shouldComplete;
                lock (_gate)
                {
                    if (_done)
                    {
                        return;
                    }

                    _completed1 = true;
                    shouldComplete = result.IsFailure || _completed2 || _queue1.Count == 0;
                    if (shouldComplete)
                    {
                        _done = true;
                    }
                }

                if (shouldComplete)
                {
                    await observer.OnCompletedAsync(result);
                }
            }

            public async ValueTask OnCompleted2Async(Result result)
            {
                bool shouldComplete;
                lock (_gate)
                {
                    if (_done)
                    {
                        return;
                    }

                    _completed2 = true;
                    shouldComplete = result.IsFailure || _completed1 || _queue2.Count == 0;
                    if (shouldComplete)
                    {
                        _done = true;
                    }
                }

                if (shouldComplete)
                {
                    await observer.OnCompletedAsync(result);
                }
            }

            public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken) =>
                observer.OnErrorResumeAsync(error, cancellationToken);
        }

        private sealed class FirstObserver(ZipState state) : ObserverAsync<T1>
        {
            protected override ValueTask OnNextAsyncCore(T1 value, CancellationToken cancellationToken) =>
                state.OnNext1Async(value, cancellationToken);

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
                state.OnErrorResumeAsync(error, cancellationToken);

            protected override ValueTask OnCompletedAsyncCore(Result result) =>
                state.OnCompleted1Async(result);
        }

        private sealed class SecondObserver(ZipState state) : ObserverAsync<T2>
        {
            protected override ValueTask OnNextAsyncCore(T2 value, CancellationToken cancellationToken) =>
                state.OnNext2Async(value, cancellationToken);

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
                state.OnErrorResumeAsync(error, cancellationToken);

            protected override ValueTask OnCompletedAsyncCore(Result result) =>
                state.OnCompleted2Async(result);
        }
    }
}
