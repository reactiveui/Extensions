// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for performing asynchronous operations on observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class contains static methods that extend the functionality of asynchronous
/// observable sequences, enabling operations such as counting elements that satisfy a specified condition. These
/// methods are designed to work with types that implement asynchronous observation patterns.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously counts the number of elements that satisfy a specified condition.
        /// </summary>
        /// <param name="predicate">A function to test each element for a condition. If null, all elements are counted.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous count operation. The task result contains the number of elements
        /// that match the predicate.</returns>
        public async ValueTask<int> CountAsync(Func<T, bool>? predicate, CancellationToken cancellationToken = default)
        {
            var observer = new CountAsyncObserver<T>(predicate, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }

        /// <summary>
        /// Asynchronously returns the total number of elements in the data source.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous count operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the number of elements in the
        /// data source.</returns>
        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
            => @this.CountAsync(null, cancellationToken);
    }

    private sealed class CountAsyncObserver<T>(Func<T, bool>? predicate, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, int>(cancellationToken)
    {
        private int _count;

        protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            if (predicate is null || predicate(value))
            {
                _count = checked(_count + 1);
            }

            return default;
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result) => !result.IsSuccess ? TrySetException(result.Exception) : TrySetCompleted(_count);
    }
}
