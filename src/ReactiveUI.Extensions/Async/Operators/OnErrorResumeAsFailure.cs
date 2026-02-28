// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class contains static methods that extend the functionality of asynchronous
/// observables, enabling advanced composition and error handling scenarios. These methods are intended to be used with
/// types that implement asynchronous push-based notification patterns.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Creates a new observable sequence that converts any error encountered in the source sequence into a failure
    /// result, allowing the sequence to complete without propagating exceptions.
    /// </summary>
    /// <remarks>This method enables error handling by transforming exceptions into failure notifications
    /// within the sequence, rather than terminating the sequence with an error. Consumers can inspect the result to
    /// determine whether an operation succeeded or failed.</remarks>
    /// <typeparam name="T">The type of the elements in the observable sequence.</typeparam>
    /// <param name="this">The source asynchronous observable sequence to monitor for errors.</param>
    /// <returns>An observable sequence that emits the same elements as the source, but represents errors as failure results
    /// instead of throwing exceptions.</returns>
    public static IObservableAsync<T> OnErrorResumeAsFailure<T>(this IObservableAsync<T> @this)
    {
        if (@this is null)
        {
            throw new ArgumentNullException(nameof(@this), "Cannot create an OnErrorResumeAsFailure observable from a null source.");
        }

        return new OnErrorResumeAsFailureObservable<T>(@this);
    }

    private sealed class OnErrorResumeAsFailureObservable<T>(IObservableAsync<T> source) : ObservableAsync<T>
    {
        protected override ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken) =>
            source.SubscribeAsync(new OnErrorResumeAsFailureObserver(observer), cancellationToken);

        private sealed class OnErrorResumeAsFailureObserver(IObserverAsync<T> observer) : ObserverAsync<T>
        {
            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) =>
                observer.OnNextAsync(value, cancellationToken);

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
                observer.OnCompletedAsync(Result.Failure(error));

            protected override ValueTask OnCompletedAsyncCore(Result result) =>
                observer.OnCompletedAsync(result);
        }
    }
}
