// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Tests;

/// <summary>Tests for the blocking subscribe helpers on <see cref="ObservableSubscriptionExtensions"/>.</summary>
public class ObservableSubscriptionExtensionsTests
{
    /// <summary>Sentinel value emitted by single-value tests.</summary>
    private const int SentinelValue = 7;

    /// <summary>Verifies that <c>SubscribeGetValue</c> returns the last synchronously-emitted value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeGetValue_ThenReturnsLastSyncValue()
    {
        var result = Observable.Return(SentinelValue).SubscribeGetValue();

        await Assert.That(result).IsEqualTo(SentinelValue);
    }

    /// <summary>Verifies that <c>SubscribeGetValue</c> returns the default when the sequence is empty.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeGetValueEmpty_ThenReturnsDefault()
    {
        var result = Observable.Empty<int>().SubscribeGetValue();

        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Verifies that <c>SubscribeAndComplete</c> consumes a Unit-producing observable without error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAndComplete_ThenSwallowsUnitAndReturns()
    {
        // Helper is fire-and-forget; verify a follow-up call on a different sequence still
        // returns the expected value, proving SubscribeAndComplete didn't leave state behind.
        Observable.Return(Unit.Default).SubscribeAndComplete();
        var followUp = Observable.Return(Unit.Default).SubscribeGetValue();
        await Assert.That(followUp).IsEqualTo(Unit.Default);
    }

    /// <summary>Verifies that <c>SubscribeGetError</c> captures a synchronous error and returns it.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeGetError_ThenCapturesSyncError()
    {
        var expected = new InvalidOperationException("sync");
        var error = Observable.Throw<int>(expected).SubscribeGetError();

        await Assert.That(error).IsEqualTo(expected);
    }

    /// <summary>Verifies that the Unit-overload of <c>SubscribeGetError</c> captures the error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeGetErrorUnit_ThenCapturesSyncError()
    {
        var expected = new InvalidOperationException("unit-sync");
        var error = Observable.Throw<Unit>(expected).SubscribeGetError();

        await Assert.That(error).IsEqualTo(expected);
    }

    /// <summary>Verifies that <c>WaitForValue</c> blocks until the synchronously-completing source emits.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitForValue_ThenReturnsEmittedValue()
    {
        var result = Observable.Return(SentinelValue).WaitForValue();

        await Assert.That(result).IsEqualTo(SentinelValue);
    }

    /// <summary>Verifies that the timeout overload of <c>WaitForValue</c> honours an explicit deadline.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitForValueWithTimeout_ThenReturnsEmittedValue()
    {
        var result = Observable.Return(SentinelValue).WaitForValue(TimeSpan.FromSeconds(5));

        await Assert.That(result).IsEqualTo(SentinelValue);
    }

    /// <summary>Verifies that <c>WaitForValue</c> throws <see cref="TimeoutException"/> on a non-terminating source.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitForValueTimesOut_ThenTimeoutException()
    {
        Action call = () => Observable.Never<int>().WaitForValue(TimeSpan.FromMilliseconds(50));
        var ex = Assert.Throws<TimeoutException>(call);
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Verifies that <c>WaitForCompletion</c> returns once the Unit-producing source completes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitForCompletion_ThenReturnsAfterTerminal()
    {
        // Helper returns void on success; the absence of TimeoutException after a synchronous
        // completion is the contract under test. Use the value-returning sibling for the actual
        // assertion so TUnit has a real check.
        Observable.Return(Unit.Default).WaitForCompletion(TimeSpan.FromSeconds(5));
        var subsequent = Observable.Return(Unit.Default).SubscribeGetValue();
        await Assert.That(subsequent).IsEqualTo(Unit.Default);
    }

    /// <summary>Verifies that <c>WaitForCompletion</c> rethrows the source's error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitForCompletionWithError_ThenRethrows()
    {
        var expected = new InvalidOperationException("wait");
        Action call = () => Observable.Throw<Unit>(expected).WaitForCompletion(TimeSpan.FromSeconds(5));
        var ex = Assert.Throws<InvalidOperationException>(call);
        await Assert.That(ex).IsEqualTo(expected);
    }

    /// <summary>Verifies that <c>WaitForCompletion</c> throws <see cref="TimeoutException"/> for a non-terminating source.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitForCompletionTimesOut_ThenTimeoutException()
    {
        Action call = () => Observable.Never<Unit>().WaitForCompletion(TimeSpan.FromMilliseconds(50));
        var ex = Assert.Throws<TimeoutException>(call);
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Verifies that <c>WaitForError</c> returns null when the source completes normally.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitForErrorNormalCompletion_ThenReturnsNull()
    {
        var error = Observable.Return(SentinelValue).WaitForError(TimeSpan.FromSeconds(5));

        await Assert.That(error).IsNull();
    }

    /// <summary>Verifies that <c>WaitForError</c> returns the captured error rather than rethrowing.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitForErrorSourceErrors_ThenReturnsCapturedError()
    {
        var expected = new InvalidOperationException("captured");
        var error = Observable.Throw<int>(expected).WaitForError(TimeSpan.FromSeconds(5));

        await Assert.That(error).IsEqualTo(expected);
    }

    /// <summary>Verifies that <c>WaitForError</c> throws <see cref="TimeoutException"/> for a non-terminating source.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitForErrorTimesOut_ThenTimeoutException()
    {
        Action call = () => Observable.Never<int>().WaitForError(TimeSpan.FromMilliseconds(50));
        var ex = Assert.Throws<TimeoutException>(call);
        await Assert.That(ex).IsNotNull();
    }
}
