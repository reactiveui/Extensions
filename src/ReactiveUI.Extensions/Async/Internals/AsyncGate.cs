// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace ReactiveUI.Extensions.Async.Internals;

/// <summary>
/// Provides an asynchronous reentrant lock that allows recursive acquisition from the same async context.
/// </summary>
internal sealed class AsyncGate : IDisposable
{
    /// <summary>
    /// Synchronization gate protecting the recursion count and semaphore access.
    /// </summary>
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    /// <summary>
    /// Semaphore that enforces mutual exclusion across async contexts.
    /// </summary>
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Tracks the lock recursion depth per async flow, enabling reentrant acquisition.
    /// </summary>
    private readonly AsyncLocal<int> _recursionCount = new();

    /// <summary>
    /// Indicates whether this instance has been disposed.
    /// </summary>
    private bool _disposedValue;

    /// <summary>
    /// Asynchronously acquires the lock, returning a <see cref="Releaser"/> that releases the lock when disposed.
    /// </summary>
    /// <returns>A <see cref="ValueTask{Releaser}"/> that completes when the lock is acquired.</returns>
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

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposedValue)
        {
            _semaphore.Dispose();
            _disposedValue = true;
        }
    }

    /// <summary>
    /// Releases the lock by decrementing the recursion count and releasing the semaphore when the count reaches zero.
    /// </summary>
    internal void Release()
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
    /// Asynchronously waits for the semaphore to become available and returns a <see cref="Releaser"/>.
    /// </summary>
    /// <returns>A <see cref="Releaser"/> that releases the lock when disposed.</returns>
    private async Task<Releaser> WaitForReleaseAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        return new Releaser(this);
    }

    /// <summary>
    /// Provides a mechanism for releasing a previously acquired lock or resource when disposed.
    /// </summary>
    /// <remarks>A Releaser is typically returned by methods that acquire an asynchronous lock or gate.
    /// Disposing the Releaser releases the associated lock or resource. This type is intended to be used with a using
    /// statement to ensure proper release, even in the presence of exceptions.</remarks>
    public readonly record struct Releaser : IDisposable
    {
        /// <summary>
        /// The parent <see cref="AsyncGate"/> whose lock is released when this releaser is disposed.
        /// </summary>
        private readonly AsyncGate _parent;

        /// <summary>
        /// Initializes a new instance of the <see cref="Releaser"/> struct with the specified parent gate.
        /// </summary>
        /// <param name="parent">The <see cref="AsyncGate"/> that owns this releaser.</param>
        public Releaser(AsyncGate parent) => _parent = parent;

        /// <inheritdoc/>
        public void Dispose() => _parent.Release();
    }
}
