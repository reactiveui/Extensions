// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Internal;

/// <summary>
/// Limits the concurrency of task execution and emits results through an observable sequence.
/// </summary>
/// <typeparam name="T">The type of the task results.</typeparam>
internal class ConcurrencyLimiter<T>
{
    /// <summary>
    /// The synchronization gate protecting task scheduling and completion state.
    /// </summary>
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    /// <summary>
    /// Indicates whether this limiter has been disposed. Updated and read lock-free
    /// via <see cref="Interlocked"/> / <see cref="Volatile"/>; the disposal path is a
    /// single-field flip, so no monitor is needed.
    /// </summary>
    private int _disposed;

    /// <summary>
    /// The number of tasks currently in flight that have not yet completed.
    /// </summary>
    private int _outstanding;

    /// <summary>
    /// The enumerator over the source task sequence, or null if all tasks have been pulled or the limiter was disposed.
    /// </summary>
    private IEnumerator<Task<T>>? _rator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyLimiter{T}"/> class.
    /// </summary>
    /// <param name="taskFunctions">The task functions.</param>
    /// <param name="maxConcurrency">The maximum concurrency.</param>
    public ConcurrencyLimiter(IEnumerable<Task<T>> taskFunctions, int maxConcurrency)
    {
        _rator = taskFunctions.GetEnumerator();
        Observable = new DelegateObservable<T>(observer =>
        {
            for (var i = 0; i < maxConcurrency; i++)
            {
                PullNextTask(observer);
            }

            return new ActionDisposable(() => Disposed = true);
        });
    }

    /// <summary>
    /// Gets the i observable.
    /// </summary>
    public IObservable<T> Observable { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="ConcurrencyLimiter{T}"/> is disposed.
    /// </summary>
    /// <value><c>true</c> if disposed; otherwise, <c>false</c>.</value>
    internal bool Disposed
    {
        get => Volatile.Read(ref _disposed) != 0;
        set => Interlocked.Exchange(ref _disposed, value ? 1 : 0);
    }

    /// <summary>
    /// Clears the rator.
    /// </summary>
    internal void ClearRator()
    {
        _rator?.Dispose();
        _rator = null;
    }

    /// <summary>
    /// Processes the task completion.
    /// </summary>
    /// <param name="observer">The observer.</param>
    /// <param name="decendantTask">The decendant Task.</param>
    [SuppressMessage(
        "Major Bug",
        "S4462:Calls to async methods should not be blocking",
        Justification = "Task is guaranteed complete at this call site (IsFaulted/IsCanceled were both false above); reading .Result drives the synchronous IObserver<T> contract without blocking.")]
    internal void ProcessTaskCompletion(IObserver<T> observer, Task<T> decendantTask)
    {
        lock (_gate)
        {
            if (Disposed || decendantTask.IsFaulted || decendantTask.IsCanceled)
            {
                ClearRator();
                if (!Disposed)
                {
                    observer.OnError((decendantTask.Exception == null
                        ? new OperationCanceledException()
                        : decendantTask.Exception.InnerException)!);
                }
            }
            else
            {
                observer.OnNext(decendantTask.Result);
                if (--_outstanding == 0 && _rator == null)
                {
                    observer.OnCompleted();
                }
                else
                {
                    PullNextTask(observer);
                }
            }
        }
    }

    /// <summary>
    /// Pulls the next task.
    /// </summary>
    /// <param name="observer">The observer.</param>
    internal void PullNextTask(IObserver<T> observer)
    {
        lock (_gate)
        {
            if (Disposed)
            {
                ClearRator();
            }

            if (_rator == null)
            {
                return;
            }

            if (!_rator.MoveNext())
            {
                ClearRator();
                if (_outstanding == 0)
                {
                    observer.OnCompleted();
                }

                return;
            }

            _outstanding++;

            // State-carrying ContinueWith so the continuation lambda doesn't capture `this` + `observer`
            // in a closure. The state tuple is value-typed; boxing happens once at scheduling time inside
            // ContinueWith itself, but no per-call closure object is allocated for the lambda.
            _rator.Current?.ContinueWith(
                static (ant, state) =>
                {
                    var (limiter, observer) = ((ConcurrencyLimiter<T> Limiter, IObserver<T> Observer))state!;
                    limiter.ProcessTaskCompletion(observer, ant);
                },
                (this, observer),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
