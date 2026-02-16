// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides the Timer factory method for creating asynchronous observable sequences that produce
/// a single value after a specified delay.
/// </summary>
/// <remarks>Timer is useful for triggering one-shot deferred actions in observable pipelines.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Creates an observable sequence that produces a single value (0) after the specified delay,
    /// then completes.
    /// </summary>
    /// <param name="dueTime">The time span after which to produce the value. Must be non-negative.</param>
    /// <param name="timeProvider">An optional time provider for controlling timing. If null, <see cref="TimeProvider.System"/>
    /// is used.</param>
    /// <returns>An observable sequence that produces a single value after the specified delay and then completes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="dueTime"/> is negative.</exception>
    public static IObservableAsync<long> Timer(TimeSpan dueTime, TimeProvider? timeProvider = null)
    {
        if (dueTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(dueTime));
        }

        var tp = timeProvider ?? TimeProvider.System;

        return CreateAsBackgroundJob<long>(
            async (observer, cancellationToken) =>
            {
                await DelayAsync(dueTime, tp, cancellationToken);
                await observer.OnNextAsync(0L, cancellationToken);
                await observer.OnCompletedAsync(Result.Success);
            },
            true);
    }

    /// <summary>
    /// Creates an observable sequence that produces a single value (0) after the specified delay,
    /// then continues to produce sequential values at each specified period.
    /// </summary>
    /// <param name="dueTime">The initial delay before the first value is produced. Must be non-negative.</param>
    /// <param name="period">The interval between subsequent values after the initial delay. Must be positive.</param>
    /// <param name="timeProvider">An optional time provider for controlling timing. If null, <see cref="TimeProvider.System"/>
    /// is used.</param>
    /// <returns>An observable sequence that produces values starting after the initial delay and continuing
    /// at the specified period.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="dueTime"/> is negative
    /// or <paramref name="period"/> is non-positive.</exception>
    public static IObservableAsync<long> Timer(TimeSpan dueTime, TimeSpan period, TimeProvider? timeProvider = null)
    {
        if (dueTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(dueTime));
        }

        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var tp = timeProvider ?? TimeProvider.System;

        return CreateAsBackgroundJob<long>(
            async (observer, cancellationToken) =>
            {
                await DelayAsync(dueTime, tp, cancellationToken);

                long tick = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    await observer.OnNextAsync(tick++, cancellationToken);
                    await DelayAsync(period, tp, cancellationToken);
                }
            },
            true);
    }
}
