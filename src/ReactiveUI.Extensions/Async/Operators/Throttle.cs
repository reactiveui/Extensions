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

    private sealed class ThrottleObservable<T>(IObservableAsync<T> source, TimeSpan dueTime, TimeProvider timeProvider) : ObservableAsync<T>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var throttleObserver = new ThrottleObserver(observer, dueTime, timeProvider);
            return await source.SubscribeAsync(throttleObserver, cancellationToken);
        }

        private sealed class ThrottleObserver(IObserverAsync<T> observer, TimeSpan dueTime, TimeProvider timeProvider) : ObserverAsync<T>
        {
            private readonly object _gate = new();
            private CancellationTokenSource? _timerCts;
            private long _id;

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

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            {
                CancelTimer();
                return observer.OnErrorResumeAsync(error, cancellationToken);
            }

            protected override ValueTask OnCompletedAsyncCore(Result result)
            {
                CancelTimer();
                return observer.OnCompletedAsync(result);
            }

            protected override ValueTask DisposeAsyncCore()
            {
                CancelTimer();
                return default;
            }

            private void CancelTimer()
            {
                lock (_gate)
                {
                    _timerCts?.Cancel();
                    _timerCts?.Dispose();
                    _timerCts = null;
                }
            }

            private async Task FireAfterDelayAsync(T value, long id, CancellationToken cancellationToken)
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
        }
    }
}
