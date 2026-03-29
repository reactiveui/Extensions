// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;

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
    public static IObservableAsync<IList<T>> CombineLatest<T>(this IEnumerable<IObservableAsync<T>> sources)
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
        Func<IList<TSource>, TResult> resultSelector)
    {
        ArgumentExceptionHelper.ThrowIfNull(sources, nameof(sources));
        ArgumentExceptionHelper.ThrowIfNull(resultSelector, nameof(resultSelector));

        return sources.CombineLatest().Select(resultSelector);
    }

    private sealed class CombineLatestEnumerableObservable<T>(IEnumerable<IObservableAsync<T>> sources) : ObservableAsync<IList<T>>
    {
        private readonly IReadOnlyList<IObservableAsync<T>> _sources = sources as IReadOnlyList<IObservableAsync<T>> ?? sources.ToList();

        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<IList<T>> observer, CancellationToken cancellationToken)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The observer lifetime is owned by the caller that supplied it during subscription.")]
        private sealed class Subscription(IReadOnlyList<IObservableAsync<T>> sources, IObserverAsync<IList<T>> observer) : IAsyncDisposable
        {
            private readonly ReactiveUI.Extensions.Async.Internals.AsyncGate _gate = new();
            private readonly CancellationTokenSource _disposeCts = new();
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The observer lifetime is owned by the caller that supplied it during subscription.")]
            private readonly IObserverAsync<IList<T>> _observer = observer;
            private readonly IReadOnlyList<IObservableAsync<T>> _sources = sources;
            private readonly Optional<T>[] _values = new Optional<T>[sources.Count];
            private readonly bool[] _completed = new bool[sources.Count];
            private readonly IAsyncDisposable?[] _subscriptions = new IAsyncDisposable?[sources.Count];
            private int _completedCount;
            private int _disposed;

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
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

            public ValueTask DisposeAsync() => CompleteAsync(null);

            private async ValueTask OnNextAsync(int index, T value, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    _values[index] = new(value);
                    if (!TryCreateSnapshot(out var snapshot))
                    {
                        return;
                    }

                    await _observer.OnNextAsync(snapshot, linkedCts.Token);
                }
            }

            private async ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private ValueTask OnCompletedAsync(int index, ReactiveUI.Extensions.Async.Internals.Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                var shouldComplete = false;
                lock (_disposeCts)
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

            private async ValueTask CompleteAsync(ReactiveUI.Extensions.Async.Internals.Result? result)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
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

                _disposeCts.Dispose();
                _gate.Dispose();
            }

            private bool TryCreateSnapshot(out IList<T> snapshot)
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
