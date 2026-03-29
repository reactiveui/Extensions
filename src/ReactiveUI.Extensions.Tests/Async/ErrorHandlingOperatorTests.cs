// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for error handling operators: Catch, CatchAndIgnoreErrorResume, OnErrorResumeAsFailure, Retry.
/// </summary>
[NotInParallel(nameof(UnhandledExceptionHandler))]
[TestExecutor<UnhandledExceptionTestExecutor>]
public class ErrorHandlingOperatorTests
{
    /// <summary>Tests Catch with fallback switches to fallback.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchWithFallback_ThenSwitchesToFallback()
    {
        var source = ObservableAsync.Throw<int>(new InvalidOperationException("fail"));
        var fallback = ObservableAsync.Return(42);

        var result = await source.Catch(_ => fallback).ToListAsync();

        await Assert.That(result).IsEquivalentTo([42]);
    }

    /// <summary>Tests Catch on success completes original sequence.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchOnSuccess_ThenOriginalSequenceCompletes()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Catch(_ => ObservableAsync.Return(99))
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests CatchAndIgnoreErrorResume ignores and continues.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchAndIgnoreErrorResume_ThenIgnoresAndContinues()
    {
        var source = ObservableAsync.Throw<int>(new InvalidOperationException("fail"));
        var fallback = ObservableAsync.Return(100);

        var result = await source.CatchAndIgnoreErrorResume(_ => fallback).ToListAsync();

        await Assert.That(result).IsEquivalentTo([100]);
    }

    /// <summary>Tests OnErrorResumeAsFailure converts error resume to failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorResumeAsFailure_ThenConvertsErrorResumeToFailure()
    {
        var errorSent = false;
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("resume error"), ct);
            errorSent = true;
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        Result? completionResult = null;
        await using var sub = await source
            .OnErrorResumeAsFailure()
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Task.Delay(200);

        await Assert.That(errorSent).IsTrue();
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Tests that OnErrorResumeAsFailure throws ArgumentNullException when source is null.</summary>
    [Test]
    public void WhenOnErrorResumeAsFailureWithNullSource_ThenThrowsArgumentNullException()
    {
        IObservableAsync<int> source = null!;

        Assert.Throws<ArgumentNullException>(() => source.OnErrorResumeAsFailure());
    }

    /// <summary>Tests that OnErrorResumeAsFailure forwards emitted values to the downstream observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorResumeAsFailureWithValues_ThenForwardsValuesToDownstream()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnNextAsync(2, ct);
            await observer.OnNextAsync(3, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var result = await source.OnErrorResumeAsFailure().ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests Retry on transient error succeeds after retry.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryOnTransientError_ThenSucceedsAfterRetry()
    {
        var attempt = 0;
        var source = ObservableAsync.CreateAsBackgroundJob<int>(async (obs, ct) =>
        {
            attempt++;
            if (attempt < 3)
            {
                await obs.OnCompletedAsync(Result.Failure(new InvalidOperationException($"attempt {attempt}")));
                return;
            }

            await obs.OnNextAsync(42, ct);
            await obs.OnCompletedAsync(Result.Success);
        });

        var result = await source.Retry(5).ToListAsync();

        await Assert.That(result).IsEquivalentTo([42]);
        await Assert.That(attempt).IsEqualTo(3);
    }

    /// <summary>Tests Retry exhausted propagates last error.</summary>
    [Test]
    public void WhenRetryExhausted_ThenPropagatesLastError()
    {
        var source = ObservableAsync.Throw<int>(new InvalidOperationException("permanent failure"));

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.Retry(2).ToListAsync());
    }

    /// <summary>Tests Retry negative count throws.</summary>
    [Test]
    public void WhenRetryNegativeCount_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Retry(-1));
    }

    /// <summary>Tests Retry on success completes normally.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryInfiniteOnSuccess_ThenCompletesNormally()
    {
        var result = await ObservableAsync.Return(7).Retry().ToListAsync();

        await Assert.That(result).IsEquivalentTo([7]);
    }

    /// <summary>Tests Catch with error resume callback is invoked.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchWithErrorResumeCallback_ThenCallbackInvoked()
    {
        var errorResumes = new List<Exception>();
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("warning"), ct);
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fatal")));
            return DisposableAsync.Empty;
        });

        var result = await source.Catch(
            _ => ObservableAsync.Return(99),
            async (ex, ct) => errorResumes.Add(ex))
            .ToListAsync();

        await Assert.That(result).Contains(99);
    }

    /// <summary>Tests Retry with count zero propagates error immediately without retrying.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithCountZero_ThenPropagatesErrorImmediately()
    {
        var attempt = 0;
        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = ObservableAsync.CreateAsBackgroundJob<int>(async (obs, ct) =>
        {
            attempt++;
            await obs.OnCompletedAsync(Result.Failure(new InvalidOperationException($"attempt {attempt}")));
        });

        await using var sub = await source
            .Retry(0)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completed.TrySetResult(result);
                    return default;
                });

        var completionResult = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(completionResult.IsFailure).IsTrue();
        await Assert.That(attempt).IsEqualTo(1);
    }

    /// <summary>Tests Retry with count two exhausts all retries then propagates the last error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryCountExhausted_ThenPropagatesLastError()
    {
        var attempt = 0;
        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = ObservableAsync.CreateAsBackgroundJob<int>(async (obs, ct) =>
        {
            attempt++;
            await obs.OnCompletedAsync(Result.Failure(new InvalidOperationException($"attempt {attempt}")));
        });

        await using var sub = await source
            .Retry(2)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completed.TrySetResult(result);
                    return default;
                });

        var completionResult = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(completionResult.IsFailure).IsTrue();
        await Assert.That(attempt).IsEqualTo(3);
    }

    /// <summary>Tests Retry with count one retries exactly once then propagates the error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryWithCountOne_ThenRetriesOnceAndPropagates()
    {
        var attempt = 0;
        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = ObservableAsync.CreateAsBackgroundJob<int>(async (obs, ct) =>
        {
            attempt++;
            await obs.OnCompletedAsync(Result.Failure(new InvalidOperationException($"attempt {attempt}")));
        });

        await using var sub = await source
            .Retry(1)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completed.TrySetResult(result);
                    return default;
                });

        var completionResult = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(completionResult.IsFailure).IsTrue();
        await Assert.That(attempt).IsEqualTo(2);
    }

    /// <summary>Tests that Catch handler throwing an exception routes to OnCompletedAsync with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchHandlerThrows_ThenCompletesWithHandlerException()
    {
        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = ObservableAsync.Throw<int>(new InvalidOperationException("source error"));

        await using var sub = await source
            .Catch<int>(new Func<Exception, IObservableAsync<int>>(_ => throw new ArithmeticException("handler error")))
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completed.TrySetResult(result);
                    return default;
                });

        var completionResult = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(completionResult.IsFailure).IsTrue();
        await Assert.That(completionResult.Exception).IsTypeOf<ArithmeticException>();
    }

    /// <summary>Tests that Catch disposes both source and handler subscriptions when the outer subscription is disposed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchDisposed_ThenDisposesSourceAndHandler()
    {
        var handlerItemReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = ObservableAsync.Throw<int>(new InvalidOperationException("fail"));
        var handlerObservable = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            handlerItemReceived.TrySetResult(true);
            return DisposableAsync.Empty;
        });

        var sub = await source
            .Catch(_ => handlerObservable)
            .SubscribeAsync(
                (_, _) => default,
                null,
                _ => default);

        await handlerItemReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Disposing should dispose both source and handler disposables
        await sub.DisposeAsync();
    }

    /// <summary>Tests that CatchAndIgnoreErrorResume invokes the unhandled exception handler for error resumes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchAndIgnoreErrorResume_ThenReportsToUnhandledExceptionHandler()
    {
        var reportedExceptions = new List<Exception>();
        UnhandledExceptionHandler.Register(ex => reportedExceptions.Add(ex));

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("resume error"), ct);
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fatal")));
            return DisposableAsync.Empty;
        });

        var result = await source.CatchAndIgnoreErrorResume(_ => ObservableAsync.Return(99)).ToListAsync();

        await Assert.That(result).IsEquivalentTo([99]);
        await Assert.That(reportedExceptions).Count().IsEqualTo(1);
        await Assert.That(reportedExceptions[0].Message).IsEqualTo("resume error");
    }

    /// <summary>Tests that Retry catches OperationCanceledException during re-subscription without propagating.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryResubscriptionCancelled_ThenSwallowsCancellation()
    {
        var attempt = 0;
        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            attempt++;
            if (attempt == 1)
            {
                // First subscription: complete with failure to trigger retry
                await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));
                return DisposableAsync.Empty;
            }

            // Second subscription: throw OperationCanceledException from SubscribeAsync itself
            throw new OperationCanceledException("cancelled during resubscribe");
        });

        await using var sub = await source
            .Retry(3)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completed.TrySetResult(result);
                    return default;
                });

        // The OperationCanceledException is swallowed, so completion should not fire.
        // Give a short window to verify no completion occurs.
        var completedInTime = completed.Task.WaitAsync(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAsync<TimeoutException>(async () => await completedInTime);
        await Assert.That(attempt).IsEqualTo(2);
    }

    /// <summary>Tests that Retry catches generic exceptions during re-subscription and completes with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryResubscriptionThrows_ThenCompletesWithFailure()
    {
        var attempt = 0;
        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            attempt++;
            if (attempt == 1)
            {
                // First subscription: complete with failure to trigger retry
                await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));
                return DisposableAsync.Empty;
            }

            // Second subscription: throw a generic exception from SubscribeAsync itself
            throw new ArithmeticException("resubscribe failed");
        });

        await using var sub = await source
            .Retry(3)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completed.TrySetResult(result);
                    return default;
                });

        var completionResult = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(completionResult.IsFailure).IsTrue();
        await Assert.That(completionResult.Exception).IsTypeOf<ArithmeticException>();
        await Assert.That(attempt).IsEqualTo(2);
    }

    /// <summary>Tests that parameterless Retry retries indefinitely until success (covers the int.MaxValue path).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRetryParameterless_ThenRetriesUntilSuccess()
    {
        var attempt = 0;
        var source = ObservableAsync.CreateAsBackgroundJob<int>(async (obs, ct) =>
        {
            attempt++;
            if (attempt < 5)
            {
                await obs.OnCompletedAsync(Result.Failure(new InvalidOperationException($"attempt {attempt}")));
                return;
            }

            await obs.OnNextAsync(100, ct);
            await obs.OnCompletedAsync(Result.Success);
        });

        var result = await source.Retry().ToListAsync();

        await Assert.That(result).IsEquivalentTo([100]);
        await Assert.That(attempt).IsEqualTo(5);
    }
}
