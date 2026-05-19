// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Tests.Internal;

/// <summary>Tests for <see cref="FirstAsTaskHelper"/> covering the error and empty-completion
/// paths that <c>ToHotTask</c> does not otherwise exercise.</summary>
public class FirstAsTaskHelperTests
{
    /// <summary>Value used by the latch-on-first-emission test.</summary>
    private const int FirstValue = 7;

    /// <summary>Value used by the latch-on-first-emission test to verify subsequent values are ignored.</summary>
    private const int SecondValue = 11;

    /// <summary>Verifies the helper faults the task when the source emits an error before any value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSourceErrors_ThenTaskFaults()
    {
        var expected = new InvalidOperationException("boom");
        var task = FirstAsTaskHelper.FirstAsTask(Observable.Throw<int>(expected));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
        await Assert.That(ex).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies the helper faults the task when the source completes empty.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSourceCompletesEmpty_ThenTaskFaultsWithInvalidOperation()
    {
        var task = FirstAsTaskHelper.FirstAsTask(Observable.Empty<int>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
    }

    /// <summary>Verifies the task latches on the first emission and ignores subsequent values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSourceEmitsMultiple_ThenTaskCompletesWithFirst()
    {
        var subject = new Subject<int>();
        var task = FirstAsTaskHelper.FirstAsTask(subject);

        subject.OnNext(FirstValue);
        subject.OnNext(SecondValue);
        subject.OnCompleted();

        await Assert.That(await task).IsEqualTo(FirstValue);
    }

    /// <summary>Verifies the helper throws when the source argument is null.</summary>
    [Test]
    public void WhenSourceNull_ThenThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(static () => FirstAsTaskHelper.FirstAsTask<int>(null!));

    /// <summary>Verifies emissions arriving after the task has already settled are silently ignored.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubjectErrorsThenLaterEvents_ThenLaterEventsIgnored()
    {
        var subject = new Subject<int>();
        var task = FirstAsTaskHelper.FirstAsTask(subject);
        var expected = new InvalidOperationException("first");

        subject.OnError(expected);
        subject.OnCompleted();
        subject.OnNext(FirstValue);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
        await Assert.That(ex).IsSameReferenceAs(expected);
    }
}
