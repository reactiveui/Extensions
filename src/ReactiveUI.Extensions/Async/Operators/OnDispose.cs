// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for composing and managing asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class offers utility methods for working with asynchronous observables, enabling
/// additional behaviors such as resource cleanup or side-effect handling when subscriptions are disposed. These methods
/// are intended to simplify the creation and management of custom observable pipelines in asynchronous programming
/// scenarios.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Registers a callback to be invoked asynchronously when the observable sequence is disposed.
        /// </summary>
        /// <remarks>Use this method to perform custom asynchronous cleanup or resource release logic when
        /// the observable sequence is disposed. The callback is invoked when the subscription is disposed, either
        /// explicitly or when the observer completes or errors.</remarks>
        /// <param name="onDispose">A function that returns a ValueTask representing the asynchronous operation to execute upon disposal of the
        /// observable sequence. Cannot be null.</param>
        /// <returns>An ObservableAsync{T} that invokes the specified asynchronous callback when disposed.</returns>
        public IObservableAsync<T> OnDispose(Func<ValueTask> onDispose) => Create<T>((observer, token) =>
                                                                                   {
                                                                                       var newObserver = new OnDisposeObserver<T>(observer, onDispose);
                                                                                       return @this.SubscribeAsync(newObserver, token);
                                                                                   });

        /// <summary>
        /// Registers an action to be invoked when the observable sequence is disposed.
        /// </summary>
        /// <remarks>Use this method to perform cleanup or resource release logic when a subscription to
        /// the observable is disposed. The specified action is called synchronously during disposal. If multiple
        /// actions are registered through chained calls, each will be invoked in the order registered.</remarks>
        /// <param name="onDispose">The action to execute when the subscription is disposed. Cannot be null.</param>
        /// <returns>An observable sequence that invokes the specified action upon disposal of the subscription.</returns>
        public IObservableAsync<T> OnDispose(Action onDispose) => Create<T>((observer, token) =>
                                                                          {
                                                                              var newObserver = new OnDisposeObserverSync<T>(observer, onDispose);
                                                                              return @this.SubscribeAsync(newObserver, token);
                                                                          });
    }

    private sealed class OnDisposeObserverSync<T>(IObserverAsync<T> observer, Action finallySync) : ObserverAsync<T>
    {
        protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            => observer.OnNextAsync(value, cancellationToken);

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            => observer.OnErrorResumeAsync(error, cancellationToken);

        protected override ValueTask OnCompletedAsyncCore(Result result)
            => observer.OnCompletedAsync(result);

        protected override async ValueTask DisposeAsyncCore()
        {
            try
            {
                finallySync();
            }
            finally
            {
                await base.DisposeAsyncCore();
            }
        }
    }

    private class OnDisposeObserver<T>(IObserverAsync<T> observer, Func<ValueTask> finallyAsync) : ObserverAsync<T>
    {
        protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            => observer.OnNextAsync(value, cancellationToken);

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            => observer.OnErrorResumeAsync(error, cancellationToken);

        protected override ValueTask OnCompletedAsyncCore(Result result)
            => observer.OnCompletedAsync(result);

        protected override async ValueTask DisposeAsyncCore()
        {
            try
            {
                await finallyAsync();
            }
            finally
            {
                await base.DisposeAsyncCore();
            }
        }
    }
}
