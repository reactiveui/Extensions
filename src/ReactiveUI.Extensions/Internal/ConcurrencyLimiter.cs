// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// A dedicated lock object for thread-safe access to the <see cref="_disposed"/> flag.
    /// </summary>
    private readonly object _disposalLocker = new();

    /// <summary>
    /// Indicates whether this limiter has been disposed.
    /// </summary>
    private bool _disposed;

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
        IObservable = Observable.Create<T>(observer =>
        {
            for (var i = 0; i < maxConcurrency; i++)
            {
                PullNextTask(observer);
            }

            return Disposable.Create(() => Disposed = true);
        });
    }

    /// <summary>
    /// Gets the i observable.
    /// </summary>
    public IObservable<T> IObservable { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="ConcurrencyLimiter{T}"/> is disposed.
    /// </summary>
    /// <value><c>true</c> if disposed; otherwise, <c>false</c>.</value>
    internal bool Disposed
    {
        get
        {
            lock (_disposalLocker)
            {
                return _disposed;
            }
        }

        set
        {
            lock (_disposalLocker)
            {
                _disposed = value;
            }
        }
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
    internal void ProcessTaskCompletion(IObserver<T> observer, Task<T> decendantTask)
    {
        lock (_gate)
        {
            if (Disposed || decendantTask.IsFaulted || decendantTask.IsCanceled)
            {
                ClearRator();
                if (!Disposed)
                {
                    observer.OnError((decendantTask.Exception == null ? new OperationCanceledException() : decendantTask.Exception.InnerException)!);
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
            _rator?.Current?.ContinueWith(
                ant => ProcessTaskCompletion(observer, ant),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
