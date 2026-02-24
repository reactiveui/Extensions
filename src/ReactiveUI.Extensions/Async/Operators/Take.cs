// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class contains static extension methods that enable advanced operations on
/// asynchronous observables, such as filtering, transformation, and sequence control. These methods are intended to be
/// used with the ObservableAsync{T} type to facilitate reactive programming patterns in asynchronous
/// scenarios.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Returns a new observable sequence that emits only the first specified number of elements from the source
        /// sequence.
        /// </summary>
        /// <remarks>If the source sequence contains fewer elements than <paramref name="count"/>, all
        /// available elements are emitted and the sequence completes. This method does not modify the source sequence;
        /// it returns a new sequence with the specified behavior.</remarks>
        /// <param name="count">The maximum number of elements to emit from the source sequence. Must be greater than or equal to zero.</param>
        /// <returns>An observable sequence that contains at most the first <paramref name="count"/> elements from the source
        /// sequence. If <paramref name="count"/> is zero, the resulting sequence completes immediately without emitting
        /// any elements.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="count"/> is less than zero.</exception>
        public IObservableAsync<T> Take(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            return Create<T>(async (observer, subscribeToken) =>
            {
                if (count == 0)
                {
                    await observer.OnCompletedAsync(Result.Success);
                    return DisposableAsync.Empty;
                }

                var remaining = count;

                return await @this.SubscribeAsync(
                    async (x, token) =>
                {
                    remaining--;
                    await observer.OnNextAsync(x, token);

                    if (remaining == 0)
                    {
                        await observer.OnCompletedAsync(Result.Success);
                    }
                },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken);
            });
        }
    }
}
