// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async.Subjects;

/// <summary>
/// Provides a base class for stateless, asynchronous subjects that replay the last value to new subscribers and support
/// resuming after errors or completion. Designed for scenarios where observers may join at any time and should receive
/// the most recent value, if available.
/// </summary>
/// <remarks>This abstract class implements asynchronous observer and observable patterns, allowing derived
/// classes to define custom notification, error handling, and completion behaviors. It manages observer subscriptions
/// and ensures that the most recent value (if any) is replayed to new subscribers. Thread safety is provided for
/// concurrent access and notification. Typical use cases include event streaming, stateful data flows, or scenarios
/// where late subscribers should receive the latest state.</remarks>
/// <typeparam name="T">The type of the elements processed and published by the subject.</typeparam>
/// <param name="startValue">The optional initial value to be used as the starting point for the subject. If provided, this value is immediately
/// available to new subscribers until a new value is published.</param>
public abstract class BaseStatelessReplayLastSubjectAsync<T>(Optional<T> startValue) : ObservableAsync<T>, ISubjectAsync<T>
{
    private readonly Optional<T> _startValue = startValue;
    private readonly AsyncGate _gate = new();
    private Optional<T> _value = startValue;
    private ImmutableList<IObserverAsync<T>> _observers = [];

    /// <summary>
    /// Gets an observable sequence that represents the asynchronous values published by the subject.
    /// </summary>
    IObservableAsync<T> ISubjectAsync<T>.Values => this;

    /// <summary>
    /// Asynchronously notifies all registered observers of a new value.
    /// </summary>
    /// <param name="value">The value to send to each observer.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the notification operation.</param>
    /// <returns>A task that represents the asynchronous notification operation.</returns>
    public async ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
    {
        ImmutableList<IObserverAsync<T>> observers;
        using (await _gate.LockAsync())
        {
            _value = new(value);
            observers = _observers;
        }

        await OnNextAsyncCore(observers, value, cancellationToken);
    }

    /// <summary>
    /// Handles an error by notifying all observers asynchronously and allows the operation to resume without
    /// propagating the exception.
    /// </summary>
    /// <remarks>This method ensures that all registered observers are notified of the specified error. The
    /// operation completes when all observers have been notified or the cancellation is requested.</remarks>
    /// <param name="error">The exception that occurred and will be reported to observers. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the notification operation.</param>
    /// <returns>A task that represents the asynchronous error notification operation.</returns>
    public async ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
    {
        ImmutableList<IObserverAsync<T>> observers;
        using (await _gate.LockAsync())
        {
            observers = _observers;
        }

        await OnErrorResumeAsyncCore(observers, error, cancellationToken);
    }

    /// <summary>
    /// Notifies all observers that the asynchronous operation has completed and performs necessary cleanup.
    /// </summary>
    /// <remarks>After this method is called, the list of observers is cleared and the internal state is
    /// reset. Subsequent calls to this method will have no effect on previously completed observers.</remarks>
    /// <param name="result">The result information to be provided to observers upon completion.</param>
    /// <returns>A task that represents the asynchronous notification operation.</returns>
    public async ValueTask OnCompletedAsync(Result result)
    {
        ImmutableList<IObserverAsync<T>> observers;
        using (await _gate.LockAsync())
        {
            observers = _observers;
            _observers = [];
            _value = _startValue;
        }

        await OnCompletedAsyncCore(observers, result);
    }

    /// <summary>
    /// Asynchronously releases the unmanaged resources used by the object.
    /// </summary>
    /// <returns>A ValueTask that represents the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        GC.SuppressFinalize(this);
        return default;
    }

    /// <summary>
    /// Subscribes the specified asynchronous observer to receive notifications from the observable sequence.
    /// </summary>
    /// <remarks>If the observable has a current value, it is immediately sent to the observer upon
    /// subscription. Disposing the returned object will unsubscribe the observer and may reset the observable's state
    /// if there are no remaining subscribers.</remarks>
    /// <param name="observer">The asynchronous observer that will receive notifications. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the subscription operation.</param>
    /// <returns>A task that represents the asynchronous operation. The result contains a disposable object that can be used to
    /// unsubscribe the observer.</returns>
    protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentExceptionHelper.ThrowIfNull(observer);

        var disposable = DisposableAsync.Create(async () =>
        {
            using (await _gate.LockAsync())
            {
                _observers = _observers.Remove(observer);
                if (_observers.Count == 0)
                {
                    _value = _startValue;
                }
            }
        });

        using (await _gate.LockAsync())
        {
            _observers = _observers.Add(observer);
            if (_value.TryGetValue(out var value))
            {
                await observer.OnNextAsync(value, cancellationToken);
            }
        }

        return disposable;
    }

    /// <summary>
    /// Asynchronously notifies the specified observers of a new value.
    /// </summary>
    /// <remarks>Derived classes should implement this method to define how notifications are delivered to
    /// observers. The method should honor the provided cancellation token and ensure that all observers in the list are
    /// notified according to the desired semantics.</remarks>
    /// <param name="observers">A read-only list of observers to be notified. Cannot be null.</param>
    /// <param name="value">The value to deliver to each observer.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the notification operation.</param>
    /// <returns>A ValueTask that represents the asynchronous notification operation.</returns>
    protected abstract ValueTask OnNextAsyncCore(IReadOnlyList<IObserverAsync<T>> observers, T value, CancellationToken cancellationToken);

    /// <summary>
    /// Handles an error by resuming asynchronous observation for the specified observers.
    /// </summary>
    /// <remarks>Implementations should ensure that error handling is performed in a way that allows observers
    /// to continue receiving notifications or to recover from the error, as appropriate. This method is intended to be
    /// overridden to provide custom error recovery strategies in asynchronous observer scenarios.</remarks>
    /// <param name="observers">A read-only list of observers to notify or resume after the error occurs. Cannot be null.</param>
    /// <param name="error">The exception that triggered the error handling logic. Cannot be null.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A ValueTask that represents the asynchronous error handling operation.</returns>
    protected abstract ValueTask OnErrorResumeAsyncCore(IReadOnlyList<IObserverAsync<T>> observers, Exception error, CancellationToken cancellationToken);

    /// <summary>
    /// Invoked to asynchronously notify all observers that the sequence has completed, providing the final result.
    /// </summary>
    /// <remarks>This method is called when the observed sequence has finished processing. Implementations
    /// should ensure that all observers are notified according to the completion semantics of the sequence. This method
    /// is intended to be overridden in derived classes to customize completion behavior.</remarks>
    /// <param name="observers">The collection of observers to be notified of the sequence completion. Cannot be null.</param>
    /// <param name="result">The result to provide to observers upon completion. Represents the outcome of the observed sequence.</param>
    /// <returns>A ValueTask that represents the asynchronous notification operation.</returns>
    protected abstract ValueTask OnCompletedAsyncCore(IReadOnlyList<IObserverAsync<T>> observers, Result result);
}
