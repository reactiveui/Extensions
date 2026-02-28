// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

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
    protected override async ValueTask OnNextAsyncCore(IReadOnlyList<IObserverAsync<T>> observers, T value, CancellationToken cancellationToken)
    {
        foreach (var obserevr in observers)
        {
            await obserevr.OnNextAsync(value, cancellationToken);
        }
    }

    /// <summary>
    /// Notifies each observer in the collection to resume after an error has occurred, using asynchronous operations.
    /// </summary>
    /// <param name="observers">A read-only list of observers to be notified to resume after the error. Cannot be null.</param>
    /// <param name="error">The exception that caused the error. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A ValueTask that represents the asynchronous notification operation.</returns>
    protected override async ValueTask OnErrorResumeAsyncCore(IReadOnlyList<IObserverAsync<T>> observers, Exception error, CancellationToken cancellationToken)
    {
        foreach (var obserevr in observers)
        {
            await obserevr.OnErrorResumeAsync(error, cancellationToken);
        }
    }

    /// <summary>
    /// Asynchronously notifies all observers that the observation has completed with the specified result.
    /// </summary>
    /// <param name="observers">A read-only list of observers to be notified of the completion event. Cannot be null.</param>
    /// <param name="result">The result to provide to each observer upon completion.</param>
    /// <returns>A ValueTask that represents the asynchronous notification operation.</returns>
    protected override async ValueTask OnCompletedAsyncCore(IReadOnlyList<IObserverAsync<T>> observers, Result result)
    {
        foreach (var obserevr in observers)
        {
            await obserevr.OnCompletedAsync(result);
        }
    }
}
