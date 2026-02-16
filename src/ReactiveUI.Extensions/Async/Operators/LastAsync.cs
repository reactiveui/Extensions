// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides a set of extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class enable querying and manipulation of asynchronous observables, such as
/// retrieving the last element of a sequence. These extensions are designed to support asynchronous and reactive
/// programming patterns.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously returns the last element in the sequence that satisfies the specified predicate.
        /// </summary>
        /// <param name="predicate">A function to test each element for a condition. The method returns the last element for which this
        /// predicate returns <see langword="true"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the last element that matches
        /// the predicate.</returns>
        public async ValueTask<T> LastAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
        {
            var observer = new LastAsyncObserver<T>(predicate, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }

        /// <summary>
        /// Asynchronously returns the last element of the sequence.
        /// </summary>
        /// <remarks>If the sequence is empty, the behavior depends on the implementation and may result
        /// in an exception being thrown. The operation is performed asynchronously and may not complete
        /// immediately.</remarks>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the last element of the
        /// sequence.</returns>
        public async ValueTask<T> LastAsync(CancellationToken cancellationToken = default)
        {
            var observer = new LastAsyncObserver<T>(null, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }
    }

    private sealed class LastAsyncObserver<T>(Func<T, bool>? predicate, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, T>(cancellationToken)
    {
        private bool _hasValue;
        private T? _last;

        protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            if (predicate is null || predicate(value))
            {
                _hasValue = true;
                _last = value;
            }

            return default;
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
        {
            return TrySetException(error);
        }

        protected override ValueTask OnCompletedAsyncCore(Result result)
        {
            if (!result.IsSuccess)
            {
                return TrySetException(result.Exception);
            }

            if (_hasValue)
            {
                return TrySetCompleted(_last!);
            }

            var message = predicate is null ? "Sequence contains no elements." : "Sequence contains no matching elements.";
            return TrySetException(new InvalidOperationException(message));
        }
    }
}
