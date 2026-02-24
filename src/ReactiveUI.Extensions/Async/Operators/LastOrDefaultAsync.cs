// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides a set of extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class enable querying and retrieving elements from asynchronous observables, such
/// as obtaining the last element or a default value if no elements are found. These extensions are designed to support
/// asynchronous and cancellation-aware operations on observable sequences.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously returns the last element in the sequence that satisfies the specified predicate, or a default
        /// value if no such element is found.
        /// </summary>
        /// <param name="predicate">A function to test each element for a condition. The method returns the last element for which this
        /// predicate returns <see langword="true"/>.</param>
        /// <param name="defaultValue">The value to return if no element in the sequence satisfies the predicate.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A value task that represents the asynchronous operation. The result contains the last element that matches
        /// the predicate, or <paramref name="defaultValue"/> if no such element is found.</returns>
        public async ValueTask<T?> LastOrDefaultAsync(Func<T, bool> predicate, T? defaultValue, CancellationToken cancellationToken = default)
        {
            var observer = new LastOrDefaultObserver<T>(predicate, defaultValue, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }

        /// <summary>
        /// Asynchronously returns the last element of a sequence, or a default value if the sequence contains no
        /// elements.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A value task that represents the asynchronous operation. The task result contains the last element of the
        /// sequence, or the default value for type T if the sequence is empty.</returns>
        public ValueTask<T?> LastOrDefaultAsync(CancellationToken cancellationToken = default) => @this.LastOrDefaultAsync(default, cancellationToken);

        /// <summary>
        /// Asynchronously returns the last element of the sequence, or a specified default value if the sequence
        /// contains no elements.
        /// </summary>
        /// <param name="defaultValue">The value to return if the sequence is empty.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A value task that represents the asynchronous operation. The task result contains the last element of the
        /// sequence, or <paramref name="defaultValue"/> if the sequence is empty.</returns>
        public async ValueTask<T?> LastOrDefaultAsync(T? defaultValue, CancellationToken cancellationToken = default)
        {
            var observer = new LastOrDefaultObserver<T>(null, defaultValue, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }
    }

    private sealed class LastOrDefaultObserver<T>(Func<T, bool>? predicate, T? defaultValue, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, T>(cancellationToken)
    {
        private T? _last = defaultValue;

        protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            if (predicate is null || predicate(value))
            {
                _last = value;
            }

            return default;
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result) =>
            result.IsSuccess ? TrySetCompleted(_last!) : TrySetException(result.Exception);
    }
}
