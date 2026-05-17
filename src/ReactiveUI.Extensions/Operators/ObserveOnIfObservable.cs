// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Conditionally switches between two schedulers based on a reactive condition.
/// </summary>
/// <typeparam name="T">The type of elements in the source sequence.</typeparam>
/// <param name="source">The source observable.</param>
/// <param name="condition">The reactive condition observable.</param>
/// <param name="trueScheduler">The scheduler to use when condition is true.</param>
/// <param name="falseScheduler">The scheduler to use when condition is false.</param>
internal sealed class ObserveOnIfObservable<T>(
    IObservable<T> source,
    IObservable<bool> condition,
    IScheduler trueScheduler,
    IScheduler falseScheduler) : IObservable<T>
{
    /// <summary>
    /// The source observable.
    /// </summary>
    private readonly IObservable<T> _source = InvalidOperationExceptionHelper.Check(source);

    /// <summary>
    /// The reactive condition observable.
    /// </summary>
    private readonly IObservable<bool> _condition = InvalidOperationExceptionHelper.Check(condition);

    /// <summary>
    /// The scheduler to use when condition is true.
    /// </summary>
    private readonly IScheduler _trueScheduler = InvalidOperationExceptionHelper.Check(trueScheduler);

    /// <summary>
    /// The scheduler to use when condition is false.
    /// </summary>
    private readonly IScheduler _falseScheduler = InvalidOperationExceptionHelper.Check(falseScheduler);

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentExceptionHelper.ThrowIfNull(observer);

        var sink = new ObserveOnIfSink(observer, _trueScheduler, _falseScheduler);
        var conditionSub = _condition.Subscribe(sink.ConditionObserver);
        var sourceSub = _source.Subscribe(sink);
        return new DisposableBag(sourceSub, conditionSub, sink);
    }

    /// <summary>
    /// Sinks the source observable and conditionally observes on different schedulers.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    /// <param name="trueScheduler">The scheduler to use when condition is true.</param>
    /// <param name="falseScheduler">The scheduler to use when condition is false.</param>
    private sealed class ObserveOnIfSink(
        IObserver<T> downstream,
        IScheduler trueScheduler,
        IScheduler falseScheduler) : IObserver<T>, IDisposable
    {
#if NET9_0_OR_GREATER
        /// <summary>
        /// The gate for synchronization.
        /// </summary>
        private readonly Lock _gate = new();
#else
        /// <summary>
        /// The gate for synchronization.
        /// </summary>
        private readonly object _gate = new();
#endif

        /// <summary>
        /// The downstream observer.
        /// </summary>
        private readonly IObserver<T> _downstream = InvalidOperationExceptionHelper.Check(downstream);

        /// <summary>
        /// The scheduler to use when condition is true.
        /// </summary>
        private readonly IScheduler _trueScheduler = InvalidOperationExceptionHelper.Check(trueScheduler);

        /// <summary>
        /// The scheduler to use when condition is false.
        /// </summary>
        private readonly IScheduler _falseScheduler = InvalidOperationExceptionHelper.Check(falseScheduler);

        /// <summary>
        /// The current scheduler.
        /// </summary>
        private IScheduler _currentScheduler = falseScheduler;

        /// <summary>
        /// The last condition value.
        /// </summary>
        private bool _lastCondition;

        /// <summary>
        /// Whether the condition has been received.
        /// </summary>
        private bool _hasCondition;

        /// <summary>
        /// Whether the sequence is done.
        /// </summary>
        private bool _done;

        /// <summary>
        /// Gets the condition observer.
        /// </summary>
        public IObserver<bool> ConditionObserver => new DelegateObserver<bool>(c =>
        {
            lock (_gate)
            {
                if (_hasCondition && _lastCondition == c)
                {
                    return;
                }

                _currentScheduler = c ? _trueScheduler : _falseScheduler;
                _lastCondition = c;
                _hasCondition = true;
            }
        });

        /// <inheritdoc/>
        public void OnNext(T value)
        {
            IScheduler scheduler;
            lock (_gate)
            {
                if (_done)
                {
                    return;
                }

                scheduler = _currentScheduler;
            }

            scheduler.Schedule(() =>
            {
                lock (_gate)
                {
                    if (_done)
                    {
                        return;
                    }
                }

                _downstream.OnNext(value);
            });
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
            }

            _downstream.OnError(error);
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
            }

            _downstream.OnCompleted();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_gate)
            {
                _done = true;
            }
        }
    }
}
