// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for Result struct, UnhandledExceptionHandler, and ConcurrentObserverCallsException.
/// </summary>
public partial class ResultAndInfrastructureTests
{
    /// <summary>String literal "custom message" used by multiple tests.</summary>
    private const string CustomMessage = "custom message";

    /// <summary>Sample value (42) used by tests.</summary>
    private const int SampleValue = 42;

    /// <summary>Tests Result.Success IsSuccess is true.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultSuccess_ThenIsSuccessTrue()
    {
        var result = Result.Success;

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.IsFailure).IsFalse();
        await Assert.That(result.Exception).IsNull();
    }

    /// <summary>Tests Result.Failure IsFailure is true.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultFailure_ThenIsFailureTrue()
    {
        var ex = new InvalidOperationException("fail");
        var result = Result.Failure(ex);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Exception).IsSameReferenceAs(ex);
    }

    /// <summary>Tests Result constructor null exception throws.</summary>
    [Test]
    public void WhenResultConstructorNullException_ThenThrowsArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() => _ = new Result(null!));

    /// <summary>Tests Result TryThrow on success does nothing.</summary>
    [Test]
    public void WhenResultTryThrowOnSuccess_ThenDoesNothing()
    {
        var result = Result.Success;
        result.TryThrow();
    }

    /// <summary>Tests Result TryThrow on failure throws original exception.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultTryThrowOnFailure_ThenThrowsOriginalException()
    {
        var original = new InvalidOperationException("test error");
        var result = Result.Failure(original);

        var thrown = Assert.Throws<InvalidOperationException>(result.TryThrow);
        await Assert.That(thrown!.Message).IsEqualTo("test error");
    }

    /// <summary>Tests Result Success ToString returns Success.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultSuccessToString_ThenReturnsSuccess() =>
        await Assert.That(Result.Success.ToString()).IsEqualTo("Success");

    /// <summary>Tests Result Failure ToString contains Failure and message.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultFailureToString_ThenContainsFailureAndMessage()
    {
        var result = Result.Failure(new InvalidOperationException("oh no"));

        await Assert.That(result.ToString()).Contains("Failure");
        await Assert.That(result.ToString()).Contains("oh no");
    }

    /// <summary>Tests Result Success equals default.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenResultEquality_ThenSuccessEqualsDefault()
    {
        var a = Result.Success;
        var b = default(Result);

        await Assert.That(a).IsEqualTo(b);
    }

    /// <summary>Tests ConcurrentObserverCallsException has descriptive message.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentObserverCallsException_ThenHasMessage()
    {
        var ex = new ConcurrentObserverCallsException();

        await Assert.That(ex.Message).Contains("Concurrent calls");
        await Assert.That(ex.Message).Contains("OnNextAsync");
    }

    /// <summary>
    /// Verifies that ConcurrentObserverCallsException with custom message stores the message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentObserverCallsExceptionWithMessage_ThenStoresMessage()
    {
        var ex = new ConcurrentObserverCallsException(CustomMessage);

        await Assert.That(ex.Message).IsEqualTo(CustomMessage);
    }

    /// <summary>
    /// Verifies that ConcurrentObserverCallsException with message and inner exception stores both.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentObserverCallsExceptionWithMessageAndInner_ThenStoresBoth()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ConcurrentObserverCallsException(CustomMessage, inner);

        await Assert.That(ex.Message).IsEqualTo(CustomMessage);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    /// <summary>
    /// Tests that Optional.Empty has no value and throws when accessed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOptionalIsEmpty_ThenHasNoValueAndThrowsOnAccess()
    {
        var optional = Optional<int>.Empty;

        await Assert.That(optional.HasValue).IsFalse();
        Assert.Throws<InvalidOperationException>(() => _ = optional.Value);
    }

    /// <summary>
    /// Tests that Optional with a value exposes that value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOptionalHasValue_ThenReturnsContainedValue()
    {
        var optional = new Optional<int>(SampleValue);

        await Assert.That(optional.HasValue).IsTrue();
        await Assert.That(optional.Value).IsEqualTo(SampleValue);
    }

    /// <summary>
    /// Tests that AsyncGate can be acquired, released, and acquired again.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncGateIsReleased_ThenCanBeAcquiredAgain()
    {
        using var gate = new AsyncGate();

        var firstAcquired = false;
        using (await gate.LockAsync())
        {
            firstAcquired = true;
        }

        var reacquired = false;
        using (await gate.LockAsync())
        {
            reacquired = true;
        }

        await Assert.That(firstAcquired).IsTrue();
        await Assert.That(reacquired).IsTrue();
    }

    /// <summary>
    /// Tests that Heartbeat distinguishes heartbeat instances from update instances.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenHeartbeatIsConstructed_ThenPropertiesReflectTheMode()
    {
        const int UpdateValue = 7;

        var heartbeat = new Heartbeat<int>();
        var update = new Heartbeat<int>(UpdateValue);

        await Assert.That(heartbeat.IsHeartbeat).IsTrue();
        await Assert.That(update.IsHeartbeat).IsFalse();
        await Assert.That(update.Update).IsEqualTo(UpdateValue);
    }

    /// <summary>
    /// Tests that Stale distinguishes stale instances from update instances.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStaleIsConstructed_ThenPropertiesReflectTheMode()
    {
        const int UpdateValue = 9;

        var stale = new Stale<int>();
        var update = new Stale<int>(UpdateValue);

        await Assert.That(stale.IsStale).IsTrue();
        Assert.Throws<InvalidOperationException>(() => _ = stale.Update);
        await Assert.That(update.IsStale).IsFalse();
        await Assert.That(update.Update).IsEqualTo(UpdateValue);
    }

    /// <summary>Tests SubjectAsync.Create with invalid PublishingOption throws.</summary>
    [Test]
    public void WhenSubjectAsyncCreateWithInvalidOptions_ThenThrows()
    {
        var options = new SubjectCreationOptions { PublishingOption = (PublishingOption)99, IsStateless = false };

        Assert.Throws<ArgumentOutOfRangeException>(() => SubjectAsync.Create<int>(options));
    }

    /// <summary>Tests SubjectAsync.CreateBehavior with invalid PublishingOption throws.</summary>
    [Test]
    public void WhenBehaviorSubjectCreateWithInvalidOptions_ThenThrows()
    {
        var options = new BehaviorSubjectCreationOptions
        {
            PublishingOption = (PublishingOption)99, IsStateless = false
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => SubjectAsync.CreateBehavior<int>(0, options));
    }

    /// <summary>Tests SubjectAsync.CreateReplayLatest with invalid PublishingOption throws.</summary>
    [Test]
    public void WhenReplayLatestCreateWithInvalidOptions_ThenThrows()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = (PublishingOption)99, IsStateless = false
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => SubjectAsync.CreateReplayLatest<int>(options));
    }

    /// <summary>Tests SubscriptionHelper returns subscription on success.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscriptionHelperSucceeds_ThenReturnsSubscription()
    {
        var disposable = DisposableAsync.Empty;
        var result = await SubscriptionHelper.SubscribeAndDisposeOnFailureAsync(
            disposable,
            () => default);

        await Assert.That(result).IsEqualTo(disposable);
    }

    /// <summary>Tests SubscriptionHelper disposes subscription and rethrows on failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscriptionHelperFails_ThenDisposesAndRethrows()
    {
        var disposed = false;
        var disposable = DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await SubscriptionHelper.SubscribeAndDisposeOnFailureAsync(
                disposable,
                () => throw new InvalidOperationException("subscribe-fail")));

        await Assert.That(disposed).IsTrue();
    }

    /// <summary>
    /// A concrete <see cref="ObserverAsync{T}"/> implementation for testing, with
    /// configurable behavior for each virtual method.
    /// </summary>
    /// <param name="onNextAsyncCore">Optional delegate for <see cref="ObserverAsync{T}.OnNextAsyncCore"/>.</param>
    /// <param name="onErrorResumeAsyncCore">Optional delegate for <see cref="ObserverAsync{T}.OnErrorResumeAsyncCore"/>.</param>
    /// <param name="onCompletedAsyncCore">Optional delegate for <see cref="ObserverAsync{T}.OnCompletedAsyncCore"/>.</param>
    internal sealed class TestableObserverAsync(
        Func<int, CancellationToken, ValueTask>? onNextAsyncCore = null,
        Func<Exception, CancellationToken, ValueTask>? onErrorResumeAsyncCore = null,
        Func<Result, ValueTask>? onCompletedAsyncCore = null) : ObserverAsync<int>
    {
        /// <inheritdoc/>
        protected override ValueTask OnNextAsyncCore(int value, CancellationToken cancellationToken) =>
            onNextAsyncCore is not null ? onNextAsyncCore(value, cancellationToken) : default;

        /// <inheritdoc/>
        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) =>
            onErrorResumeAsyncCore is not null ? onErrorResumeAsyncCore(error, cancellationToken) : default;

        /// <inheritdoc/>
        protected override ValueTask OnCompletedAsyncCore(Result result) =>
            onCompletedAsyncCore is not null ? onCompletedAsyncCore(result) : default;
    }

    /// <summary>
    /// An IScheduler that also extends SynchronizationContext, used to test the
    /// AsyncContext.From(IScheduler) path that checks for SynchronizationContext.
    /// </summary>
    private sealed class SyncContextScheduler : SynchronizationContext, IScheduler
    {
        /// <inheritdoc/>
        public DateTimeOffset Now => TimeProvider.System.GetUtcNow();

        /// <inheritdoc/>
        public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action) =>
            action(this, state);

        /// <inheritdoc/>
        public IDisposable Schedule<TState>(
            TState state,
            TimeSpan dueTime,
            Func<IScheduler, TState, IDisposable> action) =>
            action(this, state);

        /// <inheritdoc/>
        public IDisposable Schedule<TState>(
            TState state,
            DateTimeOffset dueTime,
            Func<IScheduler, TState, IDisposable> action) =>
            action(this, state);
    }

    /// <summary>
    /// A raw <see cref="IObserverAsync{T}"/> that throws from <see cref="OnCompletedAsync"/>,
    /// bypassing <see cref="ObserverAsync{T}"/> base class exception handling.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class RawThrowingOnCompletedObserver<T>(Exception completionException) : IObserverAsync<T>
    {
        /// <inheritdoc/>
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken) => default;

        /// <inheritdoc/>
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken) => default;

        /// <inheritdoc/>
        public ValueTask OnCompletedAsync(Result result) => throw completionException;

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => default;
    }
}
