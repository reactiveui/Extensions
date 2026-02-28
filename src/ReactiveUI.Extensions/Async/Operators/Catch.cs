// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for composing and handling asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class enable advanced error handling and composition scenarios for asynchronous
/// observables. These extensions are intended to be used with the ObservableAsync{T} type to facilitate robust,
/// composable, and resilient asynchronous data streams.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> source)
    {
        /// <summary>
        /// Creates a new observable sequence that continues with a handler-provided sequence when an exception occurs
        /// in the source sequence.
        /// </summary>
        /// <remarks>Use this method to recover from errors in the source sequence by switching to an
        /// alternative observable sequence. The handler function is called with the exception, allowing custom error
        /// recovery logic. If the handler itself throws an exception, the resulting sequence completes with that
        /// exception.</remarks>
        /// <param name="handler">A function that receives the exception thrown by the source sequence and returns an alternative observable
        /// sequence to continue with.</param>
        /// <param name="onErrorResume">An optional asynchronous callback invoked when an error occurs. If not specified, the observer's default
        /// error handler is used.</param>
        /// <returns>An observable sequence that emits items from the source sequence, or from the handler-provided sequence if
        /// an exception is encountered.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the source sequence or <paramref name="handler"/> is null.</exception>
        public IObservableAsync<T> Catch(Func<Exception, IObservableAsync<T>> handler, Func<Exception, CancellationToken, ValueTask>? onErrorResume = null)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(handler, nameof(handler));

            return Create<T>(async (observer, cancellationToken) =>
            {
                var onErrorResumeAsync = onErrorResume ?? observer.OnErrorResumeAsync;
                SingleAssignmentDisposableAsync handlerDisposable = new();
                IAsyncDisposable sourceDisposable = await source.SubscribeAsync(
                    async (value, ct) => await observer.OnNextAsync(value, ct),
                    onErrorResumeAsync,
                    async result =>
                    {
                        if (result.IsSuccess)
                        {
                            await observer.OnCompletedAsync(result);
                            return;
                        }

                        try
                        {
                            var handlerObservable = handler(result.Exception);
                            var handlerSubscription = await handlerObservable.SubscribeAsync(observer.Wrap(), cancellationToken);
                            await handlerDisposable.SetDisposableAsync(handlerSubscription);
                        }
                        catch (Exception e)
                        {
                            await observer.OnCompletedAsync(Result.Failure(e));
                        }
                    },
                    cancellationToken);
                return DisposableAsync.Create(async () =>
                {
                    await sourceDisposable.DisposeAsync();
                    await handlerDisposable.DisposeAsync();
                });
            });
        }

        /// <summary>
        /// Continues the observable sequence with an alternative sequence provided by the specified handler when an
        /// error occurs, and ignores the error after invoking the handler.
        /// </summary>
        /// <remarks>If an error occurs and the handler is invoked, the error is also reported to the
        /// global unhandled exception handler before being ignored. This method allows the sequence to continue without
        /// propagating the error to subscribers.</remarks>
        /// <param name="handler">A function that receives the exception and returns an alternative observable sequence to resume with after
        /// an error occurs.</param>
        /// <returns>An observable sequence that resumes with the sequence returned by the handler when an error is encountered,
        /// and ignores the error after handling.</returns>
        public IObservableAsync<T> CatchAndIgnoreErrorResume(Func<Exception, IObservableAsync<T>> handler) => source.Catch(handler, static (error, _) =>
        {
            UnhandledExceptionHandler.OnUnhandledException(error);
            return default;
        });
    }
}
