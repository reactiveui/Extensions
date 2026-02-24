// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace ReactiveUI.Extensions.Async.Internals;

internal class AsyncGate : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly AsyncLocal<int> _recursionCount = new();
    private bool _disposedValue;

    [DebuggerStepThrough]
    public ValueTask<Releaser> LockAsync()
    {
        var shouldAcquire = false;

        lock (_gate)
        {
            if (_recursionCount.Value == 0)
            {
                shouldAcquire = true;
                _recursionCount.Value = 1;
            }
            else
            {
                _recursionCount.Value++;
            }
        }

        if (shouldAcquire)
        {
            return new ValueTask<Releaser>(WaitForReleaseAsync());
        }

        return new ValueTask<Releaser>(new Releaser(this));
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _semaphore.Dispose();
            }

            _disposedValue = true;
        }
    }

    private async Task<Releaser> WaitForReleaseAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        return new Releaser(this);
    }

    private void Release()
    {
        lock (_gate)
        {
            Debug.Assert(_recursionCount.Value > 0, "Release called without a corresponding LockAsync call.");

            if (--_recursionCount.Value == 0)
            {
                _semaphore.Release();
            }
        }
    }

    /// <summary>
    /// Provides a mechanism for releasing a previously acquired lock or resource when disposed.
    /// </summary>
    /// <remarks>A Releaser is typically returned by methods that acquire an asynchronous lock or gate.
    /// Disposing the Releaser releases the associated lock or resource. This type is intended to be used with a using
    /// statement to ensure proper release, even in the presence of exceptions.</remarks>
    public readonly record struct Releaser : IDisposable
    {
        private readonly AsyncGate _parent;

        public Releaser(AsyncGate parent) => _parent = parent;

        public void Dispose() => _parent.Release();
    }
}
