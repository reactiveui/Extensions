// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

internal sealed class ConcatObservablesObservable<T>(IObservableAsync<IObservableAsync<T>> source) : ObservableAsync<T>
{
    protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
    {
        var subscription = new ConcatSubscription(observer);
        try
        {
            await subscription.SubscribeAsync(source, cancellationToken);
        }
        catch
        {
            await subscription.DisposeAsync();
            throw;
        }

        return subscription;
    }

    private sealed class ConcatSubscription : IAsyncDisposable
    {
        private readonly ConcurrentQueue<IObservableAsync<T>> _buffer = new();
        private readonly CancellationTokenSource _disposeCts = new();
        private readonly CancellationToken _disposedCancellationToken;
        private readonly SingleAssignmentDisposableAsync _outerDisposable = new();
        private readonly SerialDisposableAsync _innerSubscription = new();
        private readonly IObserverAsync<T> _observer;
        private readonly AsyncGate _observerOnSomethingGate = new();
        private bool _outerCompleted;
        private int _disposed;

        public ConcatSubscription(IObserverAsync<T> observer)
        {
            _observer = observer;
            _disposedCancellationToken = _disposeCts.Token;
        }

        public async ValueTask SubscribeAsync(IObservableAsync<IObservableAsync<T>> source, CancellationToken subscriptionToken)
        {
            var outerSubscription = await source.SubscribeAsync(new ConcatOuterObserver(this), subscriptionToken);
            await _outerDisposable.SetDisposableAsync(outerSubscription);
        }

        public ValueTask OnNextOuterAsync(IObservableAsync<T> inner)
        {
            var shouldSubscribe = false;
            lock (_buffer)
            {
                _buffer.Enqueue(inner);
                if (_buffer.Count == 1)
                {
                    shouldSubscribe = true;
                }
            }

            if (shouldSubscribe)
            {
                return SubscribeToInnerLoop(inner);
            }

            return default;
        }

        public ValueTask OnCompletedOuterAsync(Result result)
        {
            var shouldComplete = false;
            Result? completeResult = null;
            lock (_buffer)
            {
                _outerCompleted = true;
                if (result.IsFailure || _buffer.IsEmpty)
                {
                    shouldComplete = true;
                    completeResult = result;
                }
            }

            return shouldComplete ? CompleteAsync(completeResult) : default;
        }

        public ValueTask OnCompletedInnerAsync(Result result)
        {
            if (result.IsFailure)
            {
                return CompleteAsync(result);
            }

            IObservableAsync<T>? nextInner;
            bool outerCompleted;
            lock (_buffer)
            {
                _buffer.TryDequeue(out _);
                _buffer.TryPeek(out nextInner);
                outerCompleted = _outerCompleted;
            }

            if (nextInner is null)
            {
                return outerCompleted ? CompleteAsync(Result.Success) : default;
            }

            return SubscribeToInnerLoop(nextInner);
        }

        public ValueTask DisposeAsync() => CompleteAsync(null);

        private async ValueTask SubscribeToInnerLoop(IObservableAsync<T> currentInner)
        {
            try
            {
                var innerSubscription = await currentInner.SubscribeAsync(new ConcatInnerObserver(this), _disposedCancellationToken);
                await _innerSubscription.SetDisposableAsync(innerSubscription);
            }
            catch (Exception e)
            {
                await CompleteAsync(Result.Failure(e));
            }
        }

        private async ValueTask CompleteAsync(Result? result)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                if (result?.Exception is not null and var exception)
                {
                    UnhandledExceptionHandler.OnUnhandledException(exception);
                }

                return;
            }

            _disposeCts.Cancel();
            await _innerSubscription.DisposeAsync();
            await _outerDisposable.DisposeAsync();
            if (result is not null)
            {
                await _observer.OnCompletedAsync(result.Value);
            }

            _disposeCts.Dispose();
            _observerOnSomethingGate.Dispose();
        }

        private sealed class ConcatOuterObserver(ConcatSubscription subscription) : ObserverAsync<IObservableAsync<T>>
        {
            protected override ValueTask OnNextAsyncCore(IObservableAsync<T> value, CancellationToken cancellationToken)
                => subscription.OnNextOuterAsync(value);

            protected override async ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(subscription._disposedCancellationToken, cancellationToken);
                using (await subscription._observerOnSomethingGate.LockAsync())
                {
                    await subscription._observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            protected override ValueTask OnCompletedAsyncCore(Result result)
                => subscription.OnCompletedOuterAsync(result);
        }

        private sealed class ConcatInnerObserver(ConcatSubscription subscription) : ObserverAsync<T>
        {
            protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(subscription._disposedCancellationToken, cancellationToken);
                using (await subscription._observerOnSomethingGate.LockAsync())
                {
                    await subscription._observer.OnNextAsync(value, linkedCts.Token);
                }
            }

            protected override async ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(subscription._disposedCancellationToken, cancellationToken);
                using (await subscription._observerOnSomethingGate.LockAsync())
                {
                    await subscription._observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            protected override ValueTask OnCompletedAsyncCore(Result result)
                => subscription.OnCompletedInnerAsync(result);
        }
    }
}
