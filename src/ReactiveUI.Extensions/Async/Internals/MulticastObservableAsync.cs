// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Async.Internals;

internal class MulticastObservableAsync<T>(IObservableAsync<T> observable, ISubjectAsync<T> subject) : ConnectableObservableAsync<T>, IDisposable
{
    private readonly AsyncGate _gate = new();
    private SingleAssignmentDisposableAsync? _connection;
    private bool _disposedValue;

    /// <summary>
    /// Asynchronously establishes a connection to the underlying observable sequence and returns a disposable handle
    /// for managing the connection's lifetime.
    /// </summary>
    /// <remarks>If a connection is already established, the existing connection handle is returned. Disposing
    /// the returned handle will disconnect the subscription. This method is thread-safe.</remarks>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous connection operation.</param>
    /// <returns>A value task that represents the asynchronous operation. The result contains an <see cref="IAsyncDisposable"/>
    /// that disconnects the subscription when disposed.</returns>
    public override async ValueTask<IAsyncDisposable> ConnectAsync(CancellationToken cancellationToken)
    {
        using (await _gate.LockAsync())
        {
            if (_connection != null)
            {
                return _connection;
            }

            SingleAssignmentDisposableAsync? connection = new();
            _connection = connection;
            await connection.SetDisposableAsync(await observable.SubscribeAsync(subject.AsObserverAsync(), cancellationToken));
            return DisposableAsync.Create(async () =>
            {
                using (await _gate.LockAsync())
                {
                    if (connection is null)
                    {
                        return;
                    }

                    var localConn = connection;
                    connection = null;
                    _connection = null;
                    await localConn.DisposeAsync();
                }
            });
        }
    }

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    /// <remarks>Call this method when you are finished using the object to release unmanaged resources and
    /// perform other cleanup operations. After calling Dispose, the object should not be used.</remarks>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Subscribes the specified asynchronous observer to receive notifications from the sequence.
    /// </summary>
    /// <param name="observer">The asynchronous observer that will receive notifications. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the subscription operation.</param>
    /// <returns>A task that represents the asynchronous subscription operation. The result contains an object that can be
    /// disposed to unsubscribe the observer.</returns>
    protected override ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken) =>
        subject.Values.SubscribeAsync(observer.Wrap(), cancellationToken);

    /// <summary>
    /// Releases the unmanaged resources used by the object and optionally releases the managed resources.
    /// </summary>
    /// <remarks>This method is called by public Dispose methods and can be overridden to release additional
    /// resources in a derived class. When disposing is true, both managed and unmanaged resources can be disposed; when
    /// false, only unmanaged resources should be released.</remarks>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _connection?.DisposeAsync().AsTask().Wait();
                _gate.Dispose();
            }

            _disposedValue = true;
        }
    }
}
