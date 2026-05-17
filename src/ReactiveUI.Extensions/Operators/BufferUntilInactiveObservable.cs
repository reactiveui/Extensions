// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Operator that buffers items until an inactivity period elapses.
/// Replaces the closure-heavy implementation in ReactiveExtensions.BufferUntilInactive.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="source">The source observable.</param>
/// <param name="inactivityPeriod">The inactivity period.</param>
/// <param name="scheduler">The scheduler.</param>
internal sealed class BufferUntilInactiveObservable<T>(
    IObservable<T> source,
    TimeSpan inactivityPeriod,
    IScheduler scheduler) : IObservable<IList<T>>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<IList<T>> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observer);
        var sink = new BufferUntilInactiveSink(observer, inactivityPeriod, scheduler);
        var sub = source.Subscribe(sink);
        return new DisposableBag(sub, sink);
    }

    /// <summary>
    /// Sink that manages the buffer and inactivity timer.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="inactivityPeriod">The period of inactivity to wait.</param>
    /// <param name="scheduler">The scheduler to run the timer on.</param>
    private sealed class BufferUntilInactiveSink(
        IObserver<IList<T>> downstream,
        TimeSpan inactivityPeriod,
        IScheduler scheduler) : IObserver<T>, IDisposable
    {
#if NET9_0_OR_GREATER
        /// <summary>The gate for buffer access.</summary>
        private readonly Lock _gate = new();
#else
        /// <summary>The gate for buffer access.</summary>
        private readonly object _gate = new();
#endif

        /// <summary>The timer for flushing.</summary>
        private readonly SwapDisposable _timer = new();

        /// <summary>The current buffer.</summary>
        private List<T> _buffer = [];

        /// <summary>Whether the sink is terminal.</summary>
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

                _buffer.Add(value);
                ScheduleFlush();
            }
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            Flush();
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                _done = true;
                downstream.OnError(error);
            }
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
            Flush();
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                _done = true;
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

        /// <summary>Schedules a flush after the inactivity period.</summary>
        private void ScheduleFlush() => _timer.Disposable = scheduler.Schedule(inactivityPeriod, Flush);

        /// <summary>Flushes the current buffer to the downstream observer.</summary>
        private void Flush()
        {
            List<T>? toEmit = null;
            lock (_gate)
            {
                if (_buffer.Count > 0)
                {
                    toEmit = _buffer;
                    _buffer = [];
                }
            }

            if (toEmit is null)
            {
                return;
            }

            downstream.OnNext(toEmit);
        }
    }
}
