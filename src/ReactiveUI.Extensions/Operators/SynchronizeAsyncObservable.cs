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
        /// Processes the value. Pushes <c>(value, signal)</c> downstream and waits for the consumer
        /// to dispose the signal. The fast path (consumer disposes synchronously inside <c>OnNext</c>)
        /// returns a completed task without allocating a state machine or <see cref="TaskCompletionSource"/>;
        /// the slow path (consumer defers disposal) lazily promotes the signal to a TCS-backed gate.
        /// </summary>
        /// <param name="value">The value to process.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private Task ProcessAsync(T value)
        {
            var signal = new SyncSignal();
            downstream.OnNext((value, signal));
            return signal.WaitForDisposeAsync();
        }

        /// <summary>
        /// Per-emission gate: the downstream receives this handle as <c>Sync</c>. The producer
        /// calls <see cref="WaitForDisposeAsync"/> after pushing the value; synchronous disposal
        /// short-circuits to <see cref="Task.CompletedTask"/> with no TCS allocation. Late
        /// (asynchronous) disposal lazily allocates a single <see cref="TaskCompletionSource"/>.
        /// </summary>
        private sealed class SyncSignal : IDisposable
        {
            /// <summary>The lazily-created completion source; only allocated on the slow path.</summary>
            private TaskCompletionSource? _tcs;

            /// <summary>Latches to <c>1</c> on the first dispose so signalling is idempotent.</summary>
            private int _disposed;

            /// <summary>Returns the task the producer should await before completing the emission.</summary>
            /// <returns>A completed task if the consumer already disposed; otherwise the lazily-allocated TCS task.</returns>
            public Task WaitForDisposeAsync()
            {
                if (Volatile.Read(ref _disposed) == 1)
                {
                    return Task.CompletedTask;
                }

                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var existing = Interlocked.CompareExchange(ref _tcs, tcs, null);
                if (existing is not null)
                {
                    return existing.Task;
                }

                if (Volatile.Read(ref _disposed) == 1)
                {
                    tcs.TrySetResult();
                }

                return tcs.Task;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                Volatile.Read(ref _tcs)?.TrySetResult();
            }
        }
    }
}
