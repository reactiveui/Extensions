// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;
using AsyncObs = ReactiveUI.Extensions.Async.ObservableAsync;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Additional tests that target async parity helpers, subject factory edge cases, and infrastructure types.
/// </summary>
public class ParityAndInfrastructureCoverageTests
{
    /// <summary>
    /// Tests that AsSignal emits a unit value for each source item.
    /// </summary>
    [Test]
    public async Task WhenAsSignal_ThenEmitsUnitPerSourceValue()
    {
        var result = await AsyncObs.Range(1, 3)
            .AsSignal()
            .ToListAsync();

        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result.All(static value => value == Unit.Default)).IsTrue();
    }

    /// <summary>
    /// Tests that CatchIgnore suppresses terminal failures.
    /// </summary>
    [Test]
    public async Task WhenCatchIgnore_ThenSuppressesFailure()
    {
        var result = await AsyncObs.Throw<int>(new InvalidOperationException("boom"))
            .CatchIgnore()
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Tests that CatchAndReturn emits the supplied fallback value on failure.
    /// </summary>
    [Test]
    public async Task WhenCatchAndReturn_ThenReturnsFallbackValue()
    {
        var result = await AsyncObs.Throw<int>(new InvalidOperationException("boom"))
            .CatchAndReturn(4)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(4);
    }

    /// <summary>
    /// Tests that async DoOnSubscribe runs for each subscription.
    /// </summary>
    [Test]
    public async Task WhenDoOnSubscribeAsync_ThenRunsPerSubscription()
    {
        var subscriptions = 0;
        var source = AsyncObs.Return(42).DoOnSubscribe(_ =>
        {
            subscriptions++;
            return default;
        });

        await source.WaitCompletionAsync();
        await source.WaitCompletionAsync();

        await Assert.That(subscriptions).IsEqualTo(2);
    }

    /// <summary>
    /// Tests that DropIfBusy invokes the action and forwards values when the action completes immediately.
    /// </summary>
    [Test]
    public async Task WhenDropIfBusyWithImmediateAction_ThenForwardsValues()
    {
        var processed = new List<int>();

        var emitted = await AsyncObs.Range(1, 3)
            .DropIfBusy((value, _) =>
            {
                processed.Add(value);
                return default;
            })
            .ToListAsync();

        await Assert.That(processed).IsEquivalentTo(new[] { 1, 2, 3 });
        await Assert.That(emitted).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// Tests that LatestOrDefault prepends the default and suppresses duplicates.
    /// </summary>
    [Test]
    public async Task WhenLatestOrDefault_ThenPrependsDefaultAndRemovesDuplicates()
    {
        var result = await new[] { 0, 0, 1, 1 }
            .ToObservableAsync()
            .LatestOrDefault(0)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 0, 1 });
    }

    /// <summary>
    /// Tests that LogErrors invokes the logger when the source resumes with an error.
    /// </summary>
    [Test]
    public async Task WhenLogErrors_ThenInvokesLoggerOnErrorResume()
    {
        var errors = new List<Exception>();
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = AsyncObs.Create<int>(async (observer, cancellationToken) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("boom"), cancellationToken);
            await observer.OnCompletedAsync(Result.Success);
            return global::ReactiveUI.Extensions.Async.Disposables.DisposableAsync.Empty;
        });

        await using var subscription = await source
            .LogErrors(errors.Add)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completed.TrySetResult(result.IsSuccess);
                    return default;
                });

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("boom");
    }

    /// <summary>
    /// Tests that WaitUntil emits the first matching value.
    /// </summary>
    [Test]
    public async Task WhenWaitUntil_ThenEmitsFirstMatchingValue()
    {
        var result = await AsyncObs.Range(1, 5)
            .WaitUntil(static value => value >= 3)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(3);
    }

    /// <summary>
    /// Tests that ObserveOnSafe with a null async context returns the source unchanged.
    /// </summary>
    [Test]
    public async Task WhenObserveOnSafeWithNullAsyncContext_ThenReturnsSameSource()
    {
        IObservableAsync<int> source = AsyncObs.Return(1);

        var result = source.ObserveOnSafe((AsyncContext?)null);

        await Assert.That(ReferenceEquals(source, result)).IsTrue();
    }

    /// <summary>
    /// Tests that ObserveOnSafe with a null scheduler returns the source unchanged.
    /// </summary>
    [Test]
    public async Task WhenObserveOnSafeWithNullScheduler_ThenReturnsSameSource()
    {
        IObservableAsync<int> source = AsyncObs.Return(1);

        var result = source.ObserveOnSafe((TaskScheduler?)null);

        await Assert.That(ReferenceEquals(source, result)).IsTrue();
    }

    /// <summary>
    /// Tests that ObserveOnIf with a false condition returns the source unchanged.
    /// </summary>
    [Test]
    public async Task WhenObserveOnIfFalse_ThenReturnsSameSource()
    {
        IObservableAsync<int> source = AsyncObs.Return(1);

        var result = source.ObserveOnIf(false, TaskScheduler.Default);

        await Assert.That(ReferenceEquals(source, result)).IsTrue();
    }

    /// <summary>
    /// Tests that ReplayLastOnSubscribe prepends the initial value before forwarding the source value.
    /// </summary>
    [Test]
    public async Task WhenReplayLastOnSubscribe_ThenEmitsInitialAndSourceValue()
    {
        var replayed = AsyncObs.Return(5).ReplayLastOnSubscribe(0);

        var result = await replayed.ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 0, 5 });
    }

    /// <summary>
    /// Tests that ThrottleDistinct suppresses repeated values before and after throttling.
    /// </summary>
    [Test]
    public async Task WhenThrottleDistinct_ThenSuppressesDuplicateBursts()
    {
        var result = await new[] { 1, 1, 2, 2 }
            .ToObservableAsync()
            .ThrottleDistinct(TimeSpan.Zero)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2 });
    }

    /// <summary>
    /// Tests that DebounceUntil keeps the immediate matching value and cancels prior delayed values.
    /// </summary>
    [Test]
    public async Task WhenDebounceUntil_ThenConditionBypassesDelay()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var result = await new[] { 1, 2, 9 }
            .ToObservableAsync()
            .DebounceUntil(TimeSpan.FromMilliseconds(100), static value => value == 9)
            .FirstAsync(cts.Token);

        await Assert.That(result).IsEqualTo(9);
    }

    /// <summary>
    /// Tests that the async ScanWithInitial overload emits the seed and accumulated values.
    /// </summary>
    [Test]
    public async Task WhenScanWithInitialAsync_ThenEmitsSeedAndAccumulations()
    {
        var result = await AsyncObs.Range(1, 3)
            .ScanWithInitial(0, static (accumulator, value, _) => new ValueTask<int>(accumulator + value))
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 0, 1, 3, 6 });
    }

    /// <summary>
    /// Tests that CombineLatestValuesAreAllFalse returns true for an empty source set.
    /// </summary>
    [Test]
    public async Task WhenCombineLatestValuesAreAllFalseWithNoSources_ThenReturnsTrue()
    {
        var result = await Array.Empty<IObservableAsync<bool>>()
            .CombineLatestValuesAreAllFalse()
            .FirstAsync();

        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Tests that ForEach flattens nested enumerables.
    /// </summary>
    [Test]
    public async Task WhenForEach_ThenFlattensNestedEnumerables()
    {
        IEnumerable<int>[] source =
        [
            new[] { 1, 2 },
            new[] { 3, 4 },
        ];

        var result = await source
            .ToObservableAsync()
            .ForEach()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    /// <summary>
    /// Tests that Not negates each boolean value.
    /// </summary>
    [Test]
    public async Task WhenNot_ThenNegatesValues()
    {
        var result = await new[] { true, false, true }
            .ToObservableAsync()
            .Not()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { false, true, false });
    }

    /// <summary>
    /// Tests that SkipWhileNull skips leading null values until the first non-null value appears.
    /// </summary>
    [Test]
    public async Task WhenSkipWhileNull_ThenSkipsLeadingNullValues()
    {
        string?[] source = [null, null, "alpha", "beta"];

        var result = await source
            .ToObservableAsync()
            .SkipWhileNull()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "alpha", "beta" });
    }

    /// <summary>
    /// Tests that WhereFalse keeps only false values.
    /// </summary>
    [Test]
    public async Task WhenWhereFalse_ThenFiltersToFalseValues()
    {
        var result = await new[] { true, false, false, true }
            .ToObservableAsync()
            .WhereFalse()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { false, false });
    }

    /// <summary>
    /// Tests that WhereTrue keeps only true values.
    /// </summary>
    [Test]
    public async Task WhenWhereTrue_ThenFiltersToTrueValues()
    {
        var result = await new[] { true, false, true, false }
            .ToObservableAsync()
            .WhereTrue()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { true, true });
    }

    /// <summary>
    /// Tests that Start with an action and scheduler emits a unit value and runs the action.
    /// </summary>
    [Test]
    public async Task WhenStartActionWithScheduler_ThenRunsActionAndEmitsUnit()
    {
        var invoked = 0;

        var result = await AsyncObs.Start(new Action(() => invoked++), TaskScheduler.Default)
            .ToListAsync();

        await Assert.That(invoked).IsEqualTo(1);
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(Unit.Default);
    }

    /// <summary>
    /// Tests that Start with a function and scheduler publishes the function result.
    /// </summary>
    [Test]
    public async Task WhenStartFunctionWithScheduler_ThenPublishesFunctionResult()
    {
        var result = await AsyncObs.Start(() => 42, TaskScheduler.Default)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests that SubjectAsync.Create rejects unsupported publishing values.
    /// </summary>
    [Test]
    public void WhenCreateSubjectWithInvalidPublishingOption_ThenThrowsArgumentOutOfRange() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SubjectAsync.Create<int>(new SubjectCreationOptions
        {
            PublishingOption = (PublishingOption)(-1),
            IsStateless = false,
        }));

    /// <summary>
    /// Tests that SubjectAsync.CreateBehavior rejects unsupported publishing values.
    /// </summary>
    [Test]
    public void WhenCreateBehaviorSubjectWithInvalidPublishingOption_ThenThrowsArgumentOutOfRange() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SubjectAsync.CreateBehavior(1, new BehaviorSubjectCreationOptions
        {
            PublishingOption = (PublishingOption)(-1),
            IsStateless = false,
        }));

    /// <summary>
    /// Tests that SubjectAsync.CreateReplayLatest rejects unsupported publishing values.
    /// </summary>
    [Test]
    public void WhenCreateReplayLatestSubjectWithInvalidPublishingOption_ThenThrowsArgumentOutOfRange() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SubjectAsync.CreateReplayLatest<int>(new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = (PublishingOption)(-1),
            IsStateless = false,
        }));

    /// <summary>
    /// Tests that Optional.Empty has no value and throws when accessed.
    /// </summary>
    [Test]
    public async Task WhenOptionalIsEmpty_ThenHasNoValueAndThrowsOnAccess()
    {
        var optional = Optional<int>.Empty;

        await Assert.That(optional.HasValue).IsFalse();
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = optional.Value;
        });
    }

    /// <summary>
    /// Tests that Optional with a value exposes that value.
    /// </summary>
    [Test]
    public async Task WhenOptionalHasValue_ThenReturnsContainedValue()
    {
        var optional = new Optional<int>(42);

        await Assert.That(optional.HasValue).IsTrue();
        await Assert.That(optional.Value).IsEqualTo(42);
    }

    /// <summary>
    /// Tests that AsyncGate can be acquired, released, and acquired again.
    /// </summary>
    [Test]
    public async Task WhenAsyncGateIsReleased_ThenCanBeAcquiredAgain()
    {
        using var gate = new AsyncGate();

        var firstAcquired = false;
        using (await gate.LockAsync())
        {
            firstAcquired = true;
        }

        var reacquired = false;
        using (await gate.LockAsync())
        {
            reacquired = true;
        }

        await Assert.That(firstAcquired).IsTrue();
        await Assert.That(reacquired).IsTrue();
    }

    /// <summary>
    /// Tests that Heartbeat distinguishes heartbeat instances from update instances.
    /// </summary>
    [Test]
    public async Task WhenHeartbeatIsConstructed_ThenPropertiesReflectTheMode()
    {
        var heartbeat = new global::ReactiveUI.Extensions.Heartbeat<int>();
        var update = new global::ReactiveUI.Extensions.Heartbeat<int>(7);

        await Assert.That(heartbeat.IsHeartbeat).IsTrue();
        await Assert.That(update.IsHeartbeat).IsFalse();
        await Assert.That(update.Update).IsEqualTo(7);
    }

    /// <summary>
    /// Tests that Stale distinguishes stale instances from update instances.
    /// </summary>
    [Test]
    public async Task WhenStaleIsConstructed_ThenPropertiesReflectTheMode()
    {
        var stale = new global::ReactiveUI.Extensions.Stale<int>();
        var update = new global::ReactiveUI.Extensions.Stale<int>(9);

        await Assert.That(stale.IsStale).IsTrue();
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = stale.Update;
        });
        await Assert.That(update.IsStale).IsFalse();
        await Assert.That(update.Update).IsEqualTo(9);
    }
}
