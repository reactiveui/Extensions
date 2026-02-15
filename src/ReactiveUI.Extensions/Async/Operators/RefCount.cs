// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class enable advanced operations on asynchronous observables, such as reference
/// counting for connectable observables. These utilities are intended to be used with types that implement asynchronous
/// observer patterns.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Returns an observable sequence that connects to the underlying connectable observable when the first observer
    /// subscribes, and disconnects when the last observer unsubscribes.
    /// </summary>
    /// <remarks>This operator is useful for sharing a single subscription to the underlying connectable
    /// observable among multiple subscribers. When the last observer unsubscribes, the connection to the source is
    /// automatically disposed.</remarks>
    /// <typeparam name="T">The type of the elements in the observable sequence.</typeparam>
    /// <param name="source">The connectable observable sequence to ref count. Cannot be null.</param>
    /// <returns>An observable sequence that stays connected to the source as long as there is at least one subscription.</returns>
    public static ObservableAsync<T> RefCount<T>(this ConnectableObservableAsync<T> source) => new RefCountObservable<T>(source);

    private sealed class RefCountObservable<T>(ConnectableObservableAsync<T> source) : ObservableAsync<T>, IDisposable
    {
        private readonly AsyncGate _gate = new();
        private int _refCount;
        private SingleAssignmentDisposableAsync? _connection;
        private bool _disposedValue;

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        [DebuggerStepThrough]
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(ObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            using (await _gate.LockAsync())
            {
                // incr refCount before Subscribe(completed source decrement refCxount in Subscribe)
                ++_refCount;
                var needConnect = _refCount == 1;
                var coObserver = new RefCountObsever(this, observer);
                var subcription = await source.SubscribeAsync(coObserver, cancellationToken);
                if (needConnect && !coObserver.IsDisposed)
                {
                    SingleAssignmentDisposableAsync connection = new();
                    _connection = connection;
                    await connection.SetDisposableAsync(await source.ConnectAsync(cancellationToken));
                }

                return subcription;
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _gate.Dispose();
                    _connection?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }

                _disposedValue = true;
            }
        }

        private sealed class RefCountObsever(RefCountObservable<T> parent, ObserverAsync<T> observer) : ObserverAsync<T>
        {
            protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => observer.OnNextAsync(value, cancellationToken);

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => observer.OnErrorResumeAsync(error, cancellationToken);

            protected override ValueTask OnCompletedAsyncCore(Result result) => observer.OnCompletedAsync(result);

            [DebuggerStepThrough]
            protected override async ValueTask DisposeAsyncCore()
            {
                using (await parent._gate.LockAsync())
                {
                    if (--parent._refCount == 0)
                    {
                        var connection = parent._connection;
                        parent._connection = null;
                        if (connection is not null)
                        {
                            await connection.DisposeAsync();
                        }
                    }
                }
            }
        }
    }
}
