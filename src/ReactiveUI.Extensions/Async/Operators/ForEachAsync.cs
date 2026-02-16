// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class contains utility methods that enable consumers to process items emitted by
/// asynchronous observables in a convenient and idiomatic way. These methods are designed to simplify common patterns
/// when interacting with IAsyncObservable or similar asynchronous push-based data sources.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously invokes the specified action for each element in the sequence as elements are received.
        /// </summary>
        /// <remarks>If the sequence completes or is canceled, the method returns when all in-flight
        /// actions have finished. Exceptions thrown by the action or during enumeration will propagate to the returned
        /// task.</remarks>
        /// <param name="onNextAsync">A function to invoke for each element in the sequence. The function receives the element and a cancellation
        /// token, and returns a ValueTask that completes when processing is finished.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the sequence to complete. The operation is canceled if the token is
        /// signaled.</param>
        /// <returns>A ValueTask that represents the asynchronous operation. The task completes when all elements have been
        /// processed or the operation is canceled.</returns>
        public async ValueTask ForEachAsync(Func<T, CancellationToken, ValueTask> onNextAsync, CancellationToken cancellationToken = default)
        {
            var observer = new ForEachObserver<T>(onNextAsync, cancellationToken);
            await @this.SubscribeAsync(observer, cancellationToken);
            await observer.WaitValueAsync();
        }

        /// <summary>
        /// Asynchronously invokes the specified action for each element in the sequence as elements are received.
        /// </summary>
        /// <param name="onNext">The action to invoke for each element in the sequence. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the iteration.</param>
        /// <returns>A task that represents the asynchronous iteration operation. The task completes when the sequence has been
        /// fully processed or the operation is canceled.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="onNext"/> is null.</exception>
        public async ValueTask ForEachAsync(Action<T> onNext, CancellationToken cancellationToken = default)
        {
            if (onNext is null)
            {
                throw new ArgumentNullException(nameof(onNext));
            }

            var observer = new ForEachObserverSync<T>(onNext, cancellationToken);
            await @this.SubscribeAsync(observer, cancellationToken);
            await observer.WaitValueAsync();
        }
    }

    private sealed class ForEachObserver<T>(Func<T, CancellationToken, ValueTask> onNextAsync, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, bool>(cancellationToken)
    {
        protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => onNextAsync(value, cancellationToken);

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result) => result.IsSuccess ? TrySetCompleted(true) : TrySetException(result.Exception);
    }

    private sealed class ForEachObserverSync<T>(Action<T> onNext, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, bool>(cancellationToken)
    {
        private readonly Action<T> _onNext = onNext;

        protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            _onNext(value);
            return default;
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result) =>
            result.IsSuccess ? TrySetCompleted(true) : TrySetException(result.Exception);
    }
}
