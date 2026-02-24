// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Internal;

internal class ConcurrencyLimiter<T>
{
    private readonly object _locker = new();
    private readonly object _disposalLocker = new();
    private bool _disposed;
    private int _outstanding;
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
    private bool Disposed
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
    private void ClearRator()
    {
        _rator?.Dispose();
        _rator = null;
    }

    /// <summary>
    /// Processes the task completion.
    /// </summary>
    /// <param name="observer">The observer.</param>
    /// <param name="decendantTask">The decendant Task.</param>
    private void ProcessTaskCompletion(IObserver<T> observer, Task<T> decendantTask)
    {
        lock (_locker)
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
    private void PullNextTask(IObserver<T> observer)
    {
        lock (_locker)
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
