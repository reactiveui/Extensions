// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for asynchronous observable sequences, enabling functional operations such as scanning
/// and accumulation over streamed data.
/// </summary>
/// <remarks>The methods in this class allow developers to perform stateful transformations and aggregations on
/// asynchronous observables. These operations are useful for scenarios where intermediate results or running totals are
/// needed as items are received. All methods are designed to work with asynchronous patterns and support cancellation
/// via tokens.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Applies an accumulator function over the observable sequence and returns each intermediate result
        /// using the specified asynchronous accumulator.
        /// </summary>
        /// <typeparam name="TAcc">The type of the accumulated value.</typeparam>
        /// <param name="seed">The initial accumulator value.</param>
        /// <param name="accumulator">An asynchronous accumulator function to be invoked on each element. Receives the current accumulator value,
        /// the current element, and a cancellation token.</param>
        /// <returns>An observable sequence containing the accumulated values produced after each element is processed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="accumulator"/> is null.</exception>
        public IObservableAsync<TAcc> Scan<TAcc>(TAcc seed, Func<TAcc, T, CancellationToken, ValueTask<TAcc>> accumulator)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(accumulator);
#else
            if (accumulator is null)
            {
                throw new ArgumentNullException(nameof(accumulator));
            }
#endif

            return Create<TAcc>(async (observer, subscribeToken) =>
            {
                var acc = seed;
                return await @this.SubscribeAsync(
                    async (x, token) =>
                {
                    acc = await accumulator(acc, x, token);
                    await observer.OnNextAsync(acc, token);
                },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken);
            });
        }

        /// <summary>
        /// Applies an accumulator function over the observable sequence and returns each intermediate result.
        /// </summary>
        /// <typeparam name="TAcc">The type of the accumulated value.</typeparam>
        /// <param name="seed">The initial accumulator value.</param>
        /// <param name="accumulator">An accumulator function to be invoked on each element. Receives the current accumulator value and the
        /// current element.</param>
        /// <returns>An observable sequence containing the accumulated values produced after each element is processed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="accumulator"/> is null.</exception>
        public IObservableAsync<TAcc> Scan<TAcc>(TAcc seed, Func<TAcc, T, TAcc> accumulator)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(accumulator);
#else
            if (accumulator is null)
            {
                throw new ArgumentNullException(nameof(accumulator));
            }
#endif

            return Create<TAcc>(async (observer, subscribeToken) =>
            {
                var acc = seed;
                return await @this.SubscribeAsync(
                    (x, token) =>
                {
                    acc = accumulator(acc, x);
                    return observer.OnNextAsync(acc, token);
                },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken);
            });
        }
    }
}
