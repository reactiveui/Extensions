// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class enable querying and manipulation of asynchronous observables, such as
/// retrieving the first element that matches a specified condition. These extensions are designed to be used with types
/// that implement asynchronous observable patterns.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously returns the first element in the sequence that satisfies the specified predicate.
        /// </summary>
        /// <param name="predicate">A function to test each element for a condition. The method returns the first element for which this
        /// predicate returns <see langword="true"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the first element that matches
        /// the predicate.</returns>
        public async ValueTask<T> FirstAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
        {
            var observer = new FirstAsyncObserver<T>(predicate, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }

        /// <summary>
        /// Asynchronously returns the first element of the sequence.
        /// </summary>
        /// <remarks>If the sequence is empty, the behavior depends on the implementation and may result
        /// in an exception being thrown.</remarks>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the first element of the
        /// sequence.</returns>
        public async ValueTask<T> FirstAsync(CancellationToken cancellationToken = default)
        {
            var observer = new FirstAsyncObserver<T>(null, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }
    }

    private sealed class FirstAsyncObserver<T>(Func<T, bool>? predicate, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, T>(cancellationToken)
    {
        protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            if (predicate is null || predicate(value))
            {
                await TrySetCompleted(value);
            }
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result)
        {
            var exception = result.IsSuccess
                ? new InvalidOperationException(predicate is null ? "Sequence contains no elements." : "Sequence contains no matching elements.")
                : result.Exception;
            return TrySetException(exception);
        }
    }
}
