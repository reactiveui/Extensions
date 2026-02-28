// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

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
    public static IObservableAsync<T> ToObservableAsync<T>(this IObservable<T> source)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source, nameof(source));
#else
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
#endif

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
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source, nameof(source));
#else
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
#endif

        return new ObservableAsyncToObservable<T>(source);
    }

    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> into the async observable world.
    /// </summary>
    private sealed class ObservableToObservableAsync<T>(IObservable<T> source) : ObservableAsync<T>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposed by the async disposable returned to the caller")]
        protected override ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<IAsyncDisposable>(DisposableAsync.Empty);
            }

            var bridgeObserver = new BridgeObserver(observer, cancellationToken);
            var subscription = source.Subscribe(bridgeObserver);
            var asyncDisposable = DisposableAsync.Create(() =>
            {
                subscription.Dispose();
                return default;
            });
            return new ValueTask<IAsyncDisposable>(asyncDisposable);
        }

        /// <summary>
        /// Synchronous observer that forwards to an async observer, queuing items to ensure sequential delivery.
        /// </summary>
        private sealed class BridgeObserver(IObserverAsync<T> observer, CancellationToken cancellationToken) : IObserver<T>
        {
#if NET9_0_OR_GREATER
            private readonly Lock _gate = new();
#else
            private readonly object _gate = new();
#endif
            private readonly Queue<Action> _queue = new();
            private bool _busy;

            public void OnNext(T value) => Enqueue(() => observer.OnNextAsync(value, cancellationToken).AsTask());

            public void OnError(Exception error) => Enqueue(() => observer.OnCompletedAsync(Result.Failure(error)).AsTask());

            public void OnCompleted() => Enqueue(() => observer.OnCompletedAsync(Result.Success).AsTask());

            private static void ProcessAsync(Func<Task> action)
            {
                try
                {
                    action().GetAwaiter().GetResult();
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

            private void Enqueue(Func<Task> action)
            {
                lock (_gate)
                {
                    _queue.Enqueue(() => ProcessAsync(action));
                    if (_busy)
                    {
                        return;
                    }

                    _busy = true;
                }

                DrainQueue();
            }

            private void DrainQueue()
            {
                while (true)
                {
                    Action work;
                    lock (_gate)
                    {
                        if (_queue.Count == 0)
                        {
                            _busy = false;
                            return;
                        }

                        work = _queue.Dequeue();
                    }

                    work();
                }
            }
        }
    }

    /// <summary>
    /// Bridges an <see cref="ObservableAsync{T}"/> into the classic <see cref="IObservable{T}"/> world.
    /// </summary>
    private sealed class ObservableAsyncToObservable<T>(IObservableAsync<T> source) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(observer, nameof(observer));
#else
            if (observer is null)
            {
                throw new ArgumentNullException(nameof(observer));
            }
#endif

            var cts = new CancellationTokenSource();
            var asyncObserver = new BridgeAsyncObserver(observer);
            var subscriptionTask = SubscribeAndCaptureAsync(asyncObserver, cts.Token);

            return Disposable.Create(() =>
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

        private async Task<IAsyncDisposable?> SubscribeAndCaptureAsync(BridgeAsyncObserver observer, CancellationToken cancellationToken)
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
        private sealed class BridgeAsyncObserver(IObserver<T> observer) : ObserverAsync<T>
        {
            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                observer.OnNext(value);
                return default;
            }

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            {
                observer.OnError(error);
                return default;
            }

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
