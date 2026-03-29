// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides Timeout extension methods for asynchronous observable sequences.
/// </summary>
/// <remarks>Timeout applies a time limit to the observable sequence. If the sequence does not produce
/// a value within the specified time span, a <see cref="TimeoutException"/> is signalled as a failure
/// completion.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Applies a timeout policy to the observable sequence. If the next element is not received within
        /// the specified time span, the sequence completes with a <see cref="TimeoutException"/>.
        /// </summary>
        /// <param name="timeout">The maximum time span allowed between consecutive elements. Must be positive.</param>
        /// <param name="timeProvider">An optional time provider for controlling timing. If null, <see cref="TimeProvider.System"/>
        /// is used.</param>
        /// <returns>An observable sequence that mirrors the source but completes with a
        /// <see cref="TimeoutException"/> if any inter-element interval exceeds the specified timeout.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="timeout"/> is negative or zero.</exception>
        public IObservableAsync<T> Timeout(TimeSpan timeout, TimeProvider? timeProvider = null)
        {
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero, nameof(timeout));
#else
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }
#endif

            return new TimeoutObservable<T>(@this, timeout, timeProvider ?? TimeProvider.System);
        }

        /// <summary>
        /// Applies a timeout policy to the observable sequence. If the next element is not received within
        /// the specified time span, the sequence switches to the specified fallback observable.
        /// </summary>
        /// <param name="timeout">The maximum time span allowed between consecutive elements. Must be positive.</param>
        /// <param name="fallback">The fallback observable to switch to when a timeout occurs. Cannot be null.</param>
        /// <param name="timeProvider">An optional time provider for controlling timing. If null, <see cref="TimeProvider.System"/>
        /// is used.</param>
        /// <returns>An observable sequence that mirrors the source, switching to the fallback sequence
        /// if any inter-element interval exceeds the specified timeout.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="timeout"/> is negative or zero.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="fallback"/> is null.</exception>
        public IObservableAsync<T> Timeout(TimeSpan timeout, IObservableAsync<T> fallback, TimeProvider? timeProvider = null)
        {
            ArgumentExceptionHelper.ThrowIfNull(fallback, nameof(fallback));
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero, nameof(timeout));
#else
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }
#endif

            return new TimeoutWithFallbackObservable<T>(@this, timeout, fallback, timeProvider ?? TimeProvider.System);
        }
    }

    /// <summary>
    /// Async observable that mirrors the source but completes with a <see cref="TimeoutException"/>
    /// if any inter-element interval exceeds the specified timeout.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <param name="timeout">The maximum allowed inter-element interval.</param>
    /// <param name="timeProvider">The time provider used for scheduling the timeout.</param>
    internal sealed class TimeoutObservable<T>(IObservableAsync<T> source, TimeSpan timeout, TimeProvider timeProvider) : ObservableAsync<T>
    {
        /// <summary>
        /// Subscribes the specified observer and starts the timeout timer.
        /// </summary>
        /// <param name="observer">The observer to receive elements from the source.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>An async disposable that tears down the subscription when disposed.</returns>
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var timeoutObserver = new TimeoutObserver(observer, timeout, timeProvider);
            var subscription = await source.SubscribeAsync(timeoutObserver, cancellationToken);
            timeoutObserver.StartTimer(cancellationToken);
            return subscription;
        }

        /// <summary>
        /// Observer that resets a timer on each received element and signals a <see cref="TimeoutException"/>
        /// if no element arrives within the configured timeout.
        /// </summary>
        /// <param name="observer">The downstream observer to forward elements to.</param>
        /// <param name="timeout">The maximum allowed inter-element interval.</param>
        /// <param name="timeProvider">The time provider used for scheduling the timeout.</param>
        internal sealed class TimeoutObserver(IObserverAsync<T> observer, TimeSpan timeout, TimeProvider timeProvider) : ObserverAsync<T>
        {
            /// <summary>
            /// Synchronization gate protecting timer state.
            /// </summary>
#if NET9_0_OR_GREATER
            private readonly Lock _gate = new();
#else
            private readonly object _gate = new();
#endif

            /// <summary>
            /// Cancellation token source for the current timeout timer, or <see langword="null"/> if no timer is active.
            /// </summary>
            private CancellationTokenSource? _timerCts;

            /// <summary>
            /// Monotonically increasing identifier used to correlate timer callbacks with the most recent reset.
            /// </summary>
            private long _id;

            /// <summary>
            /// Indicates whether the observer has already received a completion signal.
            /// </summary>
            private bool _completed;

            /// <summary>
            /// Starts the initial timeout timer.
            /// </summary>
            /// <param name="cancellationToken">A token that cancels the timer when the subscription is disposed.</param>
            internal void StartTimer(CancellationToken cancellationToken) => ResetTimer(cancellationToken);

            /// <summary>
            /// Cancels the current timer and starts a new one with an incremented identifier.
            /// </summary>
            /// <param name="cancellationToken">A token linked to the new timer for external cancellation.</param>
            internal void ResetTimer(CancellationToken cancellationToken)
            {
                long currentId;
                CancellationTokenSource cts;
                lock (_gate)
                {
                    _timerCts?.Cancel();
                    _timerCts?.Dispose();
                    _timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts = _timerCts;
                    currentId = ++_id;
                }

                _ = OnTimeoutAsync(currentId, cts.Token);
            }

            /// <summary>
            /// Cancels and disposes the current timeout timer, if one is active.
            /// </summary>
            internal void CancelTimer()
            {
                lock (_gate)
                {
                    _timerCts?.Cancel();
                    _timerCts?.Dispose();
                    _timerCts = null;
                }
            }

            /// <summary>
            /// Waits for the timeout duration and signals a <see cref="TimeoutException"/> completion if the timer
            /// has not been reset or cancelled.
            /// </summary>
            /// <param name="id">The timer identifier at the time this timeout was scheduled.</param>
            /// <param name="cancellationToken">A token that cancels the delay when the timer is reset or disposed.</param>
            /// <returns>A task representing the asynchronous timeout operation.</returns>
            internal async Task OnTimeoutAsync(long id, CancellationToken cancellationToken)
            {
                try
                {
                    await DelayAsync(timeout, timeProvider, cancellationToken);

                    bool shouldFire;
                    lock (_gate)
                    {
                        shouldFire = _id == id && !_completed;
                    }

                    if (shouldFire)
                    {
                        await observer.OnCompletedAsync(Result.Failure(new TimeoutException()));
                    }
                }
                catch (OperationCanceledException)
                {
                    // Timer was cancelled; element received in time
                }
                catch (Exception e)
                {
                    UnhandledExceptionHandler.OnUnhandledException(e);
                }
            }

            /// <summary>
            /// Resets the timeout timer and forwards the element to the downstream observer.
            /// </summary>
            /// <param name="value">The element to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                ResetTimer(cancellationToken);
                return observer.OnNextAsync(value, cancellationToken);
            }

            /// <summary>
            /// Cancels the timeout timer and forwards the error to the downstream observer.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            {
                CancelTimer();
                return observer.OnErrorResumeAsync(error, cancellationToken);
            }

            /// <summary>
            /// Cancels the timeout timer and forwards completion to the downstream observer.
            /// </summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            protected override ValueTask OnCompletedAsyncCore(Result result)
            {
                lock (_gate)
                {
                    _completed = true;
                }

                CancelTimer();
                return observer.OnCompletedAsync(result);
            }

            /// <summary>
            /// Cancels the timeout timer during disposal.
            /// </summary>
            /// <returns>A completed task.</returns>
            protected override ValueTask DisposeAsyncCore()
            {
                CancelTimer();
                return default;
            }
        }
    }

    /// <summary>
    /// Async observable that mirrors the source but switches to a fallback observable
    /// if any inter-element interval exceeds the specified timeout.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <param name="timeout">The maximum allowed inter-element interval.</param>
    /// <param name="fallback">The fallback observable to switch to on timeout.</param>
    /// <param name="timeProvider">The time provider used for scheduling the timeout.</param>
    internal sealed class TimeoutWithFallbackObservable<T>(IObservableAsync<T> source, TimeSpan timeout, IObservableAsync<T> fallback, TimeProvider timeProvider) : ObservableAsync<T>
    {
        /// <summary>
        /// Subscribes the specified observer by wrapping the source with a timeout and a catch-to-fallback.
        /// </summary>
        /// <param name="observer">The observer to receive elements.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>An async disposable that tears down the subscription when disposed.</returns>
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            // Wrap with Catch to switch to fallback on TimeoutException
            var withTimeout = new TimeoutObservable<T>(source, timeout, timeProvider);
            var withFallback = withTimeout.Catch(ex => ex is TimeoutException ? fallback : Throw<T>(ex));
            return await withFallback.SubscribeAsync(observer.Wrap(), cancellationToken);
        }
    }
}
