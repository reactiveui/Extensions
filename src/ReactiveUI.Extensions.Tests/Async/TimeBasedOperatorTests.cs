// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NUnit.Framework;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for time-based operators: Throttle, Delay, Timeout.
/// </summary>
public class TimeBasedOperatorTests
{
    /// <summary>Tests Throttle only last in burst is emitted.</summary>
    [Test]
    public async Task WhenThrottle_ThenOnlyLastInBurstIsEmitted()
    {
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(100))
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(2, CancellationToken.None);
        await subject.OnNextAsync(3, CancellationToken.None);

        await Task.Delay(250);

        await subject.OnCompletedAsync(Result.Success);
        await Task.Delay(100);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0], Is.EqualTo(3));
    }

    /// <summary>Tests Throttle with spaced items all are emitted.</summary>
    [Test]
    public async Task WhenThrottleWithSpacedItems_ThenAllAreEmitted()
    {
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(50))
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);
        await Task.Delay(150);
        await subject.OnNextAsync(2, CancellationToken.None);
        await Task.Delay(150);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results, Is.EqualTo(new[] { 1, 2 }));
    }

    /// <summary>Tests Throttle negative due time throws.</summary>
    [Test]
    public void WhenThrottleNegativeDueTime_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Throttle(TimeSpan.FromMilliseconds(-1)));
    }

    /// <summary>Tests Delay elements are time shifted.</summary>
    [Test]
    public async Task WhenDelay_ThenElementsAreTimeShifted()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await ObservableAsync.Return(42)
            .Delay(TimeSpan.FromMilliseconds(100))
            .FirstAsync();
        stopwatch.Stop();

        Assert.That(result, Is.EqualTo(42));
        Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(80));
    }

    /// <summary>Tests Delay zero causes no delay.</summary>
    [Test]
    public async Task WhenDelayZero_ThenNoDelay()
    {
        var result = await ObservableAsync.Return(42)
            .Delay(TimeSpan.Zero)
            .FirstAsync();

        Assert.That(result, Is.EqualTo(42));
    }

    /// <summary>Tests Delay negative throws.</summary>
    [Test]
    public void WhenDelayNegative_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Delay(TimeSpan.FromMilliseconds(-1)));
    }

    /// <summary>Tests Delay sequence delays all elements.</summary>
    [Test]
    public async Task WhenDelaySequence_ThenAllElementsDelayed()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Delay(TimeSpan.FromMilliseconds(30))
            .ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    /// <summary>Tests Timeout not exceeded completes normally.</summary>
    [Test]
    public async Task WhenTimeoutNotExceeded_ThenCompletesNormally()
    {
        var result = await ObservableAsync.Return(42)
            .Timeout(TimeSpan.FromSeconds(5))
            .FirstAsync();

        Assert.That(result, Is.EqualTo(42));
    }

    /// <summary>Tests Timeout exceeded throws TimeoutException.</summary>
    [Test]
    public void WhenTimeoutExceeded_ThenThrowsTimeoutException()
    {
        var source = ObservableAsync.Never<int>()
            .Timeout(TimeSpan.FromMilliseconds(100));

        Assert.ThrowsAsync<TimeoutException>(
            async () => await source.FirstAsync());
    }

    /// <summary>Tests Timeout with fallback switches to fallback.</summary>
    [Test]
    public async Task WhenTimeoutWithFallback_ThenSwitchesToFallback()
    {
        var source = ObservableAsync.Never<int>()
            .Timeout(TimeSpan.FromMilliseconds(100), ObservableAsync.Return(99));

        var result = await source.FirstAsync();

        Assert.That(result, Is.EqualTo(99));
    }

    /// <summary>Tests Timeout zero duration throws.</summary>
    [Test]
    public void WhenTimeoutZeroDuration_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Timeout(TimeSpan.Zero));
    }

    /// <summary>Tests Timeout negative duration throws.</summary>
    [Test]
    public void WhenTimeoutNegativeDuration_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Timeout(TimeSpan.FromMilliseconds(-1)));
    }

    /// <summary>Tests Timeout with null fallback throws.</summary>
    [Test]
    public void WhenTimeoutWithFallbackNull_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).Timeout(TimeSpan.FromSeconds(1), (ObservableAsync<int>)null!));
    }
}
