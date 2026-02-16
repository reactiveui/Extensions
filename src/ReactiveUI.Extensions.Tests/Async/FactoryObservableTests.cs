// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NUnit.Framework;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for factory observables: Return, Empty, Throw, Never, Range, FromAsync, Defer, Create, Timer, Interval, ToAsyncObservable.
/// </summary>
public class FactoryObservableTests
{
    /// <summary>
    /// Tests Return emits single value.
    /// </summary>
    [Test]
    public async Task WhenReturnSingleValue_ThenEmitsValueAndCompletes()
    {
        var result = await ObservableAsync.Return(42).ToListAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(42));
    }

    /// <summary>
    /// Tests Return emits string.
    /// </summary>
    [Test]
    public async Task WhenReturnString_ThenEmitsStringAndCompletes()
    {
        var result = await ObservableAsync.Return("hello").ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { "hello" }));
    }

    /// <summary>
    /// Tests Empty completes with no items.
    /// </summary>
    [Test]
    public async Task WhenEmpty_ThenCompletesWithNoItems()
    {
        var result = await ObservableAsync.Empty<int>().ToListAsync();

        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Tests Throw completes with exception.
    /// </summary>
    [Test]
    public void WhenThrow_ThenCompletesWithException()
    {
        var ex = new InvalidOperationException("test error");
        var source = ObservableAsync.Throw<int>(ex);

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.ToListAsync());
        Assert.That(thrown!.Message, Is.EqualTo("test error"));
    }

    /// <summary>
    /// Tests Throw rejects null exception.
    /// </summary>
    [Test]
    public void WhenThrowNullException_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => ObservableAsync.Throw<int>(null!));
    }

    /// <summary>
    /// Tests Never does not complete within timeout.
    /// </summary>
    [Test]
    public async Task WhenNever_ThenDoesNotCompleteWithinTimeout()
    {
        using var cts = new CancellationTokenSource(200);
        var items = new List<int>();
        var completed = false;

        await using var sub = await ObservableAsync.Never<int>().SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            _ =>
            {
                completed = true;
                return default;
            },
            cts.Token);

        await Task.Delay(250);

        Assert.That(items, Is.Empty);
        Assert.That(completed, Is.False);
    }

    /// <summary>
    /// Tests Range from zero emits sequential integers.
    /// </summary>
    [Test]
    public async Task WhenRangeFromZero_ThenEmitsSequentialIntegers()
    {
        var result = await ObservableAsync.Range(0, 5).ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
    }

    /// <summary>
    /// Tests Range from non-zero emits correct range.
    /// </summary>
    [Test]
    public async Task WhenRangeFromNonZero_ThenEmitsCorrectRange()
    {
        var result = await ObservableAsync.Range(10, 3).ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 10, 11, 12 }));
    }

    /// <summary>
    /// Tests Range with count zero emits nothing.
    /// </summary>
    [Test]
    public async Task WhenRangeCountZero_ThenEmitsNothing()
    {
        var result = await ObservableAsync.Range(0, 0).ToListAsync();

        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Tests FromAsync with value emits single value.
    /// </summary>
    [Test]
    public async Task WhenFromAsyncWithValue_ThenEmitsSingleValue()
    {
        var source = ObservableAsync.FromAsync(async ct =>
        {
            await Task.Yield();
            return 99;
        });

        var result = await source.ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 99 }));
    }

    /// <summary>
    /// Tests FromAsync void executes action.
    /// </summary>
    [Test]
    public async Task WhenFromAsyncVoid_ThenEmitsUnit()
    {
        var executed = false;
        var source = ObservableAsync.FromAsync(async ct =>
        {
            await Task.Yield();
            executed = true;
        });

        await source.WaitCompletionAsync();

        Assert.That(executed, Is.True);
    }

    /// <summary>
    /// Tests Defer creates new sequence per subscription.
    /// </summary>
    [Test]
    public async Task WhenDefer_ThenCreatesNewSequencePerSubscription()
    {
        var counter = 0;
        var source = ObservableAsync.Defer(() =>
        {
            counter++;
            return ObservableAsync.Return(counter);
        });

        var first = await source.FirstAsync();
        var second = await source.FirstAsync();

        Assert.That(first, Is.EqualTo(1));
        Assert.That(second, Is.EqualTo(2));
    }

    /// <summary>
    /// Tests async Defer creates new sequence per subscription.
    /// </summary>
    [Test]
    public async Task WhenDeferAsync_ThenCreatesNewSequencePerSubscription()
    {
        var counter = 0;
        var source = ObservableAsync.Defer(async ct =>
        {
            await Task.Yield();
            counter++;
            return ObservableAsync.Return(counter);
        });

        var first = await source.FirstAsync();
        var second = await source.FirstAsync();

        Assert.That(first, Is.EqualTo(1));
        Assert.That(second, Is.EqualTo(2));
    }

    /// <summary>
    /// Tests Create with custom subscription logic.
    /// </summary>
    [Test]
    public async Task WhenCreate_ThenCustomSubscriptionLogicRuns()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnNextAsync(2, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var result = await source.ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 1, 2 }));
    }

    /// <summary>
    /// Tests Create with null subscribe function.
    /// </summary>
    [Test]
    public void WhenCreateWithNullSubscribeFunc_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Create<int>(null!));
    }

    /// <summary>
    /// Tests CreateAsBackgroundJob runs on background.
    /// </summary>
    [Test]
    public async Task WhenCreateAsBackgroundJob_ThenRunsOnBackground()
    {
        var source = ObservableAsync.CreateAsBackgroundJob<int>(async (observer, ct) =>
        {
            await Task.Yield();
            await observer.OnNextAsync(42, ct);
            await observer.OnCompletedAsync(Result.Success);
        });

        var result = await source.ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 42 }));
    }

    /// <summary>
    /// Tests Timer single shot emits one value.
    /// </summary>
    [Test]
    public async Task WhenTimerSingleShot_ThenEmitsSingleValueAfterDelay()
    {
        var source = ObservableAsync.Timer(TimeSpan.FromMilliseconds(50));

        var result = await source.ToListAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(0L));
    }

    /// <summary>
    /// Tests Timer periodic emits multiple values.
    /// </summary>
    [Test]
    public async Task WhenTimerPeriodic_ThenEmitsMultipleValues()
    {
        using var cts = new CancellationTokenSource(300);
        var source = ObservableAsync.Timer(
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(50));

        var items = new List<long>();
        try
        {
            await using var sub = await source.SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null,
                cts.Token);
            await Task.Delay(350, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }

        Assert.That(items.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(items[0], Is.EqualTo(0L));
    }

    /// <summary>
    /// Tests Timer negative due time.
    /// </summary>
    [Test]
    public void WhenTimerNegativeDueTime_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Timer(TimeSpan.FromMilliseconds(-1)));
    }

    /// <summary>
    /// Tests Timer periodic with non-positive period.
    /// </summary>
    [Test]
    public void WhenTimerPeriodicNonPositivePeriod_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Timer(TimeSpan.Zero, TimeSpan.Zero));
    }

    /// <summary>
    /// Tests IEnumerable to ObservableAsync.
    /// </summary>
    [Test]
    public async Task WhenEnumerableToObservableAsync_ThenEmitsAllItems()
    {
        var source = new[] { 1, 2, 3 }.ToObservableAsync();

        var result = await source.ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    /// <summary>
    /// Tests IAsyncEnumerable to ObservableAsync.
    /// </summary>
    [Test]
    public async Task WhenAsyncEnumerableToObservableAsync_ThenEmitsAllItems()
    {
        var source = AsyncEnumerable().ToObservableAsync();

        var result = await source.ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { 10, 20, 30 }));

        static async IAsyncEnumerable<int> AsyncEnumerable()
        {
            yield return 10;
            await Task.Yield();
            yield return 20;
            await Task.Yield();
            yield return 30;
        }
    }

    /// <summary>
    /// Tests Task to ObservableAsync.
    /// </summary>
    [Test]
    public async Task WhenTaskToObservableAsync_ThenEmitsTaskResult()
    {
        var task = Task.FromResult(7);
        var source = task.ToObservableAsync();

        var result = await source.FirstAsync();

        Assert.That(result, Is.EqualTo(7));
    }

    /// <summary>
    /// Tests void Task to ObservableAsync.
    /// </summary>
    [Test]
    public async Task WhenVoidTaskToObservableAsync_ThenEmitsUnit()
    {
        var task = Task.CompletedTask;
        var source = task.ToObservableAsync();

        await source.WaitCompletionAsync();
    }

    /// <summary>
    /// Tests Interval emits periodic values.
    /// </summary>
    [Test]
    public async Task WhenIntervalWithCancellation_ThenEmitsPeriodicValues()
    {
        using var cts = new CancellationTokenSource(250);
        var source = ObservableAsync.Interval(TimeSpan.FromMilliseconds(50));

        var items = new List<long>();
        try
        {
            await using var sub = await source.SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null,
                cts.Token);
            await Task.Delay(300, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }

        Assert.That(items.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(items[0], Is.EqualTo(1L));
    }
}
