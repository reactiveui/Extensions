// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>Coverage for <see cref="AsyncGate"/> — uncontended fast path, same-thread reentry,
/// contended slow path, double-dispose idempotency.</summary>
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "TUnit requires instance methods")]
public class AsyncGateTests
{
    /// <summary>Wait delay in milliseconds used to confirm a contended waiter has not resumed.</summary>
    private const int ContentionConfirmDelayMilliseconds = 20;

    /// <summary>Verifies that the uncontended fast path acquires the gate via pure CAS.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUncontendedLock_ThenAcquiresAndReleases()
    {
        using var gate = new AsyncGate();

        using (await gate.LockAsync())
        {
            await Assert.That(gate).IsNotNull();
        }

        // After release the gate must be re-acquirable.
        using (await gate.LockAsync())
        {
            await Assert.That(gate).IsNotNull();
        }
    }

    /// <summary>Verifies that same-thread reentry bumps the recursion depth and does not block.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSameThreadReentry_ThenAllowedWithoutBlocking()
    {
        using var gate = new AsyncGate();

        using (await gate.LockAsync())
        using (await gate.LockAsync())
        using (await gate.LockAsync())
        {
            await Assert.That(gate).IsNotNull();
        }

        // Gate must release cleanly after nested acquisitions.
        using (await gate.LockAsync())
        {
            await Assert.That(gate).IsNotNull();
        }
    }

    /// <summary>Verifies that a contended waiter resumes via the semaphore-signal slow path.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenContendedWaiter_ThenResumesAfterRelease()
    {
        using var gate = new AsyncGate();
        var owner = await gate.LockAsync();
        var contendedAcquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var contender = Task.Run(async () =>
        {
            using var releaser = await gate.LockAsync().ConfigureAwait(false);
            contendedAcquired.TrySetResult(true);
        });

        await Task.Delay(ContentionConfirmDelayMilliseconds).ConfigureAwait(false);
        await Assert.That(contendedAcquired.Task.IsCompleted).IsFalse();

        owner.Dispose();

        var acquired = await contendedAcquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(acquired).IsTrue();
        await contender;
    }

    /// <summary>Verifies that double-dispose is idempotent.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDisposeCalledTwice_ThenIdempotent()
    {
        var gate = new AsyncGate();

        gate.Dispose();
        gate.Dispose();

        await Assert.That(gate).IsNotNull();
    }
}
