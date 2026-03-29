// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Async observable that concatenates inner observable sequences emitted by an outer observable,
/// subscribing to each inner sequence only after the previous one completes.
/// </summary>
/// <typeparam name="T">The type of elements produced by the inner observable sequences.</typeparam>
/// <param name="source">The outer observable sequence that emits inner observable sequences to concatenate.</param>
internal sealed class ConcatObservablesObservable<T>(IObservableAsync<IObservableAsync<T>> source) : ObservableAsync<T>
{
    /// <summary>
    /// Subscribes the specified observer by creating a <see cref="ConcatSubscription"/> that manages
    /// sequential subscription to inner observables.
    /// </summary>
    /// <param name="observer">The observer to receive elements from the concatenated sequences.</param>
    /// <param name="cancellationToken">A token to cancel the subscription.</param>
    /// <returns>An async disposable that tears down the subscription when disposed.</returns>
    protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
    {
        var subscription = new ConcatSubscription(observer);
        try
        {
            await subscription.SubscribeAsync(source, cancellationToken);
        }
        catch
        {
            await subscription.DisposeAsync();
            throw;
        }

        return subscription;
    }

    /// <summary>
    /// Manages the lifetime of the outer subscription and buffers inner observables,
    /// subscribing to each one sequentially as the previous completes.
    /// </summary>
    internal sealed class ConcatSubscription : IAsyncDisposable
    {
        /// <summary>
        /// Concurrent queue that buffers inner observables waiting to be subscribed to.
        /// </summary>
        private readonly ConcurrentQueue<IObservableAsync<T>> _buffer = new();

        /// <summary>
        /// Cancellation token source used to signal disposal of the subscription.
        /// </summary>
        private readonly CancellationTokenSource _disposeCts = new();

        /// <summary>
        /// Cached cancellation token from the dispose cancellation token source.
        /// </summary>
        private readonly CancellationToken _disposedCancellationToken;

        /// <summary>
        /// Disposable that holds the single outer subscription.
        /// </summary>
        private readonly SingleAssignmentDisposableAsync _outerDisposable = new();

        /// <summary>
        /// Serial disposable that holds the currently active inner subscription, disposing the previous one when replaced.
        /// </summary>
        private readonly SerialDisposableAsync _innerSubscription = new();

        /// <summary>
        /// The downstream observer to forward elements to.
        /// </summary>
        private readonly IObserverAsync<T> _observer;

        /// <summary>
        /// Async gate that serializes observer callbacks to ensure thread-safe emission.
        /// </summary>
        private readonly AsyncGate _observerOnSomethingGate = new();

        /// <summary>
        /// Indicates whether the outer observable sequence has completed.
        /// </summary>
        private bool _outerCompleted;

        /// <summary>
        /// Flag indicating whether this subscription has been disposed (1 = disposed, 0 = active).
        /// </summary>
        private int _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcatSubscription"/> class.
        /// </summary>
        /// <param name="observer">The downstream observer to forward elements to.</param>
        public ConcatSubscription(IObserverAsync<T> observer)
        {
            _observer = observer;
            _disposedCancellationToken = _disposeCts.Token;
        }

        /// <summary>
        /// Subscribes to the outer observable sequence.
        /// </summary>
        /// <param name="source">The outer observable that emits inner observable sequences.</param>
        /// <param name="subscriptionToken">A token to cancel the subscription.</param>
        /// <returns>A task representing the asynchronous subscribe operation.</returns>
        public async ValueTask SubscribeAsync(IObservableAsync<IObservableAsync<T>> source, CancellationToken subscriptionToken)
        {
            var outerSubscription = await source.SubscribeAsync(new ConcatOuterObserver(this), subscriptionToken);
            await _outerDisposable.SetDisposableAsync(outerSubscription);
        }

        /// <summary>
        /// Handles a new inner observable from the outer sequence by buffering it and subscribing
        /// if no inner sequence is currently active.
        /// </summary>
        /// <param name="inner">The inner observable to enqueue.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public ValueTask OnNextOuterAsync(IObservableAsync<T> inner)
        {
            var shouldSubscribe = false;
            lock (_buffer)
            {
                _buffer.Enqueue(inner);
                if (_buffer.Count == 1)
                {
                    shouldSubscribe = true;
                }
            }

            if (shouldSubscribe)
            {
                return SubscribeToInnerLoop(inner);
            }

            return default;
        }

        /// <summary>
        /// Handles the outer sequence completing, propagating completion downstream when the buffer is empty
        /// or when the outer fails.
        /// </summary>
        /// <param name="result">The completion result from the outer sequence.</param>
        /// <returns>A task representing the asynchronous completion operation.</returns>
        public ValueTask OnCompletedOuterAsync(Result result)
        {
            var shouldComplete = false;
            Result? completeResult = null;
            lock (_buffer)
            {
                _outerCompleted = true;
                if (result.IsFailure || _buffer.IsEmpty)
                {
                    shouldComplete = true;
                    completeResult = result;
                }
            }

            return shouldComplete ? CompleteAsync(completeResult) : default;
        }

        /// <summary>
        /// Handles the current inner sequence completing, subscribing to the next buffered inner
        /// sequence or completing the subscription if the outer has also completed.
        /// </summary>
        /// <param name="result">The completion result from the inner sequence.</param>
        /// <returns>A task representing the asynchronous completion operation.</returns>
        public ValueTask OnCompletedInnerAsync(Result result)
        {
            if (result.IsFailure)
            {
                return CompleteAsync(result);
            }

            IObservableAsync<T>? nextInner;
            bool outerCompleted;
            lock (_buffer)
            {
                _buffer.TryDequeue(out _);
                _buffer.TryPeek(out nextInner);
                outerCompleted = _outerCompleted;
            }

            if (nextInner is null)
            {
                return outerCompleted ? CompleteAsync(Result.Success) : default;
            }

            return SubscribeToInnerLoop(nextInner);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => CompleteAsync(null);

        /// <summary>
        /// Subscribes to the specified inner observable, setting it as the current active inner subscription.
        /// </summary>
        /// <param name="currentInner">The inner observable to subscribe to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async ValueTask SubscribeToInnerLoop(IObservableAsync<T> currentInner)
        {
            try
            {
                var innerSubscription = await currentInner.SubscribeAsync(new ConcatInnerObserver(this), _disposedCancellationToken);
                await _innerSubscription.SetDisposableAsync(innerSubscription);
            }
            catch (Exception e)
            {
                await CompleteAsync(Result.Failure(e));
            }
        }

        /// <summary>
        /// Disposes the inner and outer subscriptions and optionally forwards a completion result to
        /// the downstream observer. This method is idempotent.
        /// </summary>
        /// <param name="result">The completion result to forward, or <see langword="null"/> if disposing without signaling completion.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async ValueTask CompleteAsync(Result? result)
        {
            if (DisposalHelper.TrySetDisposed(ref _disposed))
            {
                if (result?.Exception is not null and var exception)
                {
                    UnhandledExceptionHandler.OnUnhandledException(exception);
                }

                return;
            }

            _disposeCts.Cancel();
            await _innerSubscription.DisposeAsync();
            await _outerDisposable.DisposeAsync();
            if (result is not null)
            {
                await _observer.OnCompletedAsync(result.Value);
            }

            _disposeCts.Dispose();
            _observerOnSomethingGate.Dispose();
        }

        /// <summary>
        /// Observer for the outer observable sequence that delegates to the parent <see cref="ConcatSubscription"/>.
        /// </summary>
        /// <param name="subscription">The parent concat subscription.</param>
        internal sealed class ConcatOuterObserver(ConcatSubscription subscription) : ObserverAsync<IObservableAsync<T>>
        {
            /// <summary>
            /// Forwards a new inner observable to the parent subscription for buffering and sequential subscription.
            /// </summary>
            /// <param name="value">The new inner observable.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            protected override ValueTask OnNextAsyncCore(IObservableAsync<T> value, CancellationToken cancellationToken)
                => subscription.OnNextOuterAsync(value);

            /// <summary>
            /// Forwards a non-fatal error from the outer sequence to the downstream observer.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            protected override async ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(subscription._disposedCancellationToken, cancellationToken);
                using (await subscription._observerOnSomethingGate.LockAsync())
                {
                    await subscription._observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            /// <summary>
            /// Handles the outer sequence completing.
            /// </summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            protected override ValueTask OnCompletedAsyncCore(Result result)
                => subscription.OnCompletedOuterAsync(result);
        }

        /// <summary>
        /// Observer for the currently active inner observable sequence that delegates to the parent <see cref="ConcatSubscription"/>.
        /// </summary>
        /// <param name="subscription">The parent concat subscription.</param>
        internal sealed class ConcatInnerObserver(ConcatSubscription subscription) : ObserverAsync<T>
        {
            /// <summary>
            /// Forwards an element from the inner sequence to the downstream observer.
            /// </summary>
            /// <param name="value">The element to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(subscription._disposedCancellationToken, cancellationToken);
                using (await subscription._observerOnSomethingGate.LockAsync())
                {
                    await subscription._observer.OnNextAsync(value, linkedCts.Token);
                }
            }

            /// <summary>
            /// Forwards a non-fatal error from the inner sequence to the downstream observer.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            protected override async ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(subscription._disposedCancellationToken, cancellationToken);
                using (await subscription._observerOnSomethingGate.LockAsync())
                {
                    await subscription._observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            /// <summary>
            /// Handles the inner sequence completing, triggering subscription to the next buffered sequence.
            /// </summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            protected override ValueTask OnCompletedAsyncCore(Result result)
                => subscription.OnCompletedInnerAsync(result);
        }
    }
}
