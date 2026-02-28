// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Represents an asynchronous observer that processes notifications of type <typeparamref name="T"/> using asynchronous
/// methods.
/// </summary>
/// <remarks>Implement this abstract class to handle asynchronous event streams or push-based data sources, where
/// notifications may arrive concurrently or in rapid succession. The observer provides asynchronous methods for
/// handling new data, errors, and completion signals, and supports proper resource cleanup via asynchronous disposal.
/// Instances are not thread-safe for concurrent notification handling; notifications are processed sequentially, and
/// reentrant calls are detected and reported as unhandled exceptions.</remarks>
/// <typeparam name="T">The type of the elements received by the observer.</typeparam>
public abstract class ObserverAsync<T> : IObserverAsync<T>
{
    private readonly AsyncLocal<int> _reentrantCallsCount = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private int _callsCount;
    private TaskCompletionSource<object?>? _allCallsCompletedTcs;
    private IAsyncDisposable? _sourceSubscription;

    internal bool IsDisposed => _disposeCts.IsCancellationRequested;

    /// <summary>
    /// Asynchronously processes the next value in the sequence.
    /// </summary>
    /// <param name="value">The value to be processed.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
    {
        if (!TryEnterOnSomethingCall(cancellationToken, out var linkedCts))
        {
            return;
        }

        var linkedToken = linkedCts.Token;
        try
        {
            await OnNextAsyncCore(value, linkedToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            await OnErrorResumeAsync_Private(e, linkedToken);
        }
        finally
        {
            linkedCts.Dispose();
            ExitOnSomethingCall();
        }
    }

    /// <summary>
    /// Handles an error by attempting to resume processing asynchronously.
    /// </summary>
    /// <param name="error">The exception that triggered the error handling logic. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous error handling operation.</returns>
    public async ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
    {
        if (!TryEnterOnSomethingCall(cancellationToken, out var linkedCts))
        {
            return;
        }

        try
        {
            await OnErrorResumeAsync_Private(error, linkedCts.Token);
        }
        finally
        {
            linkedCts.Dispose();
            ExitOnSomethingCall();
        }
    }

    /// <summary>
    /// Asynchronously performs completion logic when the operation has finished, handling any finalization or cleanup
    /// tasks required.
    /// </summary>
    /// <remarks>If an unhandled exception occurs during completion, it is passed to the unhandled exception
    /// handler. This method ensures that necessary resources are released after completion.</remarks>
    /// <param name="result">The result of the completed operation, containing information about its outcome.</param>
    /// <returns>A task that represents the asynchronous completion operation.</returns>
    [DebuggerStepThrough]
    public async ValueTask OnCompletedAsync(Result result)
    {
        if (!TryEnterOnSomethingCall(CancellationToken.None, out var linkedCts))
        {
            return;
        }

        try
        {
            await OnCompletedAsyncCore(result);
        }
        catch (Exception e)
        {
            UnhandledExceptionHandler.OnUnhandledException(e);
        }
        finally
        {
            linkedCts.Dispose();
            if (ExitOnSomethingCall())
            {
                await DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Asynchronously releases the resources used by the object.
    /// </summary>
    /// <remarks>Call this method to clean up resources when the object is no longer needed. This method is
    /// safe to call multiple times; subsequent calls after disposal will have no effect. Any unhandled exceptions that
    /// occur during disposal are captured and reported but do not prevent the completion of the dispose
    /// operation.</remarks>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    [DebuggerStepThrough]
    public async ValueTask DisposeAsync()
    {
        Task? allOnSomethingCallsCompleted = null;
        lock (_reentrantCallsCount)
        {
            if (_disposeCts.IsCancellationRequested)
            {
                return;
            }

            _disposeCts.Cancel();
            if (_reentrantCallsCount.Value == 0 && _callsCount > 0)
            {
                _allCallsCompletedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                allOnSomethingCallsCompleted = _allCallsCompletedTcs.Task;
            }
        }

        if (allOnSomethingCallsCompleted is not null)
        {
            await allOnSomethingCallsCompleted;
        }

        _disposeCts.Dispose();

        try
        {
            await DisposeAsyncCore();
        }
        catch (Exception e)
        {
            UnhandledExceptionHandler.OnUnhandledException(e);
        }

        try
        {
            await SingleAssignmentDisposableAsync.DisposeAsync(ref _sourceSubscription);
        }
        catch (Exception e)
        {
            UnhandledExceptionHandler.OnUnhandledException(e);
        }
    }

    internal ValueTask SetSourceSubscriptionAsync(IAsyncDisposable? value) => SingleAssignmentDisposableAsync.SetDisposableAsync(ref _sourceSubscription, value);

    /// <summary>
    /// Performs asynchronous completion logic when the operation has finished processing the specified result.
    /// </summary>
    /// <param name="result">The result of the operation to be processed during completion.</param>
    /// <returns>A ValueTask that represents the asynchronous completion operation.</returns>
    protected abstract ValueTask OnCompletedAsyncCore(Result result);

    /// <summary>
    /// Performs application-defined tasks associated with asynchronously releasing unmanaged resources.
    /// </summary>
    /// <remarks>Override this method to provide custom asynchronous resource cleanup logic in a derived
    /// class. This method is called by DisposeAsync to perform the actual resource release. The default implementation
    /// does nothing and returns a completed task.</remarks>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    [DebuggerStepThrough]
    protected virtual ValueTask DisposeAsyncCore() => default;

    /// <summary>
    /// Handles an error by providing an asynchronous mechanism to resume execution after an exception occurs.
    /// </summary>
    /// <remarks>Override this method to implement custom error recovery or resumption logic in derived
    /// classes. The method is called when an error occurs and allows the operation to continue or perform cleanup
    /// asynchronously.</remarks>
    /// <param name="error">The exception that triggered the error handling logic. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous error handling operation.</param>
    /// <returns>A ValueTask that represents the asynchronous operation of resuming execution after the error.</returns>
    protected abstract ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken);

    /// <summary>
    /// Processes the next value in the asynchronous sequence.
    /// </summary>
    /// <param name="value">The value to be processed.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    protected abstract ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken);

    [DebuggerStepThrough]
    private bool TryEnterOnSomethingCall(CancellationToken cancellationToken, [NotNullWhen(true)] out CancellationTokenSource? linkedCts)
    {
        lock (_reentrantCallsCount)
        {
            if (_disposeCts.IsCancellationRequested || cancellationToken.IsCancellationRequested)
            {
                linkedCts = null;
                return false;
            }

            var reentrantCallsCount = _reentrantCallsCount.Value;
            if (_callsCount != reentrantCallsCount)
            {
                UnhandledExceptionHandler.OnUnhandledException(new ConcurrentObserverCallsException());
                linkedCts = null;
                return false;
            }

            _callsCount++;
            _reentrantCallsCount.Value = reentrantCallsCount + 1;

            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
            return true;
        }
    }

    [DebuggerStepThrough]
    private bool ExitOnSomethingCall()
    {
        lock (_reentrantCallsCount)
        {
            _callsCount--;
            var reentrantCallsCount = --_reentrantCallsCount.Value;
            Debug.Assert(reentrantCallsCount >= 0, "Reentrant calls count should never be negative.");
            Debug.Assert(_callsCount == reentrantCallsCount, "Calls count and reentrant calls count should be equal when exiting a call.");
            if (_allCallsCompletedTcs is not null)
            {
                _allCallsCompletedTcs.SetResult(null);
                return false;
            }
        }

        return true;
    }

    private async ValueTask OnErrorResumeAsync_Private(Exception error, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                UnhandledExceptionHandler.OnUnhandledException(error);
                return;
            }

            await OnErrorResumeAsyncCore(error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            UnhandledExceptionHandler.OnUnhandledException(error);
        }
        catch (Exception e)
        {
            UnhandledExceptionHandler.OnUnhandledException(e);
        }
    }
}
