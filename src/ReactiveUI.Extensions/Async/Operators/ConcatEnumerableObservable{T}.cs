// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

internal sealed class ConcatEnumerableObservable<T>(IEnumerable<IObservableAsync<T>> observables) : ObservableAsync<T>
{
    private readonly IEnumerable<IObservableAsync<T>> _observables = observables;

    protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
    {
        var subscription = new ConcatEnumerableSubscription(this, observer);
        try
        {
            await subscription.SubscribeNextAsync();
        }
        catch
        {
            await subscription.DisposeAsync();
            throw;
        }

        return subscription;
    }

    private sealed class ConcatEnumerableSubscription : IAsyncDisposable
    {
        private readonly IEnumerator<IObservableAsync<T>> _enumerator;
        private readonly SerialDisposableAsync _innerDisposable = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly CancellationToken _disposedCancellationToken;
        private readonly IObserverAsync<T> _observer;
        private int _disposed;

        public ConcatEnumerableSubscription(ConcatEnumerableObservable<T> parent, IObserverAsync<T> observer)
        {
            _observer = observer;
            _enumerator = parent._observables.GetEnumerator();
            _disposedCancellationToken = _cts.Token;
        }

        public async ValueTask SubscribeNextAsync()
        {
            try
            {
                if (_enumerator.MoveNext())
                {
                    var current = _enumerator.Current;
                    var subscription = await current!.SubscribeAsync(
                        OnInnerNextAsync,
                        OnInnerErrorResumeAsync,
                        result => result.IsFailure ? CompleteAsync(result) : SubscribeNextAsync(),
                        _disposedCancellationToken);

                    await _innerDisposable.SetDisposableAsync(subscription);
                }
                else
                {
                    await CompleteAsync(Result.Success);
                }
            }
            catch (Exception e)
            {
                await CompleteAsync(Result.Failure(e));
            }
        }

        public ValueTask DisposeAsync() => CompleteAsync(null);

        private async ValueTask OnInnerErrorResumeAsync(Exception exception, CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken, cancellationToken);
            await _observer.OnErrorResumeAsync(exception, linkedCts.Token);
        }

        private async ValueTask OnInnerNextAsync(T value, CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken, cancellationToken);
            await _observer.OnNextAsync(value, linkedCts.Token);
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

            _cts.Cancel();
            await _innerDisposable.DisposeAsync();
            if (result is not null)
            {
                await _observer.OnCompletedAsync(result.Value);
            }

            _enumerator.Dispose();
            _cts.Dispose();
        }
    }
}
