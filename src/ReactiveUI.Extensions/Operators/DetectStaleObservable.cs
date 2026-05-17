// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Detects when a sequence becomes stale (no emissions for a specified period).
/// </summary>
/// <typeparam name="T">The type of elements in the source sequence.</typeparam>
/// <param name="source">The source observable.</param>
/// <param name="stalenessPeriod">The period after which the sequence is considered stale.</param>
/// <param name="scheduler">The scheduler to run the staleness timer on.</param>
internal sealed class DetectStaleObservable<T>(
    IObservable<T> source,
    TimeSpan stalenessPeriod,
    IScheduler scheduler) : IObservable<Stale<T>>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<Stale<T>> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(source);
        InvalidOperationExceptionHelper.ThrowIfNull(scheduler);
        ArgumentExceptionHelper.ThrowIfNull(observer);

        var sink = new DetectStaleSink(observer, stalenessPeriod, scheduler);
        var sub = source.Subscribe(sink);
        sink.Initialize();
        return new DisposableBag(sub, sink);
    }

    /// <summary>
    /// Sink that manages staleness detection.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="stalenessPeriod">The staleness period.</param>
    /// <param name="scheduler">The scheduler.</param>
    private sealed class DetectStaleSink(
        IObserver<Stale<T>> downstream,
        TimeSpan stalenessPeriod,
        IScheduler scheduler) : IObserver<T>, IDisposable
    {
#if NET9_0_OR_GREATER
        /// <summary>The gate for state access.</summary>
        private readonly Lock _gate = new();
#else
        /// <summary>The gate for state access.</summary>
        private readonly object _gate = new();
#endif

        /// <summary>The timer for staleness notification.</summary>
        private readonly SwapDisposable _timer = new();

        /// <summary>Whether the sink has reached a terminal state.</summary>
        private bool _done;

        /// <summary>Initializes the staleness timer.</summary>
        public void Initialize() => ScheduleStale();

        /// <inheritdoc/>
        public void OnNext(T value)
        {
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                downstream.OnNext(new Stale<T>(value));
                ScheduleStale();
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

        /// <summary>Schedules the staleness notification.</summary>
        private void ScheduleStale() =>
            _timer.Disposable = scheduler.Schedule(stalenessPeriod, () =>
            {
                lock (_gate)
                {
                    if (!_done)
                    {
                        downstream.OnNext(new Stale<T>());
                    }
                }
            });
    }
}
