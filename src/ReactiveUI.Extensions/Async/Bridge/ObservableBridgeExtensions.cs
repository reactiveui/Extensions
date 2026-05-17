// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides bridge extension methods to convert between <see cref="IObservable{T}"/> (System.Reactive)
/// and <see cref="IObservableAsync{T}"/> (async observable) sequences.
/// </summary>
/// <remarks>These methods enable bi-directional interoperability, allowing synchronous and asynchronous
/// reactive streams to be used together in the same code base. Use <see cref="ToObservableAsync{T}(IObservable{T})"/>
/// to wrap a classic observable as an async observable, and <see cref="ToObservable{T}(IObservableAsync{T})"/>
/// to expose an async observable as a classic <see cref="IObservable{T}"/>.</remarks>
public static class ObservableBridgeExtensions
{
    /// <summary>
    /// Converts a classic <see cref="IObservable{T}"/> sequence into an <see cref="ObservableAsync{T}"/>
    /// that forwards all notifications through asynchronous observer callbacks.
    /// </summary>
    /// <remarks>
    /// <para>The returned async observable subscribes to the source <see cref="IObservable{T}"/> when an
    /// async observer subscribes. Because <see cref="IObservable{T}"/> notifications are synchronous,
    /// each OnNext/OnError/OnCompleted callback is awaited sequentially before the next notification
    /// is processed.</para>
    /// <para>Disposing the async subscription also disposes the underlying <see cref="IDisposable"/>
    /// subscription to the source observable.</para>
    /// </remarks>
    /// <typeparam name="T">The type of elements in the observable sequence.</typeparam>
    /// <param name="source">The classic observable sequence to bridge. Cannot be null.</param>
    /// <returns>An <see cref="ObservableAsync{T}"/> that mirrors the source observable sequence.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is null.</exception>
    [SuppressMessage(
        "Roslynator",
        "RCS1047:Non-asynchronous method name should not end with \'Async\'",
        Justification = "This is an existing method")]
    public static IObservableAsync<T> ToObservableAsync<T>(this IObservable<T> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new ObservableToObservableAsync<T>(source);
    }

    /// <summary>
    /// Converts an <see cref="ObservableAsync{T}"/> sequence into a classic <see cref="IObservable{T}"/>
    /// that can be consumed by System.Reactive operators and subscribers.
    /// </summary>
    /// <remarks>
    /// <para>The returned <see cref="IObservable{T}"/> subscribes to the async observable on each
    /// <see cref="IObservable{T}.Subscribe"/> call. Async OnNext callbacks are awaited sequentially;
    /// the synchronous <see cref="IObserver{T}"/> is notified on the thread that completes each await.</para>
    /// <para>Disposing the <see cref="IDisposable"/> subscription returned by Subscribe disposes the
    /// underlying <see cref="IAsyncDisposable"/> async subscription.</para>
    /// </remarks>
    /// <typeparam name="T">The type of elements in the observable sequence.</typeparam>
    /// <param name="source">The async observable sequence to bridge. Cannot be null.</param>
    /// <returns>An <see cref="IObservable{T}"/> that mirrors the async observable sequence.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is null.</exception>
    public static IObservable<T> ToObservable<T>(this IObservableAsync<T> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new ObservableAsyncToObservable<T>(source);
    }

    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> into the async observable world.
    /// </summary>
    /// <typeparam name="T">The element type of the observable sequence.</typeparam>
    internal sealed class ObservableToObservableAsync<T>(IObservable<T> source) : ObservableAsync<T>
    {
        /// <summary>
        /// Kind discriminator for queued bridge notifications.
        /// </summary>
        internal enum WorkKind
        {
            /// <summary>OnNext with a value.</summary>
            Next,

            /// <summary>OnCompleted with success.</summary>
            CompletedSuccess,

            /// <summary>OnCompleted with failure.</summary>
            CompletedFailure,
        }

        /// <summary>
        /// Subscribes an async observer to the underlying synchronous observable, bridging notifications asynchronously.
        /// </summary>
        /// <param name="observer">The async observer to receive bridged notifications.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>An async disposable that disposes the underlying synchronous subscription.</returns>
        [SuppressMessage(
            "Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "Disposed by the async disposable returned to the caller")]
        protected override ValueTask<IAsyncDisposable> SubscribeAsyncCore(
            IObserverAsync<T> observer,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new(DisposableAsync.Empty);
            }

            var bridgeObserver = new BridgeObserver(observer, cancellationToken);
            var subscription = source.Subscribe(bridgeObserver);
            var asyncDisposable = DisposableAsync.Create(() =>
            {
                subscription.Dispose();
                return default;
            });
            return new(asyncDisposable);
        }

        /// <summary>
        /// Discriminated payload for one queued sync→async notification. Replaces the per-emission
        /// <c>Func&lt;Task&gt;</c> + outer <c>Action</c> + <c>.AsTask()</c> trio with a single struct enqueued
        /// by value.
        /// </summary>
        /// <param name="Kind">The notification kind.</param>
        /// <param name="Value">The element forwarded by <see cref="WorkKind.Next"/>; default otherwise.</param>
        /// <param name="Error">The error forwarded by <see cref="WorkKind.CompletedFailure"/>; null otherwise.</param>
        internal readonly record struct WorkItem(WorkKind Kind, T Value, Exception? Error);

        /// <summary>
        /// Synchronous observer that forwards to an async observer, queuing items to ensure sequential delivery.
        /// </summary>
        internal sealed class BridgeObserver(IObserverAsync<T> observer, CancellationToken cancellationToken)
            : IObserver<T>
        {
            /// <summary>
            /// Synchronization gate protecting the work queue and busy flag.
            /// </summary>
#if NET9_0_OR_GREATER
            private readonly Lock _gate = new();
#else
            private readonly object _gate = new();
#endif

            /// <summary>
            /// Queue of pending work items to be drained sequentially.
            /// </summary>
            private readonly Queue<WorkItem> _queue = new();

            /// <summary>
            /// Indicates whether a drain loop is currently executing.
            /// </summary>
            private bool _busy;

            /// <summary>
            /// Enqueues a forwarding of the element to the async observer.
            /// </summary>
            /// <param name="value">The element to forward.</param>
            public void OnNext(T value) => Enqueue(new WorkItem(WorkKind.Next, value, null));

            /// <summary>
            /// Enqueues a failure completion on the async observer.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            public void OnError(Exception error) =>
                Enqueue(new WorkItem(WorkKind.CompletedFailure, default!, error));

            /// <summary>
            /// Enqueues a successful completion on the async observer.
            /// </summary>
            public void OnCompleted() => Enqueue(new WorkItem(WorkKind.CompletedSuccess, default!, null));

            /// <summary>
            /// Converts a <see cref="ValueTask"/> to a <see cref="Task"/> so the bridge can synchronously
            /// await it via <c>GetAwaiter().GetResult()</c>. Routing the conversion through a helper keeps the
            /// analyzer's single-consumption analysis local and avoids per-emission false positives.
            /// </summary>
            /// <param name="valueTask">The <see cref="ValueTask"/> to convert.</param>
            /// <returns>A <see cref="Task"/> that represents the same operation.</returns>
            internal static Task ToTask(ValueTask valueTask) => valueTask.AsTask();

            /// <summary>
            /// Synchronously dispatches one queued work item to the async observer, routing exceptions to the
            /// unhandled exception handler.
            /// </summary>
            /// <param name="work">The queued notification to dispatch.</param>
            [SuppressMessage(
                "Major Bug",
                "S4462:Calls to async methods should not be blocking",
                Justification = "This is the explicit serialization point that drains async work onto the synchronous IObserver<T> pump; the whole purpose of the method is to bridge async→sync.")]
            internal void Dispatch(WorkItem work)
            {
                try
                {
                    Task task;
                    switch (work.Kind)
                    {
                        case WorkKind.Next:
                        {
                            task = ToTask(observer.OnNextAsync(work.Value, cancellationToken));
                            break;
                        }

                        case WorkKind.CompletedSuccess:
                        {
                            task = ToTask(observer.OnCompletedAsync(Result.Success));
                            break;
                        }

                        case WorkKind.CompletedFailure:
                        {
                            task = ToTask(observer.OnCompletedAsync(Result.Failure(work.Error!)));
                            break;
                        }

                        default:
                            return;
                    }

                    task.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    // Subscription was cancelled
                }
                catch (Exception e)
                {
                    UnhandledExceptionHandler.OnUnhandledException(e);
                }
            }

            /// <summary>
            /// Enqueues the specified work item and starts draining if not already in progress.
            /// </summary>
            /// <param name="work">The work item to enqueue.</param>
            internal void Enqueue(WorkItem work)
            {
                lock (_gate)
                {
                    _queue.Enqueue(work);
                    if (_busy)
                    {
                        return;
                    }

                    _busy = true;
                }

                DrainQueue();
            }

            /// <summary>
            /// Drains the work queue, executing each action sequentially until the queue is empty.
            /// </summary>
            internal void DrainQueue()
            {
                while (true)
                {
                    WorkItem work;
                    lock (_gate)
                    {
                        if (_queue.Count == 0)
                        {
                            _busy = false;
                            return;
                        }

                        work = _queue.Dequeue();
                    }

                    Dispatch(work);
                }
            }
        }
    }

    /// <summary>
    /// Bridges an <see cref="ObservableAsync{T}"/> into the classic <see cref="IObservable{T}"/> world.
    /// </summary>
    /// <typeparam name="T">The element type of the observable sequence.</typeparam>
    internal sealed class ObservableAsyncToObservable<T>(IObservableAsync<T> source) : IObservable<T>
    {
        /// <summary>
        /// Subscribes a synchronous observer by bridging from the underlying async observable.
        /// </summary>
        /// <param name="observer">The synchronous observer to receive notifications.</param>
        /// <returns>A disposable that tears down the async subscription when disposed.</returns>
        [SuppressMessage(
            "Major Bug",
            "S4462:Calls to async methods should not be blocking",
            Justification = "The IDisposable.Dispose callback is intrinsically synchronous and must tear down the async subscription it bridged on subscribe.")]
        public IDisposable Subscribe(IObserver<T> observer)
        {
            ArgumentExceptionHelper.ThrowIfNull(observer);

            var cts = new CancellationTokenSource();
            var asyncObserver = new BridgeAsyncObserver(observer);
            var subscriptionTask = SubscribeAndCaptureAsync(asyncObserver, cts.Token);

            return new ActionDisposable(() =>
            {
                cts.Cancel();

                try
                {
                    var task = subscriptionTask;
                    if (task.IsCompleted)
                    {
                        if (task.Result is { } subscription)
                        {
                            subscription.DisposeAsync().AsTask().GetAwaiter().GetResult();
                        }
                    }
                    else
                    {
                        var subscription = task.GetAwaiter().GetResult();
                        subscription?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during cancellation
                }
                catch (Exception e)
                {
                    UnhandledExceptionHandler.OnUnhandledException(e);
                }
                finally
                {
                    cts.Dispose();
                }
            });
        }

        /// <summary>
        /// Subscribes the bridge observer to the async source, capturing exceptions to avoid unobserved task faults.
        /// </summary>
        /// <param name="observer">The bridge observer to subscribe.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>The async disposable subscription, or <see langword="null"/> if the subscription failed or was cancelled.</returns>
        private async Task<IAsyncDisposable?> SubscribeAndCaptureAsync(
            BridgeAsyncObserver observer,
            CancellationToken cancellationToken)
        {
            try
            {
                return await source.SubscribeAsync(observer, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception e)
            {
                UnhandledExceptionHandler.OnUnhandledException(e);
                return null;
            }
        }

        /// <summary>
        /// Async observer that forwards notifications to a classic <see cref="IObserver{T}"/>.
        /// </summary>
        internal sealed class BridgeAsyncObserver(IObserver<T> observer) : ObserverAsync<T>
        {
            /// <summary>
            /// Forwards the element to the synchronous observer.
            /// </summary>
            /// <param name="value">The element to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A completed task.</returns>
            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                observer.OnNext(value);
                return default;
            }

            /// <summary>
            /// Forwards a non-fatal error to the synchronous observer as an OnError call.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A completed task.</returns>
            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            {
                observer.OnError(error);
                return default;
            }

            /// <summary>
            /// Forwards completion or failure to the synchronous observer.
            /// </summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A completed task.</returns>
            protected override ValueTask OnCompletedAsyncCore(Result result)
            {
                if (result.IsFailure)
                {
                    observer.OnError(result.Exception);
                }
                else
                {
                    observer.OnCompleted();
                }

                return default;
            }
        }
    }
}
