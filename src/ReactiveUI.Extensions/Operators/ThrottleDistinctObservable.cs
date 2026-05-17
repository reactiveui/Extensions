// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Throttles a sequence and ensures only distinct values are emitted.
/// </summary>
/// <typeparam name="T">The type of elements in the source sequence.</typeparam>
/// <param name="source">The source observable.</param>
/// <param name="throttle">The throttle duration.</param>
/// <param name="scheduler">The scheduler to use for timing.</param>
internal sealed class ThrottleDistinctObservable<T>(
    IObservable<T> source,
    TimeSpan throttle,
    IScheduler scheduler) : IObservable<T>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(source);
        InvalidOperationExceptionHelper.ThrowIfNull(scheduler);
        ArgumentExceptionHelper.ThrowIfNull(observer);

        // Implementation of .DistinctUntilChanged().Throttle(throttle, scheduler).DistinctUntilChanged()
        // But fused into a single sink to avoid multiple operator allocations and observer chains.
        var sink = new ThrottleDistinctSink(observer, throttle, scheduler);
        var subscription = source.Subscribe(sink);
        return new DisposableBag(subscription, sink);
    }

    /// <summary>
    /// Sink that implements the throttle distinct logic.
    /// </summary>
    /// <param name="downstream">The observer to forward elements to.</param>
    /// <param name="throttle">The throttle duration.</param>
    /// <param name="scheduler">The scheduler to use for timing.</param>
    private sealed class ThrottleDistinctSink(
        IObserver<T> downstream,
        TimeSpan throttle,
        IScheduler scheduler) : IObserver<T>, IDisposable
    {
#if NET9_0_OR_GREATER
        /// <summary>
        /// The gate to synchronize access to the sink state.
        /// </summary>
        private readonly Lock _gate = new();
#else
        /// <summary>
        /// The gate to synchronize access to the sink state.
        /// </summary>
        private readonly object _gate = new();
#endif

        /// <summary>
        /// The timer for throttling.
        /// </summary>
        private readonly SwapDisposable _timer = new();

        /// <summary>
        /// The last emitted value.
        /// </summary>
        private T? _lastEmitted;

        /// <summary>
        /// The last received value.
        /// </summary>
        private T? _lastReceived;

        /// <summary>
        /// Whether a value has been received but not yet emitted.
        /// </summary>
        private bool _hasLastReceived;

        /// <summary>
        /// Whether any value has been emitted yet.
        /// </summary>
        private bool _hasLastEmitted;

        /// <summary>
        /// Whether the sink has finished.
        /// </summary>
        private bool _done;

        /// <inheritdoc/>
        public void OnNext(T value)
        {
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                if (_hasLastEmitted && EqualityComparer<T>.Default.Equals(value, _lastEmitted!))
                {
                    _hasLastReceived = false;
                    _timer.Disposable = null;
                    return;
                }

                _lastReceived = value;
                _hasLastReceived = true;
                _timer.Disposable = scheduler.Schedule(throttle, Emit);
            }
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                _done = true;
                _timer.Dispose();
                downstream.OnError(error);
            }
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                _done = true;
                _timer.Dispose();
                downstream.OnCompleted();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_gate)
            {
                _done = true;
                _timer.Dispose();
            }
        }

        /// <summary>
        /// Emits the last received value if it differs from the last emitted value.
        /// </summary>
        private void Emit()
        {
            T? toEmit;
            lock (_gate)
            {
                if (_done || !_hasLastReceived)
                {
                    return;
                }

                toEmit = _lastReceived;
                _lastEmitted = toEmit;
                _hasLastEmitted = true;
                _hasLastReceived = false;
            }

            downstream.OnNext(toEmit!);
        }
    }
}
