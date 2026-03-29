// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Shared test helpers for async observable tests.
/// </summary>
internal static class AsyncTestHelpers
{
    /// <summary>
    /// Collects all items and the completion result from an async observable.
    /// </summary>
    /// <typeparam name="T">The type of elements in the observable sequence.</typeparam>
    /// <param name="source">The async observable to collect from.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A tuple containing the collected items and the completion result.</returns>
    internal static async Task<(List<T> Items, Result? Completion)> CollectAsync<T>(
        ObservableAsync<T> source,
        CancellationToken cancellationToken = default)
    {
        var items = new List<T>();
        Result? completion = null;

        await using var subscription = await source.SubscribeAsync(
            (x, ct) =>
            {
                items.Add(x);
                return default;
            },
            null,
            result =>
            {
                completion = result;
                return default;
            },
            cancellationToken);

        // Wait briefly for completion to propagate
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (completion is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, CancellationToken.None);
        }

        return (items, completion);
    }

    /// <summary>
    /// Collects all items from an async observable using ToListAsync.
    /// </summary>
    /// <typeparam name="T">The type of elements in the observable sequence.</typeparam>
    /// <param name="source">The async observable to collect from.</param>
    /// <param name="timeoutMs">The timeout in milliseconds before the operation is cancelled.</param>
    /// <returns>A list of all collected items.</returns>
    internal static async Task<List<T>> ToListWithTimeoutAsync<T>(
        ObservableAsync<T> source,
        int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        return await source.ToListAsync(cts.Token);
    }

    /// <summary>
    /// Creates a <see cref="DirectSource{T}"/> for test scenarios that need to call observer
    /// methods directly, bypassing subject teardown on disposal.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>A new <see cref="DirectSource{T}"/> instance.</returns>
    internal static DirectSource<T> CreateDirectSource<T>() => new();

    /// <summary>
    /// Waits until the provided condition is met or the timeout expires.
    /// </summary>
    /// <param name="condition">Condition to evaluate.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="pollInterval">Optional polling interval.</param>
    /// <returns>True if the condition was met before timing out.</returns>
    internal static async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);

        var interval = pollInterval ?? TimeSpan.FromMilliseconds(10);
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(interval, CancellationToken.None);
        }

        return condition();
    }
}
