// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for creating and manipulating asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class offers LINQ-style operators for working with asynchronous observables,
/// enabling developers to compose, filter, and transform event streams in an asynchronous context. These methods are
/// designed to integrate with the ObservableAsync{T} type, supporting both synchronous and asynchronous predicate
/// functions for filtering sequences.</remarks>
public static partial class ObservableAsync
{
    extension<T>(ObservableAsync<T> @this)
    {
        /// <summary>
        /// Creates a new observable sequence that contains only the elements from the source sequence that satisfy the
        /// specified asynchronous predicate.
        /// </summary>
        /// <remarks>The predicate is invoked asynchronously for each element as it is observed. If the
        /// predicate throws an exception or the ValueTask is faulted, the resulting sequence will propagate the error
        /// to its observers. The cancellation token provided to the predicate can be used to observe cancellation
        /// requests during predicate evaluation.</remarks>
        /// <param name="predicate">A function that evaluates each element and its associated cancellation token, returning a ValueTask that
        /// resolves to <see langword="true"/> to include the element in the resulting sequence; otherwise, <see
        /// langword="false"/>.</param>
        /// <returns>An observable sequence that emits only those elements for which the predicate returns <see
        /// langword="true"/>.</returns>
        public ObservableAsync<T> Where(Func<T, CancellationToken, ValueTask<bool>> predicate) =>
            Create<T>(async (observer, subscribeToken) => await @this.SubscribeAsync(
                async (x, token) =>
            {
                if (await predicate(x, token))
                {
                    await observer.OnNextAsync(x, token);
                }
            },
                observer.OnErrorResumeAsync,
                observer.OnCompletedAsync,
                subscribeToken));

        /// <summary>
        /// Creates a new observable sequence that contains only the elements from the current sequence that satisfy the
        /// specified predicate.
        /// </summary>
        /// <remarks>The resulting observable emits only those elements for which the <paramref
        /// name="predicate"/> returns <see langword="true"/>. The order and timing of element emission are preserved
        /// from the original sequence.</remarks>
        /// <param name="predicate">A function to test each element for a condition. The element is included in the resulting sequence if the
        /// function returns <see langword="true"/>.</param>
        /// <returns>An observable sequence that contains elements from the current sequence that satisfy the specified
        /// predicate.</returns>
        public ObservableAsync<T> Where(Func<T, bool> predicate) =>
            Create<T>(async (observer, subscribeToken) => await @this.SubscribeAsync(
                (x, token) =>
                {
                    if (predicate(x))
                    {
                        return observer.OnNextAsync(x, token);
                    }

                    return default;
                },
                observer.OnErrorResumeAsync,
                observer.OnCompletedAsync,
                subscribeToken));
    }
}
