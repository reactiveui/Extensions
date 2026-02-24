// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for terminal operators: FirstAsync, LastAsync, SingleAsync, CountAsync, AnyAsync, AllAsync,
/// ContainsAsync, AggregateAsync, ToListAsync, ToDictionaryAsync, ForEachAsync, WaitCompletionAsync, ToAsyncEnumerable.
/// </summary>
public class TerminalOperatorTests
{
    /// <summary>Tests FirstAsync returns first element.</summary>
    [Test]
    public async Task WhenFirstAsync_ThenReturnsFirstElement()
    {
        var result = await ObservableAsync.Range(10, 3).FirstAsync();
        await Assert.That(result).IsEqualTo(10);
    }

    /// <summary>Tests FirstAsync with predicate returns first match.</summary>
    [Test]
    public async Task WhenFirstAsyncWithPredicate_ThenReturnsFirstMatch()
    {
        var result = await ObservableAsync.Range(1, 5).FirstAsync(x => x > 3);
        await Assert.That(result).IsEqualTo(4);
    }

    /// <summary>Tests FirstAsync on empty throws.</summary>
    [Test]
    public void WhenFirstAsyncOnEmpty_ThenThrowsInvalidOperation()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Empty<int>().FirstAsync());
    }

    /// <summary>Tests FirstOrDefault on empty returns default.</summary>
    [Test]
    public async Task WhenFirstOrDefaultOnEmpty_ThenReturnsDefault()
    {
        var result = await ObservableAsync.Empty<int>().FirstOrDefaultAsync();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Tests FirstOrDefault with predicate match returns first.</summary>
    [Test]
    public async Task WhenFirstOrDefaultWithMatch_ThenReturnsFirst()
    {
        var result = await ObservableAsync.Range(1, 5).Where(x => x > 3).FirstOrDefaultAsync(0);
        await Assert.That(result).IsEqualTo(4);
    }

    /// <summary>Tests LastAsync returns last element.</summary>
    [Test]
    public async Task WhenLastAsync_ThenReturnsLastElement()
    {
        var result = await ObservableAsync.Range(1, 5).LastAsync();
        await Assert.That(result).IsEqualTo(5);
    }

    /// <summary>Tests LastAsync with predicate returns last match.</summary>
    [Test]
    public async Task WhenLastAsyncWithPredicate_ThenReturnsLastMatch()
    {
        var result = await ObservableAsync.Range(1, 5).LastAsync(x => x < 4);
        await Assert.That(result).IsEqualTo(3);
    }

    /// <summary>Tests LastAsync on empty throws.</summary>
    [Test]
    public void WhenLastAsyncOnEmpty_ThenThrowsInvalidOperation()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Empty<int>().LastAsync());
    }

    /// <summary>Tests LastOrDefault on empty returns default.</summary>
    [Test]
    public async Task WhenLastOrDefaultOnEmpty_ThenReturnsDefault()
    {
        var result = await ObservableAsync.Empty<int>().LastOrDefaultAsync();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Tests SingleAsync returns single element.</summary>
    [Test]
    public async Task WhenSingleAsync_ThenReturnsSingleElement()
    {
        var result = await ObservableAsync.Return(42).SingleAsync();
        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>Tests SingleAsync multiple elements throws.</summary>
    [Test]
    public void WhenSingleAsyncMultipleElements_ThenThrowsInvalidOperation()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Range(1, 3).SingleAsync());
    }

    /// <summary>Tests SingleAsync on empty throws.</summary>
    [Test]
    public void WhenSingleAsyncOnEmpty_ThenThrowsInvalidOperation()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Empty<int>().SingleAsync());
    }

    /// <summary>Tests SingleOrDefault on empty returns default.</summary>
    [Test]
    public async Task WhenSingleOrDefaultOnEmpty_ThenReturnsDefault()
    {
        var result = await ObservableAsync.Empty<int>().SingleOrDefaultAsync();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Tests CountAsync returns element count.</summary>
    [Test]
    public async Task WhenCountAsync_ThenReturnsElementCount()
    {
        var result = await ObservableAsync.Range(1, 5).CountAsync();
        await Assert.That(result).IsEqualTo(5);
    }

    /// <summary>Tests CountAsync on empty returns zero.</summary>
    [Test]
    public async Task WhenCountAsyncOnEmpty_ThenReturnsZero()
    {
        var result = await ObservableAsync.Empty<int>().CountAsync();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Tests LongCountAsync returns element count.</summary>
    [Test]
    public async Task WhenLongCountAsync_ThenReturnsElementCount()
    {
        var result = await ObservableAsync.Range(1, 3).LongCountAsync();
        await Assert.That(result).IsEqualTo(3L);
    }

    /// <summary>Tests AnyAsync on non-empty returns true.</summary>
    [Test]
    public async Task WhenAnyAsyncOnNonEmpty_ThenReturnsTrue()
    {
        var result = await ObservableAsync.Return(1).AnyAsync();
        await Assert.That(result).IsTrue();
    }

    /// <summary>Tests AnyAsync on empty returns false.</summary>
    [Test]
    public async Task WhenAnyAsyncOnEmpty_ThenReturnsFalse()
    {
        var result = await ObservableAsync.Empty<int>().AnyAsync();
        await Assert.That(result).IsFalse();
    }

    /// <summary>Tests AnyAsync with predicate checks condition.</summary>
    [Test]
    public async Task WhenAnyAsyncWithPredicateMatch_ThenReturnsTrue()
    {
        var hasEven = await ObservableAsync.Range(1, 5).AnyAsync(x => x % 2 == 0);
        await Assert.That(hasEven).IsTrue();
    }

    /// <summary>Tests AnyAsync with predicate no match returns false.</summary>
    [Test]
    public async Task WhenAnyAsyncWithPredicateNoMatch_ThenReturnsFalse()
    {
        var hasNeg = await ObservableAsync.Range(1, 5).AnyAsync(x => x < 0);
        await Assert.That(hasNeg).IsFalse();
    }

    /// <summary>Tests AllAsync checks all elements.</summary>
    [Test]
    public async Task WhenAllAsyncAllMatch_ThenReturnsTrue()
    {
        var allPositive = await ObservableAsync.Range(1, 5).AllAsync(x => x > 0);
        await Assert.That(allPositive).IsTrue();
    }

    /// <summary>Tests AllAsync with partial match returns false.</summary>
    [Test]
    public async Task WhenAllAsyncPartialMatch_ThenReturnsFalse()
    {
        var allGreaterThan3 = await ObservableAsync.Range(1, 5).AllAsync(x => x > 3);
        await Assert.That(allGreaterThan3).IsFalse();
    }

    /// <summary>Tests AllAsync on empty returns true.</summary>
    [Test]
    public async Task WhenAllAsyncOnEmpty_ThenReturnsTrue()
    {
        var result = await ObservableAsync.Empty<int>().AllAsync(x => false);
        await Assert.That(result).IsTrue();
    }

    /// <summary>Tests ContainsAsync with match returns true.</summary>
    [Test]
    public async Task WhenContainsAsyncWithMatch_ThenReturnsTrue()
    {
        var result = await ObservableAsync.Range(1, 5).ContainsAsync(3);
        await Assert.That(result).IsTrue();
    }

    /// <summary>Tests ContainsAsync with no match returns false.</summary>
    [Test]
    public async Task WhenContainsAsyncWithNoMatch_ThenReturnsFalse()
    {
        var result = await ObservableAsync.Range(1, 5).ContainsAsync(99);
        await Assert.That(result).IsFalse();
    }

    /// <summary>Tests sync AggregateAsync computes final value.</summary>
    [Test]
    public async Task WhenAggregateAsyncSync_ThenComputesFinalValue()
    {
        var result = await ObservableAsync.Range(1, 4).AggregateAsync(0, (acc, x) => acc + x);
        await Assert.That(result).IsEqualTo(10);
    }

    /// <summary>Tests async AggregateAsync computes final value.</summary>
    [Test]
    public async Task WhenAggregateAsyncAsync_ThenComputesFinalValue()
    {
        var result = await ObservableAsync.Range(1, 3).AggregateAsync(
            string.Empty,
            async (acc, x, ct) =>
            {
                await Task.Yield();
                return acc + x;
            });

        await Assert.That(result).IsEqualTo("123");
    }

    /// <summary>Tests AggregateAsync with result selector transforms final value.</summary>
    [Test]
    public async Task WhenAggregateAsyncWithResultSelector_ThenTransformsFinalValue()
    {
        var result = await ObservableAsync.Range(1, 4).AggregateAsync(
            0,
            (acc, x) => acc + x,
            acc => $"Sum={acc}");

        await Assert.That(result).IsEqualTo("Sum=10");
    }

    /// <summary>Tests AggregateAsync null accumulator throws.</summary>
    [Test]
    public void WhenAggregateAsyncNullAccumulator_ThenThrowsArgumentNull()
    {
        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ObservableAsync.Return(1).AggregateAsync(0, (Func<int, int, int>)null!));
    }

    /// <summary>Tests ToListAsync collects all elements.</summary>
    [Test]
    public async Task WhenToListAsync_ThenCollectsAllElements()
    {
        var result = await ObservableAsync.Range(1, 4).ToListAsync();
        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    /// <summary>Tests ToDictionaryAsync creates correct dictionary.</summary>
    [Test]
    public async Task WhenToDictionaryAsync_ThenCreatesCorrectDictionary()
    {
        var source = new[] { "a", "bb", "ccc" }.ToObservableAsync();
        var result = await source.ToDictionaryAsync(s => s.Length);

        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[1]).IsEqualTo("a");
    }

    /// <summary>Tests ForEachAsync processes all elements.</summary>
    [Test]
    public async Task WhenForEachAsync_ThenProcessesAllElements()
    {
        var items = new List<int>();
        await ObservableAsync.Range(1, 3).ForEachAsync(x => items.Add(x));
        await Assert.That(items).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests WaitCompletionAsync waits for completion.</summary>
    [Test]
    public async Task WhenWaitCompletionAsync_ThenWaitsForCompletion()
    {
        await ObservableAsync.Range(1, 3).WaitCompletionAsync();
    }

    /// <summary>Tests WaitCompletionAsync on error throws.</summary>
    [Test]
    public void WhenWaitCompletionAsyncOnError_ThenThrows()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(new InvalidOperationException("err")).WaitCompletionAsync());
    }

    /// <summary>Tests ToAsyncEnumerable can be enumerated.</summary>
    [Test]
    public async Task WhenToAsyncEnumerable_ThenCanBeEnumerated()
    {
        var items = new List<int>();
        await foreach (var item in ObservableAsync.Range(1, 3).ToAsyncEnumerable(
            () => System.Threading.Channels.Channel.CreateUnbounded<int>()))
        {
            items.Add(item);
        }

        await Assert.That(items).IsEquivalentTo(new[] { 1, 2, 3 });
    }
}
