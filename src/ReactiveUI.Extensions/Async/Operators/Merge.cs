// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
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

    /// <summary>
    /// Async observable that merges items from an observable of observables into a single stream.
    /// </summary>
    /// <typeparam name="T">The type of the elements emitted by the inner observable sequences.</typeparam>
    internal sealed class MergeObservableObservables<T>(IObservableAsync<IObservableAsync<T>> sources) : ObservableAsync<T>
    {
        /// <inheritdoc/>
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

    /// <summary>
    /// Async observable that merges items from an observable of observables with a maximum concurrency limit.
    /// </summary>
    /// <typeparam name="T">The type of the elements emitted by the inner observable sequences.</typeparam>
    internal sealed class MergeObservableObservablesWithMaxConcurrency<T>(IObservableAsync<IObservableAsync<T>> sources, int maxConcurrent) : ObservableAsync<T>
    {
        /// <inheritdoc/>
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

    /// <summary>
    /// Manages subscriptions for merged observable sequences, forwarding items from all inner sources to a single observer.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the merged sequence.</typeparam>
    internal class MergeSubscription<T> : IAsyncDisposable
    {
#pragma warning disable SA1401 // Fields should be private

        /// <summary>
        /// A cancellation token that is canceled when this subscription is disposed.
        /// </summary>
        protected readonly CancellationToken DisposedCancellationToken;
#pragma warning restore SA1401 // Fields should be private

        /// <summary>The cancellation token source backing <see cref="DisposedCancellationToken"/>.</summary>
        private readonly CancellationTokenSource _disposeCts = new();

        /// <summary>Holds the outer subscription so it can be disposed on teardown.</summary>
        private readonly SingleAssignmentDisposableAsync _outerDisposable = new();

        /// <summary>Tracks all inner subscriptions for disposal.</summary>
        private readonly CompositeDisposableAsync _innerDisposables = new();

        /// <summary>Serializes observer notifications to prevent concurrent calls.</summary>
        private readonly AsyncGate _onSomethingGate = new();

        /// <summary>The downstream observer that receives merged items.</summary>
        private readonly IObserverAsync<T> _observer;

        /// <summary>The number of currently active inner subscriptions.</summary>
        private int _innerActiveCount;

        /// <summary>Whether the outer source has completed.</summary>
        private bool _outerCompleted;

        /// <summary>Whether this subscription has been disposed.</summary>
        private int _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MergeSubscription{T}"/> class.
        /// </summary>
        /// <param name="observer">The downstream observer to forward merged items to.</param>
        public MergeSubscription(IObserverAsync<T> observer)
        {
            _observer = observer;
            DisposedCancellationToken = _disposeCts.Token;
        }

        /// <summary>
        /// Subscribes to the outer observable and begins merging inner observable sequences.
        /// </summary>
        /// <param name="this">The outer observable whose inner sequences will be merged.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>A task representing the asynchronous subscribe operation.</returns>
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

        /// <summary>
        /// Asynchronously releases resources used by this subscription.
        /// </summary>
        /// <returns>A task representing the asynchronous dispose operation.</returns>
        public ValueTask DisposeAsync() => CompleteAsync(null);

        /// <summary>
        /// Forwards a value to the downstream observer under the serialization gate.
        /// </summary>
        /// <param name="value">The value to forward.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous forward operation.</returns>
        internal async ValueTask ForwardOnNext(T value, CancellationToken cancellationToken)
        {
            if (DisposalHelper.IsDisposed(_disposed))
            {
                return;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposedCancellationToken);
            using (await _onSomethingGate.LockAsync())
            {
                if (DisposalHelper.IsDisposed(_disposed))
                {
                    return;
                }

                await _observer.OnNextAsync(value, linkedCts.Token);
            }
        }

        /// <summary>
        /// Forwards a non-terminal error to the downstream observer under the serialization gate.
        /// </summary>
        /// <param name="exception">The error to forward.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous forward operation.</returns>
        internal async ValueTask ForwardOnErrorResume(Exception exception, CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposedCancellationToken);
            using (await _onSomethingGate.LockAsync())
            {
                if (DisposalHelper.IsDisposed(_disposed))
                {
                    return;
                }

                await _observer.OnErrorResumeAsync(exception, linkedCts.Token);
            }
        }

        /// <summary>
        /// Subscribes to an inner observable sequence and begins forwarding its items.
        /// </summary>
        /// <param name="inner">The inner observable to subscribe to.</param>
        /// <returns>A task representing the asynchronous subscribe operation.</returns>
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

        /// <summary>
        /// Creates a new inner observer for subscribing to an inner observable sequence.
        /// </summary>
        /// <returns>A new inner async observer instance.</returns>
        protected virtual InnerAsyncObserver CreateInnerObserver() => new(this);

        /// <summary>
        /// Completes the merged sequence, disposes all subscriptions, and optionally signals the downstream observer.
        /// </summary>
        /// <param name="result">The completion result to forward, or null if disposing without signaling completion.</param>
        /// <returns>A task representing the asynchronous completion operation.</returns>
        protected async ValueTask CompleteAsync(Result? result)
        {
            if (DisposalHelper.TrySetDisposed(ref _disposed))
            {
                return;
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

        /// <summary>
        /// Observer that forwards items from an inner observable to the parent merge subscription.
        /// </summary>
        internal class InnerAsyncObserver(MergeSubscription<T> parent) : ObserverAsync<T>
        {
            /// <summary>
            /// Subscribes this observer to an inner observable sequence.
            /// </summary>
            /// <param name="inner">The inner observable to subscribe to.</param>
            /// <returns>A task representing the asynchronous subscribe operation.</returns>
            public async ValueTask SubscribeAsync(IObservableAsync<T> inner)
            {
                lock (parent._disposeCts)
                {
                    parent._innerActiveCount++;
                }

                await parent._innerDisposables.AddAsync(this);
                await inner.SubscribeAsync(this, parent.DisposedCancellationToken);
            }

            /// <inheritdoc/>
            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => parent.ForwardOnNext(value, cancellationToken);

            /// <inheritdoc/>
            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => parent.ForwardOnErrorResume(error, cancellationToken);

            /// <inheritdoc/>
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

            /// <inheritdoc/>
            protected override async ValueTask DisposeAsyncCore()
            {
                await OnDisposeAsync();
                await parent._innerDisposables.Remove(this);
            }

            /// <summary>
            /// Called during disposal to perform subclass-specific cleanup such as releasing semaphore slots.
            /// </summary>
            /// <returns>A task representing the asynchronous cleanup operation.</returns>
            protected virtual ValueTask OnDisposeAsync() => default;
        }
    }

    /// <summary>
    /// Extends <see cref="MergeSubscription{T}"/> to limit the number of concurrently subscribed inner observables.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the merged sequence.</typeparam>
    internal sealed class MergeSubscriptionWithMaxConcurrency<T>(IObserverAsync<T> observer, int maxConcurrent) : MergeSubscription<T>(observer)
    {
        /// <summary>Limits the number of concurrently subscribed inner observables.</summary>
        private readonly SemaphoreSlim _semaphore = new(maxConcurrent, maxConcurrent);

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        protected override InnerAsyncObserver CreateInnerObserver() => new InnerAsyncObserverWithSemaphore(this);

        /// <summary>
        /// Inner observer that releases a semaphore slot on disposal.
        /// </summary>
        internal sealed class InnerAsyncObserverWithSemaphore(MergeSubscriptionWithMaxConcurrency<T> parent) : InnerAsyncObserver(parent)
        {
            /// <inheritdoc/>
            protected override ValueTask OnDisposeAsync()
            {
                parent._semaphore.Release();
                return default;
            }
        }
    }

    /// <summary>
    /// Async observable that merges items from an enumerable collection of observables into a single stream.
    /// </summary>
    /// <typeparam name="T">The type of the elements emitted by the observable sequences.</typeparam>
    internal sealed class MergeEnumerableObservable<T>(IEnumerable<IObservableAsync<T>> sources) : ObservableAsync<T>
    {
        /// <inheritdoc/>
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new MergeEnumerableSubscription(observer, sources);
            subscription.StartAsync();

            return subscription;
        }

        /// <summary>
        /// Manages subscriptions to all sources in the enumerable and forwards their items to a single observer.
        /// </summary>
        internal sealed class MergeEnumerableSubscription : IAsyncDisposable
        {
            /// <summary>The collection of source observables to merge.</summary>
            private readonly IEnumerable<IObservableAsync<T>> _sources;

            /// <summary>Tracks all inner subscriptions for disposal.</summary>
            private readonly CompositeDisposableAsync _innerDisposables = new();

            /// <summary>Cancellation source for disposal.</summary>
            private readonly CancellationTokenSource _cts = new();

            /// <summary>A cached token from <see cref="_cts"/> used to link with per-emission tokens.</summary>
            private readonly CancellationToken _disposedCancellationToken;

            /// <summary>Serializes observer notifications to prevent concurrent calls.</summary>
            private readonly AsyncGate _onSomethingGate = new();

            /// <summary>Signals when the initial subscription loop has finished.</summary>
            private readonly TaskCompletionSource<bool> _subscriptionFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>Tracks reentrant calls to prevent deadlocks.</summary>
            private readonly AsyncLocal<bool> _reentrant = new();

            /// <summary>The downstream observer.</summary>
            private readonly IObserverAsync<T> _observer;

            /// <summary>The number of currently active inner subscriptions.</summary>
            private int _active;

            /// <summary>Whether this subscription has been disposed.</summary>
            private int _disposed;

            /// <summary>
            /// Initializes a new instance of the <see cref="MergeEnumerableSubscription"/> class.
            /// </summary>
            /// <param name="observer">The downstream observer to forward merged items to.</param>
            /// <param name="sources">The enumerable of observable sources to merge.</param>
            public MergeEnumerableSubscription(IObserverAsync<T> observer, IEnumerable<IObservableAsync<T>> sources)
            {
                _observer = observer;
                _sources = sources;
                _disposedCancellationToken = _cts.Token;
            }

            /// <summary>
            /// Begins subscribing to all source observables asynchronously.
            /// </summary>
            public void StartAsync() => FireAndForgetHelper.Run(async () =>
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
            });

            /// <summary>
            /// Asynchronously releases resources used by this subscription.
            /// </summary>
            /// <returns>A task representing the asynchronous dispose operation.</returns>
            public ValueTask DisposeAsync() => CompleteAsync(null);

            /// <summary>
            /// Routes an exception from a post-disposal completion result to the unhandled exception handler.
            /// Called when <see cref="DisposalHelper.TrySetDisposed"/> returns true (already disposed)
            /// and the completion result carries an exception.
            /// </summary>
            /// <param name="result">The completion result, or null if disposing without signaling.</param>
            internal static void RoutePostDisposalException(Result? result)
            {
                if (result?.Exception is not null and var ex)
                {
                    UnhandledExceptionHandler.OnUnhandledException(ex);
                }
            }

            /// <summary>
            /// Forwards a value to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="value">The value to forward.</param>
            /// <param name="token">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask OnNextAsync(T value, CancellationToken token)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken, token);
                using (await _onSomethingGate.LockAsync())
                {
                    if (DisposalHelper.IsDisposed(_disposed))
                    {
                        return;
                    }

                    await _observer.OnNextAsync(value, linked.Token);
                }
            }

            /// <summary>
            /// Forwards a non-terminal error to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="ex">The error to forward.</param>
            /// <param name="token">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask OnErrorResumeAsync(Exception ex, CancellationToken token)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken, token);
                using (await _onSomethingGate.LockAsync())
                {
                    if (DisposalHelper.IsDisposed(_disposed))
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(ex, linked.Token);
                }
            }

            /// <summary>
            /// Handles completion from an inner source, triggering final completion when all sources are done.
            /// </summary>
            /// <param name="result">The completion result from the inner source.</param>
            /// <returns>A task representing the asynchronous completion operation.</returns>
            internal ValueTask OnCompletedAsync(Result result)
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

            /// <summary>
            /// Completes the merged sequence, disposes all subscriptions, and optionally signals the downstream observer.
            /// </summary>
            /// <param name="result">The completion result to forward, or <see langword="null"/> if disposing without signaling completion.</param>
            /// <returns>A task representing the asynchronous completion operation.</returns>
            internal async ValueTask CompleteAsync(Result? result)
            {
                if (DisposalHelper.TrySetDisposed(ref _disposed))
                {
                    RoutePostDisposalException(result);
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

            /// <summary>
            /// Observer that forwards items from an inner source to the parent enumerable merge subscription.
            /// </summary>
            internal sealed class InnerAsyncObserver(MergeEnumerableSubscription parent) : ObserverAsync<T>
            {
                /// <inheritdoc/>
                protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
                    => parent.OnNextAsync(value, cancellationToken);

                /// <inheritdoc/>
                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
                    => parent.OnErrorResumeAsync(error, cancellationToken);

                /// <inheritdoc/>
                protected override ValueTask OnCompletedAsyncCore(Result result)
                    => parent.OnCompletedAsync(result);

                /// <inheritdoc/>
                protected override async ValueTask DisposeAsyncCore() =>
                    await parent._innerDisposables.Remove(this);
            }
        }
    }
}
