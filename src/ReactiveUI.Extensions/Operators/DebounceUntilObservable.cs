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
    /// Sink for the debounce until observable.
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
        /// <summary>
        /// The gate for thread safety.
        /// </summary>
#if NET9_0_OR_GREATER
        private readonly Lock _gate = new();
#else
        private readonly object _gate = new();
#endif

        /// <summary>
        /// The timer for debouncing.
        /// </summary>
        private readonly SwapDisposable _timer = new();

        /// <summary>
        /// Whether the sequence is done.
        /// </summary>
        private bool _done;

        /// <inheritdoc/>
        public void OnNext(T value)
        {
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                if (condition(value))
                {
                    _timer.Disposable = null;
                    downstream.OnNext(value);
                }
                else
                {
                    _timer.Disposable = scheduler.Schedule(debounce, () =>
                    {
                        lock (_gate)
                        {
                            if (!_done)
                            {
                                downstream.OnNext(value);
                            }
                        }
                    });
                }
            }
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                _done = true;
                _timer.Dispose();
                downstream.OnError(error);
            }
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                _done = true;
                _timer.Dispose();
                downstream.OnCompleted();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_gate)
            {
                _done = true;
                _timer.Dispose();
            }
        }
    }
}
