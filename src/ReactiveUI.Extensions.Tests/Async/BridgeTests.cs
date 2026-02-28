// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;
using AsyncObs = ReactiveUI.Extensions.Async.ObservableAsync;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for the bi-directional bridge between IObservable{T} and ObservableAsync{T},
/// including combined real-world scenarios.
/// </summary>
public class BridgeTests
{
    /// <summary>
    /// Tests that ToObservableAsync forwards all items from IObservable.
    /// </summary>
    [Test]
    public async Task WhenIObservableToObservableAsync_ThenForwardsAllItems()
    {
        var rxSource = Observable.Range(1, 5);
        var asyncObs = rxSource.ToObservableAsync();

        var result = await asyncObs.ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    /// <summary>
    /// Tests that ToObservableAsync forwards completion from IObservable.
    /// </summary>
    [Test]
    public async Task WhenIObservableToObservableAsync_ThenForwardsCompletion()
    {
        var rxSource = Observable.Empty<int>();
        var asyncObs = rxSource.ToObservableAsync();

        var result = await asyncObs.ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Tests that ToObservableAsync forwards errors from IObservable.
    /// </summary>
    [Test]
    public async Task WhenIObservableToObservableAsync_ThenForwardsError()
    {
        var ex = new InvalidOperationException("rx error");
        var rxSource = Observable.Throw<int>(ex);
        var asyncObs = rxSource.ToObservableAsync();

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await asyncObs.ToListAsync());
    }

    /// <summary>
    /// Tests that ToObservable forwards all items from ObservableAsync.
    /// </summary>
    [Test]
    public async Task WhenObservableAsyncToObservable_ThenForwardsAllItems()
    {
        var asyncSource = AsyncObs.Range(1, 5);
        var rxObs = asyncSource.ToObservable();

        var result = await rxObs.ToList();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    /// <summary>
    /// Tests that ToObservable forwards completion from ObservableAsync.
    /// </summary>
    [Test]
    public async Task WhenObservableAsyncToObservable_ThenForwardsCompletion()
    {
        var asyncSource = AsyncObs.Empty<int>();
        var rxObs = asyncSource.ToObservable();

        var result = await rxObs.ToList();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Tests that ToObservable forwards errors from ObservableAsync.
    /// </summary>
    [Test]
    public void WhenObservableAsyncToObservable_ThenForwardsError()
    {
        var asyncSource = AsyncObs.Throw<int>(new InvalidOperationException("async error"));
        var rxObs = asyncSource.ToObservable();

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await rxObs.ToList());
    }

    /// <summary>
    /// Tests that ToObservableAsync rejects null source.
    /// </summary>
    [Test]
    public void WhenToObservableAsyncNullSource_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IObservable<int>)null!).ToObservableAsync());
    }

    /// <summary>
    /// Tests that ToObservable rejects null source.
    /// </summary>
    [Test]
    public void WhenToObservableNullSource_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((ObservableAsync<int>)null!).ToObservable());
    }

    /// <summary>
    /// Tests round-trip IObservable through ObservableAsync.
    /// </summary>
    [Test]
    public async Task WhenRoundTripIObservableThroughAsync_ThenPreservesSequence()
    {
        var rxSource = Observable.Range(1, 5);

        var roundTripped = rxSource
            .ToObservableAsync()
            .ToObservable();

        var result = await roundTripped.ToList();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    /// <summary>
    /// Tests round-trip ObservableAsync through IObservable.
    /// </summary>
    [Test]
    public async Task WhenRoundTripAsyncThroughIObservable_ThenPreservesSequence()
    {
        var asyncSource = AsyncObs.Range(1, 5);

        var roundTripped = asyncSource
            .ToObservable()
            .ToObservableAsync();

        var result = await roundTripped.ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    /// <summary>
    /// Tests bridged IObservable with async operators.
    /// </summary>
    [Test]
    public async Task WhenBridgedObservableWithAsyncOperators_ThenPipelineWorks()
    {
        var rxSource = Observable.Range(1, 10);

        var result = await rxSource.ToObservableAsync()
            .Where(x => x % 2 == 0)
            .Select(x => x * 10)
            .Take(3)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 20, 40, 60 });
    }

    /// <summary>
    /// Tests async observable bridged to Rx with Rx operators.
    /// </summary>
    [Test]
    public async Task WhenAsyncObservableWithRxOperators_ThenPipelineWorks()
    {
        var asyncSource = AsyncObs.Range(1, 10);

        var result = await asyncSource.ToObservable()
            .Where(x => x > 5)
            .Take(3)
            .Select(x => x * 2)
            .ToList();

        await Assert.That(result).IsEquivalentTo(new[] { 12, 14, 16 });
    }

    /// <summary>
    /// Tests Rx Subject pushing data through async pipeline.
    /// </summary>
    [Test]
    public async Task WhenIObservableSubjectToAsyncPipeline_ThenBidirectionalFlows()
    {
        var rxSubject = new Subject<int>();
        var asyncPipeline = rxSubject.ToObservableAsync()
            .Select(x => x * 2)
            .Where(x => x > 5);

        var items = new List<int>();
        await using var sub = await asyncPipeline.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        rxSubject.OnNext(1);  // 2 -> filtered
        rxSubject.OnNext(3);  // 6 -> passes
        rxSubject.OnNext(5);  // 10 -> passes
        rxSubject.OnNext(2);  // 4 -> filtered
        rxSubject.OnCompleted();

        await Task.Delay(200);

        await Assert.That(items).IsEquivalentTo(new[] { 6, 10 });
    }

    /// <summary>
    /// Tests async subject bridged to Rx pipeline.
    /// </summary>
    [Test]
    public async Task WhenAsyncSubjectToRxPipeline_ThenBidirectionalFlows()
    {
        var asyncSubject = SubjectAsync.Create<int>();
        var rxPipeline = asyncSubject.Values
            .Select(x => x + 100)
            .ToObservable();

        var items = new List<int>();
        using var sub = rxPipeline.Subscribe(x => items.Add(x));

        await asyncSubject.OnNextAsync(1, CancellationToken.None);
        await asyncSubject.OnNextAsync(2, CancellationToken.None);
        await asyncSubject.OnNextAsync(3, CancellationToken.None);
        await asyncSubject.OnCompletedAsync(Result.Success);

        await Task.Delay(200);

        await Assert.That(items).IsEquivalentTo(new[] { 101, 102, 103 });
    }

    /// <summary>
    /// Tests merging bridged IObservable with native async observable.
    /// </summary>
    [Test]
    public async Task WhenMixedRxAndAsyncMerge_ThenBothSourcesContribute()
    {
        var rxSource = Observable.Range(1, 3);
        var asyncSource = AsyncObs.Range(10, 3);

        var result = await rxSource.ToObservableAsync().Concat(asyncSource).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(6);
        await Assert.That(result).Contains(1);
        await Assert.That(result).Contains(10);
    }

    /// <summary>
    /// Tests concatenating bridged IObservable with async observable preserves order.
    /// </summary>
    [Test]
    public async Task WhenMixedConcatRxAndAsync_ThenOrderPreserved()
    {
        var rxSource = Observable.Range(1, 2);
        var asyncSource = AsyncObs.Range(3, 2);

        var result = await rxSource.ToObservableAsync()
            .Concat(asyncSource)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    /// <summary>
    /// Tests SelectMany across bridged sources.
    /// </summary>
    [Test]
    public async Task WhenSelectManyBridgedSources_ThenFlattensCorrectly()
    {
        var rxSource = Observable.Range(1, 3);

        var result = await rxSource.ToObservableAsync()
            .SelectMany(x => AsyncObs.Return(x * 100))
            .ToListAsync();

        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result).Contains(100);
        await Assert.That(result).Contains(200);
        await Assert.That(result).Contains(300);
    }

    /// <summary>
    /// Tests Zip across bridged Rx and async sources.
    /// </summary>
    [Test]
    public async Task WhenZipRxAndAsync_ThenPairsCorrectly()
    {
        var rxSource = Observable.Range(1, 3);
        var asyncSource = new[] { "a", "b", "c" }.ToObservableAsync();

        var result = await rxSource.ToObservableAsync()
            .Zip(asyncSource, (n, s) => $"{n}{s}")
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "1a", "2b", "3c" });
    }
}
