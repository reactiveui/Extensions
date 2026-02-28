// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides TakeWhile extension methods for asynchronous observable sequences.
/// </summary>
/// <remarks>TakeWhile emits elements from the source sequence as long as a predicate is satisfied,
/// then completes the sequence when the predicate returns false.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Returns elements from the observable sequence as long as the specified asynchronous condition is true,
        /// then completes.
        /// </summary>
        /// <param name="predicate">An asynchronous function to test each element for a condition. Receives the element
        /// and a cancellation token.</param>
        /// <returns>An observable sequence that contains elements from the source sequence that satisfy the
        /// condition, completing as soon as the predicate returns false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is null.</exception>
        public IObservableAsync<T> TakeWhile(Func<T, CancellationToken, ValueTask<bool>> predicate)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
#else
            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
#endif

            return Create<T>(async (observer, subscribeToken) =>
                await @this.SubscribeAsync(
                    async (x, token) =>
                    {
                        if (await predicate(x, token))
                        {
                            await observer.OnNextAsync(x, token);
                        }
                        else
                        {
                            await observer.OnCompletedAsync(Result.Success);
                        }
                    },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken));
        }

        /// <summary>
        /// Returns elements from the observable sequence as long as the specified condition is true,
        /// then completes.
        /// </summary>
        /// <param name="predicate">A function to test each element for a condition.</param>
        /// <returns>An observable sequence that contains elements from the source sequence that satisfy the
        /// condition, completing as soon as the predicate returns false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is null.</exception>
        public IObservableAsync<T> TakeWhile(Func<T, bool> predicate)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
#else
            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
#endif

            return Create<T>(async (observer, subscribeToken) =>
                await @this.SubscribeAsync(
                    async (x, token) =>
                    {
                        if (predicate(x))
                        {
                            await observer.OnNextAsync(x, token);
                        }
                        else
                        {
                            await observer.OnCompletedAsync(Result.Success);
                        }
                    },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken));
        }
    }
}
