// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides a set of static methods for composing and merging asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class offers extension methods that enable advanced composition patterns for
/// asynchronous observables, such as merging multiple sequences into a single stream. These methods are designed to
/// work with the ObservableAsync{T} abstraction, supporting scenarios where asynchronous event streams need to be
/// combined or coordinated. All methods are thread-safe and intended for use in asynchronous, reactive programming
/// models.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Merges multiple asynchronous observable sequences into a single observable sequence that emits items from all
    /// inner sequences as they arrive.
    /// </summary>
    /// <remarks>The resulting sequence emits items from all inner sequences concurrently as they become
    /// available. The merged sequence completes when the source sequence and all inner sequences have completed. If any
    /// inner sequence signals an error, the merged sequence will propagate that error and terminate.</remarks>
    /// <typeparam name="T">The type of the elements emitted by the inner observable sequences.</typeparam>
    /// <param name="this">The source asynchronous observable sequence whose elements are themselves observable sequences to be merged.
    /// Cannot be null.</param>
    /// <returns>An asynchronous observable sequence that emits items from all inner observable sequences as they are produced.</returns>
    public static IObservableAsync<T> Merge<T>(this IObservableAsync<IObservableAsync<T>> @this) => new MergeObservableObservables<T>(@this);

    /// <summary>
    /// Merges the emissions of multiple asynchronous observable sequences into a single observable sequence, limiting
    /// the number of concurrent subscriptions.
    /// </summary>
    /// <remarks>If the number of active inner subscriptions reaches the specified maximum, additional inner
    /// sequences are queued and subscribed to as others complete. The resulting sequence completes when all inner
    /// sequences have completed. If the source or any inner observable sequence signals an error, the resulting
    /// sequence will propagate that error and terminate.</remarks>
    /// <typeparam name="T">The type of the elements emitted by the inner observable sequences.</typeparam>
    /// <param name="this">The source observable sequence whose elements are themselves observable sequences to be merged.</param>
    /// <param name="maxConcurrent">The maximum number of inner observable sequences to subscribe to concurrently. Must be greater than zero.</param>
    /// <returns>An observable sequence that emits the items from the merged inner observable sequences, with at most the
    /// specified number of concurrent subscriptions.</returns>
    public static IObservableAsync<T> Merge<T>(this IObservableAsync<IObservableAsync<T>> @this, int maxConcurrent) => new MergeObservableObservablesWithMaxConcurrency<T>(@this, maxConcurrent);

    /// <summary>
    /// Combines multiple asynchronous observable sequences into a single observable sequence that emits items from all
    /// source sequences as they arrive.
    /// </summary>
    /// <remarks>The resulting observable sequence emits items from all source sequences in the order they
    /// arrive, interleaving emissions if sources produce items concurrently. The merged sequence completes when all
    /// source sequences have completed. If any source sequence signals an error, the merged sequence will propagate
    /// that error and terminate.</remarks>
    /// <typeparam name="T">The type of the elements produced by the observable sequences.</typeparam>
    /// <param name="this">A collection of asynchronous observable sequences to be merged.</param>
    /// <returns>An observable sequence that emits items from all input sequences as they are produced.</returns>
    public static IObservableAsync<T> Merge<T>(this IEnumerable<IObservableAsync<T>> @this) => new MergeEnumerableObservable<T>(@this);

    /// <summary>
    /// Combines the elements of two asynchronous observable sequences into a single sequence by merging their
    /// emissions.
    /// </summary>
    /// <remarks>The resulting sequence emits items from both source sequences in the order they are produced.
    /// The merged sequence completes when both input sequences have completed. If either source sequence signals an
    /// error, the merged sequence will propagate that error and terminate.</remarks>
    /// <typeparam name="T">The type of the elements in the observable sequences.</typeparam>
    /// <param name="this">The first asynchronous observable sequence to merge.</param>
    /// <param name="other">The second asynchronous observable sequence to merge with the first.</param>
    /// <returns>An ObservableAsync{T} that emits the elements from both input sequences as they arrive.</returns>
    public static IObservableAsync<T> Merge<T>(this IObservableAsync<T> @this, IObservableAsync<T> other) => new MergeEnumerableObservable<T>([@this, other]);

    private sealed class MergeObservableObservables<T>(IObservableAsync<IObservableAsync<T>> sources) : ObservableAsync<T>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new MergeSubscription<T>(observer);
            try
            {
                await subscription.SubscribeAsync(sources, cancellationToken);
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }

            return subscription;
        }
    }

    private sealed class MergeObservableObservablesWithMaxConcurrency<T>(IObservableAsync<IObservableAsync<T>> sources, int maxConcurrent) : ObservableAsync<T>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new MergeSubscriptionWithMaxConcurrency<T>(observer, maxConcurrent);
            try
            {
                await subscription.SubscribeAsync(sources, cancellationToken);
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }

            return subscription;
        }
    }

    private class MergeSubscription<T> : IAsyncDisposable
    {
#pragma warning disable SA1401 // Fields should be private
        protected readonly CancellationToken DisposedCancellationToken;
#pragma warning restore SA1401 // Fields should be private
        private readonly CancellationTokenSource _disposeCts = new();
        private readonly SingleAssignmentDisposableAsync _outerDisposable = new();
        private readonly CompositeDisposableAsync _innerDisposables = new();
        private readonly AsyncGate _onSomethingGate = new();
        private readonly IObserverAsync<T> _observer;
        private int _innerActiveCount;
        private bool _outerCompleted;
        private bool _disposed;

        public MergeSubscription(IObserverAsync<T> observer)
        {
            _observer = observer;
            DisposedCancellationToken = _disposeCts.Token;
        }

        public async ValueTask SubscribeAsync(IObservableAsync<IObservableAsync<T>> @this, CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposedCancellationToken);

            var outerSubscription = await @this.SubscribeAsync(
                (x, _) => SubscribeInnerAsync(x),
                ForwardOnErrorResume,
                result =>
                {
                    bool shouldComplete;
                    lock (_disposeCts)
                    {
                        _outerCompleted = true;
                        shouldComplete = _innerActiveCount == 0 || result.IsFailure;
                    }

                    return shouldComplete ? CompleteAsync(result) : default;
                },
                linkedCts.Token);

            await _outerDisposable.SetDisposableAsync(outerSubscription);
        }

        public ValueTask DisposeAsync() => CompleteAsync(null);

        protected virtual async ValueTask SubscribeInnerAsync(IObservableAsync<T> inner)
        {
            try
            {
                var innerObserver = CreateInnerObserver();
                await innerObserver.SubscribeAsync(inner);
            }
            catch (Exception e)
            {
                await CompleteAsync(Result.Failure(e));
            }
        }

        protected virtual InnerAsyncObserver CreateInnerObserver() => new(this);

        protected async ValueTask CompleteAsync(Result? result)
        {
            lock (_disposeCts)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            _disposeCts.Cancel();
            await _innerDisposables.DisposeAsync();
            await _outerDisposable.DisposeAsync();
            if (result is not null)
            {
                await _observer.OnCompletedAsync(result.Value);
            }

            _disposeCts.Dispose();
            _onSomethingGate.Dispose();
        }

        private async ValueTask ForwardOnNext(T value, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposedCancellationToken);
            using (await _onSomethingGate.LockAsync())
            {
                if (_disposed)
                {
                    return;
                }

                await _observer.OnNextAsync(value, linkedCts.Token);
            }
        }

        private async ValueTask ForwardOnErrorResume(Exception exception, CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposedCancellationToken);
            using (await _onSomethingGate.LockAsync())
            {
                if (_disposed)
                {
                    return;
                }

                await _observer.OnErrorResumeAsync(exception, linkedCts.Token);
            }
        }

        protected class InnerAsyncObserver(MergeSubscription<T> parent) : ObserverAsync<T>
        {
            public async ValueTask SubscribeAsync(IObservableAsync<T> inner)
            {
                lock (parent._disposeCts)
                {
                    parent._innerActiveCount++;
                }

                await parent._innerDisposables.AddAsync(this);
                await inner.SubscribeAsync(this, parent.DisposedCancellationToken);
            }

            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => parent.ForwardOnNext(value, cancellationToken);

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => parent.ForwardOnErrorResume(error, cancellationToken);

            protected override ValueTask OnCompletedAsyncCore(Result result)
            {
                bool shouldComplete;
                lock (parent._disposeCts)
                {
                    var count = --parent._innerActiveCount;
                    shouldComplete = result.IsFailure || (count == 0 && parent._outerCompleted);
                }

                return shouldComplete ? parent.CompleteAsync(result) : default;
            }

            protected override async ValueTask DisposeAsyncCore()
            {
                await OnDisposeAsync();
                await parent._innerDisposables.Remove(this);
            }

            protected virtual ValueTask OnDisposeAsync() => default;
        }
    }

    private sealed class MergeSubscriptionWithMaxConcurrency<T>(IObserverAsync<T> observer, int maxConcurrent) : MergeSubscription<T>(observer)
    {
        private readonly SemaphoreSlim _semaphore = new(maxConcurrent, maxConcurrent);

        protected override async ValueTask SubscribeInnerAsync(IObservableAsync<T> inner)
        {
            await _semaphore.WaitAsync(DisposedCancellationToken);
            try
            {
                var innerObserver = CreateInnerObserver();
                await innerObserver.SubscribeAsync(inner);
            }
            catch (Exception e)
            {
                _semaphore.Release();
                await CompleteAsync(Result.Failure(e));
            }
        }

        protected override InnerAsyncObserver CreateInnerObserver() => new InnerAsyncObserverWithSemaphore(this);

        private sealed class InnerAsyncObserverWithSemaphore(MergeSubscriptionWithMaxConcurrency<T> parent) : InnerAsyncObserver(parent)
        {
            protected override ValueTask OnDisposeAsync()
            {
                parent._semaphore.Release();
                return default;
            }
        }
    }

    private sealed class MergeEnumerableObservable<T>(IEnumerable<IObservableAsync<T>> sources) : ObservableAsync<T>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new MergeEnumerableSubscription(observer, sources);
            try
            {
                subscription.StartAsync();
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }

            return subscription;
        }

        private sealed class MergeEnumerableSubscription : IAsyncDisposable
        {
            private readonly IEnumerable<IObservableAsync<T>> _sources;
            private readonly CompositeDisposableAsync _innerDisposables = new();
            private readonly CancellationTokenSource _cts = new();
            private readonly CancellationToken _disposedCancellationToken;
            private readonly AsyncGate _onSomethingGate = new();
            private readonly TaskCompletionSource<bool> _subscriptionFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly AsyncLocal<bool> _reentrant = new();
            private readonly IObserverAsync<T> _observer;
            private int _active;
            private int _disposed;

            public MergeEnumerableSubscription(IObserverAsync<T> observer, IEnumerable<IObservableAsync<T>> sources)
            {
                _observer = observer;
                _sources = sources;
                _disposedCancellationToken = _cts.Token;
            }

            public async void StartAsync()
            {
                try
                {
                    _reentrant.Value = true;
                    try
                    {
                        // Sentinel: prevents premature completion while the loop is subscribing to sources.
                        // Without this, a synchronously-completing source (e.g. Return) can decrement _active
                        // to zero before the next source is subscribed, terminating the merge early.
                        Interlocked.Increment(ref _active);

                        foreach (var src in _sources)
                        {
                            Interlocked.Increment(ref _active);

                            var innerObserver = new InnerAsyncObserver(this);
                            await _innerDisposables.AddAsync(innerObserver);
                            try
                            {
                                await src.SubscribeAsync(innerObserver, _disposedCancellationToken);
                            }
                            catch (TaskCanceledException)
                            {
                                return;
                            }
                            catch (Exception ex)
                            {
                                await CompleteAsync(Result.Failure(ex));
                                return;
                            }
                        }

                        // Remove sentinel: if all inner sources completed during the loop, this triggers final completion.
                        if (Interlocked.Decrement(ref _active) == 0)
                        {
                            await CompleteAsync(Result.Success);
                        }
                    }
                    catch (Exception e)
                    {
                        await CompleteAsync(Result.Failure(e));
                    }
                    finally
                    {
                        _subscriptionFinished.SetResult(true);
                    }
                }
                catch (Exception e)
                {
                    UnhandledExceptionHandler.OnUnhandledException(e);
                }
            }

            public ValueTask DisposeAsync() => CompleteAsync(null);

            private async ValueTask OnNextAsync(T value, CancellationToken token)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken, token);
                using (await _onSomethingGate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    await _observer.OnNextAsync(value, linked.Token);
                }
            }

            private async ValueTask OnErrorResumeAsync(Exception ex, CancellationToken token)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken, token);
                using (await _onSomethingGate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(ex, linked.Token);
                }
            }

            private ValueTask OnCompletedAsync(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                if (Interlocked.Decrement(ref _active) == 0)
                {
                    return CompleteAsync(Result.Success);
                }

                return default;
            }

            private async ValueTask CompleteAsync(Result? result)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    if (result?.Exception is not null and var ex)
                    {
                        UnhandledExceptionHandler.OnUnhandledException(ex);
                    }

                    return;
                }

                _cts.Cancel();
                await _innerDisposables.DisposeAsync();
                if (!_reentrant.Value)
                {
                    await _subscriptionFinished.Task;
                }

                if (result is not null)
                {
                    await _observer.OnCompletedAsync(result.Value);
                }

                _cts.Dispose();
                _onSomethingGate.Dispose();
            }

            private sealed class InnerAsyncObserver(MergeEnumerableSubscription parent) : ObserverAsync<T>
            {
                protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
                    => parent.OnNextAsync(value, cancellationToken);

                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
                    => parent.OnErrorResumeAsync(error, cancellationToken);

                protected override ValueTask OnCompletedAsyncCore(Result result)
                    => parent.OnCompletedAsync(result);

                protected override async ValueTask DisposeAsyncCore()
                {
                    await parent._innerDisposables.Remove(this);
                }
            }
        }
    }
}
