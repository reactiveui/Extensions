// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides a set of extension methods for creating observable sequences that emit items from a source sequence until a
/// specified condition is met or an external signal is received.
/// </summary>
/// <remarks>The methods in this class allow you to control the lifetime of an observable sequence based on
/// various triggers, such as another observable, a task, a cancellation token, or a predicate. These methods are useful
/// for scenarios where you need to automatically stop processing items from a source sequence when a certain event
/// occurs or a condition is satisfied.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> source)
    {
        /// <summary>
        /// Returns an observable sequence that emits items from the source sequence until the specified other
        /// observable emits an item or completes.
        /// </summary>
        /// <typeparam name="TOther">The type of the elements in the other observable sequence that triggers termination of the source sequence.</typeparam>
        /// <param name="other">The observable sequence whose first emission or completion will cause the returned sequence to stop emitting
        /// items from the source.</param>
        /// <param name="options">An optional set of options that control the behavior of the take-until operation. If null, default options
        /// are used.</param>
        /// <returns>An observable sequence that emits items from the source sequence until the other observable emits an item or
        /// completes.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either the source sequence or the other observable is null.</exception>
        public IObservableAsync<T> TakeUntil<TOther>(IObservableAsync<TOther> other, TakeUntilOptions? options = null)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));
            ArgumentExceptionHelper.ThrowIfNull(other, nameof(other));

            return new TakeUntilAsyncObservable<T, TOther>(source, other, options ?? TakeUntilOptions.Default);
        }

        /// <summary>
        /// Returns an observable sequence that emits items from the source until the specified task completes.
        /// </summary>
        /// <param name="task">The task whose completion will signal the termination of the observable sequence. The sequence will stop
        /// emitting items when this task completes, regardless of its result.</param>
        /// <param name="options">An optional set of options that control the behavior of the take-until operation. If null, default options
        /// are used.</param>
        /// <returns>An observable sequence that emits items from the source until the specified task completes.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the source observable is null.</exception>
        public IObservableAsync<T> TakeUntil(Task task, TakeUntilOptions? options = null)
        {
            ArgumentExceptionHelper.ThrowIfNull(source, nameof(source));

            return new TakeUntilTask<T>(source, task, options ?? TakeUntilOptions.Default);
        }

        /// <summary>
        /// Returns an observable sequence that emits items from the source sequence until the specified cancellation
        /// token is canceled.
        /// </summary>
        /// <remarks>If the cancellation token is already canceled when the method is called, the
        /// resulting observable sequence will complete immediately.</remarks>
        /// <param name="cancellationToken">A cancellation token that, when canceled, will terminate the resulting observable sequence.</param>
        /// <returns>An observable sequence that completes when the provided cancellation token is canceled or when the source
        /// sequence completes.</returns>
        public IObservableAsync<T> TakeUntil(CancellationToken cancellationToken) =>
            new TakeUntilCancellationToken<T>(source, cancellationToken);

        /// <summary>
        /// Returns a sequence that emits elements from the source until the specified predicate returns true for an
        /// element.
        /// </summary>
        /// <remarks>The element that causes the predicate to return true is not included in the resulting
        /// sequence. Subsequent elements from the source are not emitted.</remarks>
        /// <param name="predicate">A function to test each element for a condition. The sequence will stop emitting elements when this function
        /// returns true.</param>
        /// <returns>An observable sequence that contains the elements from the source sequence up to, but not including, the
        /// first element for which the predicate returns true.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is null.</exception>
        public IObservableAsync<T> TakeUntil(Func<T, bool> predicate)
        {
            ArgumentExceptionHelper.ThrowIfNull(predicate, nameof(predicate));

            return new TakeUntilPredicate<T>(source, predicate);
        }

        /// <summary>
        /// Returns an observable sequence that emits elements from the source sequence until the specified asynchronous
        /// predicate returns true for an element.
        /// </summary>
        /// <param name="asyncPredicate">A function that evaluates each element and its associated cancellation token asynchronously. The sequence
        /// stops emitting elements when this function returns true.</param>
        /// <returns>An observable sequence that contains the elements from the source sequence up to, but not including, the
        /// first element for which the asynchronous predicate returns true.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="asyncPredicate"/> is null.</exception>
        public IObservableAsync<T> TakeUntil(Func<T, CancellationToken, ValueTask<bool>> asyncPredicate)
        {
            ArgumentExceptionHelper.ThrowIfNull(asyncPredicate, nameof(asyncPredicate));

            return new TakeUntilAsyncPredicate<T>(source, asyncPredicate);
        }

        /// <summary>
        /// Returns an observable sequence that emits items from the source sequence until the specified stop signal
        /// completes.
        /// </summary>
        /// <param name="stopSignalSignal">A delegate that provides a completion signal. The returned observable will stop emitting items when this
        /// signal completes.</param>
        /// <param name="options">An optional set of options that configure the behavior of the take-until operation. If null, default options
        /// are used.</param>
        /// <returns>An observable sequence that emits items from the source until the stop signal completes.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stopSignalSignal"/> is null.</exception>
        public IObservableAsync<T> TakeUntil(CompletionObservableDelegate stopSignalSignal, TakeUntilOptions? options = null)
        {
            ArgumentExceptionHelper.ThrowIfNull(stopSignalSignal, nameof(stopSignalSignal));

            return new TakeUntilFromRawSignal<T>(source, stopSignalSignal, options ?? TakeUntilOptions.Default);
        }
    }

    /// <summary>
    /// Async observable that emits items from the source until the specified predicate returns true.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    internal sealed class TakeUntilPredicate<T>(IObservableAsync<T> source, Func<T, bool> predicate) : ObservableAsync<T>
    {
        /// <summary>The predicate that signals when to stop emitting items.</summary>
        private readonly Func<T, bool> _predicate = predicate;

        /// <summary>The source observable sequence.</summary>
        private readonly IObservableAsync<T> _source = source;

        /// <inheritdoc/>
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new TakeUntilPredicateSubscription(this, observer);
            return await SubscriptionHelper.SubscribeAndDisposeOnFailureAsync(
                subscription,
                () => subscription.SubscribeAsync(cancellationToken));
        }

        /// <summary>
        /// Observer that forwards items from the source until the predicate returns true.
        /// </summary>
        internal sealed class TakeUntilPredicateSubscription(TakeUntilPredicate<T> parent, IObserverAsync<T> observer) : ObserverAsync<T>
        {
            /// <summary>The inner subscription handle.</summary>
            private IAsyncDisposable? _subscription;

            /// <summary>
            /// Subscribes to the source observable.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the subscription.</param>
            /// <returns>A task representing the asynchronous subscribe operation.</returns>
            public async ValueTask SubscribeAsync(CancellationToken cancellationToken) => _subscription = await parent._source.SubscribeAsync(this, cancellationToken);

            /// <inheritdoc/>
            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                if (parent._predicate(value))
                {
                    return OnCompletedAsyncCore(Result.Success);
                }

                return observer.OnNextAsync(value, cancellationToken);
            }

            /// <inheritdoc/>
            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => observer.OnErrorResumeAsync(error, cancellationToken);

            /// <inheritdoc/>
            protected override ValueTask OnCompletedAsyncCore(Result result) => observer.OnCompletedAsync(result);

            /// <inheritdoc/>
            protected override async ValueTask DisposeAsyncCore()
            {
                if (_subscription is not null)
                {
                    await _subscription.DisposeAsync();
                }

                await base.DisposeAsyncCore();
            }
        }
    }

    /// <summary>
    /// Async observable that emits items from the source until the specified cancellation token is canceled.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    internal sealed class TakeUntilCancellationToken<T>(IObservableAsync<T> source, CancellationToken cancellationToken) : ObservableAsync<T>
    {
        /// <summary>The source observable sequence.</summary>
        private readonly IObservableAsync<T> _source = source;

        /// <summary>The cancellation token that triggers completion.</summary>
        private readonly CancellationToken _cancellationToken = cancellationToken;

        /// <inheritdoc/>
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new Subscription(this, observer);
            return await SubscriptionHelper.SubscribeAndDisposeOnFailureAsync(
                subscription,
                () => subscription.SubscribeAsync(cancellationToken));
        }

        /// <summary>
        /// Manages the subscription lifetime and completes when the cancellation token is canceled.
        /// </summary>
        internal sealed class Subscription : IAsyncDisposable
        {
            /// <summary>Cancellation source for disposal.</summary>
            private readonly CancellationTokenSource _cts = new();

            /// <summary>The parent observable that owns this subscription.</summary>
            private readonly TakeUntilCancellationToken<T> _parent;

            /// <summary>The downstream observer.</summary>
            private readonly IObserverAsync<T> _observer;

            /// <summary>Serializes observer notifications.</summary>
            private readonly AsyncGate _gate = new();

            /// <summary>A cached token from <see cref="_cts"/> used to link with per-emission tokens.</summary>
            private readonly CancellationToken _disposeCancellationToken;

            /// <summary>The inner subscription handle.</summary>
            private IAsyncDisposable? _subscription;

            /// <summary>The registration handle for the external cancellation token callback.</summary>
            private IDisposable? _tokenRegistration;

            /// <summary>
            /// Initializes a new instance of the <see cref="Subscription"/> class.
            /// </summary>
            /// <param name="parent">The parent observable that owns this subscription.</param>
            /// <param name="observer">The downstream observer to forward items to.</param>
            public Subscription(TakeUntilCancellationToken<T> parent, IObserverAsync<T> observer)
            {
                _parent = parent;
                _observer = observer;
                _disposeCancellationToken = _cts.Token;
            }

            /// <summary>
            /// Subscribes to the source observable and registers the cancellation token callback.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the subscription.</param>
            /// <returns>A task representing the asynchronous subscribe operation.</returns>
            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                _tokenRegistration = _parent._cancellationToken.Register(OnTokenCanceled);
                _subscription = await _parent._source.SubscribeAsync(new SourceObserver(this), cancellationToken);
            }

            /// <summary>
            /// Asynchronously releases resources used by this subscription.
            /// </summary>
            /// <returns>A task representing the asynchronous dispose operation.</returns>
            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                _cts.Dispose();
                _tokenRegistration?.Dispose();
                if (_subscription is not null)
                {
                    await _subscription.DisposeAsync();
                }

                _gate.Dispose();
            }

            /// <summary>
            /// Callback invoked when the external cancellation token is canceled; forwards completion to the observer.
            /// </summary>
            internal void OnTokenCanceled() => FireAndForgetHelper.Run(async () =>
            {
                await Task.Yield();
                await ForwardOnCompletedAsync(Result.Success);
            });

            /// <summary>
            /// Forwards a value to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="value">The value to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnNextAsync(T value, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnNextAsync(value, linkedCts.Token);
                }
            }

            /// <summary>
            /// Forwards a non-terminal error to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            /// <summary>
            /// Forwards the completion signal to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnCompletedAsync(Result result)
            {
                using (await _gate.LockAsync())
                {
                    await _observer.OnCompletedAsync(result);
                }
            }

            /// <summary>
            /// Observer that forwards source items to the parent subscription.
            /// </summary>
            internal sealed class SourceObserver(Subscription parent) : ObserverAsync<T>
            {
                /// <inheritdoc/>
                protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => parent.ForwardOnNextAsync(value, cancellationToken);

                /// <inheritdoc/>
                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => parent.ForwardOnErrorResumeAsync(error, cancellationToken);

                /// <inheritdoc/>
                protected override ValueTask OnCompletedAsyncCore(Result result) => parent.ForwardOnCompletedAsync(result);
            }
        }
    }

    /// <summary>
    /// Async observable that emits items from the source until a raw completion signal fires.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    internal sealed class TakeUntilFromRawSignal<T>(IObservableAsync<T> source, CompletionObservableDelegate stopSignalSignal, TakeUntilOptions options) : ObservableAsync<T>
    {
        /// <summary>The source observable sequence.</summary>
        private readonly IObservableAsync<T> _source = source;

        /// <summary>The delegate that provides the stop signal.</summary>
        private readonly CompletionObservableDelegate _stopSignalSignal = stopSignalSignal;

        /// <summary>Options controlling the take-until behavior.</summary>
        private readonly TakeUntilOptions _options = options;

        /// <inheritdoc/>
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new Subscription(this, observer);
            return await SubscriptionHelper.SubscribeAndDisposeOnFailureAsync(
                subscription,
                () => subscription.SubscribeAsync(cancellationToken));
        }

        /// <summary>
        /// Manages the subscription lifetime and completes when the raw stop signal fires.
        /// </summary>
        internal sealed class Subscription : IAsyncDisposable
        {
            /// <summary>Cancellation source for disposal.</summary>
            private readonly CancellationTokenSource _cts = new();

            /// <summary>The parent observable that owns this subscription.</summary>
            private readonly TakeUntilFromRawSignal<T> _parent;

            /// <summary>The downstream observer.</summary>
            private readonly IObserverAsync<T> _observer;

            /// <summary>Serializes observer notifications.</summary>
            private readonly AsyncGate _gate = new();

            /// <summary>A cached token from <see cref="_cts"/> used to link with per-emission tokens.</summary>
            private readonly CancellationToken _disposeCancellationToken;

            /// <summary>The inner subscription handle.</summary>
            private IAsyncDisposable? _subscription;

            /// <summary>
            /// Initializes a new instance of the <see cref="Subscription"/> class.
            /// </summary>
            /// <param name="parent">The parent observable that owns this subscription.</param>
            /// <param name="observer">The downstream observer to forward items to.</param>
            public Subscription(TakeUntilFromRawSignal<T> parent, IObserverAsync<T> observer)
            {
                _parent = parent;
                _observer = observer;
                _disposeCancellationToken = _cts.Token;
            }

            /// <summary>
            /// Subscribes to the source observable and begins waiting for the stop signal.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the subscription.</param>
            /// <returns>A task representing the asynchronous subscribe operation.</returns>
            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                WaitAndComplete();
                _subscription = await _parent._source.SubscribeAsync(new SourceObserver(this), cancellationToken);
            }

            /// <summary>
            /// Asynchronously releases resources used by this subscription.
            /// </summary>
            /// <returns>A task representing the asynchronous dispose operation.</returns>
            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                _cts.Dispose();
                if (_subscription is not null)
                {
                    await _subscription.DisposeAsync();
                }

                _gate.Dispose();
            }

            /// <summary>
            /// Waits for the stop signal to fire, then forwards completion or error to the downstream observer.
            /// </summary>
            internal void WaitAndComplete() => FireAndForgetHelper.Run(async () =>
            {
                var tcs = new TaskCompletionSource<object?>();

                void Stop(Result result)
                {
                    if (result.IsFailure)
                    {
                        tcs.SetException(result.Exception);
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                }

                var disposable = _parent._stopSignalSignal(Stop);

                try
                {
                    await tcs.Task.WaitAsync(System.Threading.Timeout.InfiniteTimeSpan, _disposeCancellationToken);
                    try
                    {
                        await disposable.DisposeAsync();
                    }
                    catch
                    {
                        // Ignored
                    }

                    await ForwardOnCompletedAsync(Result.Success);
                }
                catch (Exception e)
                {
                    try
                    {
                        await disposable.DisposeAsync();
                    }
                    catch
                    {
                        // Ignored
                    }

                    if (_parent._options.SourceFailsWhenOtherFails)
                    {
                        await ForwardOnCompletedAsync(Result.Failure(e));
                    }
                    else
                    {
                        await ForwardOnErrorResumeAsync(e, CancellationToken.None);
                    }
                }
            });

            /// <summary>
            /// Forwards a value to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="value">The value to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnNextAsync(T value, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnNextAsync(value, linkedCts.Token);
                }
            }

            /// <summary>
            /// Forwards a non-terminal error to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            /// <summary>
            /// Forwards the completion signal to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnCompletedAsync(Result result)
            {
                using (await _gate.LockAsync())
                {
                    await _observer.OnCompletedAsync(result);
                }
            }

            /// <summary>
            /// Observer that forwards source items to the parent subscription.
            /// </summary>
            internal sealed class SourceObserver(Subscription parent) : ObserverAsync<T>
            {
                /// <inheritdoc/>
                protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) =>
                    parent.ForwardOnNextAsync(value, cancellationToken);

                /// <inheritdoc/>
                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
                    parent.ForwardOnErrorResumeAsync(error, cancellationToken);

                /// <inheritdoc/>
                protected override ValueTask OnCompletedAsyncCore(Result result) =>
                    parent.ForwardOnCompletedAsync(result);
            }
        }
    }

    /// <summary>
    /// Async observable that emits items from the source until the specified task completes.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    internal sealed class TakeUntilTask<T>(IObservableAsync<T> source, Task task, TakeUntilOptions options) : ObservableAsync<T>
    {
        /// <summary>The source observable sequence.</summary>
        private readonly IObservableAsync<T> _source = source;

        /// <summary>The task whose completion triggers the end of the sequence.</summary>
        private readonly Task _task = task;

        /// <summary>Options controlling the take-until behavior.</summary>
        private readonly TakeUntilOptions _options = options;

        /// <inheritdoc/>
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new Subscription(this, observer);
            return await SubscriptionHelper.SubscribeAndDisposeOnFailureAsync(
                subscription,
                () => subscription.SubscribeAsync(cancellationToken));
        }

        /// <summary>
        /// Manages the subscription lifetime and completes when the task finishes.
        /// </summary>
        internal sealed class Subscription : IAsyncDisposable
        {
            /// <summary>Cancellation source for disposal.</summary>
            private readonly CancellationTokenSource _cts = new();

            /// <summary>The parent observable that owns this subscription.</summary>
            private readonly TakeUntilTask<T> _parent;

            /// <summary>The downstream observer.</summary>
            private readonly IObserverAsync<T> _observer;

            /// <summary>Serializes observer notifications.</summary>
            private readonly AsyncGate _gate = new();

            /// <summary>A cached token from <see cref="_cts"/> used to link with per-emission tokens.</summary>
            private readonly CancellationToken _disposeCancellationToken;

            /// <summary>The inner subscription handle.</summary>
            private IAsyncDisposable? _subscription;

            /// <summary>
            /// Initializes a new instance of the <see cref="Subscription"/> class.
            /// </summary>
            /// <param name="parent">The parent observable that owns this subscription.</param>
            /// <param name="observer">The downstream observer to forward items to.</param>
            public Subscription(TakeUntilTask<T> parent, IObserverAsync<T> observer)
            {
                _parent = parent;
                _observer = observer;
                _disposeCancellationToken = _cts.Token;
            }

            /// <summary>
            /// Subscribes to the source observable and begins waiting for the task to complete.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the subscription.</param>
            /// <returns>A task representing the asynchronous subscribe operation.</returns>
            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                var task = _parent._task;
                WaitAndComplete(task);
                _subscription = await _parent._source.SubscribeAsync(new SourceObserver(this), cancellationToken);
            }

            /// <summary>
            /// Asynchronously releases resources used by this subscription.
            /// </summary>
            /// <returns>A task representing the asynchronous dispose operation.</returns>
            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                _cts.Dispose();
                if (_subscription is not null)
                {
                    await _subscription.DisposeAsync();
                }

                _gate.Dispose();
            }

            /// <summary>
            /// Waits for the task to complete, then forwards completion or error to the downstream observer.
            /// </summary>
            /// <param name="task">The task to await.</param>
            internal void WaitAndComplete(Task task) => FireAndForgetHelper.Run(async () =>
            {
                try
                {
                    await task.WaitAsync(System.Threading.Timeout.InfiniteTimeSpan, _disposeCancellationToken);
                    await ForwardOnCompletedAsync(Result.Success);
                }
                catch (Exception e)
                {
                    if (_parent._options.SourceFailsWhenOtherFails)
                    {
                        await ForwardOnCompletedAsync(Result.Failure(e));
                    }
                    else
                    {
                        await ForwardOnErrorResumeAsync(e, CancellationToken.None);
                    }
                }
            });

            /// <summary>
            /// Forwards a value to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="value">The value to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnNextAsync(T value, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnNextAsync(value, linkedCts.Token);
                }
            }

            /// <summary>
            /// Forwards a non-terminal error to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            /// <summary>
            /// Forwards the completion signal to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnCompletedAsync(Result result)
            {
                using (await _gate.LockAsync())
                {
                    await _observer.OnCompletedAsync(result);
                }
            }

            /// <summary>
            /// Observer that forwards source items to the parent subscription.
            /// </summary>
            internal sealed class SourceObserver(Subscription parent) : ObserverAsync<T>
            {
                /// <inheritdoc/>
                protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) =>
                    parent.ForwardOnNextAsync(value, cancellationToken);

                /// <inheritdoc/>
                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
                    parent.ForwardOnErrorResumeAsync(error, cancellationToken);

                /// <inheritdoc/>
                protected override ValueTask OnCompletedAsyncCore(Result result) => parent.ForwardOnCompletedAsync(result);
            }
        }
    }

    /// <summary>
    /// Async observable that emits items from the source until another async observable emits or completes.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <typeparam name="TOther">The type of the elements in the signal sequence.</typeparam>
    internal sealed class TakeUntilAsyncObservable<T, TOther>(IObservableAsync<T> source, IObservableAsync<TOther> other, TakeUntilOptions options) : ObservableAsync<T>
    {
        /// <summary>The source observable sequence.</summary>
        private readonly IObservableAsync<T> _source = source;

        /// <summary>The signal observable whose emission triggers completion.</summary>
        private readonly IObservableAsync<TOther> _other = other;

        /// <summary>Options controlling the take-until behavior.</summary>
        private readonly TakeUntilOptions _options = options;

        /// <inheritdoc/>
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new Subscription(this, observer);
            return await SubscriptionHelper.SubscribeAndDisposeOnFailureAsync(
                subscription,
                async () => { await subscription.SubscribeAsync(cancellationToken); });
        }

        /// <summary>
        /// Manages subscriptions to both the source and signal observables, completing when the signal fires.
        /// </summary>
        internal sealed class Subscription : IAsyncDisposable
        {
            /// <summary>The parent observable that owns this subscription.</summary>
            private readonly TakeUntilAsyncObservable<T, TOther> _parent;

            /// <summary>The downstream observer.</summary>
            private readonly IObserverAsync<T> _observer;

            /// <summary>Serializes observer notifications.</summary>
            private readonly AsyncGate _gate = new();

            /// <summary>Holds the source subscription so it can be disposed on teardown.</summary>
            private readonly SingleAssignmentDisposableAsync _disposable = new();

            /// <summary>Holds the signal subscription so it can be disposed on teardown.</summary>
            private readonly SingleAssignmentDisposableAsync _otherDisposable = new();

            /// <summary>Cancellation source for disposal.</summary>
            private readonly CancellationTokenSource _cts = new();

            /// <summary>A cached token from <see cref="_cts"/> used to link with per-emission tokens.</summary>
            private readonly CancellationToken _disposeCancellationToken;

            /// <summary>
            /// Initializes a new instance of the <see cref="Subscription"/> class.
            /// </summary>
            /// <param name="parent">The parent observable that owns this subscription.</param>
            /// <param name="observer">The downstream observer to forward items to.</param>
            public Subscription(TakeUntilAsyncObservable<T, TOther> parent, IObserverAsync<T> observer)
            {
                _parent = parent;
                _observer = observer;
                _disposeCancellationToken = _cts.Token;
            }

            /// <summary>
            /// Subscribes to both the source and signal observables.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the subscription.</param>
            /// <returns>This subscription as an async disposable.</returns>
            public async ValueTask<IAsyncDisposable> SubscribeAsync(CancellationToken cancellationToken)
            {
                var otherSubscription = await _parent._other.SubscribeAsync(new OtherObserver(this), cancellationToken);
                await _otherDisposable.SetDisposableAsync(otherSubscription);

                var sourceSubscription = await _parent._source.SubscribeAsync(new FirstSubscription(this), cancellationToken);
                await _disposable.SetDisposableAsync(sourceSubscription);

                return this;
            }

            /// <summary>
            /// Asynchronously releases resources used by this subscription.
            /// </summary>
            /// <returns>A task representing the asynchronous dispose operation.</returns>
            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                await _otherDisposable.DisposeAsync();
                await _disposable.DisposeAsync();
                _cts.Dispose();
                _gate.Dispose();
            }

            /// <summary>
            /// Forwards a value to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="value">The value to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnNextAsync(T value, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnNextAsync(value, linkedCts.Token);
                }
            }

            /// <summary>
            /// Forwards a non-terminal error to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            /// <summary>
            /// Forwards the completion signal to the downstream observer under the serialization gate.
            /// </summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A task representing the asynchronous forward operation.</returns>
            internal async ValueTask ForwardOnCompletedAsync(Result result)
            {
                using (await _gate.LockAsync())
                {
                    await _observer.OnCompletedAsync(result);
                }
            }

            /// <summary>
            /// Observer that forwards source items to the parent subscription.
            /// </summary>
            internal sealed class FirstSubscription(Subscription parent) : ObserverAsync<T>
            {
                /// <inheritdoc/>
                protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => parent.ForwardOnNextAsync(value, cancellationToken);

                /// <inheritdoc/>
                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => parent.ForwardOnErrorResumeAsync(error, cancellationToken);

                /// <inheritdoc/>
                protected override ValueTask OnCompletedAsyncCore(Result result) => parent.ForwardOnCompletedAsync(result);
            }

            /// <summary>
            /// Observer for the signal observable that triggers completion of the source subscription.
            /// </summary>
            internal sealed class OtherObserver(Subscription parent) : ObserverAsync<TOther>
            {
                /// <inheritdoc/>
                protected override async ValueTask OnNextAsyncCore(TOther value, CancellationToken cancellationToken)
                {
                    await parent.ForwardOnCompletedAsync(Result.Success);
                    await DisposeAsync();
                }

                /// <inheritdoc/>
                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
                    parent.ForwardOnErrorResumeAsync(error, cancellationToken);

                /// <inheritdoc/>
                protected override ValueTask OnCompletedAsyncCore(Result result)
                {
                    if (result.IsFailure)
                    {
                        if (parent._parent._options.SourceFailsWhenOtherFails)
                        {
                            return parent.ForwardOnCompletedAsync(result);
                        }

                        return parent.ForwardOnCompletedAsync(Result.Success);
                    }

                    return default;
                }
            }
        }
    }

    /// <summary>
    /// Async observable that emits items from the source until the specified asynchronous predicate returns true.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    internal sealed class TakeUntilAsyncPredicate<T>(IObservableAsync<T> source, Func<T, CancellationToken, ValueTask<bool>> asyncPredicate) : ObservableAsync<T>
    {
        /// <summary>The async predicate that signals when to stop emitting items.</summary>
        private readonly Func<T, CancellationToken, ValueTask<bool>> _asyncPredicate = asyncPredicate;

        /// <summary>The source observable sequence.</summary>
        private readonly IObservableAsync<T> _source = source;

        /// <inheritdoc/>
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new TakeUntilAsyncPredicateSubscription(this, observer);
            return await SubscriptionHelper.SubscribeAndDisposeOnFailureAsync(
                subscription,
                () => subscription.SubscribeAsync(cancellationToken));
        }

        /// <summary>
        /// Observer that forwards items from the source until the async predicate returns true.
        /// </summary>
        internal sealed class TakeUntilAsyncPredicateSubscription(TakeUntilAsyncPredicate<T> parent, IObserverAsync<T> observer) : ObserverAsync<T>
        {
            /// <summary>The inner subscription handle.</summary>
            private IAsyncDisposable? _subscription;

            /// <summary>
            /// Subscribes to the source observable.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the subscription.</param>
            /// <returns>A task representing the asynchronous subscribe operation.</returns>
            public async ValueTask SubscribeAsync(CancellationToken cancellationToken) => _subscription = await parent._source.SubscribeAsync(this, cancellationToken);

            /// <inheritdoc/>
            protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                if (await parent._asyncPredicate(value, cancellationToken))
                {
                    await OnCompletedAsyncCore(Result.Success);
                    return;
                }

                await observer.OnNextAsync(value, cancellationToken);
            }

            /// <inheritdoc/>
            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => observer.OnErrorResumeAsync(error, cancellationToken);

            /// <inheritdoc/>
            protected override ValueTask OnCompletedAsyncCore(Result result) => observer.OnCompletedAsync(result);

            /// <inheritdoc/>
            protected override async ValueTask DisposeAsyncCore()
            {
                if (_subscription is not null)
                {
                    await _subscription.DisposeAsync();
                }

                await base.DisposeAsyncCore();
            }
        }
    }
}
