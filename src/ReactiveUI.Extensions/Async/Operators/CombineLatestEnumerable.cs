// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides CombineLatest overloads for enumerable collections of asynchronous observable sequences.
/// </summary>
/// <remarks>These overloads mirror the collection-oriented CombineLatest shape from the synchronous reactive surface,
/// but they are implemented entirely on top of <see cref="IObservableAsync{T}"/>, <see cref="IObserverAsync{T}"/>,
/// and the async coordination primitives already present in this library.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Combines the latest value from each asynchronous observable sequence in the supplied collection.
    /// </summary>
    /// <typeparam name="T">The element type produced by the source sequences.</typeparam>
    /// <param name="sources">The source sequences to combine.</param>
    /// <returns>An observable sequence that emits a snapshot of the latest values whenever any source produces a new value,
    /// after all sources have produced at least one value.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="sources"/> is <see langword="null"/>.</exception>
    public static IObservableAsync<IReadOnlyList<T>> CombineLatest<T>(this IEnumerable<IObservableAsync<T>> sources)
    {
        ArgumentExceptionHelper.ThrowIfNull(sources, nameof(sources));

        return new CombineLatestEnumerableObservable<T>(sources);
    }

    /// <summary>
    /// Combines the latest value from each asynchronous observable sequence in the supplied collection and projects the
    /// resulting snapshot into a result value.
    /// </summary>
    /// <typeparam name="TSource">The element type produced by the source sequences.</typeparam>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="sources">The source sequences to combine.</param>
    /// <param name="resultSelector">A selector that projects the current snapshot of latest values into a result value.</param>
    /// <returns>An observable sequence that emits projected results whenever any source produces a new value, after all
    /// sources have produced at least one value.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="sources"/> or <paramref name="resultSelector"/>
    /// is <see langword="null"/>.</exception>
    public static IObservableAsync<TResult> CombineLatest<TSource, TResult>(
        this IEnumerable<IObservableAsync<TSource>> sources,
        Func<IReadOnlyList<TSource>, TResult> resultSelector)
    {
        ArgumentExceptionHelper.ThrowIfNull(sources, nameof(sources));
        ArgumentExceptionHelper.ThrowIfNull(resultSelector, nameof(resultSelector));

        return sources.CombineLatest().Select(resultSelector);
    }

    /// <summary>
    /// Async observable that combines the latest values from an enumerable collection of source sequences,
    /// emitting a snapshot array whenever any source produces a new value (after all sources have produced at least one).
    /// </summary>
    /// <typeparam name="T">The element type produced by the source sequences.</typeparam>
    /// <param name="sources">The source sequences to combine.</param>
    internal sealed class CombineLatestEnumerableObservable<T>(IEnumerable<IObservableAsync<T>> sources) : ObservableAsync<IReadOnlyList<T>>
    {
        /// <summary>
        /// The materialized list of source observable sequences to combine.
        /// </summary>
        private readonly IReadOnlyList<IObservableAsync<T>> _sources = sources as IReadOnlyList<IObservableAsync<T>> ?? sources.ToList();

        /// <summary>
        /// Subscribes the specified observer by creating a <see cref="Subscription"/> that manages
        /// all source subscriptions and emits combined snapshots.
        /// </summary>
        /// <param name="observer">The observer to receive combined value snapshots.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>An async disposable that tears down the subscription when disposed.</returns>
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<IReadOnlyList<T>> observer, CancellationToken cancellationToken)
        {
            if (_sources.Count == 0)
            {
                await observer.OnCompletedAsync(ReactiveUI.Extensions.Async.Internals.Result.Success);
                return DisposableAsync.Empty;
            }

            var subscription = new Subscription(_sources, observer);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }

            return subscription;
        }

        /// <summary>
        /// Manages subscriptions to all source sequences and coordinates emission of combined value snapshots.
        /// </summary>
        /// <param name="sources">The source observable sequences.</param>
        /// <param name="observer">The downstream observer to forward combined snapshots to.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The observer lifetime is owned by the caller that supplied it during subscription.")]
        internal sealed class Subscription(IReadOnlyList<IObservableAsync<T>> sources, IObserverAsync<IReadOnlyList<T>> observer) : IAsyncDisposable
        {
            /// <summary>
            /// Gate that serializes observer callbacks to ensure thread-safe emission.
            /// </summary>
            private readonly ReactiveUI.Extensions.Async.Internals.AsyncGate _gate = new();

            /// <summary>
            /// Cancellation token source used to signal disposal of the subscription.
            /// </summary>
            private readonly CancellationTokenSource _disposeCts = new();

#if NET9_0_OR_GREATER
            /// <summary>
            /// Lock that protects completion-related state from concurrent access.
            /// </summary>
            private readonly Lock _completionLock = new();
#else
            /// <summary>
            /// Lock that protects completion-related state from concurrent access.
            /// </summary>
            private readonly object _completionLock = new();
#endif

            /// <summary>
            /// The downstream observer to forward combined value snapshots to.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The observer lifetime is owned by the caller that supplied it during subscription.")]
            private readonly IObserverAsync<IReadOnlyList<T>> _observer = observer;

            /// <summary>
            /// The source observable sequences being combined.
            /// </summary>
            private readonly IReadOnlyList<IObservableAsync<T>> _sources = sources;

            /// <summary>
            /// Array holding the latest value from each source, wrapped in <see cref="Optional{T}"/> to track whether a value has been received.
            /// </summary>
            private readonly Optional<T>[] _values = new Optional<T>[sources.Count];

            /// <summary>
            /// Array tracking which source sequences have completed.
            /// </summary>
            private readonly bool[] _completed = new bool[sources.Count];

            /// <summary>
            /// Array holding the disposable subscriptions to each source sequence.
            /// </summary>
            private readonly IAsyncDisposable?[] _subscriptions = new IAsyncDisposable?[sources.Count];

            /// <summary>
            /// The number of source sequences that have completed.
            /// </summary>
            private int _completedCount;

            /// <summary>
            /// Flag indicating whether this subscription has been disposed (1 = disposed, 0 = active).
            /// </summary>
            private int _disposed;

            /// <summary>
            /// Linked cancellation token source combining the caller's token with the dispose token.
            /// </summary>
            private CancellationTokenSource? _linkedCts;

            /// <summary>
            /// The cancellation token from the linked cancellation token source, cached for use in observer callbacks.
            /// </summary>
            private CancellationToken _linkedToken;

            /// <summary>
            /// Subscribes to all source sequences, creating a linked cancellation token for coordinated teardown.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the subscription.</param>
            /// <returns>A task representing the asynchronous subscribe operation.</returns>
            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
                _linkedToken = _linkedCts.Token;

                for (var index = 0; index < _sources.Count; index++)
                {
                    if (_disposeCts.IsCancellationRequested)
                    {
                        return;
                    }

                    var currentIndex = index;
                    _subscriptions[index] = await _sources[index].SubscribeAsync(
                        (value, token) => OnNextAsync(currentIndex, value, token),
                        OnErrorResumeAsync,
                        result => OnCompletedAsync(currentIndex, result),
                        cancellationToken);
                }
            }

            /// <inheritdoc/>
            public ValueTask DisposeAsync() => CompleteAsync(null);

            /// <summary>
            /// Handles a new value from the source at the specified index, updating the latest value
            /// and emitting a snapshot if all sources have produced at least one value.
            /// </summary>
            /// <param name="index">The index of the source that produced the value.</param>
            /// <param name="value">The value produced by the source.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            internal async ValueTask OnNextAsync(int index, T value, CancellationToken cancellationToken)
            {
                using (await _gate.LockAsync())
                {
                    if (DisposalHelper.IsDisposed(_disposed))
                    {
                        return;
                    }

                    _values[index] = new(value);
                    if (!TryCreateSnapshot(out var snapshot))
                    {
                        return;
                    }

                    await _observer.OnNextAsync(snapshot, _linkedToken);
                }
            }

            /// <summary>
            /// Forwards a non-fatal error from any source sequence to the downstream observer.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            internal async ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
            {
                using (await _gate.LockAsync())
                {
                    if (DisposalHelper.IsDisposed(_disposed))
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(error, _linkedToken);
                }
            }

            /// <summary>
            /// Handles a source sequence completing. Propagates completion downstream if the source
            /// failed, if the source completed without ever emitting a value, or if all sources have completed.
            /// </summary>
            /// <param name="index">The index of the source that completed.</param>
            /// <param name="result">The completion result from the source.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            internal ValueTask OnCompletedAsync(int index, ReactiveUI.Extensions.Async.Internals.Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                var shouldComplete = false;
                lock (_completionLock)
                {
                    if (_disposed == 1 || _completed[index])
                    {
                        return default;
                    }

                    _completed[index] = true;
                    _completedCount++;
                    shouldComplete = !_values[index].HasValue || _completedCount == _sources.Count;
                }

                return shouldComplete ? CompleteAsync(ReactiveUI.Extensions.Async.Internals.Result.Success) : default;
            }

            /// <summary>
            /// Disposes all source subscriptions, cancels the linked token, and optionally forwards a
            /// completion result to the downstream observer. This method is idempotent.
            /// </summary>
            /// <param name="result">The completion result to forward, or <see langword="null"/> if disposing without signaling completion.</param>
            /// <returns>A task representing the asynchronous operation.</returns>
            internal async ValueTask CompleteAsync(ReactiveUI.Extensions.Async.Internals.Result? result)
            {
                if (DisposalHelper.TrySetDisposed(ref _disposed))
                {
                    return;
                }

                _disposeCts.Cancel();

                foreach (var subscription in _subscriptions)
                {
                    if (subscription is not null)
                    {
                        await subscription.DisposeAsync();
                    }
                }

                if (result is not null)
                {
                    await _observer.OnCompletedAsync(result.Value);
                }

                _linkedCts?.Dispose();
                _disposeCts.Dispose();
                _gate.Dispose();
            }

            /// <summary>
            /// Attempts to create a snapshot array containing the latest value from each source.
            /// Succeeds only when all sources have produced at least one value.
            /// </summary>
            /// <param name="snapshot">When this method returns <see langword="true"/>, contains the snapshot array; otherwise, an empty array.</param>
            /// <returns><see langword="true"/> if all sources have a value and the snapshot was created; otherwise, <see langword="false"/>.</returns>
            internal bool TryCreateSnapshot(out IReadOnlyList<T> snapshot)
            {
                var values = new T[_values.Length];
                for (var index = 0; index < _values.Length; index++)
                {
                    if (!_values[index].TryGetValue(out var value))
                    {
                        snapshot = Array.Empty<T>();
                        return false;
                    }

                    values[index] = value;
                }

                snapshot = values;
                return true;
            }
        }
    }
}
