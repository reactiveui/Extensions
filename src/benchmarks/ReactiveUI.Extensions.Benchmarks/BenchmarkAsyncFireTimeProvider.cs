// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// <see cref="TimeProvider"/> whose <see cref="CreateTimer"/> queues the timer callback to the
/// threadpool rather than firing it inline. Used by benchmarks of recurring time-driven pumps
/// (<c>Interval</c>, periodic <c>Timer</c>) where an immediate-fire provider would recurse
/// without yielding — each tick's rearm would synchronously fire the next tick on the same
/// stack, exhausting it before the subscriber could observe a value and unwind.
/// </summary>
/// <remarks>
/// Queueing the callback matches the pattern <see cref="TimeProvider.System"/> uses via
/// <c>Task.Delay</c>: the pump observes its tick from a thread other than the one that called
/// <c>Change</c>, so the surrounding await reaches a true suspension point. The result is a
/// benchmark that measures the real subscribe / first-tick / dispose cost without scheduler
/// wall-time variance, while keeping the pump's natural async shape.
/// </remarks>
internal sealed class BenchmarkAsyncFireTimeProvider : TimeProvider
{
    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        _ = dueTime;
        _ = period;
        ThreadPool.UnsafeQueueUserWorkItem(new QueuedCallback(callback, state), preferLocal: false);
        return BenchmarkNoOpTimer.Instance;
    }

    /// <summary>Inline work item that invokes the captured timer callback when the threadpool schedules it.</summary>
    /// <param name="callback">The user-supplied timer callback.</param>
    /// <param name="state">Opaque state forwarded to the callback.</param>
    private sealed class QueuedCallback(TimerCallback callback, object? state) : IThreadPoolWorkItem
    {
        /// <inheritdoc/>
        public void Execute() => callback(state);
    }
}
