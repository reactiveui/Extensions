// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

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
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero, nameof(timeout));
            ArgumentNullException.ThrowIfNull(fallback, nameof(fallback));
#else
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            if (fallback is null)
            {
                throw new ArgumentNullException(nameof(fallback));
            }
#endif

            return new TimeoutWithFallbackObservable<T>(@this, timeout, fallback, timeProvider ?? TimeProvider.System);
        }
    }

    private sealed class TimeoutObservable<T>(IObservableAsync<T> source, TimeSpan timeout, TimeProvider timeProvider) : ObservableAsync<T>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var timeoutObserver = new TimeoutObserver(observer, timeout, timeProvider);
            var subscription = await source.SubscribeAsync(timeoutObserver, cancellationToken);
            timeoutObserver.StartTimer(cancellationToken);
            return subscription;
        }

        private sealed class TimeoutObserver(IObserverAsync<T> observer, TimeSpan timeout, TimeProvider timeProvider) : ObserverAsync<T>
        {
#if NET9_0_OR_GREATER
            private readonly Lock _gate = new();
#else
            private readonly object _gate = new();
#endif
            private CancellationTokenSource? _timerCts;
            private long _id;
            private bool _completed;

            internal void StartTimer(CancellationToken cancellationToken) => ResetTimer(cancellationToken);

            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                ResetTimer(cancellationToken);
                return observer.OnNextAsync(value, cancellationToken);
            }

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            {
                CancelTimer();
                return observer.OnErrorResumeAsync(error, cancellationToken);
            }

            protected override ValueTask OnCompletedAsyncCore(Result result)
            {
                lock (_gate)
                {
                    _completed = true;
                }

                CancelTimer();
                return observer.OnCompletedAsync(result);
            }

            protected override ValueTask DisposeAsyncCore()
            {
                CancelTimer();
                return default;
            }

            private void ResetTimer(CancellationToken cancellationToken)
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

            private void CancelTimer()
            {
                lock (_gate)
                {
                    _timerCts?.Cancel();
                    _timerCts?.Dispose();
                    _timerCts = null;
                }
            }

            private async Task OnTimeoutAsync(long id, CancellationToken cancellationToken)
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
        }
    }

    private sealed class TimeoutWithFallbackObservable<T>(IObservableAsync<T> source, TimeSpan timeout, IObservableAsync<T> fallback, TimeProvider timeProvider) : ObservableAsync<T>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            // Wrap with Catch to switch to fallback on TimeoutException
            var withTimeout = new TimeoutObservable<T>(source, timeout, timeProvider);
            var withFallback = withTimeout.Catch(ex => ex is TimeoutException ? fallback : Throw<T>(ex));
            return await withFallback.SubscribeAsync(observer.Wrap(), cancellationToken);
        }
    }
}
