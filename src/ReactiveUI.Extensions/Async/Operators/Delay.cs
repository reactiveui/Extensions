// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides Delay extension methods for asynchronous observable sequences.
/// </summary>
/// <remarks>Delay time-shifts the observable sequence by the specified time span. Each element is
/// emitted after a relative delay from the time it was produced by the source. Errors and completion
/// are not delayed.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Time-shifts the observable sequence by the specified time span. Each element notification
        /// is delayed by the specified duration.
        /// </summary>
        /// <param name="delay">The time span by which to delay each element notification. Must be non-negative.</param>
        /// <param name="timeProvider">An optional time provider for controlling timing. If null, <see cref="TimeProvider.System"/>
        /// is used.</param>
        /// <returns>An observable sequence with element notifications time-shifted by the specified duration.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="delay"/> is negative.</exception>
        public IObservableAsync<T> Delay(TimeSpan delay, TimeProvider? timeProvider = null)
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            if (delay == TimeSpan.Zero)
            {
                return @this;
            }

            return new DelayObservable<T>(@this, delay, timeProvider ?? TimeProvider.System);
        }
    }

    private sealed class DelayObservable<T>(IObservableAsync<T> source, TimeSpan delay, TimeProvider timeProvider) : ObservableAsync<T>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var delayObserver = new DelayObserver(observer, delay, timeProvider);
            return await source.SubscribeAsync(delayObserver, cancellationToken);
        }

        private sealed class DelayObserver(IObserverAsync<T> observer, TimeSpan delay, TimeProvider timeProvider) : ObserverAsync<T>
        {
            protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                await DelayAsync(delay, timeProvider, cancellationToken);
                await observer.OnNextAsync(value, cancellationToken);
            }

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
                observer.OnErrorResumeAsync(error, cancellationToken);

            protected override ValueTask OnCompletedAsyncCore(Result result) =>
                observer.OnCompletedAsync(result);
        }
    }
}
