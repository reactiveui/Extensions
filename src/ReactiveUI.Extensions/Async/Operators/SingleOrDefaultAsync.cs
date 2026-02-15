// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides a set of extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class contains static extension methods that operate on instances of
/// ObservableAsync{T}. These methods enable querying and manipulation of asynchronous observable sequences in a manner
/// similar to LINQ, supporting scenarios such as retrieving single elements or default values asynchronously.</remarks>
public static partial class ObservableAsync
{
    extension<T>(ObservableAsync<T> @this)
    {
        /// <summary>
        /// Asynchronously returns the only element of a sequence that satisfies a specified condition, or a default
        /// value if no such element exists; this operation throws if more than one matching element is found.
        /// </summary>
        /// <remarks>If more than one element satisfies the condition, an exception is thrown. If no
        /// elements satisfy the condition, the specified default value is returned. The operation observes the provided
        /// cancellation token.</remarks>
        /// <param name="predicate">A function to test each element for a condition. The method returns the element for which this predicate
        /// returns <see langword="true"/>.</param>
        /// <param name="defaultValue">The value to return if no element in the sequence satisfies the condition specified by <paramref
        /// name="predicate"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A value task that represents the asynchronous operation. The result contains the single element that matches
        /// the predicate, the specified default value if no such element is found, or throws an exception if more than
        /// one matching element exists.</returns>
        public async ValueTask<T?> SingleOrDefaultAsync(Func<T, bool> predicate, T? defaultValue, CancellationToken cancellationToken = default)
        {
            var observer = new SingleOrDefaultObserver<T>(predicate, defaultValue, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }

        /// <summary>
        /// Asynchronously returns the only element of a sequence, or a default value if the sequence is empty; this
        /// operation throws an exception if more than one element is found.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A value task that represents the asynchronous operation. The task result contains the single element of the
        /// sequence, or the default value of <typeparamref name="T"/> if the sequence is empty.</returns>
        public ValueTask<T?> SingleOrDefaultAsync(CancellationToken cancellationToken = default) => @this.SingleOrDefaultAsync(default, cancellationToken);

        /// <summary>
        /// Asynchronously returns the single element of the sequence, or a specified default value if the sequence is
        /// empty. Throws an exception if the sequence contains more than one element.
        /// </summary>
        /// <remarks>Use this method when you expect the sequence to contain zero or one element. If the
        /// sequence contains more than one element, an exception is thrown. If the sequence is empty, the specified
        /// default value is returned.</remarks>
        /// <param name="defaultValue">The value to return if the sequence contains no elements.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the single element of the
        /// sequence, the specified default value if the sequence is empty, or throws if more than one element is
        /// present.</returns>
        public async ValueTask<T?> SingleOrDefaultAsync(T? defaultValue, CancellationToken cancellationToken = default)
        {
            var observer = new SingleOrDefaultObserver<T>(null, defaultValue, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }
    }

    private sealed class SingleOrDefaultObserver<T>(Func<T, bool>? predicate, T? defaultValue, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, T>(cancellationToken)
    {
        private bool _hasValue;
        private T? _value = defaultValue;

        protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            if (predicate is null || predicate(value))
            {
                if (_hasValue)
                {
                    var message = predicate is null ? "Sequence contains more than one element." : "Sequence contains more than one matching element.";
                    await TrySetException(new InvalidOperationException(message));
                }
                else
                {
                    _hasValue = true;
                    _value = value;
                }
            }
        }

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result)
        {
            if (!result.IsSuccess)
            {
                return TrySetException(result.Exception);
            }

            return TrySetCompleted(_value!);
        }
    }
}
