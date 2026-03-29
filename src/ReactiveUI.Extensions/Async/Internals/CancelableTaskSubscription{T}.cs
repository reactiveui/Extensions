// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async.Internals;

/// <summary>
/// Represents an asynchronous subscription that can be cancelled and disposed, managing the lifecycle of an
/// observer and its associated operations.
/// </summary>
/// <remarks>This type provides a base for implementing cancellable, asynchronously disposable
/// subscriptions that coordinate observer notifications and resource cleanup. Disposal cancels any ongoing
/// operations and ensures that all resources are released before completion. Derived classes should implement the
/// core execution logic in RunCoreAsync.</remarks>
/// <typeparam name="T">The type of the elements observed by the subscription.</typeparam>
/// <param name="observer">The observer that receives notifications for the subscription. Cannot be null.</param>
internal abstract class CancelableTaskSubscription<T>(IObserverAsync<T> observer) : IAsyncDisposable
{
    /// <summary>
    /// The task completion source used to signal when the subscription's asynchronous operation has finished.
    /// </summary>
    private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The cancellation token source used to cancel the subscription's asynchronous operation upon disposal.
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// An async-local flag that indicates whether the current call is reentrant, preventing deadlocks during disposal.
    /// </summary>
    private readonly AsyncLocal<bool> _reentrant = new();

    /// <summary>
    /// Indicates whether disposal has already been initiated to prevent double-disposal.
    /// </summary>
    private int _disposed;

    /// <summary>
    /// Starts the operation synchronously using the current cancellation token.
    /// </summary>
    /// <remarks>This method initiates the asynchronous operation and does not wait for its completion. To
    /// monitor progress or handle completion, use the asynchronous counterpart directly.</remarks>
#pragma warning disable CA2012 // Use ValueTasks correctly
    public void Run() => _ = RunAsync(_cts.Token);
#pragma warning restore CA2012 // Use ValueTasks correctly

    /// <summary>
    /// Asynchronously releases the resources used by the object and cancels any ongoing operations.
    /// </summary>
    /// <remarks>Call this method to ensure that all resources are released and any pending operations
    /// are cancelled before the object is discarded. Await the returned ValueTask to guarantee that disposal has
    /// completed.</remarks>
    /// <returns>A ValueTask that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cts.Cancel();
        if (!_reentrant.Value)
        {
            await _tcs.Task;
        }

        _cts.Dispose();
    }

    /// <summary>
    /// Attempts to complete the observer with a failure result. If the observer's completion handler
    /// also throws, the exception is routed to <see cref="UnhandledExceptionHandler"/>.
    /// </summary>
    /// <param name="observer">The observer to complete.</param>
    /// <param name="error">The original exception.</param>
    /// <returns>A <see cref="ValueTask"/> representing the operation.</returns>
    internal static async ValueTask CompleteWithFailureAsync(IObserverAsync<T> observer, Exception error)
    {
        try
        {
            await observer.OnCompletedAsync(Result.Failure(error));
        }
        catch (Exception exception)
        {
            UnhandledExceptionHandler.OnUnhandledException(exception);
        }
    }

    /// <summary>
    /// Executes the subscription's core logic, handling exceptions by completing the observer with a failure result.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    internal async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        _reentrant.Value = true;
        try
        {
            await RunAsyncCore(observer, cancellationToken);
        }
        catch (Exception e)
        {
            await CompleteWithFailureAsync(observer, e);
        }
        finally
        {
            _tcs.SetResult(true);
        }
    }

    /// <summary>
    /// When overridden in a derived class, executes the core subscription logic asynchronously.
    /// </summary>
    /// <param name="observer">The observer that receives notifications.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    protected abstract ValueTask RunAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken);
}
