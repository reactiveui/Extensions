// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Represents an asynchronous observable sequence that concatenates multiple asynchronous observables, emitting their
/// elements in order as each completes.
/// </summary>
/// <remarks>This class enables sequential composition of multiple asynchronous observables, ensuring that items
/// from each source are emitted in order and that subsequent observables are not subscribed to until the preceding one
/// has completed. If any observable in the sequence signals an error, the concatenation terminates and the error is
/// propagated to the observer.</remarks>
/// <typeparam name="T">The type of elements produced by the concatenated observable sequences.</typeparam>
/// <param name="observables">A collection of asynchronous observables to be concatenated. Each observable is subscribed to sequentially; the next
/// begins only after the previous completes.</param>
internal sealed class ConcatEnumerableObservable<T>(IEnumerable<IObservableAsync<T>> observables) : ObservableAsync<T>
{
    /// <summary>
    /// The enumerable collection of observable sequences to concatenate.
    /// </summary>
    private readonly IEnumerable<IObservableAsync<T>> _observables = observables;

    /// <summary>
    /// Subscribes the specified observer by creating a <see cref="ConcatEnumerableSubscription"/> that iterates
    /// through the enumerable of observables sequentially.
    /// </summary>
    /// <param name="observer">The observer to receive elements from the concatenated sequences.</param>
    /// <param name="cancellationToken">A token to cancel the subscription.</param>
    /// <returns>An async disposable that tears down the subscription when disposed.</returns>
    protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
    {
        var subscription = new ConcatEnumerableSubscription(this, observer);
        try
        {
            await subscription.SubscribeNextAsync();
        }
        catch
        {
            await subscription.DisposeAsync();
            throw;
        }

        return subscription;
    }

    /// <summary>
    /// Manages sequential iteration through the enumerable of observables, subscribing to each
    /// inner observable only after the previous one completes.
    /// </summary>
    internal sealed class ConcatEnumerableSubscription : IAsyncDisposable
    {
        /// <summary>
        /// Enumerator that iterates through the collection of observable sequences to concatenate.
        /// </summary>
        private readonly IEnumerator<IObservableAsync<T>> _enumerator;

        /// <summary>
        /// Serial disposable that holds the currently active inner subscription, disposing the previous one when replaced.
        /// </summary>
        private readonly SerialDisposableAsync _innerDisposable = new();

        /// <summary>
        /// Cancellation token source used to signal disposal of the subscription.
        /// </summary>
        private readonly CancellationTokenSource _cts = new();

        /// <summary>
        /// Cached cancellation token from the dispose cancellation token source.
        /// </summary>
        private readonly CancellationToken _disposedCancellationToken;

        /// <summary>
        /// The downstream observer to forward elements to.
        /// </summary>
        private readonly IObserverAsync<T> _observer;

        /// <summary>
        /// Flag indicating whether this subscription has been disposed (1 = disposed, 0 = active).
        /// </summary>
        private int _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcatEnumerableSubscription"/> class.
        /// </summary>
        /// <param name="parent">The parent observable that provides the enumerable of observables.</param>
        /// <param name="observer">The downstream observer to forward elements to.</param>
        public ConcatEnumerableSubscription(ConcatEnumerableObservable<T> parent, IObserverAsync<T> observer)
        {
            _observer = observer;
            _enumerator = parent._observables.GetEnumerator();
            _disposedCancellationToken = _cts.Token;
        }

        /// <summary>
        /// Advances to and subscribes to the next observable in the enumerable,
        /// or completes if no more observables are available.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async ValueTask SubscribeNextAsync()
        {
            try
            {
                if (_enumerator.MoveNext())
                {
                    var current = _enumerator.Current;
                    var subscription = await current!.SubscribeAsync(
                        OnInnerNextAsync,
                        OnInnerErrorResumeAsync,
                        result => result.IsFailure ? CompleteAsync(result) : SubscribeNextAsync(),
                        _disposedCancellationToken);

                    await _innerDisposable.SetDisposableAsync(subscription);
                }
                else
                {
                    await CompleteAsync(Result.Success);
                }
            }
            catch (Exception e)
            {
                await CompleteAsync(Result.Failure(e));
            }
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => CompleteAsync(null);

        /// <summary>
        /// Handles a second call to <see cref="CompleteAsync"/> when already disposed,
        /// routing any failure exception to the unhandled exception handler.
        /// </summary>
        /// <param name="result">The completion result from the second call.</param>
        internal static void HandleAlreadyDisposed(Result? result)
        {
            if (result?.Exception is not null and var exception)
            {
                UnhandledExceptionHandler.OnUnhandledException(exception);
            }
        }

        /// <summary>
        /// Forwards a non-fatal error from the current inner sequence to the downstream observer.
        /// </summary>
        /// <param name="exception">The error to forward.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async ValueTask OnInnerErrorResumeAsync(Exception exception, CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken, cancellationToken);
            await _observer.OnErrorResumeAsync(exception, linkedCts.Token);
        }

        /// <summary>
        /// Forwards an element from the current inner sequence to the downstream observer.
        /// </summary>
        /// <param name="value">The element to forward.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async ValueTask OnInnerNextAsync(T value, CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken, cancellationToken);
            await _observer.OnNextAsync(value, linkedCts.Token);
        }

        /// <summary>
        /// Disposes the inner subscription and enumerator, and optionally forwards a completion result
        /// to the downstream observer. This method is idempotent.
        /// </summary>
        /// <param name="result">The completion result to forward, or <see langword="null"/> if disposing without signaling completion.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async ValueTask CompleteAsync(Result? result)
        {
            if (DisposalHelper.TrySetDisposed(ref _disposed))
            {
                HandleAlreadyDisposed(result);
                return;
            }

            _cts.Cancel();
            await _innerDisposable.DisposeAsync();
            if (result is not null)
            {
                await _observer.OnCompletedAsync(result.Value);
            }

            _enumerator.Dispose();
            _cts.Dispose();
        }
    }
}
