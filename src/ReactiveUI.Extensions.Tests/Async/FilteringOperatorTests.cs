// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for filtering operators: Where, Take, Skip, TakeWhile, SkipWhile, Distinct, DistinctUntilChanged.
/// </summary>
public class FilteringOperatorTests
{
    /// <summary>Tests sync Where filters elements.</summary>
    [Test]
    public async Task WhenWhereSync_ThenFiltersElements()
    {
        var result = await ObservableAsync.Range(1, 6)
            .Where(x => x % 2 == 0)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 2, 4, 6 });
    }

    /// <summary>Tests async Where filters elements.</summary>
    [Test]
    public async Task WhenWhereAsync_ThenFiltersElements()
    {
        var result = await ObservableAsync.Range(1, 5)
            .Where(async (x, ct) =>
            {
                await Task.Yield();
                return x > 3;
            })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 4, 5 });
    }

    /// <summary>Tests Where filtering all emits nothing.</summary>
    [Test]
    public async Task WhenWhereFilterAll_ThenEmitsNothing()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Where(x => false)
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests Take emits only first N.</summary>
    [Test]
    public async Task WhenTake_ThenEmitsOnlyFirstN()
    {
        var result = await ObservableAsync.Range(1, 10)
            .Take(3)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests Take zero emits nothing.</summary>
    [Test]
    public async Task WhenTakeZero_ThenEmitsNothing()
    {
        var result = await ObservableAsync.Range(1, 10)
            .Take(0)
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests Take more than available emits all.</summary>
    [Test]
    public async Task WhenTakeMoreThanAvailable_ThenEmitsAll()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Take(100)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests Take negative throws.</summary>
    [Test]
    public void WhenTakeNegative_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Take(-1));
    }

    /// <summary>Tests Skip skips first N.</summary>
    [Test]
    public async Task WhenSkip_ThenSkipsFirstN()
    {
        var result = await ObservableAsync.Range(1, 5)
            .Skip(2)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 3, 4, 5 });
    }

    /// <summary>Tests Skip zero emits all.</summary>
    [Test]
    public async Task WhenSkipZero_ThenEmitsAll()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Skip(0)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests Skip more than available emits nothing.</summary>
    [Test]
    public async Task WhenSkipMoreThanAvailable_ThenEmitsNothing()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Skip(100)
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests Skip negative throws.</summary>
    [Test]
    public void WhenSkipNegative_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Skip(-1));
    }

    /// <summary>Tests sync TakeWhile emits while true.</summary>
    [Test]
    public async Task WhenTakeWhileSync_ThenEmitsWhileTrue()
    {
        var result = await ObservableAsync.Range(1, 10)
            .TakeWhile(x => x < 4)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests async TakeWhile emits while true.</summary>
    [Test]
    public async Task WhenTakeWhileAsync_ThenEmitsWhileTrue()
    {
        var result = await ObservableAsync.Range(1, 10)
            .TakeWhile(async (x, ct) =>
            {
                await Task.Yield();
                return x <= 2;
            })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2 });
    }

    /// <summary>Tests TakeWhile all true emits all.</summary>
    [Test]
    public async Task WhenTakeWhileAllTrue_ThenEmitsAll()
    {
        var result = await ObservableAsync.Range(1, 3)
            .TakeWhile(x => true)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests TakeWhile all false emits nothing.</summary>
    [Test]
    public async Task WhenTakeWhileAllFalse_ThenEmitsNothing()
    {
        var result = await ObservableAsync.Range(1, 3)
            .TakeWhile(x => false)
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests TakeWhile null predicate throws.</summary>
    [Test]
    public void WhenTakeWhileNullPredicate_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).TakeWhile((Func<int, bool>)null!));
    }

    /// <summary>Tests sync SkipWhile skips while true.</summary>
    [Test]
    public async Task WhenSkipWhileSync_ThenSkipsWhileTrue()
    {
        var result = await ObservableAsync.Range(1, 6)
            .SkipWhile(x => x < 4)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 4, 5, 6 });
    }

    /// <summary>Tests async SkipWhile skips while true.</summary>
    [Test]
    public async Task WhenSkipWhileAsync_ThenSkipsWhileTrue()
    {
        var result = await ObservableAsync.Range(1, 5)
            .SkipWhile(async (x, ct) =>
            {
                await Task.Yield();
                return x < 3;
            })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 3, 4, 5 });
    }

    /// <summary>Tests SkipWhile always true emits nothing.</summary>
    [Test]
    public async Task WhenSkipWhileAlwaysTrue_ThenEmitsNothing()
    {
        var result = await ObservableAsync.Range(1, 3)
            .SkipWhile(x => true)
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests SkipWhile always false emits all.</summary>
    [Test]
    public async Task WhenSkipWhileAlwaysFalse_ThenEmitsAll()
    {
        var result = await ObservableAsync.Range(1, 3)
            .SkipWhile(x => false)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests SkipWhile null predicate throws.</summary>
    [Test]
    public void WhenSkipWhileNullPredicate_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).SkipWhile((Func<int, bool>)null!));
    }

    /// <summary>Tests Distinct removes duplicates.</summary>
    [Test]
    public async Task WhenDistinct_ThenRemovesDuplicates()
    {
        var source = new[] { 1, 2, 2, 3, 1, 3 }.ToObservableAsync();

        var result = await source.Distinct().ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests Distinct with comparer uses case insensitive.</summary>
    [Test]
    public async Task WhenDistinctWithComparer_ThenUsesCaseInsensitive()
    {
        var source = new[] { "a", "A", "b", "B" }.ToObservableAsync();

        var result = await source.Distinct(StringComparer.OrdinalIgnoreCase).ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "a", "b" });
    }

    /// <summary>Tests DistinctBy distinguishes by key.</summary>
    [Test]
    public async Task WhenDistinctBy_ThenDistinguishesByKey()
    {
        var source = new[] { "abc", "ab", "a", "def", "de" }.ToObservableAsync();

        var result = await source.DistinctBy(s => s.Length).ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "abc", "ab", "a" });
    }

    /// <summary>Tests DistinctUntilChanged suppresses consecutive duplicates.</summary>
    [Test]
    public async Task WhenDistinctUntilChanged_ThenSuppressesConsecutiveDuplicates()
    {
        var source = new[] { 1, 1, 2, 2, 3, 1 }.ToObservableAsync();

        var result = await source.DistinctUntilChanged().ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 1 });
    }

    /// <summary>Tests DistinctUntilChanged with comparer.</summary>
    [Test]
    public async Task WhenDistinctUntilChangedWithComparer_ThenUsesComparer()
    {
        var source = new[] { "a", "A", "b", "B", "b" }.ToObservableAsync();

        var result = await source.DistinctUntilChanged(StringComparer.OrdinalIgnoreCase).ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "a", "b" });
    }

    /// <summary>Tests DistinctUntilChangedBy distinguishes by key.</summary>
    [Test]
    public async Task WhenDistinctUntilChangedBy_ThenDistinguishesByKey()
    {
        var source = new[] { "aa", "ab", "ba", "bb" }.ToObservableAsync();

        var result = await source.DistinctUntilChangedBy(s => s[0]).ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "aa", "ba" });
    }
}
