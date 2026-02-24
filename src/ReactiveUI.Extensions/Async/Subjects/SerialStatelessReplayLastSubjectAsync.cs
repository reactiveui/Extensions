// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async.Subjects;

/// <summary>
/// Represents a serial, stateless asynchronous subject that replays only the last value to new observers and supports
/// asynchronous notification delivery.
/// </summary>
/// <remarks>This subject delivers notifications to observers one at a time in the order they are received. It
/// does not maintain any state beyond the most recent value, and only the last value (if any) is replayed to new
/// subscribers. All observer notifications are dispatched asynchronously and serially, ensuring that each observer
/// receives notifications in the correct order.</remarks>
/// <typeparam name="T">The type of the elements processed by the subject.</typeparam>
/// <param name="startValue">An optional initial value to be replayed to new observers before any values are published. If not specified, no
/// value is replayed until the first value is received.</param>
public sealed class SerialStatelessReplayLastSubjectAsync<T>(Optional<T> startValue) : BaseStatelessReplayLastSubjectAsync<T>(startValue)
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
        foreach (var observer in observers)
        {
            await observer.OnNextAsync(value, cancellationToken);
        }
    }

    /// <summary>
    /// Notifies each observer in the collection to resume after an error has occurred, using asynchronous operations.
    /// </summary>
    /// <param name="observers">A read-only list of observers to be notified to resume after the error. Cannot be null.</param>
    /// <param name="error">The exception that caused the error. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous notification operation.</returns>
    protected override async ValueTask OnErrorResumeAsyncCore(IReadOnlyList<IObserverAsync<T>> observers, Exception error, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            await observer.OnErrorResumeAsync(error, cancellationToken);
        }
    }

    /// <summary>
    /// Notifies all observers that the asynchronous operation has completed, passing the specified result to each
    /// observer.
    /// </summary>
    /// <remarks>Each observer in the list is notified sequentially by invoking its OnCompletedAsync method
    /// with the provided result. If any observer's notification fails, the exception will propagate and may prevent
    /// subsequent observers from being notified.</remarks>
    /// <param name="observers">A read-only list of observers to be notified of the operation's completion. Cannot be null.</param>
    /// <param name="result">The result to provide to each observer upon completion.</param>
    /// <returns>A task that represents the asynchronous notification operation.</returns>
    protected override async ValueTask OnCompletedAsyncCore(IReadOnlyList<IObserverAsync<T>> observers, Result result)
    {
        foreach (var observer in observers)
        {
            await observer.OnCompletedAsync(result);
        }
    }
}
