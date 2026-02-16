// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences, enabling operations such as
/// suppressing consecutive duplicate elements.
/// </summary>
/// <remarks>The methods in this class allow developers to filter out consecutive duplicates in observable
/// sequences, either by value or by a specified key. These operations are useful for scenarios where only changes or
/// distinct consecutive values are of interest, such as event streams or state change notifications.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Returns an observable sequence that emits only distinct consecutive elements, suppressing duplicates that
        /// are equal to the previous element.
        /// </summary>
        /// <remarks>Elements are compared using the default equality comparer for the type <typeparamref
        /// name="T"/>. Only consecutive duplicate elements are suppressed; non-consecutive duplicates are not
        /// affected.</remarks>
        /// <returns>An observable sequence that contains only the elements from the source sequence that are not equal to their
        /// immediate predecessor.</returns>
        public IObservableAsync<T> DistinctUntilChanged() => @this.DistinctUntilChanged(EqualityComparer<T>.Default);

        /// <summary>
        /// Returns an observable sequence that emits elements from the source sequence only when the current element is
        /// not equal to the previous element, as determined by the specified equality comparer.
        /// </summary>
        /// <remarks>Use this method to suppress consecutive duplicate elements in the sequence. Only
        /// elements that differ from their immediate predecessor, according to the provided comparer, are emitted to
        /// observers.</remarks>
        /// <param name="equalityComparer">An equality comparer used to determine whether consecutive elements are considered equal.</param>
        /// <returns>An observable sequence that contains only distinct consecutive elements from the source sequence, as
        /// determined by the specified equality comparer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="equalityComparer"/> is <see langword="null"/>.</exception>
        public IObservableAsync<T> DistinctUntilChanged(IEqualityComparer<T> equalityComparer)
        {
            if (equalityComparer is null)
            {
                throw new ArgumentNullException(nameof(equalityComparer));
            }

            return Create<T>(async (observer, subscribeToken) =>
            {
                bool hasPrevious = false;
                T? previous = default;
                return await @this.SubscribeAsync(
                    async (x, token) =>
                {
                    var hadPrevious = hasPrevious;
                    var previousValue = previous;
                    hasPrevious = true;
                    previous = x;
                    if (!hadPrevious || !equalityComparer.Equals(previousValue!, x))
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
        /// Returns an observable sequence that emits elements from the source sequence, suppressing consecutive
        /// duplicates as determined by a key selector function.
        /// </summary>
        /// <remarks>The comparison of keys uses the default equality comparer for the type <typeparamref
        /// name="TKey"/>. Only consecutive duplicate elements are suppressed; non-consecutive duplicates are not
        /// affected.</remarks>
        /// <typeparam name="TKey">The type of the key used to determine whether consecutive elements are considered duplicates.</typeparam>
        /// <param name="keySelector">A function that extracts the comparison key from each element in the source sequence.</param>
        /// <returns>An observable sequence that contains only the elements from the source sequence that are not consecutive
        /// duplicates according to the specified key.</returns>
        public IObservableAsync<T> DistinctUntilChangedBy<TKey>(Func<T, TKey> keySelector) => @this.DistinctUntilChangedBy(keySelector, EqualityComparer<TKey>.Default);

        /// <summary>
        /// Returns an observable sequence that emits elements from the source sequence, suppressing consecutive
        /// duplicates as determined by a key selector and equality comparer.
        /// </summary>
        /// <remarks>The first element in the sequence is always emitted. Subsequent elements are emitted
        /// only if their key, as determined by <paramref name="keySelector"/>, is not equal to the key of the
        /// immediately preceding element, as determined by <paramref name="equalityComparer"/>.</remarks>
        /// <typeparam name="TKey">The type of the key used to determine whether consecutive elements are considered duplicates.</typeparam>
        /// <param name="keySelector">A function that extracts the comparison key from each element in the source sequence.</param>
        /// <param name="equalityComparer">An equality comparer used to compare keys for equality.</param>
        /// <returns>An observable sequence that contains only the elements from the source sequence that are not consecutive
        /// duplicates according to the specified key and comparer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="keySelector"/> or <paramref name="equalityComparer"/> is null.</exception>
        public IObservableAsync<T> DistinctUntilChangedBy<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey> equalityComparer)
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
                bool hasPrevious = false;
                TKey? previousKey = default;
                return await @this.SubscribeAsync(
                    async (x, token) =>
                {
                    var hadPrevious = hasPrevious;
                    var prev = previousKey;
                    var key = keySelector(x);
                    hasPrevious = true;
                    previousKey = key;
                    if (!hadPrevious || !equalityComparer.Equals(prev!, key))
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
