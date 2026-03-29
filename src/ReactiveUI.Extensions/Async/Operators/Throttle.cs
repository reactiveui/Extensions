// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides Throttle (debounce) extension methods for asynchronous observable sequences.
/// </summary>
/// <remarks>Throttle ignores elements from the source sequence that are followed by another element
/// within a specified time span. Only values that are not followed by another value within the due time
/// are forwarded to observers. This is commonly used to suppress rapid bursts of events such as keystrokes
/// or mouse movements.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Ignores elements from the source sequence that are followed by another element within
        /// the specified time span. Only the last element in each burst is forwarded.
        /// </summary>
        /// <param name="dueTime">The time span that must elapse after the last element before it is forwarded.
        /// Must be non-negative.</param>
        /// <param name="timeProvider">An optional time provider for controlling timing. If null, <see cref="TimeProvider.System"/>
        /// is used.</param>
        /// <returns>An observable sequence containing only those elements that are not followed by another
        /// element within the specified due time.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="dueTime"/> is negative.</exception>
        public IObservableAsync<T> Throttle(TimeSpan dueTime, TimeProvider? timeProvider = null)
        {
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfLessThan(dueTime, TimeSpan.Zero, nameof(dueTime));
#else
            if (dueTime < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(dueTime));
            }
#endif

            return new ThrottleObservable<T>(@this, dueTime, timeProvider ?? TimeProvider.System);
        }
    }

    /// <summary>
    /// Asynchronously delays for the specified duration using the provided time provider.
    /// </summary>
    /// <param name="delay">The duration to delay.</param>
    /// <param name="timeProvider">The time provider to use for the delay.</param>
    /// <param name="cancellationToken">A token to cancel the delay.</param>
    /// <returns>A task that completes after the specified delay.</returns>
    internal static async Task DelayAsync(TimeSpan delay, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        if (timeProvider == TimeProvider.System)
        {
            await Task.Delay(delay, cancellationToken);
        }
        else
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var timer = timeProvider.CreateTimer(
                static x => ((TaskCompletionSource<bool>)x!).TrySetResult(true),
                tcs,
                delay,
                System.Threading.Timeout.InfiniteTimeSpan);
            using var ctReg = cancellationToken.Register(
                static x => ((TaskCompletionSource<bool>)x!).TrySetCanceled(),
                tcs);
            await tcs.Task;
        }
    }

    /// <summary>
    /// Async observable that debounces the source sequence, only forwarding elements that are not
    /// followed by another element within the specified due time.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="source">The source observable sequence to throttle.</param>
    /// <param name="dueTime">The quiet period that must elapse before an element is forwarded.</param>
    /// <param name="timeProvider">The time provider used for scheduling the debounce timer.</param>
    internal sealed class ThrottleObservable<T>(IObservableAsync<T> source, TimeSpan dueTime, TimeProvider timeProvider) : ObservableAsync<T>
    {
        /// <summary>
        /// Subscribes the specified observer with throttle behavior applied.
        /// </summary>
        /// <param name="observer">The observer to receive throttled elements.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>An async disposable that tears down the subscription when disposed.</returns>
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var throttleObserver = new ThrottleObserver(observer, dueTime, timeProvider);
            return await source.SubscribeAsync(throttleObserver, cancellationToken);
        }

        /// <summary>
        /// Observer that implements throttle/debounce logic by starting a timer on each element
        /// and only forwarding the element if no newer element supersedes it before the timer fires.
        /// </summary>
        /// <param name="observer">The downstream observer to forward debounced elements to.</param>
        /// <param name="dueTime">The quiet period that must elapse before an element is forwarded.</param>
        /// <param name="timeProvider">The time provider used for scheduling the debounce timer.</param>
        internal sealed class ThrottleObserver(IObserverAsync<T> observer, TimeSpan dueTime, TimeProvider timeProvider) : ObserverAsync<T>
        {
            /// <summary>
            /// The synchronization gate protecting shared throttle state.
            /// </summary>
            private readonly object _gate = new();

            /// <summary>
            /// The cancellation token source for the currently pending debounce timer, or null if no timer is active.
            /// </summary>
            private CancellationTokenSource? _timerCts;

            /// <summary>
            /// A monotonically increasing identifier used to detect whether a newer element has superseded the current timer.
            /// </summary>
            private long _id;

            /// <summary>
            /// Cancels and disposes the currently pending debounce timer, if any.
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
            /// Waits for the debounce delay and then forwards the value if it has not been superseded.
            /// </summary>
            /// <param name="value">The value to forward after the delay.</param>
            /// <param name="id">The identifier of this timer; if superseded by a newer id, the value is discarded.</param>
            /// <param name="cancellationToken">A token to cancel the delay.</param>
            /// <returns>A task representing the asynchronous delay and forwarding operation.</returns>
            internal async Task FireAfterDelayAsync(T value, long id, CancellationToken cancellationToken)
            {
                try
                {
                    await DelayAsync(dueTime, timeProvider, cancellationToken);

                    lock (_gate)
                    {
                        if (_id != id)
                        {
                            return;
                        }
                    }

                    await observer.OnNextAsync(value, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Timer was cancelled; value superseded
                }
                catch (Exception e)
                {
                    UnhandledExceptionHandler.OnUnhandledException(e);
                }
            }

            /// <summary>
            /// Cancels any pending timer and starts a new debounce timer for the received element.
            /// </summary>
            /// <param name="value">The element to potentially forward after the debounce period.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A completed task; the actual forwarding happens asynchronously after the delay.</returns>
            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                long currentId;
                lock (_gate)
                {
                    _timerCts?.Cancel();
                    _timerCts?.Dispose();
                    _timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    currentId = ++_id;
                }

                var cts = _timerCts;
                _ = FireAfterDelayAsync(value, currentId, cts.Token);
                return default;
            }

            /// <summary>
            /// Cancels any pending timer and forwards the error to the downstream observer.
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
            /// Cancels any pending timer and forwards completion to the downstream observer.
            /// </summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            protected override ValueTask OnCompletedAsyncCore(Result result)
            {
                CancelTimer();
                return observer.OnCompletedAsync(result);
            }

            /// <summary>
            /// Cancels any pending timer during disposal.
            /// </summary>
            /// <returns>A completed task.</returns>
            protected override ValueTask DisposeAsyncCore()
            {
                CancelTimer();
                return default;
            }
        }
    }
}
