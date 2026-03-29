// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Channels;

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for terminal operators: FirstAsync, LastAsync, SingleAsync, CountAsync, AnyAsync, AllAsync,
/// ContainsAsync, AggregateAsync, ToListAsync, ToDictionaryAsync, ForEachAsync, WaitCompletionAsync, ToAsyncEnumerable.
/// </summary>
public class TerminalOperatorTests
{
    /// <summary>Tests FirstAsync returns first element.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstAsync_ThenReturnsFirstElement()
    {
        var result = await ObservableAsync.Range(10, 3).FirstAsync();
        await Assert.That(result).IsEqualTo(10);
    }

    /// <summary>Tests FirstAsync with predicate returns first match.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

    /// <summary>Tests FirstAsync with predicate when no elements match throws InvalidOperationException with matching message.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstAsyncWithPredicateNoMatch_ThenThrowsInvalidOperationWithMatchingMessage()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Range(1, 5).FirstAsync(x => x > 100));

        await Assert.That(ex!.Message).IsEqualTo("Sequence contains no matching elements.");
    }

    /// <summary>Tests FirstAsync propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstAsyncSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.FirstAsync());

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>Tests FirstAsync propagates error when source completes with failure result.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstAsyncSourceCompletesWithFailure_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("source failed");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(new Result(expectedError));
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.FirstAsync());

        await Assert.That(ex!.Message).IsEqualTo("source failed");
    }

    /// <summary>Tests FirstOrDefault on empty returns default.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstOrDefaultOnEmpty_ThenReturnsDefault()
    {
        var result = await ObservableAsync.Empty<int>().FirstOrDefaultAsync();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Tests FirstOrDefault with predicate match returns first.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstOrDefaultWithMatch_ThenReturnsFirst()
    {
        var result = await ObservableAsync.Range(1, 5).Where(x => x > 3).FirstOrDefaultAsync(0);
        await Assert.That(result).IsEqualTo(4);
    }

    /// <summary>Tests FirstOrDefaultAsync with predicate returns first matching element.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstOrDefaultAsyncWithPredicate_ThenReturnsFirstMatch()
    {
        var result = await ObservableAsync.Range(1, 5).FirstOrDefaultAsync(x => x > 3, -1);
        await Assert.That(result).IsEqualTo(4);
    }

    /// <summary>Tests FirstOrDefaultAsync with predicate and no match returns specified default value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstOrDefaultAsyncWithPredicateNoMatch_ThenReturnsDefaultValue()
    {
        var result = await ObservableAsync.Range(1, 5).FirstOrDefaultAsync(x => x > 100, -1);
        await Assert.That(result).IsEqualTo(-1);
    }

    /// <summary>Tests FirstOrDefaultAsync with predicate propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstOrDefaultAsyncWithPredicateSourceEmitsErrorResume_ThenThrows()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.FirstOrDefaultAsync(x => x > 0, -1));

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>Tests FirstOrDefaultAsync propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstOrDefaultAsyncSourceEmitsErrorResume_ThenThrows()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.FirstOrDefaultAsync());

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>Tests LastAsync returns last element.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastAsync_ThenReturnsLastElement()
    {
        var result = await ObservableAsync.Range(1, 5).LastAsync();
        await Assert.That(result).IsEqualTo(5);
    }

    /// <summary>Tests LastAsync with predicate returns last match.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

    /// <summary>Tests LastAsync with predicate and no match throws with matching-elements message.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastAsyncWithPredicateNoMatch_ThenThrowsWithMatchingMessage()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Range(1, 5).LastAsync(x => x > 100));

        await Assert.That(ex!.Message).IsEqualTo("Sequence contains no matching elements.");
    }

    /// <summary>Tests LastAsync on empty throws with no-elements message.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastAsyncOnEmpty_ThenThrowsWithNoElementsMessage()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Empty<int>().LastAsync());

        await Assert.That(ex!.Message).IsEqualTo("Sequence contains no elements.");
    }

    /// <summary>Tests LastAsync propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastAsyncSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.LastAsync());

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>Tests LastAsync propagates error when source completes with failure result.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastAsyncSourceCompletesWithFailure_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("source failed");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(new Result(expectedError));
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.LastAsync());

        await Assert.That(ex!.Message).IsEqualTo("source failed");
    }

    /// <summary>Tests LastOrDefault on empty returns default.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastOrDefaultOnEmpty_ThenReturnsDefault()
    {
        var result = await ObservableAsync.Empty<int>().LastOrDefaultAsync();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Tests LastOrDefaultAsync with predicate returns the last matching element.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastOrDefaultAsyncWithPredicate_ThenReturnsLastMatch()
    {
        var result = await ObservableAsync.Range(1, 5).LastOrDefaultAsync(x => x < 4, -1);
        await Assert.That(result).IsEqualTo(3);
    }

    /// <summary>Tests LastOrDefaultAsync with predicate returns default when no elements match.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastOrDefaultAsyncWithPredicateNoMatch_ThenReturnsDefaultValue()
    {
        var result = await ObservableAsync.Range(1, 5).LastOrDefaultAsync(x => x > 100, -1);
        await Assert.That(result).IsEqualTo(-1);
    }

    /// <summary>Tests LastOrDefaultAsync with predicate on empty returns default value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastOrDefaultAsyncWithPredicateOnEmpty_ThenReturnsDefaultValue()
    {
        var result = await ObservableAsync.Empty<int>().LastOrDefaultAsync(x => true, 42);
        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>Tests LastOrDefaultAsync with custom default value on empty returns that default.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastOrDefaultAsyncWithDefaultValueOnEmpty_ThenReturnsCustomDefault()
    {
        var result = await ObservableAsync.Empty<int>().LastOrDefaultAsync(99);
        await Assert.That(result).IsEqualTo(99);
    }

    /// <summary>Tests LastOrDefaultAsync propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastOrDefaultAsyncSourceEmitsErrorResume_ThenThrows()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.LastOrDefaultAsync());

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>Tests LastOrDefaultAsync propagates error when source completes with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLastOrDefaultAsyncSourceCompletesWithFailure_ThenThrows()
    {
        var expectedError = new InvalidOperationException("source failed");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(new Result(expectedError));
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.LastOrDefaultAsync());

        await Assert.That(ex!.Message).IsEqualTo("source failed");
    }

    /// <summary>Tests SingleAsync returns single element.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleOrDefaultOnEmpty_ThenReturnsDefault()
    {
        var result = await ObservableAsync.Empty<int>().SingleOrDefaultAsync();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Tests SingleOrDefaultAsync with predicate returns matching element.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleOrDefaultAsyncWithPredicate_ThenReturnsMatchingElement()
    {
        var result = await ObservableAsync.Range(1, 5).SingleOrDefaultAsync(x => x == 3, -1);
        await Assert.That(result).IsEqualTo(3);
    }

    /// <summary>Tests SingleOrDefaultAsync with predicate and no match returns default value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleOrDefaultAsyncWithPredicateNoMatch_ThenReturnsDefaultValue()
    {
        var result = await ObservableAsync.Range(1, 5).SingleOrDefaultAsync(x => x > 100, -1);
        await Assert.That(result).IsEqualTo(-1);
    }

    /// <summary>Tests SingleOrDefaultAsync with predicate matching multiple elements throws.</summary>
    [Test]
    public void WhenSingleOrDefaultAsyncWithPredicateMultipleMatches_ThenThrowsInvalidOperation()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Range(1, 5).SingleOrDefaultAsync(x => x > 2, -1));
    }

    /// <summary>Tests SingleOrDefaultAsync with no predicate and multiple elements throws.</summary>
    [Test]
    public void WhenSingleOrDefaultAsyncMultipleElements_ThenThrowsInvalidOperation()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Range(1, 3).SingleOrDefaultAsync(0));
    }

    /// <summary>Tests SingleOrDefaultAsync with custom default value on empty returns that default.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleOrDefaultAsyncWithDefaultValueOnEmpty_ThenReturnsCustomDefault()
    {
        var result = await ObservableAsync.Empty<int>().SingleOrDefaultAsync(99);
        await Assert.That(result).IsEqualTo(99);
    }

    /// <summary>Tests SingleOrDefaultAsync propagates error from OnErrorResumeAsync.</summary>
    [Test]
    public void WhenSingleOrDefaultAsyncSourceEmitsErrorResume_ThenThrows()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("resume"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.SingleOrDefaultAsync());
    }

    /// <summary>Tests SingleOrDefaultAsync propagates error from source completing with failure.</summary>
    [Test]
    public void WhenSingleOrDefaultAsyncSourceCompletesWithError_ThenThrows()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));
            return DisposableAsync.Empty;
        });

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.SingleOrDefaultAsync());
    }

    /// <summary>Tests SingleOrDefaultAsync without predicate reports correct message when multiple elements exist.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleOrDefaultAsyncMultipleElementsNoPredicate_ThenMessageReportsMoreThanOneElement()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Range(1, 3).SingleOrDefaultAsync(0));

        await Assert.That(ex!.Message).IsEqualTo("Sequence contains more than one element.");
    }

    /// <summary>Tests SingleOrDefaultAsync with predicate reports correct message when multiple elements match.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleOrDefaultAsyncMultipleMatchesWithPredicate_ThenMessageReportsMoreThanOneMatchingElement()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Range(1, 5).SingleOrDefaultAsync(x => x > 2, -1));

        await Assert.That(ex!.Message).IsEqualTo("Sequence contains more than one matching element.");
    }

    /// <summary>Tests SingleOrDefaultAsync propagates error from OnErrorResumeAsync with the defaultValue overload.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleOrDefaultAsyncWithDefaultValueSourceEmitsErrorResume_ThenThrowsWithCorrectMessage()
    {
        var expectedError = new InvalidOperationException("resume error detail");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.SingleOrDefaultAsync(0));

        await Assert.That(ex!.Message).IsEqualTo("resume error detail");
    }

    /// <summary>Tests SingleOrDefaultAsync propagates failure result from OnCompletedAsync with the defaultValue overload.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleOrDefaultAsyncWithDefaultValueSourceCompletesWithFailure_ThenThrowsWithCorrectMessage()
    {
        var expectedError = new InvalidOperationException("completion failure detail");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(Result.Failure(expectedError));
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.SingleOrDefaultAsync(0));

        await Assert.That(ex!.Message).IsEqualTo("completion failure detail");
    }

    /// <summary>Tests CountAsync returns element count.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCountAsync_ThenReturnsElementCount()
    {
        var result = await ObservableAsync.Range(1, 5).CountAsync();
        await Assert.That(result).IsEqualTo(5);
    }

    /// <summary>Tests CountAsync on empty returns zero.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCountAsyncOnEmpty_ThenReturnsZero()
    {
        var result = await ObservableAsync.Empty<int>().CountAsync();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Tests CountAsync propagates error from OnErrorResumeAsync through the OnErrorResumeAsyncCore path.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCountAsyncSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.CountAsync());

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>Tests LongCountAsync returns element count.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLongCountAsync_ThenReturnsElementCount()
    {
        var result = await ObservableAsync.Range(1, 3).LongCountAsync();
        await Assert.That(result).IsEqualTo(3L);
    }

    /// <summary>Tests AnyAsync on non-empty returns true.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAnyAsyncOnNonEmpty_ThenReturnsTrue()
    {
        var result = await ObservableAsync.Return(1).AnyAsync();
        await Assert.That(result).IsTrue();
    }

    /// <summary>Tests AnyAsync on empty returns false.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAnyAsyncOnEmpty_ThenReturnsFalse()
    {
        var result = await ObservableAsync.Empty<int>().AnyAsync();
        await Assert.That(result).IsFalse();
    }

    /// <summary>Tests AnyAsync with predicate checks condition.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAnyAsyncWithPredicateMatch_ThenReturnsTrue()
    {
        var hasEven = await ObservableAsync.Range(1, 5).AnyAsync(x => x % 2 == 0);
        await Assert.That(hasEven).IsTrue();
    }

    /// <summary>Tests AnyAsync with predicate no match returns false.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAnyAsyncWithPredicateNoMatch_ThenReturnsFalse()
    {
        var hasNeg = await ObservableAsync.Range(1, 5).AnyAsync(x => x < 0);
        await Assert.That(hasNeg).IsFalse();
    }

    /// <summary>Tests AllAsync checks all elements.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAllAsyncAllMatch_ThenReturnsTrue()
    {
        var allPositive = await ObservableAsync.Range(1, 5).AllAsync(x => x > 0);
        await Assert.That(allPositive).IsTrue();
    }

    /// <summary>Tests AllAsync with partial match returns false.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAllAsyncPartialMatch_ThenReturnsFalse()
    {
        var allGreaterThan3 = await ObservableAsync.Range(1, 5).AllAsync(x => x > 3);
        await Assert.That(allGreaterThan3).IsFalse();
    }

    /// <summary>Tests AllAsync on empty returns true.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAllAsyncOnEmpty_ThenReturnsTrue()
    {
        var result = await ObservableAsync.Empty<int>().AllAsync(x => false);
        await Assert.That(result).IsTrue();
    }

    /// <summary>Tests AnyAsync propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAnyAsyncSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("any resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.AnyAsync());

        await Assert.That(ex!.Message).IsEqualTo("any resume error");
    }

    /// <summary>Tests AnyAsync with predicate propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAnyAsyncWithPredicateSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("any predicate resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.AnyAsync(x => x > 0));

        await Assert.That(ex!.Message).IsEqualTo("any predicate resume error");
    }

    /// <summary>Tests AllAsync propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAllAsyncSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("all resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.AllAsync(x => x > 0));

        await Assert.That(ex!.Message).IsEqualTo("all resume error");
    }

    /// <summary>Tests ContainsAsync with match returns true.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenContainsAsyncWithMatch_ThenReturnsTrue()
    {
        var result = await ObservableAsync.Range(1, 5).ContainsAsync(3);
        await Assert.That(result).IsTrue();
    }

    /// <summary>Tests ContainsAsync with no match returns false.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenContainsAsyncWithNoMatch_ThenReturnsFalse()
    {
        var result = await ObservableAsync.Range(1, 5).ContainsAsync(99);
        await Assert.That(result).IsFalse();
    }

    /// <summary>Tests ContainsAsync propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenContainsAsyncSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.ContainsAsync(42));

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>Tests sync AggregateAsync computes final value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAggregateAsyncSync_ThenComputesFinalValue()
    {
        var result = await ObservableAsync.Range(1, 4).AggregateAsync(0, (acc, x) => acc + x);
        await Assert.That(result).IsEqualTo(10);
    }

    /// <summary>Tests async AggregateAsync computes final value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAggregateAsyncWithResultSelector_ThenTransformsFinalValue()
    {
        var result = await ObservableAsync.Range(1, 4).AggregateAsync(
            0,
            (acc, x) => acc + x,
            acc => $"Sum={acc}");

        await Assert.That(result).IsEqualTo("Sum=10");
    }

    /// <summary>Tests AggregateAsync propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAggregateAsyncSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("aggregate resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.AggregateAsync(0, (acc, x) => acc + x));

        await Assert.That(ex!.Message).IsEqualTo("aggregate resume error");
    }

    /// <summary>Tests AggregateAsync null accumulator throws.</summary>
    [Test]
    public void WhenAggregateAsyncNullAccumulator_ThenThrowsArgumentNull()
    {
        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ObservableAsync.Return(1).AggregateAsync(0, (Func<int, int, int>)null!));
    }

    /// <summary>Tests ToListAsync collects all elements.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToListAsync_ThenCollectsAllElements()
    {
        var result = await ObservableAsync.Range(1, 4).ToListAsync();
        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4]);
    }

    /// <summary>Tests ToListAsync propagates error when source emits OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToListAsyncSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.ToListAsync());

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>Tests ToDictionaryAsync creates correct dictionary.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToDictionaryAsync_ThenCreatesCorrectDictionary()
    {
        var source = new[] { "a", "bb", "ccc" }.ToObservableAsync();
        var result = await source.ToDictionaryAsync(s => s.Length);

        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[1]).IsEqualTo("a");
    }

    /// <summary>Tests ToDictionaryAsync with key and element selectors creates correct dictionary.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToDictionaryAsyncWithElementSelector_ThenCreatesCorrectDictionary()
    {
        var source = new[] { "a", "bb", "ccc" }.ToObservableAsync();
        var result = await source.ToDictionaryAsync(s => s.Length, s => s.ToUpperInvariant());

        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[1]).IsEqualTo("A");
        await Assert.That(result[2]).IsEqualTo("BB");
        await Assert.That(result[3]).IsEqualTo("CCC");
    }

    /// <summary>Tests ToDictionaryAsync with key and element selectors and custom comparer uses the comparer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToDictionaryAsyncWithElementSelectorAndComparer_ThenUsesComparer()
    {
        var source = new[] { "Hello", "World" }.ToObservableAsync();
        var result = await source.ToDictionaryAsync(
            s => s,
            s => s.Length,
            StringComparer.OrdinalIgnoreCase);

        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result["hello"]).IsEqualTo(5);
        await Assert.That(result["WORLD"]).IsEqualTo(5);
    }

    /// <summary>Tests ToDictionaryAsync with element selector throws ArgumentNullException when keySelector is null.</summary>
    [Test]
    public void WhenToDictionaryAsyncWithElementSelectorNullKeySelector_ThenThrowsArgumentNull()
    {
        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ObservableAsync.Return("a").ToDictionaryAsync((Func<string, string>)null!, s => s.Length));
    }

    /// <summary>Tests ToDictionaryAsync with element selector throws ArgumentNullException when elementSelector is null.</summary>
    [Test]
    public void WhenToDictionaryAsyncWithNullElementSelector_ThenThrowsArgumentNull()
    {
        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ObservableAsync.Return("a").ToDictionaryAsync(s => s.Length, (Func<string, int>)null!));
    }

    /// <summary>Tests ToDictionaryAsync propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToDictionaryAsyncSourceEmitsErrorResume_ThenThrows()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<string>(async (observer, ct) =>
        {
            await observer.OnNextAsync("a", ct);
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.ToDictionaryAsync(s => s));

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>Tests ToDictionaryAsync propagates error when source completes with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToDictionaryAsyncSourceCompletesWithFailure_ThenThrows()
    {
        var expectedError = new InvalidOperationException("source failed");
        var source = ObservableAsync.Create<string>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(Result.Failure(expectedError));
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.ToDictionaryAsync(s => s));

        await Assert.That(ex!.Message).IsEqualTo("source failed");
    }

    /// <summary>Tests ForEachAsync processes all elements.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForEachAsync_ThenProcessesAllElements()
    {
        var items = new List<int>();
        await ObservableAsync.Range(1, 3).ForEachAsync(x => items.Add(x));
        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests WaitCompletionAsync waits for completion.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

    /// <summary>Tests WaitCompletionAsync propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitCompletionAsyncSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.WaitCompletionAsync());

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>Tests ToAsyncEnumerable can be enumerated.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToAsyncEnumerable_ThenCanBeEnumerated()
    {
        var items = new List<int>();
        await foreach (var item in ObservableAsync.Range(1, 3).ToAsyncEnumerable(
            () => System.Threading.Channels.Channel.CreateUnbounded<int>()))
        {
            items.Add(item);
        }

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests that ToAsyncEnumerable yields all elements from the source when completed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToAsyncEnumerableCompletes_ThenAllElementsYielded()
    {
        var items = new List<int>();
        await foreach (var item in ObservableAsync.Range(1, 3).ToAsyncEnumerable(
            () => System.Threading.Channels.Channel.CreateUnbounded<int>()))
        {
            items.Add(item);
        }

        await Assert.That(items).Count().IsEqualTo(3);
        await Assert.That(items[0]).IsEqualTo(1);
        await Assert.That(items[1]).IsEqualTo(2);
        await Assert.That(items[2]).IsEqualTo(3);
    }

    /// <summary>Tests SingleAsync with predicate returns the single matching element.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleAsyncWithPredicate_ThenReturnsSingleMatch()
    {
        var result = await ObservableAsync.Range(1, 5).SingleAsync(x => x == 3);
        await Assert.That(result).IsEqualTo(3);
    }

    /// <summary>Tests SingleAsync with predicate throws when multiple elements match.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleAsyncWithPredicateMultipleMatches_ThenThrowsInvalidOperation()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Range(1, 5).SingleAsync(x => x > 2));

        await Assert.That(ex!.Message).IsEqualTo("Sequence contains more than one matching element.");
    }

    /// <summary>Tests SingleAsync with predicate throws when no elements match.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleAsyncWithPredicateNoMatch_ThenThrowsInvalidOperation()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Range(1, 5).SingleAsync(x => x > 100));

        await Assert.That(ex!.Message).IsEqualTo("Sequence contains no matching elements.");
    }

    /// <summary>Tests SingleAsync propagates error from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleAsyncSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.SingleAsync());

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>Tests SingleAsync propagates error when source completes with failure result.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleAsyncSourceCompletesWithFailure_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("source failed");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(new Result(expectedError));
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.SingleAsync());

        await Assert.That(ex!.Message).IsEqualTo("source failed");
    }

    /// <summary>Tests that ForEachAsync captures an exception sent via OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForEachAsyncSourceEmitsErrorResume_ThenExceptionCaptured()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var items = new List<int>();
        var caughtException = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.ForEachAsync(x => items.Add(x)));

        await Assert.That(caughtException!.Message).IsEqualTo("resume error");
        await Assert.That(items).IsEquivalentTo([1]);
    }

    /// <summary>Tests that the async ForEachAsync overload throws ArgumentNullException when onNextAsync is null.</summary>
    [Test]
    public void WhenAsyncForEachAsyncNullCallback_ThenThrowsArgumentNull()
    {
        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ObservableAsync.Return(1).ForEachAsync((Func<int, CancellationToken, ValueTask>)null!));
    }

    /// <summary>Tests that the synchronous ForEachAsync overload throws ArgumentNullException when onNext action is null.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSyncForEachAsyncNullAction_ThenThrowsArgumentNull()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ObservableAsync.Return(1).ForEachAsync((Action<int>)null!));

        await Assert.That(ex!.ParamName).IsEqualTo("onNext");
    }

    /// <summary>Tests that the async ForEachAsync overload processes all elements.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncForEachAsync_ThenProcessesAllElements()
    {
        var items = new List<int>();
        await ObservableAsync.Range(1, 3).ForEachAsync(
            async (x, ct) =>
            {
                await Task.Yield();
                items.Add(x);
            });

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests that the async ForEachAsync overload propagates errors from OnErrorResumeAsync.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncForEachAsyncSourceEmitsErrorResume_ThenThrows()
    {
        var expectedError = new InvalidOperationException("async resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var items = new List<int>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.ForEachAsync(
                async (x, ct) =>
                {
                    await Task.Yield();
                    items.Add(x);
                }));

        await Assert.That(ex!.Message).IsEqualTo("async resume error");
        await Assert.That(items).IsEquivalentTo([1]);
    }

    /// <summary>Tests that the async ForEachAsync overload propagates errors when source completes with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncForEachAsyncSourceCompletesWithFailure_ThenThrows()
    {
        var expectedError = new InvalidOperationException("source failed");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(new Result(expectedError));
            return DisposableAsync.Empty;
        });

        var items = new List<int>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.ForEachAsync(
                async (x, ct) =>
                {
                    await Task.Yield();
                    items.Add(x);
                }));

        await Assert.That(ex!.Message).IsEqualTo("source failed");
        await Assert.That(items).IsEquivalentTo([1]);
    }

    /// <summary>Tests LongCountAsync propagates error from OnErrorResumeAsync through the observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLongCountAsyncSourceEmitsErrorResume_ThenThrowsSourceException()
    {
        var expectedError = new InvalidOperationException("resume error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.LongCountAsync(null));

        await Assert.That(ex!.Message).IsEqualTo("resume error");
    }

    /// <summary>
    /// Tests that ToAsyncEnumerable uses the default error handler when onErrorResume is null,
    /// completing the channel with the exception so that enumeration throws.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToAsyncEnumerableSourceErrorsWithoutErrorHandler_ThenEnumerationThrows()
    {
        var expectedError = new InvalidOperationException("source error");
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(expectedError, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var items = new List<int>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var item in source.ToAsyncEnumerable(
                () => Channel.CreateUnbounded<int>()))
            {
                items.Add(item);
            }
        });

        await Assert.That(ex!.Message).IsEqualTo("source error");
        await Assert.That(items).IsEquivalentTo([1]);
    }

    /// <summary>
    /// Tests that ToAsyncEnumerable yields each element from a multi-item source
    /// and completes enumeration when the source completes successfully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToAsyncEnumerableMultipleItems_ThenYieldsAllAndCompletes()
    {
        var source = new[] { 10, 20, 30, 40, 50 }.ToObservableAsync();

        var items = new List<int>();
        await foreach (var item in source.ToAsyncEnumerable(
            () => Channel.CreateUnbounded<int>()))
        {
            items.Add(item);
        }

        await Assert.That(items).IsEquivalentTo([10, 20, 30, 40, 50]);
    }

    /// <summary>Tests AggregateAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAggregateAsyncSourceFails_ThenThrows()
    {
        var error = new InvalidOperationException("test");
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(error).AggregateAsync(0, (acc, x) => acc + x));
    }

    /// <summary>Tests AnyAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAnyAsyncSourceFails_ThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(new InvalidOperationException("fail")).AnyAsync());
    }

    /// <summary>Tests AllAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAllAsyncSourceFails_ThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(new InvalidOperationException("fail")).AllAsync(_ => true));
    }

    /// <summary>Tests ContainsAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenContainsAsyncSourceFails_ThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(new InvalidOperationException("fail")).ContainsAsync(1));
    }

    /// <summary>Tests CountAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCountAsyncSourceFails_ThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(new InvalidOperationException("fail")).CountAsync());
    }

    /// <summary>Tests LongCountAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLongCountAsyncSourceFails_ThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(new InvalidOperationException("fail")).LongCountAsync());
    }

    /// <summary>Tests SingleAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSingleAsyncSourceFails_ThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(new InvalidOperationException("fail")).SingleAsync());
    }

    /// <summary>Tests FirstAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstAsyncSourceFails_ThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(new InvalidOperationException("fail")).FirstAsync());
    }

    /// <summary>Tests FirstOrDefaultAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstOrDefaultAsyncSourceFails_ThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(new InvalidOperationException("fail")).FirstOrDefaultAsync());
    }

    /// <summary>Tests WaitCompletionAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitCompletionAsyncSourceFails_ThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(new InvalidOperationException("fail")).WaitCompletionAsync());
    }

    /// <summary>Tests ContainsAsync when value is not found returns false.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenContainsAsyncValueNotFound_ThenReturnsFalse()
    {
        var result = await ObservableAsync.Range(1, 3).ContainsAsync(99);
        await Assert.That(result).IsFalse();
    }

    /// <summary>Tests CountAsync with predicate that filters some elements.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCountAsyncWithPredicate_ThenCountsMatchesOnly()
    {
        var result = await ObservableAsync.Range(1, 5).CountAsync(x => x > 3);
        await Assert.That(result).IsEqualTo(2);
    }

    /// <summary>Tests LongCountAsync with predicate that filters some elements.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenLongCountAsyncWithPredicate_ThenCountsMatchesOnly()
    {
        var result = await ObservableAsync.Range(1, 5).LongCountAsync(x => x > 3);
        await Assert.That(result).IsEqualTo(2L);
    }

    /// <summary>Tests FirstOrDefaultAsync on empty returns default.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenFirstOrDefaultAsyncOnEmpty_ThenReturnsDefault()
    {
        var result = await ObservableAsync.Empty<int>().FirstOrDefaultAsync();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Tests WaitCompletionAsync on successful sequence completes without error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWaitCompletionAsyncOnSuccess_ThenCompletes()
    {
        await ObservableAsync.Return(42).WaitCompletionAsync();
    }

    /// <summary>Tests ForEachAsync with null sync action throws ArgumentNullException.</summary>
    [Test]
    public void WhenForEachAsyncWithNullSyncAction_ThenThrowsArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ObservableAsync.Return(1).ForEachAsync((Action<int>)null!));
    }

    /// <summary>Tests ForEachAsync with null async action throws ArgumentNullException.</summary>
    [Test]
    public void WhenForEachAsyncWithNullAsyncAction_ThenThrowsArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ObservableAsync.Return(1).ForEachAsync((Func<int, CancellationToken, ValueTask>)null!));
    }

    /// <summary>Tests async ForEachAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForEachAsyncSourceFails_ThenThrows()
    {
        var error = new InvalidOperationException("test");
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(error).ForEachAsync((_, _) => default));
    }

    /// <summary>Tests sync ForEachAsync propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForEachAsyncSyncOverloadSourceFails_ThenThrows()
    {
        var error = new InvalidOperationException("test");
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(error).ForEachAsync(_ => { }));
    }

    /// <summary>Tests ToAsyncEnumerable propagates source failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenToAsyncEnumerableSourceFails_ThenThrows()
    {
        var error = new InvalidOperationException("enum-error");
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var item in ObservableAsync.Throw<int>(error).ToAsyncEnumerable(
                () => Channel.CreateUnbounded<int>()))
            {
                _ = item;
            }
        });
    }

    /// <summary>Tests Wrap with a null observer throws ArgumentNullException.</summary>
    [Test]
    public void WhenWrapWithNullObserver_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ObservableAsync.Wrap<int>(null!));
    }
}
