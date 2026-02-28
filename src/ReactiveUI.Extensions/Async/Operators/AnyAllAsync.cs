// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides a set of extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class enable querying and evaluating asynchronous observable sequences, such as
/// determining whether any or all elements satisfy a condition. These methods are designed to be used with types that
/// implement asynchronous observation patterns.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously determines whether any element in the sequence satisfies the specified predicate.
        /// </summary>
        /// <param name="predicate">A function to test each element for a condition. If null, the method checks whether the sequence contains
        /// any elements.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if any
        /// element satisfies the predicate or, if the predicate is null, if the sequence contains any elements;
        /// otherwise, <see langword="false"/>.</returns>
        public async ValueTask<bool> AnyAsync(Func<T, bool>? predicate, CancellationToken cancellationToken = default)
        {
            var observer = new AnyAsyncObserver<T>(predicate, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }

        /// <summary>
        /// Asynchronously determines whether the source contains any elements.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if the
        /// source contains any elements; otherwise, <see langword="false"/>.</returns>
        public ValueTask<bool> AnyAsync(CancellationToken cancellationToken = default)
            => @this.AnyAsync(null, cancellationToken);

        /// <summary>
        /// Asynchronously determines whether all elements in the sequence satisfy the specified predicate.
        /// </summary>
        /// <param name="predicate">A function to test each element for a condition. The method evaluates this predicate for each element in the
        /// sequence.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if every
        /// element of the sequence passes the test in the specified predicate, or if the sequence is empty; otherwise,
        /// <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is <see langword="null"/>.</exception>
        public async ValueTask<bool> AllAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(predicate);
#else
            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
#endif

            var observer = new AllAsyncObserver<T>(predicate, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }
    }

    private sealed class AnyAsyncObserver<T>(Func<T, bool>? predicate, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, bool>(cancellationToken)
    {
        protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            if (predicate is null || predicate(value))
            {
                await TrySetCompleted(true);
            }
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result) =>
            !result.IsSuccess ? TrySetException(result.Exception) : TrySetCompleted(false);
    }

    private sealed class AllAsyncObserver<T>(Func<T, bool> predicate, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, bool>(cancellationToken)
    {
        private readonly Func<T, bool> _predicate = predicate;

        protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            if (!_predicate(value))
            {
                await TrySetCompleted(false);
            }
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result) =>
            !result.IsSuccess ? TrySetException(result.Exception) : TrySetCompleted(true);
    }
}
