// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>Tests for <see cref="ObserveOnAsyncObservable{T}"/> — exercises the
/// <c>forceYielding: true</c> slow-path branches that switch context on every
/// <c>OnNext</c> / <c>OnErrorResume</c> / <c>OnCompleted</c> regardless of whether
/// the call site is already on the target context.</summary>
public class ObserveOnAsyncObservableTests
{
    /// <summary>Single sentinel emitted by the happy-path tests.</summary>
    private const int Sentinel = 7;

    /// <summary>Verifies the <c>forceYielding: true</c> overload forwards values via the
    /// context-switching slow path.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForceYielding_ThenValueForwarded()
    {
        var result = await ObservableAsync.Return(Sentinel)
            .ObserveOn(AsyncContext.Default, forceYielding: true)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(Sentinel);
    }

    /// <summary>Verifies the <c>forceYielding: true</c> overload routes <c>OnErrorResume</c>
    /// through the context-switching slow path.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForceYieldingSourceErrors_ThenErrorForwarded()
    {
        var expected = new InvalidOperationException("forced");
        InvalidOperationException? caught = null;

        try
        {
            await ObservableAsync.Throw<int>(expected)
                .ObserveOn(AsyncContext.Default, forceYielding: true)
                .ToListAsync();
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies the <c>forceYielding: true</c> overload routes the completion
    /// notification through the context-switching slow path.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForceYieldingSourceEmpty_ThenCompletesSuccessfully()
    {
        var result = await ObservableAsync.Empty<int>()
            .ObserveOn(AsyncContext.Default, forceYielding: true)
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Verifies the <c>SynchronizationContext</c> + <c>forceYielding: true</c> overload
    /// also forwards values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSyncContextForceYielding_ThenEmits()
    {
        var ctx = SynchronizationContext.Current ?? new SynchronizationContext();

        var result = await ObservableAsync.Return(Sentinel)
            .ObserveOn(ctx, forceYielding: true)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(Sentinel);
    }
}
