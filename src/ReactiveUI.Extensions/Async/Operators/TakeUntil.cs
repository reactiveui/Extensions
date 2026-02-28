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

    private sealed class TakeUntilPredicate<T>(IObservableAsync<T> source, Func<T, bool> predicate) : ObservableAsync<T>
    {
        private readonly Func<T, bool> _predicate = predicate;
        private readonly IObservableAsync<T> _source = source;

        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new TakeUntilPredicateSubscription(this, observer);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
                return subscription;
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }
        }

        private sealed class TakeUntilPredicateSubscription(TakeUntilPredicate<T> parent, IObserverAsync<T> observer) : ObserverAsync<T>
        {
            private IAsyncDisposable? _subscription;

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken) => _subscription = await parent._source.SubscribeAsync(this, cancellationToken);

            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                if (parent._predicate(value))
                {
                    return OnCompletedAsyncCore(Result.Success);
                }

                return observer.OnNextAsync(value, cancellationToken);
            }

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => observer.OnErrorResumeAsync(error, cancellationToken);

            protected override ValueTask OnCompletedAsyncCore(Result result) => observer.OnCompletedAsync(result);

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

    private sealed class TakeUntilCancellationToken<T>(IObservableAsync<T> source, CancellationToken cancellationToken) : ObservableAsync<T>
    {
        private readonly IObservableAsync<T> _source = source;
        private readonly CancellationToken _cancellationToken = cancellationToken;

        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new Subscription(this, observer);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
                return subscription;
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }
        }

        private sealed class Subscription : IAsyncDisposable
        {
            private readonly CancellationTokenSource _cts = new();
            private readonly TakeUntilCancellationToken<T> _parent;
            private readonly IObserverAsync<T> _observer;
            private readonly AsyncGate _gate = new();
            private readonly CancellationToken _disposeCancellationToken;
            private IAsyncDisposable? _subscription;
            private IDisposable? _tokenRegistration;

            public Subscription(TakeUntilCancellationToken<T> parent, IObserverAsync<T> observer)
            {
                _parent = parent;
                _observer = observer;
                _disposeCancellationToken = _cts.Token;
            }

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                _tokenRegistration = _parent._cancellationToken.Register(OnTokenCanceled);
                _subscription = await _parent._source.SubscribeAsync(new SourceObserver(this), cancellationToken);
            }

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

            private async void OnTokenCanceled()
            {
                try
                {
                    await Task.Yield();
                    await ForwardOnCompletedAsync(Result.Success);
                }
                catch
                {
                    // Ignored
                }
            }

            private async ValueTask ForwardOnNextAsync(T value, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnNextAsync(value, linkedCts.Token);
                }
            }

            private async ValueTask ForwardOnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private async ValueTask ForwardOnCompletedAsync(Result result)
            {
                using (await _gate.LockAsync())
                {
                    await _observer.OnCompletedAsync(result);
                }
            }

            private sealed class SourceObserver(Subscription parent) : ObserverAsync<T>
            {
                protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => parent.ForwardOnNextAsync(value, cancellationToken);

                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => parent.ForwardOnErrorResumeAsync(error, cancellationToken);

                protected override ValueTask OnCompletedAsyncCore(Result result) => parent.ForwardOnCompletedAsync(result);
            }
        }
    }

    private sealed class TakeUntilFromRawSignal<T>(IObservableAsync<T> source, CompletionObservableDelegate stopSignalSignal, TakeUntilOptions options) : ObservableAsync<T>
    {
        private readonly IObservableAsync<T> _source = source;
        private readonly CompletionObservableDelegate _stopSignalSignal = stopSignalSignal;
        private readonly TakeUntilOptions _options = options;

        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new Subscription(this, observer);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
                return subscription;
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }
        }

        private sealed class Subscription : IAsyncDisposable
        {
            private readonly CancellationTokenSource _cts = new();
            private readonly TakeUntilFromRawSignal<T> _parent;
            private readonly IObserverAsync<T> _observer;
            private readonly AsyncGate _gate = new();
            private readonly CancellationToken _disposeCancellationToken;
            private IAsyncDisposable? _subscription;

            public Subscription(TakeUntilFromRawSignal<T> parent, IObserverAsync<T> observer)
            {
                _parent = parent;
                _observer = observer;
                _disposeCancellationToken = _cts.Token;
            }

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                WaitAndComplete();
                _subscription = await _parent._source.SubscribeAsync(new SourceObserver(this), cancellationToken);
            }

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

            private async void WaitAndComplete()
            {
                try
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
                }
                catch
                {
                    // Ignored
                }
            }

            private async ValueTask ForwardOnNextAsync(T value, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnNextAsync(value, linkedCts.Token);
                }
            }

            private async ValueTask ForwardOnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private async ValueTask ForwardOnCompletedAsync(Result result)
            {
                using (await _gate.LockAsync())
                {
                    await _observer.OnCompletedAsync(result);
                }
            }

            private sealed class SourceObserver(Subscription parent) : ObserverAsync<T>
            {
                protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) =>
                    parent.ForwardOnNextAsync(value, cancellationToken);

                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
                    parent.ForwardOnErrorResumeAsync(error, cancellationToken);

                protected override ValueTask OnCompletedAsyncCore(Result result) =>
                    parent.ForwardOnCompletedAsync(result);
            }
        }
    }

    private sealed class TakeUntilTask<T>(IObservableAsync<T> source, Task task, TakeUntilOptions options) : ObservableAsync<T>
    {
        private readonly IObservableAsync<T> _source = source;
        private readonly Task _task = task;
        private readonly TakeUntilOptions _options = options;

        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new Subscription(this, observer);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
                return subscription;
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }
        }

        private sealed class Subscription : IAsyncDisposable
        {
            private readonly CancellationTokenSource _cts = new();
            private readonly TakeUntilTask<T> _parent;
            private readonly IObserverAsync<T> _observer;
            private readonly AsyncGate _gate = new();
            private readonly CancellationToken _disposeCancellationToken;
            private IAsyncDisposable? _subscription;

            public Subscription(TakeUntilTask<T> parent, IObserverAsync<T> observer)
            {
                _parent = parent;
                _observer = observer;
                _disposeCancellationToken = _cts.Token;
            }

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                var task = _parent._task;
                WaitAndComplete(task);
                _subscription = await _parent._source.SubscribeAsync(new SourceObserver(this), cancellationToken);
            }

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

            private async void WaitAndComplete(Task task)
            {
                try
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
                }
                catch
                {
                    // Ignored
                }
            }

            private async ValueTask ForwardOnNextAsync(T value, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnNextAsync(value, linkedCts.Token);
                }
            }

            private async ValueTask ForwardOnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private async ValueTask ForwardOnCompletedAsync(Result result)
            {
                using (await _gate.LockAsync())
                {
                    await _observer.OnCompletedAsync(result);
                }
            }

            private sealed class SourceObserver(Subscription parent) : ObserverAsync<T>
            {
                protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) =>
                    parent.ForwardOnNextAsync(value, cancellationToken);

                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
                    parent.ForwardOnErrorResumeAsync(error, cancellationToken);

                protected override ValueTask OnCompletedAsyncCore(Result result) => parent.ForwardOnCompletedAsync(result);
            }
        }
    }

    private sealed class TakeUntilAsyncObservable<T, TOther>(IObservableAsync<T> source, IObservableAsync<TOther> other, TakeUntilOptions options) : ObservableAsync<T>
    {
        private readonly IObservableAsync<T> _source = source;
        private readonly IObservableAsync<TOther> _other = other;
        private readonly TakeUntilOptions _options = options;

        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new Subscription(this, observer);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
                return subscription;
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }
        }

        private sealed class Subscription : IAsyncDisposable
        {
            private readonly TakeUntilAsyncObservable<T, TOther> _parent;
            private readonly IObserverAsync<T> _observer;
            private readonly AsyncGate _gate = new();
            private readonly SingleAssignmentDisposableAsync _disposable = new();
            private readonly SingleAssignmentDisposableAsync _otherDisposable = new();
            private readonly CancellationTokenSource _cts = new();
            private readonly CancellationToken _disposeCancellationToken;

            public Subscription(TakeUntilAsyncObservable<T, TOther> parent, IObserverAsync<T> observer)
            {
                _parent = parent;
                _observer = observer;
                _disposeCancellationToken = _cts.Token;
            }

            public async ValueTask<IAsyncDisposable> SubscribeAsync(CancellationToken cancellationToken)
            {
                var otherSubscription = await _parent._other.SubscribeAsync(new OtherObserver(this), cancellationToken);
                await _otherDisposable.SetDisposableAsync(otherSubscription);

                var sourceSubscription = await _parent._source.SubscribeAsync(new FirstSubscription(this), cancellationToken);
                await _disposable.SetDisposableAsync(sourceSubscription);

                return this;
            }

            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                await _otherDisposable.DisposeAsync();
                await _disposable.DisposeAsync();
                _cts.Dispose();
                _gate.Dispose();
            }

            private async ValueTask ForwardOnNextAsync(T value, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnNextAsync(value, linkedCts.Token);
                }
            }

            private async ValueTask ForwardOnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationToken, cancellationToken);
                using (await _gate.LockAsync())
                {
                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private async ValueTask ForwardOnCompletedAsync(Result result)
            {
                using (await _gate.LockAsync())
                {
                    await _observer.OnCompletedAsync(result);
                }
            }

            private sealed class FirstSubscription(Subscription parent) : ObserverAsync<T>
            {
                protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => parent.ForwardOnNextAsync(value, cancellationToken);

                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => parent.ForwardOnErrorResumeAsync(error, cancellationToken);

                protected override ValueTask OnCompletedAsyncCore(Result result) => parent.ForwardOnCompletedAsync(result);
            }

            private sealed class OtherObserver(Subscription parent) : ObserverAsync<TOther>
            {
                protected override async ValueTask OnNextAsyncCore(TOther value, CancellationToken cancellationToken)
                {
                    await parent.ForwardOnCompletedAsync(Result.Success);
                    await DisposeAsync();
                }

                protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
                    parent.ForwardOnErrorResumeAsync(error, cancellationToken);

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

    private sealed class TakeUntilAsyncPredicate<T>(IObservableAsync<T> source, Func<T, CancellationToken, ValueTask<bool>> asyncPredicate) : ObservableAsync<T>
    {
        private readonly Func<T, CancellationToken, ValueTask<bool>> _asyncPredicate = asyncPredicate;
        private readonly IObservableAsync<T> _source = source;

        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            var subscription = new TakeUntilAsyncPredicateSubscription(this, observer);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
                return subscription;
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }
        }

        private sealed class TakeUntilAsyncPredicateSubscription(TakeUntilAsyncPredicate<T> parent, IObserverAsync<T> observer) : ObserverAsync<T>
        {
            private IAsyncDisposable? _subscription;

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                _subscription = await parent._source.SubscribeAsync(this, cancellationToken);
            }

            protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                if (await parent._asyncPredicate(value, cancellationToken))
                {
                    await OnCompletedAsyncCore(Result.Success);
                    return;
                }

                await observer.OnNextAsync(value, cancellationToken);
            }

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => observer.OnErrorResumeAsync(error, cancellationToken);

            protected override ValueTask OnCompletedAsyncCore(Result result) => observer.OnCompletedAsync(result);

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
