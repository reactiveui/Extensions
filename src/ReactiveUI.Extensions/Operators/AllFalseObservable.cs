// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Optimized operator that combines latest booleans and emits true only when all are false.
/// Replaces .CombineLatest(xs => xs.All(x => !x)) to avoid array allocations on every emission.
/// </summary>
/// <param name="sources">The source observables.</param>
internal sealed class AllFalseObservable(IEnumerable<IObservable<bool>> sources) : IObservable<bool>
{
    /// <summary>The source list.</summary>
    private readonly IReadOnlyList<IObservable<bool>> _sourceList =
        InvalidOperationExceptionHelper.Check(sources as IReadOnlyList<IObservable<bool>> ?? sources?.ToList());

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<bool> observer)
    {
        ArgumentExceptionHelper.ThrowIfNull(observer);

        if (_sourceList.Count == 0)
        {
            observer.OnNext(true);
            observer.OnCompleted();
            return EmptyDisposable.Instance;
        }

        var sink = new AllFalseSink(observer, _sourceList.Count);
        var composite = new DisposableBag();
        for (var i = 0; i < _sourceList.Count; i++)
        {
            var index = i;
            composite.Add(_sourceList[i].SubscribeCallbacks(
                value => sink.OnNext(index, value),
                sink.OnError,
                () => sink.OnCompleted(index)));
        }

        return composite;
    }

    /// <summary>
    /// Sink that manages the combined state for <see cref="AllFalseObservable"/>.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="count">The number of sources.</param>
    private sealed class AllFalseSink(IObserver<bool> downstream, int count)
    {
        /// <summary>The synchronization gate.</summary>
#if NET9_0_OR_GREATER
        private readonly Lock _gate = new();
#else
        private readonly object _gate = new();
#endif

        /// <summary>The latest values.</summary>
        private readonly bool?[] _values = new bool?[count];

        /// <summary>The completion status.</summary>
        private readonly bool[] _completed = new bool[count];

        /// <summary>The number of sources with values.</summary>
        private int _hasValueCount;

        /// <summary>The number of completed sources.</summary>
        private int _completedCount;

        /// <summary>The terminal flag.</summary>
        private bool _isDone;

        /// <summary>Handles OnNext from a source.</summary>
        /// <param name="index">Source index.</param>
        /// <param name="value">Emitted value.</param>
        public void OnNext(int index, bool value)
        {
            lock (_gate)
            {
                if (_isDone)
                {
                    return;
                }

                if (!_values[index].HasValue)
                {
                    _hasValueCount++;
                }

                _values[index] = value;

                if (_hasValueCount < _values.Length)
                {
                    return;
                }

                var allFalse = true;
                for (var i = 0; i < _values.Length; i++)
                {
                    if (_values[i] == true)
                    {
                        allFalse = false;
                        break;
                    }
                }

                downstream.OnNext(allFalse);
            }
        }

        /// <summary>Handles OnError from any source.</summary>
        /// <param name="error">The error.</param>
        public void OnError(Exception error)
        {
            lock (_gate)
            {
                if (_isDone)
                {
                    return;
                }

                _isDone = true;
                downstream.OnError(error);
            }
        }

        /// <summary>Handles OnCompleted from a source.</summary>
        /// <param name="index">Source index.</param>
        public void OnCompleted(int index)
        {
            lock (_gate)
            {
                if (_isDone || _completed[index])
                {
                    return;
                }

                _completed[index] = true;
                _completedCount++;

                // Complete if all sources completed OR if a source completed without ever emitting a value
                if (_completedCount == _values.Length || !_values[index].HasValue)
                {
                    _isDone = true;
                    downstream.OnCompleted();
                }
            }
        }
    }
}
