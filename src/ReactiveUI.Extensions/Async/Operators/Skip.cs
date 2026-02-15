// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class contains static extension methods that enable LINQ-style and other
/// operations on asynchronous observables. These methods are intended to facilitate the composition and manipulation of
/// asynchronous data streams in a reactive programming style.</remarks>
public static partial class ObservableAsync
{
    extension<T>(ObservableAsync<T> @this)
    {
        /// <summary>
        /// Returns a new observable sequence that skips the specified number of elements from the start of the source
        /// sequence.
        /// </summary>
        /// <param name="count">The number of elements to skip. Must be greater than or equal to 0.</param>
        /// <returns>An observable sequence that contains the elements of the source sequence after the specified number of
        /// elements have been skipped. If the count is 0, the original sequence is returned.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if count is less than 0.</exception>
        public ObservableAsync<T> Skip(int count) => count switch
        {
            < 0 => throw new ArgumentOutOfRangeException(nameof(count)),
            0 => @this,
            _ => Create<T>(async (observer, subscribeToken) =>
            {
                var remaining = count;

                return await @this.SubscribeAsync(
                    async (x, token) =>
                {
                    if (remaining > 0)
                    {
                        remaining--;
                        return;
                    }

                    await observer.OnNextAsync(x, token);
                },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken);
            })
        };
    }
}
