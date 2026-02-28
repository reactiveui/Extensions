// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for asynchronously converting an observable sequence to a dictionary.
/// </summary>
/// <remarks>The methods in this class enable the transformation of an asynchronous observable sequence into a
/// dictionary, using user-supplied key and element selector functions. These operations are performed asynchronously
/// and support cancellation via a CancellationToken. All methods throw an exception if duplicate keys are encountered
/// in the source sequence, consistent with the behavior of Dictionary{TKey, TValue}.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously creates a dictionary from the elements of the sequence, using the specified key selector
        /// function.
        /// </summary>
        /// <typeparam name="TKey">The type of the keys in the resulting dictionary. Must be non-nullable.</typeparam>
        /// <param name="keySelector">A function to extract a key from each element in the sequence. Cannot be null.</param>
        /// <param name="comparer">An optional equality comparer to compare keys. If null, the default equality comparer for the key type is
        /// used.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a dictionary mapping keys to
        /// elements from the sequence.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the keySelector parameter is null.</exception>
        public async ValueTask<Dictionary<TKey, T>> ToDictionaryAsync<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer = null, CancellationToken cancellationToken = default)
            where TKey : notnull
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(keySelector, nameof(keySelector));
#else
            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }
#endif

            var observer = new ToDictionaryAsyncObserver<T, TKey, T>(keySelector, x => x, comparer, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }

        /// <summary>
        /// Asynchronously creates a dictionary from the elements of the sequence using the specified key and element
        /// selector functions.
        /// </summary>
        /// <remarks>If multiple elements produce the same key, an exception may be thrown. The operation
        /// is performed asynchronously and can be cancelled using the provided cancellation token.</remarks>
        /// <typeparam name="TKey">The type of the keys in the resulting dictionary. Must be non-nullable.</typeparam>
        /// <typeparam name="TValue">The type of the values in the resulting dictionary.</typeparam>
        /// <param name="keySelector">A function to extract a key from each element in the sequence.</param>
        /// <param name="elementSelector">A function to map each element in the sequence to a value in the resulting dictionary.</param>
        /// <param name="comparer">An optional equality comparer to compare keys. If null, the default equality comparer for the key type is
        /// used.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a dictionary mapping keys to
        /// values as defined by the selector functions.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="keySelector"/> or <paramref name="elementSelector"/> is null.</exception>
        public async ValueTask<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(Func<T, TKey> keySelector, Func<T, TValue> elementSelector, IEqualityComparer<TKey>? comparer = null, CancellationToken cancellationToken = default)
            where TKey : notnull
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(keySelector, nameof(keySelector));
            ArgumentNullException.ThrowIfNull(elementSelector, nameof(elementSelector));
#else
            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            if (elementSelector is null)
            {
                throw new ArgumentNullException(nameof(elementSelector));
            }
#endif

            var observer = new ToDictionaryAsyncObserver<T, TKey, TValue>(keySelector, elementSelector, comparer, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }
    }

    private sealed class ToDictionaryAsyncObserver<TSource, TKey, TValue>(Func<TSource, TKey> keySelector, Func<TSource, TValue> elementSelector, IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken) : TaskObserverAsyncBase<TSource, Dictionary<TKey, TValue>>(cancellationToken)
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _map = comparer is null ? new() : new(comparer);

        protected override ValueTask OnNextAsyncCore(TSource value, CancellationToken cancellationToken)
        {
            var key = keySelector(value);
            _map.Add(key, elementSelector(value));
            return default;
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result)
            => !result.IsSuccess ? TrySetException(result.Exception) : TrySetCompleted(_map);
    }
}
