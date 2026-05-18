// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Wraps elements in a synchronization context that waits for a disposal signal before proceeding to the next element.
/// </summary>
/// <typeparam name="T">The type of elements in the source sequence.</typeparam>
/// <param name="source">The source observable.</param>
internal sealed class SynchronizeAsyncObservable<T>(IObservable<T> source) : IObservable<(T Value, IDisposable Sync)>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<(T Value, IDisposable Sync)> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observer);

        var sink = new SynchronizeAsyncSink(observer);
        var sub = source.Subscribe(sink);
        return new DisposableBag(sub, sink);
    }

    /// <summary>
    /// The sink for the <see cref="SynchronizeAsyncObservable{T}"/>.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    private sealed class SynchronizeAsyncSink(IObserver<(T Value, IDisposable Sync)> downstream)
        : IObserver<T>, IDisposable
    {
#if NET9_0_OR_GREATER
        /// <summary>The gate for state access.</summary>
        private readonly Lock _gate = new();
#else
        /// <summary>The gate for state access.</summary>
        private readonly object _gate = new();
#endif

        /// <summary>Whether the sink has completed.</summary>
        private bool _done;

        /// <summary>Whether the sink has been disposed.</summary>
        private bool _disposed;

        /// <inheritdoc/>
        /// <param name="value">The value.</param>
        public void OnNext(T value)
        {
            lock (_gate)
            {
                if (_done || _disposed)
                {
                    return;
                }
            }

            // Implementation note: The original used 'new Continuation().Lock(item, observer)'.
            // This is complex and stateful, so we maintain that logic in a way that respects sequentiality.
            _ = ProcessAsync(value);
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            lock (_gate)
            {
                if (_done || _disposed)
                {
                    return;
                }

                _done = true;
                downstream.OnError(error);
            }
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
            lock (_gate)
            {
                if (_done || _disposed)
                {
                    return;
                }

                _done = true;
                downstream.OnCompleted();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
            }
        }

        /// <summary>
        /// Processes the value asynchronously.
        /// </summary>
        /// <param name="value">The value to process.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task ProcessAsync(T value)
        {
            using var continuation = new Continuation();
            await continuation.Lock(value, downstream).ConfigureAwait(false);
        }
    }
}
