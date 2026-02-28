// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides a set of extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class enable querying and manipulation of asynchronous observables, such as
/// determining whether a sequence contains a specified element. These extensions are designed to integrate with the
/// ObservableAsync{T} pattern for asynchronous, push-based data streams.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously determines whether the sequence contains a specified value using the given equality comparer.
        /// </summary>
        /// <param name="value">The value to locate in the sequence.</param>
        /// <param name="comparer">The equality comparer to use for comparing values, or null to use the default equality comparer for the
        /// type.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if the
        /// value is found in the sequence; otherwise, <see langword="false"/>.</returns>
        public async ValueTask<bool> ContainsAsync(T value, IEqualityComparer<T>? comparer, CancellationToken cancellationToken = default)
        {
            var cmp = comparer ?? EqualityComparer<T>.Default;
            var observer = new ContainsAsyncObserver<T>(value, cmp, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }

        /// <summary>
        /// Asynchronously determines whether the collection contains a specified value.
        /// </summary>
        /// <param name="value">The value to locate in the collection.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if the
        /// value is found in the collection; otherwise, <see langword="false"/>.</returns>
        public ValueTask<bool> ContainsAsync(T value, CancellationToken cancellationToken = default)
            => @this.ContainsAsync(value, null, cancellationToken);
    }

    private sealed class ContainsAsyncObserver<T>(T value, IEqualityComparer<T> comparer, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, bool>(cancellationToken)
    {
        protected override async ValueTask OnNextAsyncCore(T value1, CancellationToken cancellationToken)
        {
            if (comparer.Equals(value, value1))
            {
                await TrySetCompleted(true);
            }
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result)
            => result.IsSuccess ? TrySetCompleted(false) : TrySetException(result.Exception);
    }
}
