// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions;

/// <summary>
/// Continuation.
/// </summary>
public class Continuation : IDisposable
{
    private readonly Barrier _phaseSync = new(2);
    private bool _disposedValue;
    private bool _locked;

    /// <summary>
    /// Gets the number of completed phases.
    /// </summary>
    /// <value>
    /// The completed phases.
    /// </value>
    public long CompletedPhases => _phaseSync.CurrentPhaseNumber;

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Locks this instance.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="item">The item.</param>
    /// <param name="observer">The observer.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public Task Lock<T>(T item, IObserver<(T value, IDisposable Sync)> observer)
    {
        if (_locked)
        {
            return Task.CompletedTask;
        }

        _locked = true;
        observer?.OnNext((item, this));
        return Task.Run(() => _phaseSync?.SignalAndWait(CancellationToken.None));
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual async void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                await UnLock();
                _phaseSync.Dispose();
            }

            _disposedValue = true;
        }
    }

    /// <summary>
    /// UnLocks this instance.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private Task UnLock()
    {
        if (!_locked)
        {
            return Task.CompletedTask;
        }

        _locked = false;
        return Task.Run(() => _phaseSync?.SignalAndWait(CancellationToken.None));
    }
}
