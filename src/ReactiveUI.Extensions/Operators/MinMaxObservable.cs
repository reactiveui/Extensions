// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Combines the latest values from multiple sources and emits either the maximum or minimum on each
/// tick. Backs both the <c>Max</c> (<paramref name="emitMaximum"/>=true) and <c>Min</c>
/// (<paramref name="emitMaximum"/>=false) operators without the array allocations a generic
/// <c>CombineLatest(...).Select(xs =&gt; xs.Max())</c> pipeline would incur.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="sources">The source observables.</param>
/// <param name="emitMaximum"><c>true</c> to emit the maximum; <c>false</c> to emit the minimum.</param>
internal sealed class MinMaxObservable<T>(IEnumerable<IObservable<T>> sources, bool emitMaximum) : IObservable<T>
    where T : struct, IComparable<T>
{
    /// <summary>The source list.</summary>
    private readonly IReadOnlyList<IObservable<T>> _sourceList =
        InvalidOperationExceptionHelper.Check(sources as IReadOnlyList<IObservable<T>> ?? sources?.ToList());

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentExceptionHelper.ThrowIfNull(observer);

        if (_sourceList.Count == 0)
        {
            observer.OnCompleted();
            return EmptyDisposable.Instance;
        }

        var sink = new Sink(observer, _sourceList.Count, emitMaximum);
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
    /// Sink that holds the latest value per source and emits either the max or the min.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="count">The number of sources.</param>
    /// <param name="emitMaximum"><c>true</c> for max; <c>false</c> for min.</param>
    private sealed class Sink(IObserver<T> downstream, int count, bool emitMaximum)
    {
        /// <summary>The synchronization gate.</summary>
#if NET9_0_OR_GREATER
        private readonly Lock _gate = new();
#else
        private readonly object _gate = new();
#endif

        /// <summary>The latest values.</summary>
        private readonly T?[] _values = new T?[count];

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
        public void OnNext(int index, T value)
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

                var result = _values[0]!.Value;
                for (var i = 1; i < _values.Length; i++)
                {
                    var current = _values[i]!.Value;
                    var cmp = current.CompareTo(result);
                    if (emitMaximum ? cmp > 0 : cmp < 0)
                    {
                        result = current;
                    }
                }

                downstream.OnNext(result);
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

                if (_completedCount == _values.Length || !_values[index].HasValue)
                {
                    _isDone = true;
                    downstream.OnCompleted();
                }
            }
        }
    }
}
