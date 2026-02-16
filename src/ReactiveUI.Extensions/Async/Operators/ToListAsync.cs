// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class contains static methods that extend the functionality of asynchronous
/// observables, enabling operations such as materializing the sequence into a list asynchronously. These methods are
/// intended to simplify common tasks when consuming asynchronous observable streams.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously collects all elements from the source sequence into a list.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of all elements in the
        /// source sequence, in the order they were received.</returns>
        public async ValueTask<List<T>> ToListAsync(CancellationToken cancellationToken = default)
        {
            var observer = new ToListAsyncObserver<T>(cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }
    }

    private sealed class ToListAsyncObserver<T>(CancellationToken cancellationToken) : TaskObserverAsyncBase<T, List<T>>(cancellationToken)
    {
        private readonly List<T> _items = new();

        protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            _items.Add(value);
            return default;
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result) =>
            !result.IsSuccess ? TrySetException(result.Exception) : TrySetCompleted(_items);
    }
}
