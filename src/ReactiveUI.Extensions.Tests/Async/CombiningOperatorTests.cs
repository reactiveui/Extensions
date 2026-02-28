// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for combining operators: Merge, Concat, CombineLatest, Zip, Prepend, StartWith.
/// </summary>
public class CombiningOperatorTests
{
    /// <summary>Tests Merge two sequences emits from both.</summary>
    [Test]
    public async Task WhenMergeTwoSequences_ThenEmitsFromBoth()
    {
        var first = ObservableAsync.Return(1);
        var second = ObservableAsync.Return(2);

        var result = await first.Merge(second).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result).Contains(1);
    }

    /// <summary>Tests Merge enumerable emits from all.</summary>
    [Test]
    public async Task WhenMergeEnumerable_ThenEmitsFromAll()
    {
        var sources = new[]
        {
            ObservableAsync.Return(10),
            ObservableAsync.Return(20),
            ObservableAsync.Return(30),
        };

        var result = await sources.Merge().ToListAsync();

        await Assert.That(result).Count().IsEqualTo(3);
    }

    /// <summary>Tests Merge observable of observables flattens.</summary>
    [Test]
    public async Task WhenMergeObservableOfObservables_ThenFlattens()
    {
        var source = new[]
        {
            ObservableAsync.Return(1),
            ObservableAsync.Return(2),
        }.ToObservableAsync();

        var result = await source.Merge().ToListAsync();

        await Assert.That(result).Count().IsEqualTo(2);
    }

    /// <summary>Tests Merge with max concurrency respects limit.</summary>
    [Test]
    public async Task WhenMergeWithMaxConcurrency_ThenRespectsLimit()
    {
        var activeConcurrency = 0;
        var maxConcurrency = 0;
        var gate = new object();

        var source = ObservableAsync.Range(1, 5).Select(i =>
            ObservableAsync.CreateAsBackgroundJob<int>(async (obs, ct) =>
            {
                lock (gate)
                {
                    activeConcurrency++;
                    maxConcurrency = Math.Max(maxConcurrency, activeConcurrency);
                }

                await Task.Delay(50, ct);

                lock (gate)
                {
                    activeConcurrency--;
                }

                await obs.OnNextAsync(i, ct);
                await obs.OnCompletedAsync(Result.Success);
            }));

        var result = await source.Merge(2).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(5);
        await Assert.That(maxConcurrency).IsLessThanOrEqualTo(2);
    }

    /// <summary>Tests Concat two sequences emits in order.</summary>
    [Test]
    public async Task WhenConcatTwoSequences_ThenEmitsInOrder()
    {
        var first = ObservableAsync.Range(1, 2);
        var second = ObservableAsync.Range(3, 2);

        var result = await first.Concat(second).ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    /// <summary>Tests Concat enumerable emits in sequential order.</summary>
    [Test]
    public async Task WhenConcatEnumerable_ThenEmitsInSequentialOrder()
    {
        var sources = new[]
        {
            ObservableAsync.Return(1),
            ObservableAsync.Return(2),
            ObservableAsync.Return(3),
        };

        var result = await sources.Concat().ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests Concat observable of observables concatenates sequentially.</summary>
    [Test]
    public async Task WhenConcatObservableOfObservables_ThenConcatenatesSequentially()
    {
        var sources = new[]
        {
            ObservableAsync.Range(1, 2),
            ObservableAsync.Range(3, 2),
        }.ToObservableAsync();

        var result = await sources.Concat().ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    /// <summary>Tests CombineLatest two sources combines latest values.</summary>
    [Test]
    public async Task WhenCombineLatestTwoSources_ThenCombinesLatestValues()
    {
        var subject1 = SubjectAsync.Create<int>();
        var subject2 = SubjectAsync.Create<string>();

        var results = new List<(int, string)>();
        await using var sub = await ObservableAsync
            .CombineLatest(subject1.Values, subject2.Values, (a, b) => (a, b))
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await subject1.OnNextAsync(1, CancellationToken.None);
        await subject2.OnNextAsync("a", CancellationToken.None);
        await subject1.OnNextAsync(2, CancellationToken.None);

        await Task.Delay(100);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo((1, "a"));
    }

    /// <summary>Tests Zip two sequences pairs by index.</summary>
    [Test]
    public async Task WhenZipTwoSequences_ThenPairsByIndex()
    {
        var first = ObservableAsync.Range(1, 3);
        var second = new[] { "a", "b", "c" }.ToObservableAsync();

        var result = await first.Zip(second, (n, s) => $"{n}{s}").ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "1a", "2b", "3c" });
    }

    /// <summary>Tests Zip tuple overload creates tuples.</summary>
    [Test]
    public async Task WhenZipTupleOverload_ThenCreatesTuples()
    {
        var first = ObservableAsync.Range(1, 2);
        var second = new[] { "x", "y" }.ToObservableAsync();

        var result = await first.Zip(second).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0]).IsEqualTo((1, "x"));
    }

    /// <summary>Tests Zip different lengths stops at shortest.</summary>
    [Test]
    public async Task WhenZipDifferentLengths_ThenStopsAtShortest()
    {
        var first = ObservableAsync.Range(1, 5);
        var second = ObservableAsync.Range(10, 2);

        var result = await first.Zip(second, (a, b) => a + b).ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 11, 13 });
    }

    /// <summary>Tests Zip null arguments throws.</summary>
    [Test]
    public void WhenZipNullArguments_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((ObservableAsync<int>)null!).Zip(ObservableAsync.Return(1), (a, b) => a + b));
    }

    /// <summary>Tests Prepend value comes first.</summary>
    [Test]
    public async Task WhenPrependValue_ThenValueComesFirst()
    {
        var result = await ObservableAsync.Range(2, 3)
            .Prepend(1)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    /// <summary>Tests Prepend enumerable values come first.</summary>
    [Test]
    public async Task WhenPrependEnumerable_ThenValuesComesFirst()
    {
        var result = await ObservableAsync.Range(3, 2)
            .Prepend(new[] { 1, 2 })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    /// <summary>Tests StartWith value comes first.</summary>
    [Test]
    public async Task WhenStartWithValue_ThenValueComesFirst()
    {
        var result = await ObservableAsync.Range(2, 2)
            .StartWith(1)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests StartWith enumerable values come first.</summary>
    [Test]
    public async Task WhenStartWithEnumerable_ThenValuesComesFirst()
    {
        var result = await ObservableAsync.Return(3)
            .StartWith(new[] { 1, 2 })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests StartWith params values come first.</summary>
    [Test]
    public async Task WhenStartWithParams_ThenValuesComesFirst()
    {
        var values = new[] { 1, 2, 3 };
        var result = await ObservableAsync.Return(4)
            .StartWith(values)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }
}
