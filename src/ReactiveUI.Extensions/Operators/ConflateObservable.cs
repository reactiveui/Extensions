// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Conflates an observable stream by delaying updates that occur within a minimum period.
/// </summary>
/// <typeparam name="T">The type of elements in the source sequence.</typeparam>
/// <param name="source">The source observable.</param>
/// <param name="minimumUpdatePeriod">The minimum period between emissions.</param>
/// <param name="scheduler">The scheduler to run the conflation on.</param>
internal sealed class ConflateObservable<T>(
    IObservable<T> source,
    TimeSpan minimumUpdatePeriod,
    IScheduler scheduler) : IObservable<T>
{
    /// <summary>Notification kind enqueued by the scheduler marshaller.</summary>
    private enum NotificationKind
    {
        /// <summary>OnNext with a value.</summary>
        Next,

        /// <summary>OnError with an exception.</summary>
        Error,

        /// <summary>OnCompleted (no value).</summary>
        Completed,
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(source);
        InvalidOperationExceptionHelper.ThrowIfNull(scheduler);
        ArgumentExceptionHelper.ThrowIfNull(observer);

        var sink = new ConflateSink(observer, minimumUpdatePeriod, scheduler);
        var marshaller = new SchedulerMarshaller(sink, scheduler);
        var subscription = source.Subscribe(marshaller);
        return new DisposableBag(subscription, marshaller, sink);
    }

    /// <summary>
    /// Discriminated payload enqueued by <see cref="SchedulerMarshaller"/>. Replaces the per-emission
    /// closure / delegate trio that the previous <c>source.ObserveOn(scheduler).Subscribe(sink)</c>
    /// allocated.
    /// </summary>
    /// <param name="Kind">The notification kind.</param>
    /// <param name="Value">The element carried by <see cref="NotificationKind.Next"/>; default otherwise.</param>
    /// <param name="Error">The error carried by <see cref="NotificationKind.Error"/>; null otherwise.</param>
    private readonly record struct Notification(NotificationKind Kind, T Value, Exception? Error);

    /// <summary>
    /// FIFO scheduler marshaller that replaces <c>source.ObserveOn(scheduler)</c>. Each upstream
    /// notification is enqueued and a single drain action is scheduled on the scheduler; the drain
    /// runs every queued notification in order before yielding. New notifications arriving during a
    /// drain are picked up by the same drain pass.
    /// </summary>
    /// <param name="downstream">The downstream observer to forward notifications to.</param>
    /// <param name="scheduler">The scheduler used to dispatch the drain.</param>
    private sealed class SchedulerMarshaller(IObserver<T> downstream, IScheduler scheduler)
        : IObserver<T>, IDisposable
    {
#if NET9_0_OR_GREATER
        /// <summary>Protects <see cref="_queue"/>, <see cref="_draining"/>, and <see cref="_disposed"/>.</summary>
        private readonly Lock _gate = new();
#else
        /// <summary>Protects <see cref="_queue"/>, <see cref="_draining"/>, and <see cref="_disposed"/>.</summary>
        private readonly object _gate = new();
#endif

        /// <summary>The FIFO queue of pending notifications.</summary>
        private readonly Queue<Notification> _queue = new();

        /// <summary><c>true</c> while a drain pass is in flight.</summary>
        private bool _draining;

        /// <summary><c>true</c> after disposal.</summary>
        private bool _disposed;

        /// <inheritdoc/>
        public void OnNext(T value) => Enqueue(new Notification(NotificationKind.Next, value, null));

        /// <inheritdoc/>
        public void OnError(Exception error) => Enqueue(new Notification(NotificationKind.Error, default!, error));

        /// <inheritdoc/>
        public void OnCompleted() => Enqueue(new Notification(NotificationKind.Completed, default!, null));

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _queue.Clear();
            }
        }

        /// <summary>
        /// Enqueues a notification and, if no drain is already in flight, schedules one onto the
        /// configured scheduler.
        /// </summary>
        /// <param name="notification">The notification to forward.</param>
        private void Enqueue(Notification notification)
        {
            bool scheduleDrain;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _queue.Enqueue(notification);
                scheduleDrain = !_draining;
                if (scheduleDrain)
                {
                    _draining = true;
                }
            }

            if (!scheduleDrain)
            {
                return;
            }

            scheduler.Schedule(this, static (_, self) =>
            {
                self.Drain();
                return EmptyDisposable.Instance;
            });
        }

        /// <summary>
        /// Drains every queued notification synchronously inside one scheduler callback. Terminal
        /// notifications (OnError / OnCompleted) end the drain and leave <see cref="_draining"/>
        /// <c>true</c> so no further drains are scheduled.
        /// </summary>
        private void Drain()
        {
            while (true)
            {
                Notification notification;
                lock (_gate)
                {
                    if (_disposed || _queue.Count == 0)
                    {
                        _draining = false;
                        return;
                    }

                    notification = _queue.Dequeue();
                }

                switch (notification.Kind)
                {
                    case NotificationKind.Next:
                    {
                        downstream.OnNext(notification.Value);
                        break;
                    }

                    case NotificationKind.Error:
                    {
                        downstream.OnError(notification.Error!);
                        return;
                    }

                    case NotificationKind.Completed:
                    {
                        downstream.OnCompleted();
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sink for the conflate operator.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="minimumUpdatePeriod">The minimum period between emissions.</param>
    /// <param name="scheduler">The scheduler to run the conflation on.</param>
    private sealed class ConflateSink(
        IObserver<T> downstream,
        TimeSpan minimumUpdatePeriod,
        IScheduler scheduler) : IObserver<T>, IDisposable
    {
#if NET9_0_OR_GREATER
        /// <summary>
        /// The gate to synchronize access to the state.
        /// </summary>
        private readonly Lock _gate = new();
#else
        /// <summary>
        /// The gate to synchronize access to the state.
        /// </summary>
        private readonly object _gate = new();
#endif

        /// <summary>
        /// The disposable for the scheduled update.
        /// </summary>
        private readonly MutableDisposable _updateScheduled = new();

        /// <summary>
        /// The last time an update was sent.
        /// </summary>
        private DateTimeOffset _lastUpdateTime = DateTimeOffset.MinValue;

        /// <summary>
        /// Whether a completion has been requested.
        /// </summary>
        private bool _completionRequested;

        /// <summary>
        /// Whether the sink is done.
        /// </summary>
        private bool _done;

        /// <inheritdoc/>
        public void OnNext(T value)
        {
            var currentUpdateTime = scheduler.Now;
            bool scheduleRequired;

            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                scheduleRequired = currentUpdateTime - _lastUpdateTime < minimumUpdatePeriod;
                if (scheduleRequired && _updateScheduled.Disposable != null)
                {
                    _updateScheduled.Disposable.Dispose();
                    _updateScheduled.Disposable = null;
                }
            }

            if (scheduleRequired)
            {
                _updateScheduled.Disposable = scheduler.Schedule(
                    _lastUpdateTime + minimumUpdatePeriod,
                    () =>
                    {
                        downstream.OnNext(value);

                        lock (_gate)
                        {
                            _lastUpdateTime = scheduler.Now;
                            _updateScheduled.Disposable = null;
                            if (_completionRequested)
                            {
                                _done = true;
                                downstream.OnCompleted();
                            }
                        }
                    });
            }
            else
            {
                downstream.OnNext(value);
                lock (_gate)
                {
                    _lastUpdateTime = scheduler.Now;
                }
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
                _updateScheduled.Dispose();
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

                if (_updateScheduled.Disposable != null)
                {
                    _completionRequested = true;
                }
                else
                {
                    _done = true;
                    downstream.OnCompleted();
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_gate)
            {
                _done = true;
                _updateScheduled.Dispose();
            }
        }
    }
}
