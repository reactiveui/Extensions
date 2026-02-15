// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class enable the addition of side effects, such as logging or resource
/// management, to asynchronous observable sequences without modifying their elements or control flow. These methods are
/// intended to be used as part of a fluent query or processing pipeline for asynchronous observables.</remarks>
public static partial class ObservableAsync
{
    extension<T>(ObservableAsync<T> @this)
    {
        /// <summary>
        /// Invokes the specified asynchronous actions for each element, error, or completion notification in the
        /// observable sequence without modifying the sequence.
        /// </summary>
        /// <remarks>Use this method to perform side effects such as logging, resource cleanup, or
        /// notification in response to elements, errors, or completion events in the sequence. The callbacks are
        /// invoked asynchronously and do not alter the elements or flow of the sequence.</remarks>
        /// <param name="onNext">An asynchronous callback to invoke for each element in the sequence. Receives the element and a cancellation
        /// token. If null, no action is taken on elements.</param>
        /// <param name="onErrorResume">An optional asynchronous callback to invoke if an error occurs in the sequence. Receives the exception and a
        /// cancellation token. If null, errors are not handled by this observer.</param>
        /// <param name="onCompleted">An optional asynchronous callback to invoke when the sequence completes. Receives the result of the
        /// sequence. If null, no action is taken on completion.</param>
        /// <returns>An observable sequence that is identical to the source sequence but invokes the specified callbacks for side
        /// effects.</returns>
        public ObservableAsync<T> Do(
            Func<T, CancellationToken, ValueTask>? onNext,
            Func<Exception, CancellationToken, ValueTask>? onErrorResume = null,
            Func<Result, ValueTask>? onCompleted = null) => new DoAsyncObservable<T>(@this, onNext, onErrorResume, onCompleted);

        /// <summary>
        /// Invokes the specified actions in response to notifications from the observable sequence without modifying
        /// the sequence itself.
        /// </summary>
        /// <remarks>Use this method to perform side effects such as logging, monitoring, or debugging in
        /// response to sequence events without altering the sequence's behavior. The returned observable passes through
        /// all elements and notifications unchanged.</remarks>
        /// <param name="onNext">An action to invoke for each element in the sequence as it is emitted. If null, no action is taken on
        /// element emission.</param>
        /// <param name="onErrorResume">An action to invoke if an error occurs in the sequence. Receives the exception that caused the error. If
        /// null, no action is taken on error.</param>
        /// <param name="onCompleted">An action to invoke when the sequence completes, receiving the final result. If null, no action is taken on
        /// completion.</param>
        /// <returns>An observable sequence that is identical to the source sequence but invokes the specified actions for each
        /// notification.</returns>
        public ObservableAsync<T> Do(
            Action<T>? onNext = null,
            Action<Exception>? onErrorResume = null,
            Action<Result>? onCompleted = null) => new DoSyncObservable<T>(@this, onNext, onErrorResume, onCompleted);
    }

    private sealed class DoAsyncObservable<T>(ObservableAsync<T> source,
                                      Func<T, CancellationToken, ValueTask>? onNext,
                                      Func<Exception, CancellationToken, ValueTask>? onErrorResume,
                                      Func<Result, ValueTask>? onCompleted) : ObservableAsync<T>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(ObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var doObserver = new DoAsyncObserver(observer, onNext, onErrorResume, onCompleted);
            return await source.SubscribeAsync(doObserver, cancellationToken);
        }

        private sealed class DoAsyncObserver(ObserverAsync<T> observer,
                                      Func<T, CancellationToken, ValueTask>? onNext,
                                      Func<Exception, CancellationToken, ValueTask>? onErrorResume,
                                      Func<Result, ValueTask>? onCompleted) : ObserverAsync<T>
        {
            protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                if (onNext is not null)
                {
                    await onNext(value, cancellationToken);
                }

                await observer.OnNextAsync(value, cancellationToken);
            }

            protected override async ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            {
                if (onErrorResume is not null)
                {
                    await onErrorResume(error, cancellationToken);
                }

                await observer.OnErrorResumeAsync(error, cancellationToken);
            }

            protected override async ValueTask OnCompletedAsyncCore(Result result)
            {
                if (onCompleted is not null)
                {
                    await onCompleted(result);
                }

                await observer.OnCompletedAsync(result);
            }
        }
    }

    private sealed class DoSyncObservable<T>(ObservableAsync<T> source,
                                     Action<T>? onNext,
                                     Action<Exception>? onErrorResume,
                                     Action<Result>? onCompleted) : ObservableAsync<T>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(ObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var doObserver = new DoSyncObserver(observer, onNext, onErrorResume, onCompleted);
            return await source.SubscribeAsync(doObserver, cancellationToken);
        }

        private sealed class DoSyncObserver(ObserverAsync<T> observer,
                                    Action<T>? onNext,
                                    Action<Exception>? onErrorResume,
                                    Action<Result>? onCompleted) : ObserverAsync<T>
        {
            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                onNext?.Invoke(value);
                return observer.OnNextAsync(value, cancellationToken);
            }

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            {
                onErrorResume?.Invoke(error);
                return observer.OnErrorResumeAsync(error, cancellationToken);
            }

            protected override ValueTask OnCompletedAsyncCore(Result result)
            {
                onCompleted?.Invoke(result);
                return observer.OnCompletedAsync(result);
            }
        }
    }
}
