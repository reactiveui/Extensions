// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace ReactiveUI.Extensions.Async.Subjects;

/// <summary>
/// Represents a stateless asynchronous subject that notifies observers of events in a serial, sequential manner.
/// </summary>
/// <remarks>Observers are notified one at a time in the order they are registered. Each observer receives the
/// event only after the previous observer has completed processing. This class is suitable for scenarios where event
/// delivery order and sequential processing are required. Thread safety and ordering are managed internally.</remarks>
/// <typeparam name="T">The type of the elements processed and observed by the subject.</typeparam>
public sealed class SerialStatelessSubjectAsync<T> : BaseStatelessSubjectAsync<T>
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
        CancellationToken cancellationToken)
    {
        // Fast path: a single observer is by far the most common shape — operators that take a
        // pipeline always present one subscriber. Returning the observer's ValueTask directly
        // skips the async state-machine box that the loop variant would allocate every emission.
        if (observers.Length == 1)
        {
            return observers[0].OnNextAsync(value, cancellationToken);
        }

        return OnNextAsyncCoreMulti(observers, value, cancellationToken);
    }

    /// <summary>
    /// Notifies each observer in the collection to resume processing after an error has occurred, using asynchronous
    /// operations.
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
    /// Notifies all specified observers that the asynchronous operation has completed, passing the provided result to
    /// each observer.
    /// </summary>
    /// <remarks>The method awaits the completion of each observer's notification in sequence. If any
    /// observer's notification fails, the exception will propagate and subsequent observers will not be
    /// notified.</remarks>
    /// <param name="observers">A read-only list of observers to be notified of the operation's completion. Cannot be null.</param>
    /// <param name="result">The result to provide to each observer upon completion.</param>
    /// <returns>A task that represents the asynchronous notification operation.</returns>
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
