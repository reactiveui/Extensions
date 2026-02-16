// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
public static partial class ObservableAsync
{
    /// <summary>
    /// Asynchronously waits for the observable sequence to complete without retrieving any values.
    /// </summary>
    /// <remarks>This method subscribes to the observable sequence and completes when the sequence signals
    /// completion or when the operation is canceled. Any values produced by the sequence are ignored.</remarks>
    /// <typeparam name="T">The type of the elements in the observable sequence.</typeparam>
    /// <param name="this">The observable sequence to wait for completion.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the wait operation.</param>
    /// <returns>A ValueTask that represents the asynchronous wait operation.</returns>
    public static async ValueTask WaitCompletionAsync<T>(this IObservableAsync<T> @this, CancellationToken cancellationToken = default)
    {
        var observer = new WaitCompletionAsyncObserver<T>(cancellationToken);
        _ = await @this.SubscribeAsync(observer, cancellationToken);
        await observer.WaitValueAsync();
    }

    private sealed class WaitCompletionAsyncObserver<T>(CancellationToken cancellationToken) : TaskObserverAsyncBase<T, object?>(cancellationToken)
    {
        protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => default;

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result) =>
            !result.IsSuccess ? TrySetException(result.Exception) : TrySetCompleted(null);
    }
}
