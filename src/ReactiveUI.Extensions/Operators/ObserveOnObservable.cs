// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Marshals every source notification onto the supplied <see cref="IScheduler"/>, preserving order.
/// Replaces the <c>System.Reactive.Linq.Observable.ObserveOn</c> delegation behind the sync
/// <c>ObserveOnSafe</c> / <c>ObserveOnIf</c> helpers with our own queue-and-single-drain marshaller:
/// notifications are enqueued and a single drain pass is scheduled per burst (rather than one
/// scheduled action per item), and the drain lambda carries no captures.
/// </summary>
/// <typeparam name="T">The element type of the source sequence.</typeparam>
/// <param name="source">The source observable.</param>
/// <param name="scheduler">The scheduler every notification is delivered on.</param>
internal sealed class ObserveOnObservable<T>(IObservable<T> source, IScheduler scheduler) : IObservable<T>
{
    /// <summary>Notification kind enqueued for the scheduled drain.</summary>
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

        // The immediate scheduler runs scheduled work inline on the calling thread, so the
        // queue-and-drain machinery would be pure overhead: forward straight through.
        if (ReferenceEquals(scheduler, ImmediateScheduler.Instance))
        {
            return source.Subscribe(observer);
        }

        var sink = new ObserveOnSink(observer, scheduler);
        sink.AttachSourceSubscription(source.Subscribe(sink));
        return sink;
    }

    /// <summary>Discriminated notification payload enqueued for the scheduled drain.</summary>
    /// <param name="Kind">The notification kind.</param>
    /// <param name="Value">The element carried by <see cref="NotificationKind.Next"/>; default otherwise.</param>
    /// <param name="Error">The error carried by <see cref="NotificationKind.Error"/>; null otherwise.</param>
    private readonly record struct Notification(NotificationKind Kind, T Value, Exception? Error);

    /// <summary>
    /// Single observer that queues upstream notifications and drains them on the scheduler thread in
    /// FIFO order. Terminal notifications travel through the same queue so they never overtake
    /// still-queued values.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="scheduler">The scheduler notifications are delivered on.</param>
    private sealed class ObserveOnSink(IObserver<T> downstream, IScheduler scheduler) : IObserver<T>, IDisposable
    {
#if NET9_0_OR_GREATER
        /// <summary>The gate protecting queue + draining + done state.</summary>
        private readonly Lock _gate = new();
#else
        /// <summary>The gate protecting queue + draining + done state.</summary>
        private readonly object _gate = new();
#endif

        /// <summary>The FIFO queue of pending upstream notifications.</summary>
        private readonly Queue<Notification> _queue = new();

        /// <summary>Upstream subscription handle, set after <see cref="AttachSourceSubscription"/>.</summary>
        private IDisposable? _sourceSubscription;

        /// <summary><see langword="true"/> while a drain pass is in flight on the scheduler.</summary>
        private bool _draining;

        /// <summary><see langword="true"/> once a terminal notification has been delivered or the sink disposed.</summary>
        private bool _done;

        /// <summary>Records the upstream subscription so <see cref="Dispose"/> can tear it down.</summary>
        /// <param name="subscription">The upstream subscription handle.</param>
        public void AttachSourceSubscription(IDisposable subscription)
        {
            lock (_gate)
            {
                if (!_done)
                {
                    _sourceSubscription = subscription;
                    return;
                }
            }

            subscription.Dispose();
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
            }

            subscription?.Dispose();
        }

        /// <summary>Enqueues a notification; schedules a drain pass if one isn't already running.</summary>
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

        /// <summary>Drains queued notifications on the scheduler thread, delivering each downstream in FIFO order.</summary>
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
                        downstream.OnNext(notification.Value);
                        break;
                    }

                    case NotificationKind.Error:
                    {
                        Terminate();
                        downstream.OnError(notification.Error!);
                        return;
                    }

                    default:
                    {
                        // NotificationKind has only three values; the discard arm absorbs Completed.
                        Terminate();
                        downstream.OnCompleted();
                        return;
                    }
                }
            }
        }

        /// <summary>Marks the sink terminated and drops any still-queued notifications.</summary>
        private void Terminate()
        {
            lock (_gate)
            {
                _done = true;
                _queue.Clear();
            }
        }
    }
}
