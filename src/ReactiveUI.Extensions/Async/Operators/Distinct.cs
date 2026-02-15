// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides a set of static methods for creating and composing asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class offers extension methods that enable functional-style operations, such as
/// filtering for distinct elements, on asynchronous observable sequences. These methods are designed to work with the
/// ObservableAsync{T} type, allowing developers to build complex, asynchronous event processing pipelines in a
/// composable manner.</remarks>
public static partial class ObservableAsync
{
    extension<T>(ObservableAsync<T> @this)
    {
        /// <summary>
        /// Returns a sequence that contains only distinct elements from the source sequence, using the default equality
        /// comparer for the element type.
        /// </summary>
        /// <remarks>Elements are considered distinct based on the default equality comparer for type T.
        /// The order of elements is preserved.</remarks>
        /// <returns>An observable sequence that contains distinct elements from the source sequence.</returns>
        public ObservableAsync<T> Distinct() => @this.Distinct(EqualityComparer<T>.Default);

        /// <summary>
        /// Returns an observable sequence that contains only distinct elements from the source sequence, using the
        /// specified equality comparer to determine uniqueness.
        /// </summary>
        /// <remarks>Only the first occurrence of each element, as determined by the specified equality
        /// comparer, is emitted to observers. Subsequent duplicate elements are ignored.</remarks>
        /// <param name="equalityComparer">An equality comparer to compare values for equality. If null, the default equality comparer for the type is
        /// used.</param>
        /// <returns>An observable sequence that emits each distinct element from the source sequence, in the order in which they
        /// are received.</returns>
        public ObservableAsync<T> Distinct(IEqualityComparer<T> equalityComparer)
        {
            return Create<T>(async (observer, subscribeToken) =>
            {
                var seen = new HashSet<T>(equalityComparer);
                return await @this.SubscribeAsync(
                    async (x, token) =>
                {
                    if (seen.Add(x))
                    {
                        await observer.OnNextAsync(x, token);
                    }
                },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken);
            });
        }

        /// <summary>
        /// Returns a sequence that contains distinct elements from the source sequence according to a specified key
        /// selector function.
        /// </summary>
        /// <remarks>Elements are considered distinct based on the value returned by the key selector and
        /// the default equality comparer for the key type.</remarks>
        /// <typeparam name="TKey">The type of the key returned by the key selector function.</typeparam>
        /// <param name="keySelector">A function to extract the key for each element. Cannot be null.</param>
        /// <returns>An observable sequence that contains only the first occurrence of each distinct key as determined by the key
        /// selector.</returns>
        public ObservableAsync<T> DistinctBy<TKey>(Func<T, TKey> keySelector) => @this.DistinctBy(keySelector, EqualityComparer<TKey>.Default);

        /// <summary>
        /// Returns an observable sequence that contains only distinct elements from the source sequence, comparing
        /// values based on a specified key and equality comparer.
        /// </summary>
        /// <remarks>Elements are considered distinct based on the value returned by the <paramref
        /// name="keySelector"/> function and compared using the provided <paramref name="equalityComparer"/>. Only the
        /// first occurrence of each key is included in the resulting sequence.</remarks>
        /// <typeparam name="TKey">The type of the key used to determine the distinctness of elements.</typeparam>
        /// <param name="keySelector">A function to extract the key for each element. Cannot be null.</param>
        /// <param name="equalityComparer">An equality comparer to compare keys for equality. Cannot be null.</param>
        /// <returns>An observable sequence that contains only the first occurrence of each distinct key as determined by the
        /// specified key selector and equality comparer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="keySelector"/> or <paramref name="equalityComparer"/> is null.</exception>
        public ObservableAsync<T> DistinctBy<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey> equalityComparer)
        {
            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            if (equalityComparer is null)
            {
                throw new ArgumentNullException(nameof(equalityComparer));
            }

            return Create<T>(async (observer, subscribeToken) =>
            {
                var seen = new HashSet<TKey>(equalityComparer);
                return await @this.SubscribeAsync(
                    async (x, token) =>
                {
                    var key = keySelector(x);
                    if (seen.Add(key))
                    {
                        await observer.OnNextAsync(x, token);
                    }
                },
                    observer.OnErrorResumeAsync,
                    observer.OnCompletedAsync,
                    subscribeToken);
            });
        }
    }
}
