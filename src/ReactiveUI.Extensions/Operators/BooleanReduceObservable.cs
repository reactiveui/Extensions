// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Combines the latest boolean values from multiple sources and emits <c>true</c> iff every latest
/// value equals <paramref name="target"/>. Backs both <c>AllTrue</c> (target=true) and <c>AllFalse</c>
/// (target=false) without the array allocations a generic <c>CombineLatest(...).Select(xs =&gt; xs.All(...))</c>
/// pipeline would incur.
/// </summary>
/// <param name="sources">The source observables.</param>
/// <param name="target">The value every source must hold for the operator to emit <c>true</c>.</param>
internal sealed class BooleanReduceObservable(IEnumerable<IObservable<bool>> sources, bool target) : IObservable<bool>
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

        var sink = new Sink(observer, _sourceList.Count, target);
        return IndexedSubscribeHelper.SubscribeIndexed<bool>(_sourceList, sink.OnNext, sink.OnError, sink.OnCompleted);
    }

    /// <summary>
    /// Sink that holds the latest value per source and reduces them against <paramref name="target"/>.
    /// Composes <see cref="ReduceSinkState{TIn, TOut}"/> for the shared gate / value cache / OnError /
    /// OnCompleted plumbing so this class carries only the per-operator reduce step.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="count">The number of sources.</param>
    /// <param name="target">The value every source must hold for emit to be <c>true</c>.</param>
    private sealed class Sink(IObserver<bool> downstream, int count, bool target)
    {
        /// <summary>Shared gate / value cache / terminal-state plumbing.</summary>
        private readonly ReduceSinkState<bool, bool> _state = new(downstream, count);

        /// <summary>Handles OnNext from a source.</summary>
        /// <param name="index">Source index.</param>
        /// <param name="value">Emitted value.</param>
        public void OnNext(int index, bool value)
        {
            lock (_state.Gate)
            {
                if (_state.IsDone)
                {
                    return;
                }

                if (!_state.Values[index].HasValue)
                {
                    _state.HasValueCount++;
                }

                _state.Values[index] = value;

                if (!_state.AllValuesPresent)
                {
                    return;
                }

                var matches = true;
                for (var i = 0; i < _state.Values.Length; i++)
                {
                    if (_state.Values[i] != target)
                    {
                        matches = false;
                        break;
                    }
                }

                _state.Downstream.OnNext(matches);
            }
        }

        /// <summary>Handles OnError from any source.</summary>
        /// <param name="error">The error.</param>
        public void OnError(Exception error) => _state.HandleError(error);

        /// <summary>Handles OnCompleted from a source.</summary>
        /// <param name="index">Source index.</param>
        public void OnCompleted(int index) => _state.HandleCompleted(index);
    }
}
