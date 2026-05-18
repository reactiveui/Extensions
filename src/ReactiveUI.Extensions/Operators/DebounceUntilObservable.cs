// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Debounces a sequence until a condition becomes true for an element.
/// </summary>
/// <typeparam name="T">The type of elements in the source sequence.</typeparam>
/// <param name="source">The source observable.</param>
/// <param name="debounce">The debounce duration.</param>
/// <param name="condition">The condition to determine if an element should be emitted immediately or debounced.</param>
/// <param name="scheduler">The scheduler to use for timing.</param>
internal sealed class DebounceUntilObservable<T>(
    IObservable<T> source,
    TimeSpan debounce,
    Func<T, bool> condition,
    IScheduler scheduler) : IObservable<T>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(source);
        InvalidOperationExceptionHelper.ThrowIfNull(condition);
        InvalidOperationExceptionHelper.ThrowIfNull(scheduler);
        ArgumentExceptionHelper.ThrowIfNull(observer);

        var sink = new DebounceUntilSink(observer, debounce, condition, scheduler);
        var subscription = source.Subscribe(sink);
        return new DisposableBag(subscription, sink);
    }

    /// <summary>
    /// Sink for the debounce-until observable. Composes <see cref="TimerSinkState{T}"/> for the
    /// shared gate / timer / done-flag plumbing so this class only carries the OnNext logic.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="debounce">The debounce duration.</param>
    /// <param name="condition">The condition.</param>
    /// <param name="scheduler">The scheduler.</param>
    private sealed class DebounceUntilSink(
        IObserver<T> downstream,
        TimeSpan debounce,
        Func<T, bool> condition,
        IScheduler scheduler) : IObserver<T>, IDisposable
    {
        /// <summary>Shared gate / timer / done-flag plumbing.</summary>
        private readonly TimerSinkState<T> _state = new(downstream);

        /// <inheritdoc/>
        public void OnNext(T value)
        {
            lock (_state.Gate)
            {
                if (_state.Done)
                {
                    return;
                }

                if (condition(value))
                {
                    _state.Timer.Disposable = null;
                    downstream.OnNext(value);
                }
                else
                {
                    _state.Timer.Disposable = scheduler.Schedule(debounce, () =>
                    {
                        lock (_state.Gate)
                        {
                            if (!_state.Done)
                            {
                                downstream.OnNext(value);
                            }
                        }
                    });
                }
            }
        }

        /// <inheritdoc/>
        public void OnError(Exception error) => _state.HandleError(error);

        /// <inheritdoc/>
        public void OnCompleted() => _state.HandleCompleted();

        /// <inheritdoc/>
        public void Dispose() => _state.HandleDispose();
    }
}
