// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides SkipWhile extension methods for asynchronous observable sequences.
/// </summary>
/// <remarks>SkipWhile bypasses elements in the source sequence as long as a predicate is satisfied,
/// then emits all remaining elements.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Bypasses elements in the observable sequence as long as the specified asynchronous condition is true,
        /// then emits all remaining elements.
        /// </summary>
        /// <param name="predicate">An asynchronous function to test each element for a condition. Receives the element
        /// and a cancellation token.</param>
        /// <returns>An observable sequence that skips elements while the predicate returns true and emits
        /// all subsequent elements.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is null.</exception>
        public IObservableAsync<T> SkipWhile(Func<T, CancellationToken, ValueTask<bool>> predicate)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(predicate);
#else
            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
#endif

            return Create<T>(async (observer, subscribeToken) =>
            {
                var skipping = true;

                return await @this.SubscribeAsync(
                    async (x, token) =>
                    {
                        if (skipping)
                        {
                            if (await predicate(x, token))
                            {
                                return;
                            }

                            skipping = false;
                        }

                        await observer.OnNextAsync(x, token);
                    },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken);
            });
        }

        /// <summary>
        /// Bypasses elements in the observable sequence as long as the specified condition is true,
        /// then emits all remaining elements.
        /// </summary>
        /// <param name="predicate">A function to test each element for a condition.</param>
        /// <returns>An observable sequence that skips elements while the predicate returns true and emits
        /// all subsequent elements.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is null.</exception>
        public IObservableAsync<T> SkipWhile(Func<T, bool> predicate)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(predicate);
#else
            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
#endif

            return Create<T>(async (observer, subscribeToken) =>
            {
                var skipping = true;

                return await @this.SubscribeAsync(
                    async (x, token) =>
                    {
                        if (skipping)
                        {
                            if (predicate(x))
                            {
                                return;
                            }

                            skipping = false;
                        }

                        await observer.OnNextAsync(x, token);
                    },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken);
            });
        }
    }
}
