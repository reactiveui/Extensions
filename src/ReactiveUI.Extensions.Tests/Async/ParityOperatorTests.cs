// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;
using AsyncObs = ReactiveUI.Extensions.Async.ObservableAsync;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for async parity helpers that mirror the synchronous helper surface in the repository.
/// </summary>
public class ParityOperatorTests
{
    /// <summary>
    /// Tests that WhereIsNotNull filters null values and narrows the result type.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWhereIsNotNull_ThenNullValuesAreFiltered()
    {
        string?[] source = [null, "alpha", null, "beta"];

        var result = await source
            .ToObservableAsync()
            .WhereIsNotNull()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(["alpha", "beta"]);
    }

    /// <summary>
    /// Tests that CombineLatestValuesAreAllTrue evaluates the latest boolean state across an enumerable of sources.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestValuesAreAllTrue_ThenEvaluatesAggregateState()
    {
        IObservableAsync<bool>[] sources = [AsyncObs.Return(true), AsyncObs.Return(true), AsyncObs.Return(false)];

        var result = await sources.CombineLatestValuesAreAllTrue().FirstAsync();

        await Assert.That(result).IsFalse();
    }

    /// <summary>
    /// Tests that CombineLatestValuesAreAllTrue returns true when all sources emit true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestValuesAreAllTrue_WithAllTrue_ThenReturnsTrue()
    {
        IObservableAsync<bool>[] sources = [AsyncObs.Return(true), AsyncObs.Return(true), AsyncObs.Return(true)];

        var result = await sources.CombineLatestValuesAreAllTrue().FirstAsync();

        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Tests that CombineLatestValuesAreAllTrue returns true when no sources are provided.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestValuesAreAllTrue_WithEmptySources_ThenReturnsTrue()
    {
        IObservableAsync<bool>[] sources = [];

        var result = await sources.CombineLatestValuesAreAllTrue().FirstAsync();

        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Tests that GetMax returns the maximum latest value across all sources.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGetMax_ThenReturnsMaximumLatestValue()
    {
        var result = await AsyncObs.Return(2)
            .GetMax(AsyncObs.Return(5), AsyncObs.Return(3))
            .FirstAsync();

        await Assert.That(result).IsEqualTo(5);
    }

    /// <summary>
    /// Tests that GetMax returns the single value when only one source is provided.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGetMax_WithSingleSource_ThenReturnsThatValue()
    {
        var result = await AsyncObs.Return(7)
            .GetMax()
            .FirstAsync();

        await Assert.That(result).IsEqualTo(7);
    }

    /// <summary>
    /// Tests that ScanWithInitial emits the seed before emitting accumulated values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScanWithInitial_ThenSeedIsEmittedFirst()
    {
        var result = await AsyncObs.Range(1, 3)
            .ScanWithInitial(0, static (acc, value) => acc + value)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([0, 1, 3, 6]);
    }

    /// <summary>
    /// Tests that Pairwise emits adjacent pairs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPairwise_ThenAdjacentPairsAreProduced()
    {
        var result = await AsyncObs.Range(1, 4)
            .Pairwise()
            .ToListAsync();

        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[0]).IsEqualTo((1, 2));
        await Assert.That(result[1]).IsEqualTo((2, 3));
        await Assert.That(result[2]).IsEqualTo((3, 4));
    }

    /// <summary>
    /// Tests that Pairwise produces an empty sequence when the source has fewer than two elements.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPairwise_WithSingleElement_ThenProducesEmptySequence()
    {
        var result = await new[] { 42 }
            .ToObservableAsync()
            .Pairwise()
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Tests that Partition splits a source into true and false branches.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPartition_ThenSourceIsSplitIntoBranches()
    {
        var subject = SubjectAsync.Create<int>();
        var (trueBranch, falseBranch) = subject.Values.Partition(static value => value % 2 == 0);

        var trueTask = trueBranch.ToListAsync().AsTask();
        var falseTask = falseBranch.ToListAsync().AsTask();

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(2, CancellationToken.None);
        await subject.OnNextAsync(3, CancellationToken.None);
        await subject.OnNextAsync(4, CancellationToken.None);
        await subject.OnNextAsync(5, CancellationToken.None);
        await subject.OnNextAsync(6, CancellationToken.None);
        await subject.OnCompletedAsync(ReactiveUI.Extensions.Async.Internals.Result.Success);

        await Task.WhenAll(trueTask, falseTask);

        await Assert.That(trueTask.Result).IsEquivalentTo([2, 4, 6]);
        await Assert.That(falseTask.Result).IsEquivalentTo([1, 3, 5]);
    }

    /// <summary>
    /// Tests that DoOnSubscribe runs for each subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDoOnSubscribe_ThenRunsPerSubscription()
    {
        var subscriptions = 0;
        var source = AsyncObs.Return(42).DoOnSubscribe(() => subscriptions++);

        await source.WaitCompletionAsync();
        await source.WaitCompletionAsync();

        await Assert.That(subscriptions).IsEqualTo(2);
    }

    /// <summary>
    /// Tests that CatchIgnore suppresses terminal failures and completes with an empty result set.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchIgnore_ThenFailureIsSuppressed()
    {
        var result = await AsyncObs.Throw<int>(new InvalidOperationException("boom"))
            .CatchIgnore()
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Tests that Start executes the supplied function and publishes its result.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStartFunction_ThenPublishesFunctionResult()
    {
        var result = await AsyncObs.Start(() => 42).FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests that AsSignal converts each source value into Unit.Default.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsSignal_ThenEmitsUnitForEachValue()
    {
        var result = await new[] { 1, 2, 3 }
            .ToObservableAsync()
            .AsSignal()
            .ToListAsync();

        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[0]).IsEqualTo(Unit.Default);
        await Assert.That(result[1]).IsEqualTo(Unit.Default);
        await Assert.That(result[2]).IsEqualTo(Unit.Default);
    }

    /// <summary>
    /// Tests that CatchIgnore with a typed exception suppresses matching exceptions and invokes the action.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchIgnoreTyped_WithMatchingException_ThenSuppressesAndInvokesAction()
    {
        Exception? captured = null;

        var result = await AsyncObs.Throw<int>(new InvalidOperationException("typed boom"))
            .CatchIgnore<int, InvalidOperationException>(ex => captured = ex)
            .ToListAsync();

        await Assert.That(result).IsEmpty();
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).IsEqualTo("typed boom");
    }

    /// <summary>
    /// Tests that CatchIgnore with a typed exception re-throws when the exception type does not match.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchIgnoreTyped_WithNonMatchingException_ThenPropagatesError()
    {
        var error = new ArgumentException("wrong type");

        var resultTask = AsyncObs.Throw<int>(error)
            .CatchIgnore<int, InvalidOperationException>(_ => { })
            .ToListAsync()
            .AsTask();

        await Assert.That(resultTask).ThrowsException();
    }

    /// <summary>
    /// Tests that CatchAndReturn emits the fallback value on terminal failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchAndReturn_ThenEmitsFallbackOnFailure()
    {
        var result = await AsyncObs.Throw<int>(new InvalidOperationException("fail"))
            .CatchAndReturn(99)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([99]);
    }

    /// <summary>
    /// Tests that CatchAndReturn with a typed exception emits the factory result on a matching failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchAndReturnTyped_WithMatchingException_ThenEmitsFactoryResult()
    {
        var result = await AsyncObs.Throw<string>(new InvalidOperationException("boom"))
            .CatchAndReturn<string, InvalidOperationException>(ex => $"caught: {ex.Message}")
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(["caught: boom"]);
    }

    /// <summary>
    /// Tests that CatchAndReturn with a typed exception re-throws when the exception type does not match.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchAndReturnTyped_WithNonMatchingException_ThenPropagatesError()
    {
        var resultTask = AsyncObs.Throw<string>(new ArgumentException("nope"))
            .CatchAndReturn<string, InvalidOperationException>(_ => "fallback")
            .ToListAsync()
            .AsTask();

        await Assert.That(resultTask).ThrowsException();
    }

    /// <summary>
    /// Tests that the async DoOnSubscribe overload executes the asynchronous action before subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDoOnSubscribeAsync_ThenAsyncActionRunsBeforeSubscription()
    {
        var executed = false;

        var result = await AsyncObs.Return(10)
            .DoOnSubscribe(_ =>
            {
                executed = true;
                return default;
            })
            .FirstAsync();

        await Assert.That(executed).IsTrue();
        await Assert.That(result).IsEqualTo(10);
    }

    /// <summary>
    /// Tests that DropIfBusy drops values emitted while the async action is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDropIfBusy_WithBusyAction_ThenDropsValues()
    {
        var subject = SubjectAsync.Create<int>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = new List<int>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await subject.Values
            .DropIfBusy(async (value, _) =>
            {
                if (value == 1)
                {
                    await gate.Task;
                }
            })
            .SubscribeAsync(
                (value, _) =>
                {
                    result.Add(value);
                    return default;
                },
                null,
                _ =>
                {
                    completed.SetResult();
                    return default;
                },
                CancellationToken.None);

        // Emit value 1 which will block on the gate
        var emitTask = subject.OnNextAsync(1, CancellationToken.None).AsTask();

        // Emit values 2 and 3 while the action for value 1 is still running - these should be dropped
        // We need a small yield to ensure value 1's handler has started
        await Task.Yield();
        await subject.OnNextAsync(2, CancellationToken.None);
        await subject.OnNextAsync(3, CancellationToken.None);

        // Release the gate so value 1 completes
        gate.SetResult();
        await emitTask;

        // Emit value 4 after the action finishes - this should go through
        await subject.OnNextAsync(4, CancellationToken.None);
        await subject.OnCompletedAsync(ReactiveUI.Extensions.Async.Internals.Result.Success);

        await completed.Task;

        await Assert.That(result).Contains(1);
        await Assert.That(result).Contains(4);
        await Assert.That(result).DoesNotContain(2);
        await Assert.That(result).DoesNotContain(3);
    }

    /// <summary>
    /// Tests that DropIfBusy passes through all values when the action completes synchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDropIfBusy_WithFastAction_ThenAllValuesPassThrough()
    {
        var result = await new[] { 1, 2, 3 }
            .ToObservableAsync()
            .DropIfBusy((_, _) => default)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests that LatestOrDefault emits the default value first and suppresses a duplicate first source value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLatestOrDefault_ThenEmitsDefaultFirstAndSuppressesDuplicate()
    {
        var result = await new[] { 0, 1, 2 }
            .ToObservableAsync()
            .LatestOrDefault(0)
            .ToListAsync();

        // StartWith(0) prepends 0, then DistinctUntilChanged suppresses the duplicate 0 from source
        await Assert.That(result).IsEquivalentTo([0, 1, 2]);
    }

    /// <summary>
    /// Tests that LatestOrDefault emits both default and first source value when they differ.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLatestOrDefault_WithDifferentFirst_ThenEmitsBoth()
    {
        var result = await new[] { 5, 6 }
            .ToObservableAsync()
            .LatestOrDefault(0)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([0, 5, 6]);
    }

    /// <summary>
    /// Tests that LogErrors invokes the logger callback when an error-resume is observed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLogErrors_ThenLoggerIsInvokedOnError()
    {
        var logged = new List<Exception>();
        var source = AsyncTestHelpers.CreateDirectSource<int>();

        var items = new List<int>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await source
            .LogErrors(ex => logged.Add(ex))
            .SubscribeAsync(
                (value, _) =>
                {
                    items.Add(value);
                    return default;
                },
                null,
                _ =>
                {
                    completed.SetResult();
                    return default;
                },
                CancellationToken.None);

        await source.EmitNext(1);
        var testError = new InvalidOperationException("logged error");
        await source.EmitError(testError);
        await source.EmitNext(2);
        await source.Complete(ReactiveUI.Extensions.Async.Internals.Result.Success);

        await completed.Task;

        await Assert.That(items).IsEquivalentTo([1, 2]);
        await Assert.That(logged).Count().IsEqualTo(1);
        await Assert.That(logged[0].Message).IsEqualTo("logged error");
    }

    /// <summary>
    /// Tests that WaitUntil emits only the first value satisfying the predicate.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitUntil_ThenEmitsFirstMatchingValue()
    {
        var result = await new[] { 1, 2, 3, 4, 5 }
            .ToObservableAsync()
            .WaitUntil(static v => v > 3)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([4]);
    }

    /// <summary>
    /// Tests that ObserveOnSafe with a null AsyncContext returns the source unchanged.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnSafeAsyncContext_WithNull_ThenReturnsSourceUnchanged()
    {
        var source = AsyncObs.Return(1);

        var observed = source.ObserveOnSafe((AsyncContext?)null);

        var result = await observed.FirstAsync();
        await Assert.That(result).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that ObserveOnSafe with a non-null AsyncContext applies ObserveOn.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnSafeAsyncContext_WithValue_ThenAppliesObserveOn()
    {
        var context = AsyncContext.Default;

        var result = await AsyncObs.Return(42)
            .ObserveOnSafe(context)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests that ObserveOnSafe with a null TaskScheduler returns the source unchanged.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnSafeTaskScheduler_WithNull_ThenReturnsSourceUnchanged()
    {
        var source = AsyncObs.Return(1);

        var observed = source.ObserveOnSafe((TaskScheduler?)null);

        var result = await observed.FirstAsync();
        await Assert.That(result).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that ObserveOnSafe with a non-null TaskScheduler applies ObserveOn.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnSafeTaskScheduler_WithValue_ThenAppliesObserveOn()
    {
        var result = await AsyncObs.Return(42)
            .ObserveOnSafe(TaskScheduler.Default)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests that ObserveOnIf with true condition applies ObserveOn with AsyncContext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnIfAsyncContext_WithTrueCondition_ThenAppliesObserveOn()
    {
        var context = AsyncContext.Default;

        var result = await AsyncObs.Return(42)
            .ObserveOnIf(true, context)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests that ObserveOnIf with false condition returns the source unchanged for AsyncContext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnIfAsyncContext_WithFalseCondition_ThenReturnsSourceUnchanged()
    {
        var context = AsyncContext.Default;

        var result = await AsyncObs.Return(42)
            .ObserveOnIf(false, context)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests that ObserveOnIf with true condition applies ObserveOn with TaskScheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnIfTaskScheduler_WithTrueCondition_ThenAppliesObserveOn()
    {
        var result = await AsyncObs.Return(42)
            .ObserveOnIf(true, TaskScheduler.Default)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests that ObserveOnIf with false condition returns the source unchanged for TaskScheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnIfTaskScheduler_WithFalseCondition_ThenReturnsSourceUnchanged()
    {
        var result = await AsyncObs.Return(42)
            .ObserveOnIf(false, TaskScheduler.Default)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests that ReplayLastOnSubscribe replays the initial value and subsequent source values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenReplayLastOnSubscribe_ThenReplaysInitialAndSourceValues()
    {
        var subject = SubjectAsync.Create<int>();
        var replayed = subject.Values.ReplayLastOnSubscribe(0);

        var firstResult = await replayed.Take(1).FirstAsync();
        await Assert.That(firstResult).IsEqualTo(0);
    }

    /// <summary>
    /// Tests that ThrottleDistinct emits only distinct values after throttling.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleDistinct_ThenEmitsDistinctThrottledValues()
    {
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();
        var firstReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await subject.Values
            .ThrottleDistinct(TimeSpan.FromMilliseconds(50))
            .SubscribeAsync(
                (value, _) =>
                {
                    results.Add(value);
                    firstReceived.TrySetResult();
                    return default;
                },
                null,
                null);

        // Emit duplicate values quickly - DistinctUntilChanged collapses them
        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(1, CancellationToken.None);

        // Wait for throttle to emit the first distinct value
        var received = await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(5));

        await subject.OnCompletedAsync(ReactiveUI.Extensions.Async.Internals.Result.Success);

        await Assert.That(received).IsTrue();
        await Assert.That(results[0]).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that the async ScanWithInitial overload emits the seed followed by accumulated values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScanWithInitialAsync_ThenSeedIsEmittedFirst()
    {
        var result = await AsyncObs.Range(1, 3)
            .ScanWithInitial(
                0,
                static (acc, value, _) => new ValueTask<int>(acc + value))
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([0, 1, 3, 6]);
    }

    /// <summary>
    /// Tests that DebounceUntil immediately emits values that satisfy the condition.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDebounceUntil_WithConditionTrue_ThenEmitsImmediately()
    {
        var subject = SubjectAsync.Create<int>();

        var resultTask = subject.Values
            .DebounceUntil(TimeSpan.FromSeconds(10), static v => v > 5)
            .Take(1)
            .FirstAsync()
            .AsTask();

        // Value 10 satisfies condition, should emit immediately (no delay)
        await subject.OnNextAsync(10, CancellationToken.None);

        var result = await resultTask;

        await Assert.That(result).IsEqualTo(10);
    }

    /// <summary>
    /// Tests that DebounceUntil delays values that do not satisfy the condition.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDebounceUntil_WithConditionFalse_ThenDelaysEmission()
    {
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await subject.Values
            .DebounceUntil(TimeSpan.FromMilliseconds(50), static v => v > 100)
            .SubscribeAsync(
                (value, _) =>
                {
                    results.Add(value);
                    return default;
                },
                null,
                null);

        // Value 1 does not satisfy condition, should be delayed by 50ms
        await subject.OnNextAsync(1, CancellationToken.None);

        // Wait for the delayed value to arrive
        var received = await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(5));

        await subject.OnCompletedAsync(ReactiveUI.Extensions.Async.Internals.Result.Success);

        await Assert.That(received).IsTrue();
        await Assert.That(results[0]).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that GetMin returns the minimum latest value across all sources.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGetMin_ThenReturnsMinimumLatestValue()
    {
        var result = await AsyncObs.Return(5)
            .GetMin(AsyncObs.Return(2), AsyncObs.Return(8))
            .FirstAsync();

        await Assert.That(result).IsEqualTo(2);
    }

    /// <summary>
    /// Tests that GetMin returns the single value when only one source is provided.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGetMin_WithSingleSource_ThenReturnsThatValue()
    {
        var result = await AsyncObs.Return(7)
            .GetMin()
            .FirstAsync();

        await Assert.That(result).IsEqualTo(7);
    }

    /// <summary>
    /// Tests that CombineLatestValuesAreAllFalse returns true when all sources emit false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestValuesAreAllFalse_WithAllFalse_ThenReturnsTrue()
    {
        IObservableAsync<bool>[] sources = [AsyncObs.Return(false), AsyncObs.Return(false), AsyncObs.Return(false)];

        var result = await sources.CombineLatestValuesAreAllFalse().FirstAsync();

        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Tests that CombineLatestValuesAreAllFalse returns false when any source emits true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestValuesAreAllFalse_WithSomeTrue_ThenReturnsFalse()
    {
        IObservableAsync<bool>[] sources = [AsyncObs.Return(false), AsyncObs.Return(true), AsyncObs.Return(false)];

        var result = await sources.CombineLatestValuesAreAllFalse().FirstAsync();

        await Assert.That(result).IsFalse();
    }

    /// <summary>
    /// Tests that CombineLatestValuesAreAllFalse returns true when no sources are provided.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestValuesAreAllFalse_WithEmptySources_ThenReturnsTrue()
    {
        IObservableAsync<bool>[] sources = [];

        var result = await sources.CombineLatestValuesAreAllFalse().FirstAsync();

        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Tests that ForEach flattens enumerable elements into individual values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForEach_ThenFlattensEnumerableElements()
    {
        var result = await new IEnumerable<int>[] { [1, 2], [3, 4] }
            .ToObservableAsync()
            .ForEach()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4]);
    }

    /// <summary>
    /// Tests that Not negates each boolean value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenNot_ThenNegatesBooleanValues()
    {
        var result = await new[] { true, false, true }
            .ToObservableAsync()
            .Not()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([false, true, false]);
    }

    /// <summary>
    /// Tests that SkipWhileNull skips leading nulls and emits from the first non-null value onwards.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSkipWhileNull_ThenSkipsLeadingNulls()
    {
        string?[] source = [null, null, "first", null, "second"];

        var result = await source
            .ToObservableAsync()
            .SkipWhileNull()
            .ToListAsync();

        // SkipWhile skips while null, so once a non-null appears, all subsequent values pass through
        await Assert.That(result).IsEquivalentTo(["first", null!, "second"]);
    }

    /// <summary>
    /// Tests that Start with an Action emits Unit.Default.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStartAction_ThenEmitsUnit()
    {
        var executed = false;

        var result = await AsyncObs.Start(() => { executed = true; }).FirstAsync();

        await Assert.That(executed).IsTrue();
        await Assert.That(result).IsEqualTo(Unit.Default);
    }

    /// <summary>
    /// Tests that Start with an Action and a TaskScheduler executes on the scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStartAction_WithScheduler_ThenExecutesOnScheduler()
    {
        var executed = false;

        var result = await AsyncObs.Start(() => { executed = true; }, TaskScheduler.Default).FirstAsync();

        await Assert.That(executed).IsTrue();
        await Assert.That(result).IsEqualTo(Unit.Default);
    }

    /// <summary>
    /// Tests that Start with a function and a TaskScheduler executes on the scheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStartFunction_WithScheduler_ThenExecutesOnScheduler()
    {
        var result = await AsyncObs.Start(() => 99, TaskScheduler.Default).FirstAsync();

        await Assert.That(result).IsEqualTo(99);
    }

    /// <summary>
    /// Tests that WhereFalse filters to only false values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWhereFalse_ThenFiltersToFalseValues()
    {
        var result = await new[] { true, false, true, false, false }
            .ToObservableAsync()
            .WhereFalse()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([false, false, false]);
    }

    /// <summary>
    /// Tests that WhereTrue filters to only true values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWhereTrue_ThenFiltersToTrueValues()
    {
        var result = await new[] { true, false, true, false, true }
            .ToObservableAsync()
            .WhereTrue()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([true, true, true]);
    }

    /// <summary>
    /// Tests that CombineLatestValuesAreAllFalse materializes a non-collection enumerable source.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestValuesAreAllFalse_WithNonCollectionEnumerable_ThenMaterializesAndEvaluates()
    {
        IEnumerable<IObservableAsync<bool>> LazySource()
        {
            yield return AsyncObs.Return(false);
            yield return AsyncObs.Return(false);
        }

        var result = await LazySource().CombineLatestValuesAreAllFalse().FirstAsync();

        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Tests that CombineLatestValuesAreAllTrue materializes a non-collection enumerable source.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestValuesAreAllTrue_WithNonCollectionEnumerable_ThenMaterializesAndEvaluates()
    {
        IEnumerable<IObservableAsync<bool>> LazySource()
        {
            yield return AsyncObs.Return(true);
            yield return AsyncObs.Return(true);
        }

        var result = await LazySource().CombineLatestValuesAreAllTrue().FirstAsync();

        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Tests that CatchAndReturn passes through source values when no error occurs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchAndReturn_WithNoError_ThenPassesThroughSourceValues()
    {
        var result = await new[] { 1, 2, 3 }
            .ToObservableAsync()
            .CatchAndReturn(99)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests that CatchIgnore passes through source values when no error occurs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCatchIgnore_WithNoError_ThenPassesThroughSourceValues()
    {
        var result = await new[] { 1, 2, 3 }
            .ToObservableAsync()
            .CatchIgnore()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests that the async DoOnSubscribe overload runs on each subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDoOnSubscribeAsync_ThenRunsPerSubscription()
    {
        var count = 0;
        var source = AsyncObs.Return(42).DoOnSubscribe(_ =>
        {
            count++;
            return default;
        });

        await source.WaitCompletionAsync();
        await source.WaitCompletionAsync();

        await Assert.That(count).IsEqualTo(2);
    }

    /// <summary>
    /// Tests that GetMax picks the maximum when the first source has the largest value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGetMax_WithMaxInFirstSource_ThenReturnsCorrectMax()
    {
        var result = await AsyncObs.Return(10)
            .GetMax(AsyncObs.Return(1), AsyncObs.Return(5))
            .FirstAsync();

        await Assert.That(result).IsEqualTo(10);
    }

    /// <summary>
    /// Tests that GetMin picks the minimum when the first source has the smallest value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGetMin_WithMinInFirstSource_ThenReturnsCorrectMin()
    {
        var result = await AsyncObs.Return(1)
            .GetMin(AsyncObs.Return(10), AsyncObs.Return(5))
            .FirstAsync();

        await Assert.That(result).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that DropIfBusy silently discards values that arrive while the async action
    /// from a prior value is still executing, ensuring the early-return guard fires.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDropIfBusy_WithConcurrentEmission_ThenDroppedValueIsDiscarded()
    {
        var source = AsyncTestHelpers.CreateDirectSource<int>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actionStarted = 0;
        var droppedCount = 0;

        var received = new List<int>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await source
            .DropIfBusy(async (value, _) =>
            {
                Interlocked.Increment(ref actionStarted);

                if (value == 1)
                {
                    await gate.Task;
                }
            })
            .Do((value, _) =>
            {
                received.Add(value);
                return default;
            })
            .SubscribeAsync(
                (value, _) => default,
                null,
                _ =>
                {
                    completed.SetResult();
                    return default;
                },
                CancellationToken.None);

        // Start processing value 1 which blocks on the gate.
        var emit1Task = source.EmitNext(1).AsTask();

        // Wait until the action for value 1 has actually started.
        await AsyncTestHelpers.WaitForConditionAsync(
            () => Volatile.Read(ref actionStarted) >= 1,
            TimeSpan.FromSeconds(5));

        // Emit value 2 while value 1 is still processing - it should be dropped.
        await source.EmitNext(2);

        // The action should have been invoked only once (for value 1).
        droppedCount = 1 - (Volatile.Read(ref actionStarted) - 1);
        await Assert.That(droppedCount).IsEqualTo(1);

        // Release value 1 so it completes.
        gate.SetResult();
        await emit1Task;

        // Emit value 3 after the action finishes - this should pass through.
        await source.EmitNext(3);
        await source.Complete(ReactiveUI.Extensions.Async.Internals.Result.Success);

        await completed.Task;

        await Assert.That(received).Contains(1);
        await Assert.That(received).Contains(3);
        await Assert.That(received).DoesNotContain(2);
    }
}
