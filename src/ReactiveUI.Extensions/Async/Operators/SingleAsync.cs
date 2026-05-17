// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for asynchronous observable sequences, enabling operations such as retrieving a single
/// element that matches a specified condition.
/// </summary>
/// <remarks>The methods in this class support querying and consuming asynchronous observables in a manner similar
/// to LINQ, but adapted for asynchronous and reactive scenarios. These extensions are intended for use with types
/// implementing the ObservableAsync pattern, allowing developers to perform operations such as filtering and retrieving
/// elements in an asynchronous context.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Asynchronously returns the single element of a sequence that satisfies a specified condition, or throws an
    /// exception if more than one such element exists.
    /// </summary>
    /// <remarks>If no element satisfies the condition, or if more than one element satisfies the
    /// condition, an exception is thrown. Use this method when exactly one element is expected to match the
    /// predicate.</remarks>
    /// <typeparam name="T">The type of elements in the source sequence.</typeparam>
    /// <param name="this">The source observable sequence.</param>
    /// <param name="predicate">A function to test each element for a condition. The method returns the element for which this predicate
    /// returns <see langword="true"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the single element that matches
    /// the predicate.</returns>
    public static ValueTask<T> SingleAsync<T>(this IObservableAsync<T> @this, Func<T, bool> predicate)
        => @this.SingleAsync(predicate, CancellationToken.None);

    /// <summary>
    /// Asynchronously returns the single element of a sequence that satisfies a specified condition, or throws an
    /// exception if more than one such element exists.
    /// </summary>
    /// <remarks>If no element satisfies the condition, or if more than one element satisfies the
    /// condition, an exception is thrown. Use this method when exactly one element is expected to match the
    /// predicate.</remarks>
    /// <typeparam name="T">The type of elements in the source sequence.</typeparam>
    /// <param name="this">The source observable sequence.</param>
    /// <param name="predicate">A function to test each element for a condition. The method returns the element for which this predicate
    /// returns <see langword="true"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the single element that matches
    /// the predicate.</returns>
    public static async ValueTask<T> SingleAsync<T>(this IObservableAsync<T> @this, Func<T, bool> predicate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var observer = new SingleAsyncObserver<T>(predicate, cancellationToken);
        await using var subscription = await @this.SubscribeAsync(observer, cancellationToken).ConfigureAwait(false);
        return await observer.WaitValueAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously returns the single element of the sequence, and throws an exception if the sequence does not
    /// contain exactly one element.
    /// </summary>
    /// <remarks>Use this method when you expect the sequence to contain exactly one element. If the
    /// sequence is empty or contains more than one element, an exception is thrown.</remarks>
    /// <typeparam name="T">The type of elements in the source sequence.</typeparam>
    /// <param name="this">The source observable sequence.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the single element of the
    /// sequence.</returns>
    public static ValueTask<T> SingleAsync<T>(this IObservableAsync<T> @this)
        => @this.SingleAsync(CancellationToken.None);

    /// <summary>
    /// Asynchronously returns the single element of the sequence, and throws an exception if the sequence does not
    /// contain exactly one element.
    /// </summary>
    /// <remarks>Use this method when you expect the sequence to contain exactly one element. If the
    /// sequence is empty or contains more than one element, an exception is thrown.</remarks>
    /// <typeparam name="T">The type of elements in the source sequence.</typeparam>
    /// <param name="this">The source observable sequence.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the single element of the
    /// sequence.</returns>
    public static async ValueTask<T> SingleAsync<T>(this IObservableAsync<T> @this, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var observer = new SingleAsyncObserver<T>(null, cancellationToken);
        await using var subscription = await @this.SubscribeAsync(observer, cancellationToken).ConfigureAwait(false);
        return await observer.WaitValueAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Observer that captures the single element matching an optional predicate, throwing if zero or multiple match.
    /// </summary>
    /// <typeparam name="T">The type of elements in the source sequence.</typeparam>
    /// <param name="predicate">An optional predicate to filter elements.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    internal sealed class SingleAsyncObserver<T>(Func<T, bool>? predicate, CancellationToken cancellationToken)
        : TaskObserverAsyncBase<T, T>(cancellationToken)
    {
        /// <summary>
        /// A value indicating whether a matching element has been found.
        /// </summary>
        private bool _hasValue;

        /// <summary>
        /// The single matching element, if one has been found.
        /// </summary>
        private T? _value;

        /// <inheritdoc/>
        protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            if (predicate is null || predicate(value))
            {
                if (_hasValue)
                {
                    var message = predicate is null
                        ? "Sequence contains more than one element."
                        : "Sequence contains more than one matching element.";
                    await TrySetException(new InvalidOperationException(message)).ConfigureAwait(false);
                }
                else
                {
                    _hasValue = true;
                    _value = value;
                }
            }
        }

        /// <inheritdoc/>
        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
            TrySetException(error);

        /// <inheritdoc/>
        protected override ValueTask OnCompletedAsyncCore(Result result)
        {
            if (!result.IsSuccess)
            {
                return TrySetException(result.Exception);
            }

            if (!_hasValue)
            {
                var message = predicate is null
                    ? "Sequence contains no elements."
                    : "Sequence contains no matching elements.";
                return TrySetException(new InvalidOperationException(message));
            }

            return TrySetCompleted(_value!);
        }
    }
}
