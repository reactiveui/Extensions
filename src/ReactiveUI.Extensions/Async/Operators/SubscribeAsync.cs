// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for subscribing to asynchronous observable sequences using various delegate-based
/// overloads.
/// </summary>
/// <remarks>The methods in this class enable consumers to subscribe to an asynchronous observable sequence by
/// specifying delegate handlers for item notifications, error handling, and completion. These overloads offer both
/// synchronous and asynchronous delegate options, allowing for flexible integration with different programming models.
/// All subscriptions return an <see cref="IAsyncDisposable"/> that should be disposed to terminate the subscription and
/// release resources. These methods are intended to simplify the process of observing asynchronous streams without
/// requiring explicit implementation of observer interfaces.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> source)
    {
        /// <summary>
        /// Subscribes to the asynchronous data source and invokes the specified callbacks for each item, error, or
        /// completion notification.
        /// </summary>
        /// <remarks>The returned <see cref="IAsyncDisposable"/> should be disposed when the subscription
        /// is no longer needed to release resources and stop receiving notifications. Callbacks may be invoked
        /// concurrently; implement thread safety in the provided delegates if required.</remarks>
        /// <param name="onNextAsync">A delegate that is invoked asynchronously for each item received from the data source. The delegate receives
        /// the item and a cancellation token.</param>
        /// <param name="onErrorResumeAsync">An optional delegate that is invoked asynchronously if an error occurs during data processing. The delegate
        /// receives the exception and a cancellation token. If null, errors are not handled by the subscriber.</param>
        /// <param name="onCompletedAsync">An optional delegate that is invoked asynchronously when the data source completes successfully. The
        /// delegate receives a result indicating the completion status. If null, no action is taken on completion.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the subscription and any in-progress callbacks.</param>
        /// <returns>A value task that represents the asynchronous operation. The result is an <see cref="IAsyncDisposable"/>
        /// that can be disposed to unsubscribe from the data source.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the underlying data source is null.</exception>
        public ValueTask<IAsyncDisposable> SubscribeAsync(
            Func<T, CancellationToken, ValueTask> onNextAsync,
            Func<Exception, CancellationToken, ValueTask>? onErrorResumeAsync,
            Func<Result, ValueTask>? onCompletedAsync = null,
            CancellationToken cancellationToken = default)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (onNextAsync is null)
            {
                throw new ArgumentNullException(nameof(onNextAsync));
            }

            var observer = new AnonymousObserverAsync<T>(onNextAsync, onErrorResumeAsync, onCompletedAsync);
            return source.SubscribeAsync(observer, cancellationToken);
        }

        /// <summary>
        /// Subscribes to the observable sequence and invokes the specified action for each element received.
        /// </summary>
        /// <param name="onNext">An action to invoke for each element in the sequence. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the subscription operation.</param>
        /// <returns>A value task that represents the asynchronous subscription operation. The result contains an <see
        /// cref="IAsyncDisposable"/> that can be disposed to unsubscribe from the sequence.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="onNext"/> is null.</exception>
        public ValueTask<IAsyncDisposable> SubscribeAsync(Action<T> onNext, CancellationToken cancellationToken = default)
        {
            if (onNext is null)
            {
                throw new ArgumentNullException(nameof(onNext));
            }

            var observer = new AnonymousObserverAsync<T>((x, _) =>
            {
                onNext(x);
                return default;
            });

            return source.SubscribeAsync(observer, cancellationToken);
        }

        /// <summary>
        /// Subscribes to the observable sequence asynchronously, invoking the specified callbacks for each element,
        /// error, or completion notification.
        /// </summary>
        /// <remarks>The returned <see cref="IAsyncDisposable"/> should be disposed when the subscription
        /// is no longer needed to release resources and stop receiving notifications. This method enables asynchronous,
        /// push-based event handling for observable sequences.</remarks>
        /// <param name="onNext">An action to invoke for each element in the sequence. Cannot be null.</param>
        /// <param name="onErrorResume">An optional action to invoke if an error occurs during the sequence. If null, errors are not handled by the
        /// subscriber.</param>
        /// <param name="onCompleted">An optional action to invoke when the sequence completes. If null, completion is not handled by the
        /// subscriber.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the subscription.</param>
        /// <returns>A value task that represents the asynchronous subscription operation. The result is an <see
        /// cref="IAsyncDisposable"/> that can be disposed to unsubscribe from the sequence.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="onNext"/> is null, or if the underlying source is null.</exception>
        public ValueTask<IAsyncDisposable> SubscribeAsync(
            Action<T> onNext,
            Action<Exception>? onErrorResume = null,
            Action<Result>? onCompleted = null,
            CancellationToken cancellationToken = default)
        {
            if (onNext is null)
            {
                throw new ArgumentNullException(nameof(onNext));
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            static ValueTask OnErrorResumeAsync(Exception e, Action<Exception>? onErrorResume)
            {
                if (onErrorResume is null)
                {
                    return default;
                }

                onErrorResume(e);
                return default;
            }

            static ValueTask OnCompletedAsync(Result x, Action<Result>? onCompleted)
            {
                if (onCompleted is null)
                {
                    return default;
                }

                onCompleted(x);
                return ValueTask.CompletedTask;
            }

            var observer = new AnonymousObserverAsync<T>(
                (x, _) =>
                {
                    onNext(x);
                    return default;
                },
                onErrorResume is null ? null : (e, _) => OnErrorResumeAsync(e, onErrorResume),
                onCompleted is null ? null : x => OnCompletedAsync(x, onCompleted));

            return source.SubscribeAsync(observer, cancellationToken);
        }

        /// <summary>
        /// Subscribes to the source without handling any items asynchronously.
        /// </summary>
        /// <returns>A value task that represents the asynchronous subscription operation. The result is an <see
        /// cref="IAsyncDisposable"/> that can be disposed to unsubscribe.</returns>
        public ValueTask<IAsyncDisposable> SubscribeAsync() =>
            source.SubscribeAsync(static (_, _) => default, CancellationToken.None);

        /// <summary>
        /// Subscribes asynchronously to receive notifications for each item published by the source.
        /// </summary>
        /// <param name="onNextAsync">A delegate that is invoked asynchronously for each item published. The delegate receives the item and a
        /// cancellation token, and returns a ValueTask that completes when processing is finished. Cannot be null.</param>
        /// <returns>A ValueTask that represents the asynchronous subscription operation. The result contains an IAsyncDisposable
        /// that can be disposed to unsubscribe from the source.</returns>
        public ValueTask<IAsyncDisposable> SubscribeAsync(Func<T, CancellationToken, ValueTask> onNextAsync) =>
            source.SubscribeAsync(onNextAsync, CancellationToken.None);

        /// <summary>
        /// Subscribes asynchronously to receive notifications for each item in the sequence using the specified
        /// asynchronous callback.
        /// </summary>
        /// <param name="onNextAsync">A function to invoke asynchronously for each item in the sequence. The function receives the item and a
        /// cancellation token, and returns a ValueTask that completes when processing is finished.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the subscription operation.</param>
        /// <returns>A ValueTask that represents the asynchronous subscription operation. The result is an IAsyncDisposable that
        /// can be disposed to unsubscribe from the sequence.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the underlying source is null.</exception>
        public ValueTask<IAsyncDisposable> SubscribeAsync(Func<T, CancellationToken, ValueTask> onNextAsync, CancellationToken cancellationToken)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (onNextAsync is null)
            {
                throw new ArgumentNullException(nameof(onNextAsync), "Cannot subscribe with a null action for each element in the sequence.");
            }

            var observer = new AnonymousObserverAsync<T>(onNextAsync);
            return source.SubscribeAsync(observer, cancellationToken);
        }
    }
}
