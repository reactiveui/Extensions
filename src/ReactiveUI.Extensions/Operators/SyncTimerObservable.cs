// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;
using IScheduler = System.Reactive.Concurrency.IScheduler;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Optimized operator that shares a single timer per <c>(TimeSpan, IScheduler)</c> key.
/// Replaces the manual <c>ConcurrentDictionary&lt;..., Lazy&lt;SharedTimer&gt;&gt;</c> shape with a stateful
/// <c>ConcurrentDictionary.GetOrAdd</c> overload that doesn't allocate a
/// <see cref="Lazy{T}"/> or its factory delegate on the hot path.
/// </summary>
internal static class SyncTimerObservable
{
    /// <summary>The timer cache, keyed by <c>(TimeSpan, IScheduler)</c>.</summary>
    private static readonly ConcurrentDictionary<(TimeSpan TimeSpan, IScheduler Scheduler), SharedTimer>
        _timerList = [];

    /// <summary>Static factory passed to <c>ConcurrentDictionary.GetOrAdd</c>;
    /// avoids a per-call delegate allocation.</summary>
    private static readonly Func<(TimeSpan TimeSpan, IScheduler Scheduler), SharedTimer> _create =
        static key => new SharedTimer(key.TimeSpan, key.Scheduler);

    /// <summary>
    /// Gets a shared timer for the specified period and scheduler.
    /// </summary>
    /// <param name="timeSpan">The period.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>A shared observable sequence of timer ticks.</returns>
    public static IObservable<DateTime> Get(TimeSpan timeSpan, IScheduler scheduler)
    {
        ArgumentExceptionHelper.ThrowIfNull(scheduler);

        return _timerList.GetOrAdd((timeSpan, scheduler), _create);
    }

    /// <summary>
    /// A manual implementation of a connectable timer that minimizes allocations and unrolls Rx chains.
    /// Tick uses a swap-on-write <see cref="IObserver{DateTime}"/> array so the read path is allocation-free
    /// and lock-free; subscribe / unsubscribe takes the gate and publishes a fresh array.
    /// </summary>
    /// <param name="timeSpan">The period.</param>
    /// <param name="scheduler">The scheduler.</param>
    private sealed class SharedTimer(TimeSpan timeSpan, IScheduler scheduler) : IObservable<DateTime>
    {
        /// <summary>Sentinel empty observer array, shared so unsubscribing the last observer doesn't allocate.</summary>
        private static readonly IObserver<DateTime>[] _emptyObservers = [];

        /// <summary>The gate for subscribe/unsubscribe writes.</summary>
#if NET9_0_OR_GREATER
        private readonly Lock _gate = new();
#else
        private readonly object _gate = new();
#endif

        /// <summary>
        /// Snapshot of currently active observers. Replaced (not mutated) on subscribe / unsubscribe under
        /// <see cref="_gate"/>. The tick path reads this via <c>Volatile.Read</c> with no lock and no
        /// allocation.
        /// </summary>
        private IObserver<DateTime>[] _observers = _emptyObservers;

        /// <summary>The active timer subscription, or <see langword="null"/> when no observers are attached.</summary>
        private IDisposable? _timerSubscription;

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<DateTime> observer)
        {
            ArgumentExceptionHelper.ThrowIfNull(observer);

            lock (_gate)
            {
                var current = _observers;
                var copy = new IObserver<DateTime>[current.Length + 1];
                for (var i = 0; i < current.Length; i++)
                {
                    copy[i] = current[i];
                }

                copy[current.Length] = observer;
                Volatile.Write(ref _observers, copy);

                _timerSubscription ??= scheduler.SchedulePeriodic(
                    TimeSpan.Zero,
                    timeSpan,
                    _ => Tick());
            }

            return new TimerSubscription(this, observer);
        }

        /// <summary>Ticks every currently-subscribed observer with the scheduler's current time.</summary>
        private void Tick()
        {
            var targets = Volatile.Read(ref _observers);
            if (targets.Length == 0)
            {
                return;
            }

            var now = scheduler.Now.DateTime;
            for (var i = 0; i < targets.Length; i++)
            {
                targets[i].OnNext(now);
            }
        }

        /// <summary>Removes <paramref name="observer"/> from the observer set, stopping the timer when the set becomes empty.</summary>
        /// <param name="observer">The observer to remove.</param>
        private void Remove(IObserver<DateTime> observer)
        {
            lock (_gate)
            {
                var current = _observers;
                var idx = Array.IndexOf(current, observer);
                if (idx < 0)
                {
                    return;
                }

                if (current.Length == 1)
                {
                    Volatile.Write(ref _observers, _emptyObservers);
                    _timerSubscription?.Dispose();
                    _timerSubscription = null;
                    return;
                }

                var copy = new IObserver<DateTime>[current.Length - 1];
                for (var i = 0; i < idx; i++)
                {
                    copy[i] = current[i];
                }

                for (var i = idx + 1; i < current.Length; i++)
                {
                    copy[i - 1] = current[i];
                }

                Volatile.Write(ref _observers, copy);
            }
        }

        /// <summary>
        /// Per-subscribe disposable. Holding <c>(parent, observer)</c> as fields instead of capturing them in
        /// a lambda removes the per-subscribe closure allocation that <see cref="ActionDisposable"/> would
        /// have required.
        /// </summary>
        /// <param name="parent">The owning timer.</param>
        /// <param name="observer">The observer to remove on dispose.</param>
        private sealed class TimerSubscription(SharedTimer parent, IObserver<DateTime> observer) : IDisposable
        {
            /// <summary>0 = active, 1 = disposed.</summary>
            private int _disposed;

            /// <inheritdoc/>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return;
                }

                parent.Remove(observer);
            }
        }
    }
}
