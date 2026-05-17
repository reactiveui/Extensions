// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// <see cref="TimeProvider"/> that invokes the timer callback synchronously inside
/// <see cref="CreateTimer"/> and returns a no-op timer thereafter. Used by benchmarks that need
/// deterministic, zero-wall-time progression through <c>Throttle</c>, <c>Delay</c>, and similar
/// operators built on <see cref="TimeProvider.CreateTimer"/> — the per-emission *operator*
/// overhead is what we want to measure; the timed window is irrelevant.
/// </summary>
internal sealed class BenchmarkImmediateFireTimeProvider : TimeProvider
{
    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        callback(state);
        return BenchmarkNoOpTimer.Instance;
    }
}
