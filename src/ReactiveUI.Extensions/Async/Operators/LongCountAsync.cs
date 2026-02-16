// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class enable querying and aggregating data from asynchronous observables in a
/// manner similar to LINQ operators. These extensions are designed to be used with types that implement asynchronous
/// observable patterns, allowing for efficient and composable asynchronous data processing.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously returns the number of elements in the sequence that satisfy an optional predicate.
        /// </summary>
        /// <param name="predicate">A function to test each element for a condition. If null, all elements are counted.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the number of elements that
        /// satisfy the predicate, or the total number of elements if the predicate is null.</returns>
        public async ValueTask<long> LongCountAsync(Func<T, bool>? predicate, CancellationToken cancellationToken = default)
        {
            var observer = new LongCountAsyncObserver<T>(predicate, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }

        /// <summary>
        /// Asynchronously returns the total number of elements in the sequence as a 64-bit integer.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A value task representing the asynchronous operation. The result contains the number of elements in the
        /// sequence as a 64-bit integer.</returns>
        public ValueTask<long> LongCountAsync(CancellationToken cancellationToken = default)
            => @this.LongCountAsync(null, cancellationToken);
    }

    private sealed class LongCountAsyncObserver<T>(Func<T, bool>? predicate, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, long>(cancellationToken)
    {
        private long _count;

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
