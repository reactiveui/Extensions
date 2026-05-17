// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace ReactiveUI.Extensions.Async.Subjects;

/// <summary>
/// Represents an asynchronous subject that notifies observers in a serial manner, ensuring each observer is notified
/// one at a time.
/// </summary>
/// <remarks>SerialSubjectAsync{T} is designed for scenarios where observers must be notified sequentially rather
/// than concurrently. This can be useful when observer operations are not thread-safe or when order of notification is
/// important. Notifications to observers are performed asynchronously and in sequence.</remarks>
/// <typeparam name="T">The type of the elements processed and observed by the subject.</typeparam>
public sealed class SerialSubjectAsync<T> : BaseSubjectAsync<T>
{
    /// <summary>
    /// Asynchronously notifies each observer in the specified collection with the provided value.
    /// </summary>
    /// <param name="observers">A read-only list of observers to be notified. Cannot be null.</param>
    /// <param name="value">The value to send to each observer.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the notification operation.</param>
    /// <returns>A task that represents the asynchronous notification operation.</returns>
    protected override ValueTask OnNextAsyncCore(
        ImmutableArray<IObserverAsync<T>> observers,
        T value,
        CancellationToken cancellationToken) =>
        observers.Length == 1
            ? observers[0].OnNextAsync(value, cancellationToken)
            : OnNextAsyncCoreMulti(observers, value, cancellationToken);

    /// <summary>
    /// Notifies each observer in the collection to resume after an error has occurred, using asynchronous operations.
    /// </summary>
    /// <param name="observers">A read-only list of observers to be notified to resume after the error. Cannot be null.</param>
    /// <param name="error">The exception that caused the error. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A ValueTask that represents the asynchronous notification operation.</returns>
    protected override async ValueTask OnErrorResumeAsyncCore(
        ImmutableArray<IObserverAsync<T>> observers,
        Exception error,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < observers.Length; i++)
        {
            await observers[i].OnErrorResumeAsync(error, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Asynchronously notifies all observers that the observation has completed with the specified result.
    /// </summary>
    /// <param name="observers">A read-only list of observers to be notified of the completion event. Cannot be null.</param>
    /// <param name="result">The result to provide to each observer upon completion.</param>
    /// <returns>A ValueTask that represents the asynchronous notification operation.</returns>
    protected override async ValueTask OnCompletedAsyncCore(ImmutableArray<IObserverAsync<T>> observers, Result result)
    {
        for (var i = 0; i < observers.Length; i++)
        {
            await observers[i].OnCompletedAsync(result).ConfigureAwait(false);
        }
    }

    /// <summary>Async fall-back for the multi-observer case; sequentially forwards the value to each subscriber.</summary>
    /// <param name="observers">The current observer snapshot.</param>
    /// <param name="value">The value being broadcast.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the notification operation.</param>
    /// <returns>A task that represents the asynchronous notification operation.</returns>
    private static async ValueTask OnNextAsyncCoreMulti(
        ImmutableArray<IObserverAsync<T>> observers,
        T value,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < observers.Length; i++)
        {
            await observers[i].OnNextAsync(value, cancellationToken).ConfigureAwait(false);
        }
    }
}
