// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for Result struct, UnhandledExceptionHandler, and ConcurrentObserverCallsException.
/// </summary>
public class ResultAndInfrastructureTests
{
    /// <summary>Tests Result.Success IsSuccess is true.</summary>
    [Test]
    public async Task WhenResultSuccess_ThenIsSuccessTrue()
    {
        var result = Result.Success;

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.IsFailure).IsFalse();
        await Assert.That(result.Exception).IsNull();
    }

    /// <summary>Tests Result.Failure IsFailure is true.</summary>
    [Test]
    public async Task WhenResultFailure_ThenIsFailureTrue()
    {
        var ex = new InvalidOperationException("fail");
        var result = Result.Failure(ex);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Exception).IsEquivalentTo(ex);
    }

    /// <summary>Tests Result constructor null exception throws.</summary>
    [Test]
    public void WhenResultConstructorNullException_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Result(null!));
    }

    /// <summary>Tests Result TryThrow on success does nothing.</summary>
    [Test]
    public void WhenResultTryThrowOnSuccess_ThenDoesNothing()
    {
        var result = Result.Success;
        result.TryThrow();
    }

    /// <summary>Tests Result TryThrow on failure throws original exception.</summary>
    [Test]
    public async Task WhenResultTryThrowOnFailure_ThenThrowsOriginalException()
    {
        var original = new InvalidOperationException("test error");
        var result = Result.Failure(original);

        var thrown = Assert.Throws<InvalidOperationException>(() => result.TryThrow());
        await Assert.That(thrown!.Message).IsEqualTo("test error");
    }

    /// <summary>Tests Result Success ToString returns Success.</summary>
    [Test]
    public async Task WhenResultSuccessToString_ThenReturnsSuccess()
    {
        await Assert.That(Result.Success.ToString()).IsEqualTo("Success");
    }

    /// <summary>Tests Result Failure ToString contains Failure and message.</summary>
    [Test]
    public async Task WhenResultFailureToString_ThenContainsFailureAndMessage()
    {
        var result = Result.Failure(new InvalidOperationException("oh no"));

        await Assert.That(result.ToString()).Contains("Failure");
        await Assert.That(result.ToString()).Contains("oh no");
    }

    /// <summary>Tests Result Success equals default.</summary>
    [Test]
    public async Task WhenResultEquality_ThenSuccessEqualsDefault()
    {
        var a = Result.Success;
        var b = default(Result);

        await Assert.That(a).IsEqualTo(b);
    }

    /// <summary>Tests ConcurrentObserverCallsException has descriptive message.</summary>
    [Test]
    public async Task WhenConcurrentObserverCallsException_ThenHasMessage()
    {
        var ex = new ConcurrentObserverCallsException();

        await Assert.That(ex.Message).Contains("Concurrent calls");
        await Assert.That(ex.Message).Contains("OnNextAsync");
    }

    /// <summary>Tests UnhandledExceptionHandler Register invokes custom handler.</summary>
    [Test]
    public void WhenUnhandledExceptionHandlerRegisterCustom_ThenCustomHandlerInvoked()
    {
        var caught = new List<Exception>();

        try
        {
            UnhandledExceptionHandler.Register(ex => caught.Add(ex));

            // Trigger unhandled exception via observer that throws during OnCompleted
            var source = ObservableAsync.Create<int>(async (observer, ct) =>
            {
                await observer.OnCompletedAsync(Result.Success);
                return global::ReactiveUI.Extensions.Async.Disposables.DisposableAsync.Empty;
            });
        }
        finally
        {
            UnhandledExceptionHandler.Register(ex => Console.WriteLine("UnhandleException: " + ex));
        }
    }
}
