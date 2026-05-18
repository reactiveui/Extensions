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
    /// Sink that manages staleness detection. Composes <see cref="TimerSinkState{T}"/> for the
    /// shared gate / timer / done-flag plumbing so this class only carries the OnNext / schedule logic.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="stalenessPeriod">The staleness period.</param>
    /// <param name="scheduler">The scheduler.</param>
    private sealed class DetectStaleSink(
        IObserver<Stale<T>> downstream,
        TimeSpan stalenessPeriod,
        IScheduler scheduler) : IObserver<T>, IDisposable
    {
        /// <summary>Shared gate / timer / done-flag plumbing.</summary>
        private readonly TimerSinkState<Stale<T>> _state = new(downstream);

        /// <summary>Initializes the staleness timer.</summary>
        public void Initialize() => ScheduleStale();

        /// <inheritdoc/>
        public void OnNext(T value)
        {
            lock (_state.Gate)
            {
                if (_state.Done)
                {
                    return;
                }

                downstream.OnNext(new Stale<T>(value));
                ScheduleStale();
            }
        }

        /// <inheritdoc/>
        public void OnError(Exception error) => _state.HandleError(error);

        /// <inheritdoc/>
        public void OnCompleted() => _state.HandleCompleted();

        /// <inheritdoc/>
        public void Dispose() => _state.HandleDispose();

        /// <summary>Schedules the staleness notification.</summary>
        private void ScheduleStale() =>
            _state.Timer.Disposable = scheduler.Schedule(stalenessPeriod, () =>
            {
                lock (_state.Gate)
                {
                    if (!_state.Done)
                    {
                        downstream.OnNext(new Stale<T>());
                    }
                }
            });
    }
}
