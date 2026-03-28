// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
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
    [Test]
    public async Task WhenWhereIsNotNull_ThenNullValuesAreFiltered()
    {
        string?[] source = [null, "alpha", null, "beta"];

        var result = await source
            .ToObservableAsync()
            .WhereIsNotNull()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "alpha", "beta" });
    }

    /// <summary>
    /// Tests that CombineLatestValuesAreAllTrue evaluates the latest boolean state across an enumerable of sources.
    /// </summary>
    [Test]
    public async Task WhenCombineLatestValuesAreAllTrue_ThenEvaluatesAggregateState()
    {
        IObservableAsync<bool>[] sources = [AsyncObs.Return(true), AsyncObs.Return(true), AsyncObs.Return(false)];

        var result = await sources.CombineLatestValuesAreAllTrue().FirstAsync();

        await Assert.That(result).IsFalse();
    }

    /// <summary>
    /// Tests that GetMax returns the maximum latest value across all sources.
    /// </summary>
    [Test]
    public async Task WhenGetMax_ThenReturnsMaximumLatestValue()
    {
        var result = await AsyncObs.Return(2)
            .GetMax(AsyncObs.Return(5), AsyncObs.Return(3))
            .FirstAsync();

        await Assert.That(result).IsEqualTo(5);
    }

    /// <summary>
    /// Tests that ScanWithInitial emits the seed before emitting accumulated values.
    /// </summary>
    [Test]
    public async Task WhenScanWithInitial_ThenSeedIsEmittedFirst()
    {
        var result = await AsyncObs.Range(1, 3)
            .ScanWithInitial(0, static (acc, value) => acc + value)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 0, 1, 3, 6 });
    }

    /// <summary>
    /// Tests that Pairwise emits adjacent pairs.
    /// </summary>
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
    /// Tests that Partition splits a source into true and false branches.
    /// </summary>
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

        await Assert.That(trueTask.Result).IsEquivalentTo(new[] { 2, 4, 6 });
        await Assert.That(falseTask.Result).IsEquivalentTo(new[] { 1, 3, 5 });
    }

    /// <summary>
    /// Tests that DoOnSubscribe runs for each subscription.
    /// </summary>
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
    [Test]
    public async Task WhenStartFunction_ThenPublishesFunctionResult()
    {
        var result = await AsyncObs.Start(() => 42).FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }
}
