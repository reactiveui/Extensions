// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
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
    extension<T>(ObservableAsync<T> @this)
    {
        public ObservableAsync<TAcc> Scan<TAcc>(TAcc seed, Func<TAcc, T, CancellationToken, ValueTask<TAcc>> accumulator)
        {
            if (accumulator is null)
            {
                throw new ArgumentNullException(nameof(accumulator));
            }

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

        public ObservableAsync<TAcc> Scan<TAcc>(TAcc seed, Func<TAcc, T, TAcc> accumulator)
        {
            if (accumulator is null)
            {
                throw new ArgumentNullException(nameof(accumulator));
            }

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
