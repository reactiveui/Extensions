// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Buffers elements and emits them when the stream has been idle for a specified duration.
/// </summary>
/// <typeparam name="T">The type of elements in the source sequence.</typeparam>
/// <param name="source">The source observable.</param>
/// <param name="idleTime">The duration of inactivity required to flush the buffer.</param>
/// <param name="scheduler">The scheduler to run the idle timer on.</param>
internal sealed class BufferUntilIdleObservable<T>(
    IObservable<T> source,
    TimeSpan idleTime,
    IScheduler scheduler) : IObservable<IList<T>>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<IList<T>> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(source);
        InvalidOperationExceptionHelper.ThrowIfNull(scheduler);
        ArgumentExceptionHelper.ThrowIfNull(observer);

        var sink = new BufferUntilIdleSink(observer, idleTime, scheduler);
        var subscription = source.Subscribe(sink);
        return new DisposableBag(subscription, sink);
    }

    /// <summary>
    /// Sink that manages the buffer and idle timer.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="idleTime">The idle time period.</param>
    /// <param name="scheduler">The scheduler.</param>
    private sealed class BufferUntilIdleSink(
        IObserver<IList<T>> downstream,
        TimeSpan idleTime,
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

        /// <summary>The current buffer of elements.</summary>
        private List<T> _buffer = [];

        /// <summary>Whether the sink has reached a terminal state.</summary>
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

        /// <summary>Schedules a flush after the idle period.</summary>
        private void ScheduleFlush() => _timer.Disposable = scheduler.Schedule(idleTime, Flush);

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

            if (toEmit == null)
            {
                return;
            }

            downstream.OnNext(toEmit);
        }
    }
}
