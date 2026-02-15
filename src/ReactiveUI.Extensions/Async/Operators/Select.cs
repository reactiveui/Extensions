// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for creating and transforming asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class enable functional-style operations, such as projection, on asynchronous
/// observables. These extensions facilitate composing and manipulating streams of data in an asynchronous context,
/// similar to LINQ operations for synchronous observables.</remarks>
public static partial class ObservableAsync
{
    extension<T>(ObservableAsync<T> @this)
    {
        /// <summary>
        /// Projects each element of the observable sequence into a new form using the specified asynchronous selector
        /// function.
        /// </summary>
        /// <remarks>The selector function is invoked for each element as it is observed. If the selector
        /// function throws an exception or returns a faulted task, the error is propagated to the observer. The
        /// operation supports cancellation via the provided cancellation token.</remarks>
        /// <typeparam name="TDest">The type of the value returned by the selector function and produced by the resulting observable sequence.</typeparam>
        /// <param name="selector">A function that transforms each element of the source sequence into a value of type <typeparamref
        /// name="TDest"/> asynchronously. The function receives the source element and a cancellation token.</param>
        /// <returns>An observable sequence of type <typeparamref name="TDest"/> containing the results of applying the selector
        /// function to each element of the source sequence.</returns>
        public ObservableAsync<TDest> Select<TDest>(Func<T, CancellationToken, ValueTask<TDest>> selector) =>
            Create<TDest>(async (observer, subscribeToken) => await @this.SubscribeAsync(
                async (x, token) =>
            {
                var mapped = await selector(x, token);
                await observer.OnNextAsync(mapped, token);
            },
                observer.OnErrorResumeAsync,
                observer.OnCompletedAsync,
                subscribeToken));

        /// <summary>
        /// Projects each element of the observable sequence into a new form using the specified selector function.
        /// </summary>
        /// <remarks>The selector function is applied to each element as it is observed. If the selector
        /// throws an exception, the error is propagated to the observer. This method does not modify the source
        /// sequence; it produces a new sequence with transformed elements.</remarks>
        /// <typeparam name="TDest">The type of the value returned by the selector function.</typeparam>
        /// <param name="selector">A function that transforms each element of the source sequence into a new value. Cannot be null.</param>
        /// <returns>An observable sequence whose elements are the result of invoking the selector function on each element of
        /// the source sequence.</returns>
        public ObservableAsync<TDest> Select<TDest>(Func<T, TDest> selector) =>
            Create<TDest>(async (observer, subscribeToken) => await @this.SubscribeAsync(
                (x, token) =>
            {
                var mapped = selector(x);
                return observer.OnNextAsync(mapped, token);
            },
                observer.OnErrorResumeAsync,
                observer.OnCompletedAsync,
                subscribeToken));
    }
}
