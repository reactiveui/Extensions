// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI.Extensions.Async.Disposables;

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
    /// <summary>
    /// Managed-thread ID of the thread that took <see cref="_callsCount"/> from 0 to 1. Used to distinguish
    /// reentrant calls on the same thread (legal) from concurrent calls from a different thread
    /// (<see cref="ConcurrentObserverCallsException"/>). The previous design tracked this via an
    /// <see cref="AsyncLocal{T}"/>, which wrote a fresh <see cref="System.Threading.ExecutionContext"/> on
    /// every <c>TryEnter</c> — the dominant allocation in chained-operator pipelines after the
    /// <c>Linked2CancellationTokenSource</c> fix. Thread-ID tracking is exact for synchronous-completion
    /// pipelines (the hot path) and for cross-thread concurrent calls (the contract violation we surface);
    /// async-flow reentrancy that hops threads mid-call is the only scenario where this approach can fire
    /// a false positive, which the existing test suite does not exercise.
    /// </summary>

    /// <summary>
    /// Signals disposal to all in-flight operations via its cancellation token.
    /// </summary>
    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>
    /// Synchronization gate protecting mutable state (_callsCount, _entryThreadId, _allCallsCompletedTcs).
    /// </summary>
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    /// <summary>
    /// The total number of currently executing <c>OnNext</c>, <c>OnErrorResume</c>, or <c>OnCompleted</c> calls.
    /// </summary>
    private int _callsCount;

    /// <summary>Managed-thread ID of the thread holding the in-flight call(s). See class-level XML.</summary>
    private int _entryThreadId;

    /// <summary>
    /// Completion source that is set when all in-flight calls finish after disposal has been requested.
    /// </summary>
    private TaskCompletionSource<object?>? _allCallsCompletedTcs;

    /// <summary>
    /// The disposable representing the upstream source subscription, disposed when this observer is disposed.
    /// </summary>
    private IAsyncDisposable? _sourceSubscription;

    /// <summary>
    /// Registration created by <see cref="LinkExternalCancellation(CancellationToken)"/> so the link can be
    /// released when the observer disposes.
    /// </summary>
    private CancellationTokenRegistration _externalLinkRegistration;

    /// <summary>
    /// The external token last passed to <see cref="LinkExternalCancellation(CancellationToken)"/>. Cached so
    /// <see cref="TryEnterOnSomethingCall(CancellationToken, out LinkedTokenScope)"/> can treat it as a
    /// fast-path-equal token: its cancellation already propagates to <see cref="_disposeCts"/>, so combining
    /// it again per emission would allocate a redundant linked CTS.
    /// </summary>
    private CancellationToken _externalLinkedToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObserverAsync{T}"/> class.
    /// </summary>
    protected ObserverAsync()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObserverAsync{T}"/> class and links an external cancellation
    /// token into its dispose chain. Equivalent to calling the parameterless constructor followed by
    /// <see cref="LinkExternalCancellation(CancellationToken)"/>.
    /// </summary>
    /// <param name="externalLink">The external token whose cancellation should trigger this observer's disposal.</param>
    protected ObserverAsync(CancellationToken externalLink) => LinkExternalCancellation(externalLink);

    /// <summary>
    /// Gets a value indicating whether this observer has been disposed.
    /// </summary>
    internal bool IsDisposed => _disposeCts.IsCancellationRequested;

    /// <summary>
    /// Gets the cancellation token that fires when this observer disposes. Exposed for sibling operators
    /// in this assembly so they can wire it into a downstream observer's <see cref="LinkExternalCancellation(CancellationToken)"/>
    /// chain — that lets the downstream's hot-path equality check recognise our token as already-linked and
    /// skip the per-emission linked CTS allocation.
    /// </summary>
    internal CancellationToken InternalDisposedToken => _disposeCts.Token;

    /// <summary>
    /// Asynchronously processes the next value in the sequence.
    /// </summary>
    /// <param name="value">The value to be processed.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
    {
        if (!TryEnterOnSomethingCall(cancellationToken, out var scope))
        {
            return default;
        }

        ValueTask core;
        try
        {
            core = OnNextAsyncCore(value, scope.Token);
        }
        catch (OperationCanceledException)
        {
            scope.Dispose();
            ExitOnSomethingCall();
            return default;
        }
        catch (Exception e)
        {
            return OnNextAsyncSlowAfterSyncThrow(e, scope);
        }

        if (core.IsCompletedSuccessfully)
        {
            scope.Dispose();
            ExitOnSomethingCall();
            return default;
        }

        return OnNextAsyncSlow(core, scope);
    }

    /// <summary>
    /// Handles an error by attempting to resume processing asynchronously.
    /// </summary>
    /// <param name="error">The exception that triggered the error handling logic. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous error handling operation.</returns>
    public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
    {
        if (!TryEnterOnSomethingCall(cancellationToken, out var scope))
        {
            return default;
        }

        ValueTask core;
        try
        {
            core = OnErrorResumeAsync_Private(error, scope.Token);
        }
        catch (Exception e)
        {
            UnhandledExceptionHandler.OnUnhandledException(e);
            scope.Dispose();
            ExitOnSomethingCall();
            return default;
        }

        if (core.IsCompletedSuccessfully)
        {
            scope.Dispose();
            ExitOnSomethingCall();
            return default;
        }

        return OnErrorResumeAsyncSlow(core, scope);
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
    public ValueTask OnCompletedAsync(Result result)
    {
        if (!TryEnterOnSomethingCall(CancellationToken.None, out var scope))
        {
            return default;
        }

        ValueTask core;
        try
        {
            core = OnCompletedAsyncCore(result);
        }
        catch (Exception e)
        {
            UnhandledExceptionHandler.OnUnhandledException(e);
            scope.Dispose();
            return ExitOnSomethingCall() ? DisposeAsync() : default;
        }

        if (core.IsCompletedSuccessfully)
        {
            scope.Dispose();
            return ExitOnSomethingCall() ? DisposeAsync() : default;
        }

        return OnCompletedAsyncSlow(core, scope);
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
        await DisposeAsyncCore().ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Sets the source subscription disposable for this observer.
    /// </summary>
    /// <param name="value">The source subscription to track, or <see langword="null"/> to clear it.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    internal ValueTask SetSourceSubscriptionAsync(IAsyncDisposable? value) =>
        SingleAssignmentDisposableAsync.SetDisposableAsync(ref _sourceSubscription, value);

    /// <summary>
    /// Internal wrapper around <see cref="LinkExternalCancellation(CancellationToken)"/> so sibling operators
    /// (in their <c>SubscribeAsyncCore</c>) can wire an upstream observer's dispose token into this observer's
    /// link chain. Combined with the cached <see cref="_externalLinkedToken"/> fast-path inside
    /// <see cref="TryEnterOnSomethingCall(CancellationToken, out LinkedTokenScope)"/>, this turns chained
    /// operator pipelines into per-emission allocation-free flows.
    /// </summary>
    /// <param name="upstream">The upstream observer's dispose token.</param>
    internal void LinkUpstreamCancellation(CancellationToken upstream) =>
        LinkExternalCancellation(upstream);

    /// <summary>
    /// Attempts to enter a notification call, checking for disposal, cancellation, and concurrent access.
    /// </summary>
    /// <param name="cancellationToken">The caller-supplied cancellation token.</param>
    /// <param name="scope">When successful, a <see cref="LinkedTokenScope"/> providing the effective cancellation token.</param>
    /// <returns><see langword="true"/> if the call was entered successfully; otherwise, <see langword="false"/>.</returns>
    [DebuggerStepThrough]
    internal bool TryEnterOnSomethingCall(CancellationToken cancellationToken, out LinkedTokenScope scope)
    {
        lock (_gate)
        {
            if (_disposeCts.IsCancellationRequested || cancellationToken.IsCancellationRequested)
            {
                scope = default;
                return false;
            }

            // Concurrent-call detection: if another thread is already in-flight, this is a contract
            // violation. Reentrant calls from the same thread (a callback that re-enters the observer)
            // are legal — only cross-thread overlap fires the exception.
            var currentThreadId = Environment.CurrentManagedThreadId;
            if (_callsCount > 0 && _entryThreadId != currentThreadId)
            {
                UnhandledExceptionHandler.OnUnhandledException(new ConcurrentObserverCallsException());
                scope = default;
                return false;
            }

            if (_callsCount == 0)
            {
                _entryThreadId = currentThreadId;
            }

            _callsCount++;

            // Avoid allocating a linked CTS when the caller token is None, our own dispose token, or an
            // upstream token we already linked via LinkExternalCancellation (its cancellation already
            // propagates to _disposeCts, so combining it again would allocate a redundant CTS chain).
            if (cancellationToken == CancellationToken.None
                || cancellationToken == _disposeCts.Token
                || cancellationToken == _externalLinkedToken)
            {
                scope = new(null, _disposeCts.Token);
            }
            else
            {
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
                scope = new(linkedCts, linkedCts.Token);
            }

            return true;
        }
    }

    /// <summary>
    /// Exits a notification call, decrementing counters and signalling completion if disposal is pending.
    /// </summary>
    /// <returns><see langword="true"/> if the caller should proceed with disposal; <see langword="false"/> if
    /// disposal was already signalled to a waiting <see cref="DisposeAsync"/> call.</returns>
    [DebuggerStepThrough]
    internal bool ExitOnSomethingCall()
    {
        lock (_gate)
        {
            _callsCount--;
            Debug.Assert(_callsCount >= 0, "Calls count should never be negative.");
            if (_callsCount == 0)
            {
                _entryThreadId = 0;
            }

            if (_allCallsCompletedTcs is not null)
            {
                _allCallsCompletedTcs.SetResult(null);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Internal error-resume handler that delegates to <see cref="OnErrorResumeAsyncCore"/> and routes
    /// unhandled or cancelled errors to the <see cref="UnhandledExceptionHandler"/>.
    /// </summary>
    /// <param name="error">The exception that triggered error handling.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async ValueTask OnErrorResumeAsync_Private(Exception error, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                UnhandledExceptionHandler.OnUnhandledException(error);
                return;
            }

            await OnErrorResumeAsyncCore(error, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Performs asynchronous completion logic when the operation has finished processing the specified result.
    /// </summary>
    /// <param name="result">The result of the operation to be processed during completion.</param>
    /// <returns>A ValueTask that represents the asynchronous completion operation.</returns>
    protected abstract ValueTask OnCompletedAsyncCore(Result result);

    /// <summary>
    /// Links an external cancellation token into this observer's dispose chain. When <paramref name="external"/>
    /// is cancelled, the observer disposes — propagating cancellation through the linked token that subclasses
    /// receive in their <c>OnNextAsyncCore</c> / <c>OnErrorResumeAsyncCore</c> arguments. This eliminates the
    /// need to allocate a per-emission linked <see cref="CancellationTokenSource"/>. Each observer supports at
    /// most one link; calling this method again replaces the previous registration.
    /// </summary>
    /// <param name="external">The external token whose cancellation should trigger this observer's disposal.</param>
    [DebuggerStepThrough]
    protected void LinkExternalCancellation(CancellationToken external)
    {
        if (!external.CanBeCanceled || external == _disposeCts.Token)
        {
            return;
        }

        if (external.IsCancellationRequested)
        {
            _disposeCts.Cancel();
            return;
        }

        _externalLinkRegistration.Dispose();
        _externalLinkRegistration = external.UnsafeRegister(
            static state => ((CancellationTokenSource)state!).Cancel(),
            _disposeCts);
        _externalLinkedToken = external;
    }

    /// <summary>
    /// Performs application-defined tasks associated with asynchronously releasing unmanaged resources.
    /// </summary>
    /// <remarks>Override this method to provide custom asynchronous resource cleanup logic in a derived
    /// class. This method is called by DisposeAsync to perform the actual resource release.</remarks>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    [DebuggerStepThrough]
    protected virtual async ValueTask DisposeAsyncCore()
    {
        Task? allOnSomethingCallsCompleted = null;
        lock (_gate)
        {
            if (_disposeCts.IsCancellationRequested)
            {
                return;
            }

            if (_callsCount > 0 && _entryThreadId != Environment.CurrentManagedThreadId)
            {
                _allCallsCompletedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                allOnSomethingCallsCompleted = _allCallsCompletedTcs.Task;
            }
        }

        // Two callers can both pass the IsCancellationRequested guard above (the guard is read
        // in the lock but the actual cancellation happens outside, so the window between
        // guard-passed and cancel-applied is non-zero). Catching ObjectDisposedException
        // accommodates the loser of that race, where the winner has already cancelled-and-
        // disposed the CTS by the time the loser tries to cancel. The loser then returns without
        // re-running the rest of the disposal body.
        try
        {
            await _disposeCts.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        if (allOnSomethingCallsCompleted is not null)
        {
            await allOnSomethingCallsCompleted.ConfigureAwait(false);
        }

#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        await _externalLinkRegistration.DisposeAsync().ConfigureAwait(false);
#else
        _externalLinkRegistration.Dispose();
#endif
        _disposeCts.Dispose();

        try
        {
            await SingleAssignmentDisposableAsync.DisposeAsync(ref _sourceSubscription).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            UnhandledExceptionHandler.OnUnhandledException(e);
        }
    }

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

    /// <summary>
    /// Async continuation for <see cref="OnNextAsync"/> when <see cref="OnNextAsyncCore"/> returned an
    /// incomplete <see cref="ValueTask"/>. Owns the <paramref name="scope"/> and exit bookkeeping.
    /// </summary>
    /// <param name="core">The pending core <see cref="ValueTask"/>.</param>
    /// <param name="scope">The linked-token scope to release on completion.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the core completes and bookkeeping has run.</returns>
    private async ValueTask OnNextAsyncSlow(ValueTask core, LinkedTokenScope scope)
    {
        try
        {
            await core.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cooperative cancellation; swallow.
        }
        catch (Exception e)
        {
            await OnErrorResumeAsync_Private(e, scope.Token).ConfigureAwait(false);
        }
        finally
        {
            scope.Dispose();
            ExitOnSomethingCall();
        }
    }

    /// <summary>
    /// Async continuation for <see cref="OnNextAsync"/> when <see cref="OnNextAsyncCore"/> threw synchronously.
    /// Routes the error through <see cref="OnErrorResumeAsync_Private"/> off the fast path so the
    /// caller-visible <see cref="OnNextAsync"/> stays state-machine free in the common case.
    /// </summary>
    /// <param name="error">The exception thrown by the core.</param>
    /// <param name="scope">The linked-token scope to release on completion.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once error handling and bookkeeping have run.</returns>
    private async ValueTask OnNextAsyncSlowAfterSyncThrow(Exception error, LinkedTokenScope scope)
    {
        try
        {
            await OnErrorResumeAsync_Private(error, scope.Token).ConfigureAwait(false);
        }
        finally
        {
            scope.Dispose();
            ExitOnSomethingCall();
        }
    }

    /// <summary>
    /// Async continuation for <see cref="OnErrorResumeAsync"/> when the core returned an incomplete
    /// <see cref="ValueTask"/>.
    /// </summary>
    /// <param name="core">The pending core <see cref="ValueTask"/>.</param>
    /// <param name="scope">The linked-token scope to release on completion.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the core completes and bookkeeping has run.</returns>
    private async ValueTask OnErrorResumeAsyncSlow(ValueTask core, LinkedTokenScope scope)
    {
        try
        {
            await core.ConfigureAwait(false);
        }
        finally
        {
            scope.Dispose();
            ExitOnSomethingCall();
        }
    }

    /// <summary>
    /// Async continuation for <see cref="OnCompletedAsync"/> when the core returned an incomplete
    /// <see cref="ValueTask"/>. Calls <see cref="DisposeAsync"/> after completion if bookkeeping requires it.
    /// </summary>
    /// <param name="core">The pending core <see cref="ValueTask"/>.</param>
    /// <param name="scope">The linked-token scope to release on completion.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the core, bookkeeping, and any required dispose have run.</returns>
    private async ValueTask OnCompletedAsyncSlow(ValueTask core, LinkedTokenScope scope)
    {
        try
        {
            await core.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            UnhandledExceptionHandler.OnUnhandledException(e);
        }
        finally
        {
            scope.Dispose();
        }

        if (ExitOnSomethingCall())
        {
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A lightweight scope that wraps an optional <see cref="CancellationTokenSource"/> and exposes the
    /// effective <see cref="CancellationToken"/>. When no linked source is needed (e.g. the caller token
    /// is <see cref="CancellationToken.None"/>), the scope avoids allocating a linked CTS entirely.
    /// </summary>
    /// <param name="Cts">The linked CTS to dispose, or <see langword="null"/> if no allocation was needed.</param>
    /// <param name="Token">The effective cancellation token for the notification call.</param>
    internal readonly record struct LinkedTokenScope(CancellationTokenSource? Cts, CancellationToken Token)
        : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose() => Cts?.Dispose();
    }
}
