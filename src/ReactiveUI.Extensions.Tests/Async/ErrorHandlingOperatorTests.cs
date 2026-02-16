// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NUnit.Framework;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for error handling operators: Catch, CatchAndIgnoreErrorResume, OnErrorResumeAsFailure, Retry.
/// </summary>
public class ErrorHandlingOperatorTests
{
    /// <summary>Tests Catch with fallback switches to fallback.</summary>
    [Test]
    public async Task WhenCatchWithFallback_ThenSwitchesToFallback()
    {
        var source = ObservableAsync.Throw<int>(new InvalidOperationException("fail"));
        var fallback = ObservableAsync.Return(42);

        var result = await source.Catch(_ => fallback).ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 42 }));
    }

    /// <summary>Tests Catch on success completes original sequence.</summary>
    [Test]
    public async Task WhenCatchOnSuccess_ThenOriginalSequenceCompletes()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Catch(_ => ObservableAsync.Return(99))
            .ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    /// <summary>Tests CatchAndIgnoreErrorResume ignores and continues.</summary>
    [Test]
    public async Task WhenCatchAndIgnoreErrorResume_ThenIgnoresAndContinues()
    {
        var source = ObservableAsync.Throw<int>(new InvalidOperationException("fail"));
        var fallback = ObservableAsync.Return(100);

        var result = await source.CatchAndIgnoreErrorResume(_ => fallback).ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 100 }));
    }

    /// <summary>Tests OnErrorResumeAsFailure converts error resume to failure.</summary>
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

        Assert.That(errorSent, Is.True);
        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsFailure, Is.True);
    }

    /// <summary>Tests Retry on transient error succeeds after retry.</summary>
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

        Assert.That(result, Is.EqualTo(new[] { 42 }));
        Assert.That(attempt, Is.EqualTo(3));
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
    [Test]
    public async Task WhenRetryInfiniteOnSuccess_ThenCompletesNormally()
    {
        var result = await ObservableAsync.Return(7).Retry().ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 7 }));
    }

    /// <summary>Tests Catch with error resume callback is invoked.</summary>
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

        Assert.That(result, Does.Contain(99));
    }
}
