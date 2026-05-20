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
    /// <summary>Notification kind enqueued by the upstream-marshalling path.</summary>
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
        sink.AttachSourceSubscription(source.Subscribe(sink));
        return sink;
    }

    /// <summary>Discriminated payload enqueued by the upstream-marshalling path.</summary>
    /// <param name="Kind">The notification kind.</param>
    /// <param name="Value">The element carried by <see cref="NotificationKind.Next"/>; default otherwise.</param>
    /// <param name="Error">The error carried by <see cref="NotificationKind.Error"/>; null otherwise.</param>
    private readonly record struct Notification(NotificationKind Kind, T Value, Exception? Error);

    /// <summary>
    /// Single observer that combines two previously-distinct concerns into one allocation:
    /// (1) marshals upstream notifications onto the scheduler thread via a FIFO queue and a
    /// scheduled drain, and (2) applies the conflate time-window throttle to each
    /// <see cref="NotificationKind.Next"/> notification. End-user-observable semantics are
    /// unchanged from the prior two-observer implementation.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="minimumUpdatePeriod">The minimum period between emissions.</param>
    /// <param name="scheduler">The scheduler to run the conflation on.</param>
    internal sealed class ConflateSink(
        IObserver<T> downstream,
        TimeSpan minimumUpdatePeriod,
        IScheduler scheduler) : IObserver<T>, IDisposable
    {
#if NET9_0_OR_GREATER
        /// <summary>The gate protecting queue + draining + throttle state.</summary>
        private readonly Lock _gate = new();
#else
        /// <summary>The gate protecting queue + draining + throttle state.</summary>
        private readonly object _gate = new();
#endif

        /// <summary>The FIFO queue of pending upstream notifications.</summary>
        private readonly Queue<Notification> _queue = new();

        /// <summary>The disposable tracking a scheduled deferred emission.</summary>
        private readonly MutableDisposable _updateScheduled = new();

        /// <summary>Upstream subscription handle, set after <see cref="AttachSourceSubscription"/>.</summary>
        private IDisposable? _sourceSubscription;

        /// <summary>Wall-clock timestamp of the last emission forwarded downstream.</summary>
        private DateTimeOffset _lastUpdateTime = DateTimeOffset.MinValue;

        /// <summary><see langword="true"/> while a drain pass is in flight on the scheduler.</summary>
        private bool _draining;

        /// <summary><see langword="true"/> when an upstream OnCompleted is queued but a deferred
        /// emission is still pending; the completion fires after that emission lands.</summary>
        private bool _completionRequested;

        /// <summary><see langword="true"/> once a terminal notification has reached downstream
        /// or the sink has been disposed.</summary>
        private bool _done;

        /// <summary>Records the upstream subscription so <see cref="Dispose"/> can tear it down.
        /// Caller invokes this once after <c>source.Subscribe(this)</c> returns.</summary>
        /// <param name="subscription">The upstream subscription handle.</param>
        public void AttachSourceSubscription(IDisposable subscription)
        {
            lock (_gate)
            {
                if (_done)
                {
                    subscription.Dispose();
                    return;
                }

                _sourceSubscription = subscription;
            }
        }

        /// <inheritdoc/>
        public void OnNext(T value) => Enqueue(new Notification(NotificationKind.Next, value, null));

        /// <inheritdoc/>
        public void OnError(Exception error) => Enqueue(new Notification(NotificationKind.Error, default!, error));

        /// <inheritdoc/>
        public void OnCompleted() => Enqueue(new Notification(NotificationKind.Completed, default!, null));

        /// <inheritdoc/>
        public void Dispose()
        {
            IDisposable? subscription;
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                _done = true;
                _queue.Clear();
                subscription = _sourceSubscription;
                _sourceSubscription = null;
                _updateScheduled.Dispose();
            }

            subscription?.Dispose();
        }

        /// <summary>Enqueues an upstream notification; schedules a drain if one isn't already running.</summary>
        /// <param name="notification">The notification to forward to the drain loop.</param>
        private void Enqueue(Notification notification)
        {
            bool scheduleDrain;
            lock (_gate)
            {
                if (_done)
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

        /// <summary>Drains queued upstream notifications on the scheduler thread, applying the
        /// conflate time-window throttle to <see cref="NotificationKind.Next"/> entries.</summary>
        private void Drain()
        {
            while (true)
            {
                Notification notification;
                lock (_gate)
                {
                    if (_done || _queue.Count == 0)
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
                        ProcessNext(notification.Value);
                        break;
                    }

                    case NotificationKind.Error:
                    {
                        ForwardError(notification.Error!);
                        return;
                    }

                    default:
                    {
                        // NotificationKind has only three values; the discard arm absorbs
                        // Completed so the compiler sees an exhaustive switch.
                        ForwardCompleted();
                        return;
                    }
                }
            }
        }

        /// <summary>Applies the throttle-window decision to a dequeued value and either emits
        /// inline or schedules a deferred emission.</summary>
        /// <param name="value">The value to forward.</param>
        private void ProcessNext(T value)
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

        /// <summary>Forwards an error to downstream and terminates the sink.</summary>
        /// <param name="error">The error to forward.</param>
        private void ForwardError(Exception error)
        {
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                _done = true;
                _updateScheduled.Dispose();
            }

            downstream.OnError(error);
        }

        /// <summary>Forwards completion, deferring if a throttled emission is still scheduled.</summary>
        private void ForwardCompleted()
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
                    return;
                }

                _done = true;
            }

            downstream.OnCompleted();
        }
    }
}
