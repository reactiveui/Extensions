// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Runs a list of one-shot <see cref="IObservable{Unit}"/> observables sequentially,
/// ignoring emitted values, and emits a single <see cref="Unit.Default"/> when all
/// have completed. If the list is empty, emits <see cref="Unit.Default"/> immediately.
/// Errors from any observable propagate to the downstream observer.
/// </summary>
/// <remarks>
/// Replaces patterns like <c>sources.Concat().LastOrDefaultAsync()</c> with a single
/// operator that subscribes sequentially. Uses an iterative loop with a sync-completion
/// flag to avoid stack overflow when sources complete synchronously during
/// <c>Subscribe</c>.
/// </remarks>
/// <param name="sources">The list of one-shot observables to run in order.</param>
internal sealed class RunAllObservable(IReadOnlyList<IObservable<Unit>> sources) : IObservable<Unit>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<Unit> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(sources);
        ArgumentExceptionHelper.ThrowIfNull(observer);
        if (sources.Count == 0)
        {
            observer.OnNext(Unit.Default);
            observer.OnCompleted();
            return EmptyDisposable.Instance;
        }

        var sink = new Sink(observer, sources);
        sink.RunNext();
        return sink;
    }

    /// <summary>
    /// Stateful observer that walks the source list sequentially. Each source's
    /// values are ignored; on <c>OnCompleted</c> the next source is subscribed.
    /// When all sources have completed, emits <see cref="Unit.Default"/> and completes.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="sources">The source list to walk.</param>
    private sealed class Sink(
        IObserver<Unit> downstream,
        IReadOnlyList<IObservable<Unit>> sources) : IObserver<Unit>, IDisposable
    {
        /// <summary>Index of the current source being observed.</summary>
        private int _index;

        /// <summary>Subscription to the current source.</summary>
        private IDisposable? _currentSubscription;

        /// <summary>Set once all sources have completed or we've been disposed.</summary>
        private bool _done;

        /// <summary>Guards against re-entrant <see cref="RunNext"/> calls.</summary>
        private bool _looping;

        /// <inheritdoc/>
        public void OnNext(Unit value)
        {
            // Ignore — we only care about completion.
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            if (_done)
            {
                return;
            }

            _done = true;
            downstream.OnError(error);
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
            if (_done)
            {
                return;
            }

            if (_looping)
            {
                // Sync-completion is captured by the probe in RunNext; nothing more to do here.
                return;
            }

            RunNext();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _done = true;
            Interlocked.Exchange(ref _currentSubscription, null)?.Dispose();
        }

        /// <summary>
        /// Subscribes to the next source, or emits Unit and completes if all are done.
        /// Iteratively loops on synchronous completion to avoid recursive stack growth.
        /// </summary>
        internal void RunNext()
        {
            _looping = true;
            try
            {
                while (!_done && _index < sources.Count)
                {
                    var source = sources[_index++];
                    var probe = new CompletionFlagObserver(this);
                    var sub = source.Subscribe(probe);
                    Interlocked.Exchange(ref _currentSubscription, sub);

                    if (!probe.Completed)
                    {
                        return;
                    }
                }
            }
            finally
            {
                _looping = false;
            }

            if (_done)
            {
                return;
            }

            _done = true;
            downstream.OnNext(Unit.Default);
            downstream.OnCompleted();
        }

        /// <summary>
        /// Forwarding <see cref="IObserver{Unit}"/> that records whether <c>OnError</c> or
        /// <c>OnCompleted</c> arrived synchronously during <c>Subscribe</c>. Replaces the
        /// prior <c>_syncCompleted</c> field on <see cref="Sink"/> so the flag is per-iteration
        /// state rather than instance state.
        /// </summary>
        /// <param name="inner">The wrapped sink that receives forwarded notifications.</param>
        private sealed class CompletionFlagObserver(IObserver<Unit> inner) : IObserver<Unit>
        {
            /// <summary>Gets a value indicating whether a terminal notification was observed.</summary>
            public bool Completed { get; private set; }

            /// <inheritdoc/>
            public void OnNext(Unit value) => inner.OnNext(value);

            /// <inheritdoc/>
            public void OnError(Exception error)
            {
                Completed = true;
                inner.OnError(error);
            }

            /// <inheritdoc/>
            public void OnCompleted()
            {
                Completed = true;
                inner.OnCompleted();
            }
        }
    }
}
