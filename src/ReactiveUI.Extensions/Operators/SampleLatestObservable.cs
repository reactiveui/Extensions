// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Samples the latest value from the source observable whenever a trigger observable emits.
/// </summary>
/// <typeparam name="T">The type of elements in the source sequence.</typeparam>
/// <param name="source">The source observable.</param>
/// <param name="trigger">The trigger observable.</param>
internal sealed class SampleLatestObservable<T>(
    IObservable<T> source,
    IObservable<object> trigger) : IObservable<T>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(source);
        InvalidOperationExceptionHelper.ThrowIfNull(trigger);
        ArgumentExceptionHelper.ThrowIfNull(observer);

        var sink = new SampleLatestSink(observer);
        var sourceSub = source.Subscribe(sink.SourceObserver);
        var triggerSub = trigger.Subscribe(sink.TriggerObserver);
        return new DisposableBag(sourceSub, triggerSub, sink);
    }

    /// <summary>
    /// Sinks the source observable and samples it based on the trigger.
    /// </summary>
    /// <param name="downstream">The downstream observer.</param>
    private sealed class SampleLatestSink(IObserver<T> downstream) : IDisposable
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
        /// The latest value from the source.
        /// </summary>
        private T? _latest;

        /// <summary>
        /// Whether the source has produced a value.
        /// </summary>
        private bool _hasValue;

        /// <summary>
        /// Whether the sequence is done.
        /// </summary>
        private bool _done;

        /// <summary>
        /// Gets the source observer.
        /// </summary>
        public IObserver<T> SourceObserver => new DelegateObserver<T>(
            v =>
            {
                lock (_gate)
                {
                    _latest = v;
                    _hasValue = true;
                }
            },
            ex =>
            {
                lock (_gate)
                {
                    if (_done)
                    {
                        return;
                    }

                    _done = true;
                    downstream.OnError(ex);
                }
            },
            () =>
            {
                lock (_gate)
                {
                    if (_done)
                    {
                        return;
                    }

                    _done = true;
                    downstream.OnCompleted();
                }
            });

        /// <summary>
        /// Gets the trigger observer.
        /// </summary>
        public IObserver<object> TriggerObserver => new DelegateObserver<object>(
            _ =>
            {
                T? value;
                bool shouldEmit;
                lock (_gate)
                {
                    shouldEmit = _hasValue;
                    value = _latest;
                }

                if (!shouldEmit)
                {
                    return;
                }

                downstream.OnNext(value!);
            },
            ex =>
            {
                lock (_gate)
                {
                    if (_done)
                    {
                        return;
                    }

                    _done = true;
                    downstream.OnError(ex);
                }
            },
            () =>
            {
                /* Trigger completion does not affect sample */
            });

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
