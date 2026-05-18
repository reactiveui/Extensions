// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Tests.Operators;

/// <summary>Edge-case coverage for <c>DropIfBusy</c> backed by
/// <c>DropIfBusyObservable&lt;T&gt;</c> — error forwarding from the source and
/// from the async action, paths the happy-path drop test does not exercise.</summary>
public class DropIfBusyObservableTests
{
    /// <summary>Message attached to synthetic source errors.</summary>
    private const string SourceErrorMessage = "source error";

    /// <summary>Message attached to a thrown async action.</summary>
    private const string ActionFailedMessage = "action failed";

    /// <summary>Verifies that <c>DropIfBusy</c> forwards an exception thrown by the async action.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDropIfBusyAsyncActionThrows_ThenForwardsError()
    {
        const int TriggerValue = 1;
        var subject = new Subject<int>();
        var faulted = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException(ActionFailedMessage);

        using var sub = subject.DropIfBusy(_ => Task.FromException(expected))
            .Subscribe(static _ => { }, ex => faulted.TrySetResult(ex));

        subject.OnNext(TriggerValue);

        var caught = await faulted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(caught).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies that <c>DropIfBusy</c> forwards source errors immediately.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDropIfBusySourceErrors_ThenForwardsError()
    {
        var subject = new Subject<int>();
        Exception? caught = null;
        var expected = new InvalidOperationException(SourceErrorMessage);

        using var sub = subject.DropIfBusy(static _ => Task.CompletedTask)
            .Subscribe(static _ => { }, ex => caught = ex);

        subject.OnError(expected);

        await Assert.That(caught).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies that <c>DropIfBusy</c> forwards source completion.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDropIfBusySourceCompletes_ThenForwardsCompletion()
    {
        var subject = new Subject<int>();
        var completed = false;

        using var sub = subject.DropIfBusy(static _ => Task.CompletedTask)
            .Subscribe(static _ => { }, () => completed = true);

        subject.OnCompleted();

        await Assert.That(completed).IsTrue();
    }
}
