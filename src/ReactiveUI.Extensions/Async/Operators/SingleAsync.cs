// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for asynchronous observable sequences, enabling operations such as retrieving a single
/// element that matches a specified condition.
/// </summary>
/// <remarks>The methods in this class support querying and consuming asynchronous observables in a manner similar
/// to LINQ, but adapted for asynchronous and reactive scenarios. These extensions are intended for use with types
/// implementing the ObservableAsync pattern, allowing developers to perform operations such as filtering and retrieving
/// elements in an asynchronous context.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously returns the single element of a sequence that satisfies a specified condition, or throws an
        /// exception if more than one such element exists.
        /// </summary>
        /// <remarks>If no element satisfies the condition, or if more than one element satisfies the
        /// condition, an exception is thrown. Use this method when exactly one element is expected to match the
        /// predicate.</remarks>
        /// <param name="predicate">A function to test each element for a condition. The method returns the element for which this predicate
        /// returns <see langword="true"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the single element that matches
        /// the predicate.</returns>
        public async ValueTask<T> SingleAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
        {
            var observer = new SingleAsyncObserver<T>(predicate, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }

        /// <summary>
        /// Asynchronously returns the single element of the sequence, and throws an exception if the sequence does not
        /// contain exactly one element.
        /// </summary>
        /// <remarks>Use this method when you expect the sequence to contain exactly one element. If the
        /// sequence is empty or contains more than one element, an exception is thrown.</remarks>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the single element of the
        /// sequence.</returns>
        public async ValueTask<T> SingleAsync(CancellationToken cancellationToken = default)
        {
            var observer = new SingleAsyncObserver<T>(null, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }
    }

    private sealed class SingleAsyncObserver<T>(Func<T, bool>? predicate, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, T>(cancellationToken)
    {
        private bool _hasValue;
        private T? _value;

        protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            if (predicate is null || predicate(value))
            {
                if (_hasValue)
                {
                    var message = predicate is null ? "Sequence contains more than one element." : "Sequence contains more than one matching element.";
                    await TrySetException(new InvalidOperationException(message));
                }
                else
                {
                    _hasValue = true;
                    _value = value;
                }
            }
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result)
        {
            if (!result.IsSuccess)
            {
                return TrySetException(result.Exception);
            }

            if (!_hasValue)
            {
                var message = predicate is null ? "Sequence contains no elements." : "Sequence contains no matching elements.";
                return TrySetException(new InvalidOperationException(message));
            }

            return TrySetCompleted(_value!);
        }
    }
}
