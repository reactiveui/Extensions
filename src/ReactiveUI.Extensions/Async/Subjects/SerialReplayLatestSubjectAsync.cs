// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async.Subjects;

/// <summary>
/// Represents an asynchronous subject that replays only the latest value to new subscribers and ensures that
/// notifications are delivered to observers in a serial, thread-safe manner.
/// </summary>
/// <remarks>This subject is designed for scenarios where only the most recent value is relevant to subscribers.
/// When a new observer subscribes, it immediately receives the latest value (if any) and then all subsequent
/// notifications. All observer notifications are performed asynchronously and in a serial order, ensuring thread
/// safety. This type is suitable for use cases where replaying only the latest value is desired, such as event streams
/// or state broadcasts.</remarks>
/// <typeparam name="T">The type of the elements processed by the subject.</typeparam>
/// <param name="startValue">An optional initial value to be emitted to new subscribers before any other values are published.</param>
public sealed class SerialReplayLatestSubjectAsync<T>(Optional<T> startValue) : BaseReplayLatestSubjectAsync<T>(startValue)
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
    /// Notifies each observer in the collection of an error and allows them to resume processing asynchronously.
    /// </summary>
    /// <param name="observers">A read-only list of observers to be notified of the error. Cannot be null.</param>
    /// <param name="error">The exception that occurred. Cannot be null.</param>
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
    /// Notifies all specified observers that the asynchronous operation has completed, passing the provided result to
    /// each observer.
    /// </summary>
    /// <param name="observers">A read-only list of observers to be notified of the operation's completion. Cannot be null.</param>
    /// <param name="result">The result to pass to each observer's completion handler.</param>
    /// <returns>A task that represents the asynchronous notification operation.</returns>
    protected override async ValueTask OnCompletedAsyncCore(IReadOnlyList<IObserverAsync<T>> observers, Result result)
    {
        foreach (var observer in observers)
        {
            await observer.OnCompletedAsync(result);
        }
    }
}
