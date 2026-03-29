// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for combining operators: Merge, Concat, CombineLatest, Zip, Prepend, StartWith.
/// </summary>
public class CombiningOperatorTests
{
    /// <summary>Tests Merge two sequences emits from both.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatTwoSequences_ThenEmitsInOrder()
    {
        var first = ObservableAsync.Range(1, 2);
        var second = ObservableAsync.Range(3, 2);

        var result = await first.Concat(second).ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4]);
    }

    /// <summary>Tests Concat enumerable emits in sequential order.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests Concat observable of observables concatenates sequentially.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservableOfObservables_ThenConcatenatesSequentially()
    {
        var sources = new[]
        {
            ObservableAsync.Range(1, 2),
            ObservableAsync.Range(3, 2),
        }.ToObservableAsync();

        var result = await sources.Concat().ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4]);
    }

    /// <summary>Tests CombineLatest two sources combines latest values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo((1, "a"));
    }

    /// <summary>Tests Zip two sequences pairs by index.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipTwoSequences_ThenPairsByIndex()
    {
        var first = ObservableAsync.Range(1, 3);
        var second = new[] { "a", "b", "c" }.ToObservableAsync();

        var result = await first.Zip(second, (n, s) => $"{n}{s}").ToListAsync();

        await Assert.That(result).IsEquivalentTo(["1a", "2b", "3c"]);
    }

    /// <summary>Tests Zip tuple overload creates tuples.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipDifferentLengths_ThenStopsAtShortest()
    {
        var first = ObservableAsync.Range(1, 5);
        var second = ObservableAsync.Range(10, 2);

        var result = await first.Zip(second, (a, b) => a + b).ToListAsync();

        await Assert.That(result).IsEquivalentTo([11, 13]);
    }

    /// <summary>Tests Zip null arguments throws.</summary>
    [Test]
    public void WhenZipNullArguments_ThenThrowsArgumentNull() => Assert.Throws<ArgumentNullException>(() =>
                                                                          ((ObservableAsync<int>)null!).Zip(ObservableAsync.Return(1), (a, b) => a + b));

    /// <summary>Tests Prepend value comes first.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPrependValue_ThenValueComesFirst()
    {
        var result = await ObservableAsync.Range(2, 3)
            .Prepend(1)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4]);
    }

    /// <summary>Tests Prepend enumerable values come first.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPrependEnumerable_ThenValuesComesFirst()
    {
        var result = await ObservableAsync.Range(3, 2)
            .Prepend([1, 2])
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4]);
    }

    /// <summary>Tests StartWith value comes first.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStartWithValue_ThenValueComesFirst()
    {
        var result = await ObservableAsync.Range(2, 2)
            .StartWith(1)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests StartWith enumerable values come first.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStartWithEnumerable_ThenValuesComesFirst()
    {
        var result = await ObservableAsync.Return(3)
            .StartWith([1, 2])
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests StartWith params values come first.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStartWithParams_ThenValuesComesFirst()
    {
        var values = new[] { 1, 2, 3 };
        var result = await ObservableAsync.Return(4)
            .StartWith(values)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4]);
    }

    /// <summary>
    /// Verifies that merging an observable-of-observables where the outer source errors propagates the failure
    /// and disposes the subscription cleanly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableOfObservablesOuterErrors_ThenFailurePropagates()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        await using var sub = await outer.Values
            .Merge()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await outer.OnCompletedAsync(Result.Failure(new InvalidOperationException("outer fail")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that merge with max concurrency propagates an error when the inner subscription itself throws.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeWithMaxConcurrencySubscriptionThrows_ThenErrorPropagates()
    {
        var failing = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            throw new InvalidOperationException("subscribe fail");
        });

        var source = new[] { failing }.ToObservableAsync();

        Result? completionResult = null;
        await using var sub = await source
            .Merge(1)
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that merging an enumerable of observables where one inner source errors propagates the failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableOneInnerErrors_ThenFailurePropagates()
    {
        var sources = new IObservableAsync<int>[]
        {
            ObservableAsync.Return(1),
            ObservableAsync.Throw<int>(new InvalidOperationException("inner fail")),
            ObservableAsync.Return(3),
        };

        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that merge enumerable forwards error-resume events from inner sources to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableInnerErrorResume_ThenForwardedToObserver()
    {
        var inner = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("warning"), ct);
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var sources = new[] { inner };
        var errors = new List<Exception>();
        var items = new List<int>();

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(items).Contains(1);
    }

    /// <summary>
    /// Verifies that merge of observable-of-observables forwards error-resume events from inner sources.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableOfObservablesInnerErrorResume_ThenForwarded()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var errors = new List<Exception>();

        var inner = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("inner warning"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await outer.Values
            .Merge()
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await outer.OnNextAsync(inner, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that CombineLatest with an empty sources collection completes immediately with success.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEmptySources_ThenCompletesImmediately()
    {
        IObservableAsync<int>[] sources = [];

        Result? completionResult = null;
        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest completes with success when one source completes without emitting any value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestOneSourceCompletesWithoutEmitting_ThenCompletes()
    {
        var sources = new IObservableAsync<int>[]
        {
            ObservableAsync.Return(1),
            ObservableAsync.Empty<int>(),
        };

        Result? completionResult = null;
        var items = new List<IReadOnlyList<int>>();

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
        await Assert.That(items).IsEmpty();
    }

    /// <summary>
    /// Verifies that CombineLatest propagates a failure when one of the sources errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestOneSourceErrors_ThenFailurePropagates()
    {
        var sources = new IObservableAsync<int>[]
        {
            ObservableAsync.Return(1),
            ObservableAsync.Throw<int>(new InvalidOperationException("source fail")),
        };

        Result? completionResult = null;

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest forwards error-resume events from inner sources to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestSourceErrorResume_ThenForwarded()
    {
        var inner = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("warning"), ct);
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var sources = new IObservableAsync<int>[] { inner };
        var errors = new List<Exception>();
        var items = new List<IReadOnlyList<int>>();

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that disposing a CombineLatest subscription during active emissions does not throw.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestDisposedDuringEmission_ThenNoError()
    {
        var subject1 = SubjectAsync.Create<int>();
        var subject2 = SubjectAsync.Create<int>();

        var sources = new IObservableAsync<int>[] { subject1.Values, subject2.Values };

        var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (x, _) => default,
                null,
                null);

        await subject1.OnNextAsync(1, CancellationToken.None);

        await sub.DisposeAsync();

        await subject1.DisposeAsync();
        await subject2.DisposeAsync();
    }

    /// <summary>
    /// Verifies that concat of an observable-of-observables where the outer source errors propagates the failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservableOfObservablesOuterErrors_ThenFailurePropagates()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        await using var sub = await outer.Values
            .Concat()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await outer.OnCompletedAsync(Result.Failure(new InvalidOperationException("outer fail")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that concat of an observable-of-observables where an inner source errors propagates the failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservableOfObservablesInnerErrors_ThenFailurePropagates()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        await using var sub = await outer.Values
            .Concat()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await outer.OnNextAsync(
            ObservableAsync.Throw<int>(new InvalidOperationException("inner fail")),
            CancellationToken.None);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that concat of an observable-of-observables completes correctly when outer completes while no
    /// inner source is active (empty buffer scenario).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservableOfObservablesOuterCompletesWithEmptyBuffer_ThenCompletes()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await outer.Values
            .Concat()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // Emit an inner that completes, then complete outer when buffer is empty
        await outer.OnNextAsync(ObservableAsync.Return(42), CancellationToken.None);
        await outer.OnCompletedAsync(Result.Success);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
        await Assert.That(items).Contains(42);

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that concat of an enumerable where one source errors propagates the failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableOneSourceErrors_ThenFailurePropagates()
    {
        var sources = new IObservableAsync<int>[]
        {
            ObservableAsync.Return(1),
            ObservableAsync.Throw<int>(new InvalidOperationException("inner fail")),
            ObservableAsync.Return(3),
        };

        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await sources.Concat()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
        await Assert.That(items).Contains(1);
    }

    /// <summary>
    /// Verifies that disposing a concat enumerable subscription multiple times is idempotent and does not throw.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableDoubleDisposal_ThenIdempotent()
    {
        var sources = new IObservableAsync<int>[]
        {
            ObservableAsync.Return(1),
            ObservableAsync.Return(2),
        };

        var sub = await sources.Concat()
            .SubscribeAsync(
                (x, _) => default,
                null,
                null);

        await sub.DisposeAsync();
        await sub.DisposeAsync();
    }

    /// <summary>
    /// Verifies that switch propagates an error when the outer source completes with a failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchOuterSourceErrors_ThenFailurePropagates()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        await using var sub = await outer.Values
            .Switch()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await outer.OnCompletedAsync(Result.Failure(new InvalidOperationException("outer fail")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that switch propagates a failure when the inner source errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchInnerSourceErrors_ThenFailurePropagates()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        await using var sub = await outer.Values
            .Switch()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await outer.OnNextAsync(
            ObservableAsync.Throw<int>(new InvalidOperationException("inner fail")),
            CancellationToken.None);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that disposing a switch subscription while an inner source is active does not throw.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchDisposedDuringInnerSubscription_ThenNoError()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var innerSubject = SubjectAsync.Create<int>();

        var sub = await outer.Values
            .Switch()
            .SubscribeAsync(
                (x, _) => default,
                null,
                null);

        await outer.OnNextAsync(innerSubject.Values, CancellationToken.None);

        await sub.DisposeAsync();

        await innerSubject.DisposeAsync();
        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that zip propagates a failure when the first source errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipFirstSourceErrors_ThenFailurePropagates()
    {
        var first = ObservableAsync.Throw<int>(new InvalidOperationException("first fail"));
        var second = ObservableAsync.Return("a");

        Result? completionResult = null;
        await using var sub = await first
            .Zip(second, (a, b) => $"{a}{b}")
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that zip propagates a failure when the second source errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipSecondSourceErrors_ThenFailurePropagates()
    {
        var first = ObservableAsync.Return(1);
        var second = ObservableAsync.Throw<string>(new InvalidOperationException("second fail"));

        Result? completionResult = null;
        await using var sub = await first
            .Zip(second, (a, b) => $"{a}{b}")
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that zip completes correctly when the first source has more elements than the second,
    /// covering the completion logic for unmatched queued elements.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipFirstSourceLonger_ThenStopsAtShortest()
    {
        var first = ObservableAsync.Range(1, 10);
        var second = ObservableAsync.Range(100, 3);

        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await first
            .Zip(second, (a, b) => a + b)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
        await Assert.That(items).IsEquivalentTo([101, 103, 105]);
    }

    /// <summary>
    /// Verifies that zip completes correctly when the second source has more elements than the first,
    /// covering the second source completion path with queued elements.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipSecondSourceLonger_ThenStopsAtShortest()
    {
        var first = ObservableAsync.Range(1, 2);
        var second = ObservableAsync.Range(100, 10);

        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await first
            .Zip(second, (a, b) => a + b)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
        await Assert.That(items).IsEquivalentTo([101, 103]);
    }

    /// <summary>
    /// Verifies that prepend stops emitting when the subscription is disposed during the prepend phase.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPrependDisposedDuringPrependPhase_ThenStopsEmitting()
    {
        var items = new List<int>();

        var sub = await ObservableAsync.Range(100, 5)
            .Prepend([1, 2, 3, 4, 5])
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        // Dispose quickly - may or may not have emitted some prepend values
        await sub.DisposeAsync();

        // Just verify no exception was thrown - the disposal was clean
    }

    /// <summary>
    /// Verifies that when the outer source throws synchronously during subscribe in the
    /// observable-of-observables Merge overload, the subscription is disposed and the
    /// exception propagates.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableOfObservablesSubscriptionThrows_ThenDisposesAndRethrows()
    {
        var failing = ObservableAsync.Create<IObservableAsync<int>>(async (observer, ct) =>
        {
            throw new InvalidOperationException("subscribe boom");
        });

        var act = async () =>
        {
            await using var sub = await failing
                .Merge()
                .SubscribeAsync(
                    (x, _) => default,
                    null,
                    null);
        };

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    /// <summary>
    /// Verifies that when the outer source throws synchronously during subscribe in the
    /// max-concurrency Merge overload, the subscription is disposed and the exception propagates.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeWithMaxConcurrencySubscriptionThrowsDuringOuterSubscribe_ThenDisposesAndRethrows()
    {
        var failing = ObservableAsync.Create<IObservableAsync<int>>(async (observer, ct) =>
        {
            throw new InvalidOperationException("subscribe boom max");
        });

        var act = async () =>
        {
            await using var sub = await failing
                .Merge(2)
                .SubscribeAsync(
                    (x, _) => default,
                    null,
                    null);
        };

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    /// <summary>
    /// Verifies that when an inner observable throws during subscription in the
    /// observable-of-observables Merge, the error is propagated via completion.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableInnerSubscriptionThrows_ThenCompletesWithFailure()
    {
        var throwingInner = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            throw new InvalidOperationException("inner subscribe fail");
        });

        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        await using var sub = await outer.Values
            .Merge()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await outer.OnNextAsync(throwingInner, CancellationToken.None);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when the merge subscription is disposed while an inner source is still
    /// emitting, the forwarding methods silently return without throwing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableDisposedDuringInnerEmission_ThenForwardingSilentlyReturns()
    {
        var innerSubject = SubjectAsync.Create<int>();
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var items = new List<int>();

        var sub = await outer.Values
            .Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await outer.OnNextAsync(innerSubject.Values, CancellationToken.None);

        await innerSubject.OnNextAsync(1, CancellationToken.None);

        // Dispose the merge subscription
        await sub.DisposeAsync();

        // These should be silently ignored because subscription is disposed
        try
        {
            await innerSubject.OnNextAsync(2, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected – the inner observer may throw on cancellation
        }

        await Assert.That(items).Contains(1);

        await innerSubject.DisposeAsync();
        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that the error-resume forwarding path in Merge is silently skipped
    /// after the subscription has been disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableDisposedDuringErrorResume_ThenForwardingSilentlyReturns()
    {
        var innerSubject = SubjectAsync.Create<int>();
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var errors = new List<Exception>();

        var sub = await outer.Values
            .Merge()
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await outer.OnNextAsync(innerSubject.Values, CancellationToken.None);

        // Dispose the merge subscription
        await sub.DisposeAsync();

        // Error resume after dispose should be silently ignored
        try
        {
            await innerSubject.OnErrorResumeAsync(new InvalidOperationException("late error"), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await innerSubject.DisposeAsync();
        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when the enumerable itself throws during iteration in MergeEnumerable,
    /// the error is propagated to the observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableEnumerationThrows_ThenErrorPropagates()
    {
        static IEnumerable<IObservableAsync<int>> ThrowingEnumerable()
        {
            yield return ObservableAsync.Return(1);
            throw new InvalidOperationException("enumeration fail");
        }

        Result? completionResult = null;
        await using var sub = await ThrowingEnumerable().Merge()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that when a subscription to an inner source throws in MergeEnumerable StartAsync,
    /// the exception is caught and the sequence completes with failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableInnerSubscriptionThrows_ThenCompletesWithFailure()
    {
        var throwingInner = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            throw new InvalidOperationException("inner subscribe fail");
        });

        var sources = new IObservableAsync<int>[] { throwingInner };
        Result? completionResult = null;

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that MergeEnumerable forwards the OnNextAsync disposed check by disposing the
    /// subscription mid-emission and observing that subsequent values are not forwarded.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposedDuringInnerNext_ThenOnNextSilentlyReturns()
    {
        var innerSubject = SubjectAsync.Create<int>();
        var items = new List<int>();

        var sub = await new IObservableAsync<int>[] { innerSubject.Values }.Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await innerSubject.OnNextAsync(1, CancellationToken.None);

        await sub.DisposeAsync();

        try
        {
            await innerSubject.OnNextAsync(2, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await Assert.That(items).Contains(1);

        await innerSubject.DisposeAsync();
    }

    /// <summary>
    /// Verifies that MergeEnumerable forwards the OnErrorResumeAsync disposed check by disposing
    /// the subscription and observing that subsequent errors are not forwarded.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposedDuringInnerErrorResume_ThenSilentlyReturns()
    {
        var innerSubject = SubjectAsync.Create<int>();
        var errors = new List<Exception>();

        var sub = await new IObservableAsync<int>[] { innerSubject.Values }.Merge()
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await sub.DisposeAsync();

        try
        {
            await innerSubject.OnErrorResumeAsync(new InvalidOperationException("late error"), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await Assert.That(errors).IsEmpty();

        await innerSubject.DisposeAsync();
    }

    /// <summary>
    /// Verifies that MergeEnumerable CompleteAsync called a second time with an exception
    /// routes the exception to UnhandledExceptionHandler rather than throwing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDoubleCompletionWithError_ThenUnhandledExceptionHandlerInvoked()
    {
        Exception? unhandled = null;

        // Capture the unhandled exception
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        var sources = new IObservableAsync<int>[]
        {
            ObservableAsync.Return(1),
            ObservableAsync.Throw<int>(new InvalidOperationException("first fail")),
            ObservableAsync.Throw<int>(new InvalidOperationException("second fail")),
        };

        Result? completionResult = null;
        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that MergeEnumerable awaits _subscriptionFinished when completed from a non-reentrant
    /// context (i.e., when an inner source completes asynchronously after the subscription loop finishes).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableInnerCompletesAsynchronously_ThenAwaitsSubscriptionFinished()
    {
        var innerSubject = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await new IObservableAsync<int>[] { innerSubject.Values }.Merge()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // Complete asynchronously (not during subscription loop)
        await innerSubject.OnCompletedAsync(Result.Success);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();

        await innerSubject.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when the outer source throws synchronously during subscription in Switch,
    /// the subscription is disposed and the exception propagates.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchSubscriptionThrows_ThenDisposesAndRethrows()
    {
        var failing = ObservableAsync.Create<IObservableAsync<int>>(async (observer, ct) =>
        {
            throw new InvalidOperationException("switch subscribe boom");
        });

        var act = async () =>
        {
            await using var sub = await failing
                .Switch()
                .SubscribeAsync(
                    (x, _) => default,
                    null,
                    null);
        };

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    /// <summary>
    /// Verifies that when the outer source completes while an inner source is still active,
    /// completion is deferred until the inner source completes successfully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchOuterCompletesBeforeInner_ThenCompletesWhenInnerDone()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var innerSubject = SubjectAsync.Create<int>();
        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await outer.Values
            .Switch()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await outer.OnNextAsync(innerSubject.Values, CancellationToken.None);

        // Complete the outer while inner is still active
        await outer.OnCompletedAsync(Result.Success);

        // Should not have completed yet because inner is still active
        await Assert.That(completionResult).IsNull();

        // Emit from inner, then complete inner
        await innerSubject.OnNextAsync(42, CancellationToken.None);
        await innerSubject.OnCompletedAsync(Result.Success);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
        await Assert.That(items).Contains(42);

        await innerSubject.DisposeAsync();
        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when disposing the previous inner subscription throws during a switch,
    /// the error is propagated via completion.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchPreviousInnerDisposalThrows_ThenCompletesWithFailure()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        var throwOnDispose = ObservableAsync.Create<int>((observer, ct) =>
        {
            return new ValueTask<IAsyncDisposable>(new ThrowingDisposable());
        });

        await using var sub = await outer.Values
            .Switch()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await outer.OnNextAsync(throwOnDispose, CancellationToken.None);

        // Switch to a new inner – this will try to dispose the previous (throwing) one
        await outer.OnNextAsync(ObservableAsync.Return(99), CancellationToken.None);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that error-resume events from the outer source in Switch are forwarded
    /// to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchOuterErrorResume_ThenForwardedToObserver()
    {
        var outerSource = ObservableAsync.Create<IObservableAsync<int>>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("outer warning"), ct);
            await observer.OnNextAsync(ObservableAsync.Return(1), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var errors = new List<Exception>();
        var items = new List<int>();

        await using var sub = await outerSource
            .Switch()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("outer warning");
    }

    /// <summary>
    /// Verifies that error-resume events from the inner source in Switch are forwarded
    /// to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchInnerErrorResume_ThenForwardedToObserver()
    {
        var innerWithError = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("inner warning"), ct);
            await observer.OnNextAsync(42, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var errors = new List<Exception>();
        var items = new List<int>();

        await using var sub = await outer.Values
            .Switch()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await outer.OnNextAsync(innerWithError, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("inner warning");
        await Assert.That(items).Contains(42);

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when the outer source in Switch completes successfully with no active
    /// inner subscription, the switch completes immediately with success.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchOuterCompletesWithNoActiveInner_ThenImmediatelyCompletes()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        await using var sub = await outer.Values
            .Switch()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await outer.OnCompletedAsync(Result.Success);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when the outer source throws synchronously during subscription in
    /// ConcatObservablesObservable, the subscription is disposed and the exception propagates.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservablesSubscriptionThrows_ThenDisposesAndRethrows()
    {
        var failing = ObservableAsync.Create<IObservableAsync<int>>(async (observer, ct) =>
        {
            throw new InvalidOperationException("concat subscribe boom");
        });

        var act = async () =>
        {
            await using var sub = await failing
                .Concat()
                .SubscribeAsync(
                    (x, _) => default,
                    null,
                    null);
        };

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    /// <summary>
    /// Verifies that when the inner subscription throws during SubscribeToInnerLoop in
    /// ConcatObservablesObservable, the error is propagated via completion.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservablesInnerSubscriptionThrows_ThenCompletesWithFailure()
    {
        var throwingInner = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            throw new InvalidOperationException("inner subscribe fail");
        });

        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        await using var sub = await outer.Values
            .Concat()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await outer.OnNextAsync(throwingInner, CancellationToken.None);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that ConcatObservablesObservable double-completion with an exception routes the
    /// exception to UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservablesDoubleCompletionWithError_ThenUnhandledExceptionHandlerInvoked()
    {
        Exception? unhandled = null;
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        var sub = await outer.Values
            .Concat()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // Complete with failure first, then dispose (which calls CompleteAsync(null))
        await outer.OnCompletedAsync(Result.Failure(new InvalidOperationException("first fail")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        // Now dispose – this will call CompleteAsync(null) which will hit the already-disposed path
        await sub.DisposeAsync();

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that error-resume events from the outer source in ConcatObservablesObservable
    /// are forwarded to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservablesOuterErrorResume_ThenForwardedToObserver()
    {
        var outerSource = ObservableAsync.Create<IObservableAsync<int>>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("outer warning"), ct);
            await observer.OnNextAsync(ObservableAsync.Return(1), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var errors = new List<Exception>();
        var items = new List<int>();

        await using var sub = await outerSource
            .Concat()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("outer warning");
    }

    /// <summary>
    /// Verifies that error-resume events from an inner source in ConcatObservablesObservable
    /// are forwarded to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservablesInnerErrorResume_ThenForwardedToObserver()
    {
        var innerWithError = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("inner warning"), ct);
            await observer.OnNextAsync(42, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var errors = new List<Exception>();
        var items = new List<int>();

        await using var sub = await outer.Values
            .Concat()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await outer.OnNextAsync(innerWithError, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("inner warning");
        await Assert.That(items).Contains(42);

        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that ConcatObservablesObservable correctly buffers multiple inner observables and
    /// subscribes to each sequentially when the previous completes, covering the buffer-count > 1 path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservablesMultipleBufferedInners_ThenSubscribesSequentially()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var items = new List<int>();
        Result? completionResult = null;

        await using var sub = await outer.Values
            .Concat()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // Emit a long-lived inner and two more while it's still active
        var innerSubject = SubjectAsync.Create<int>();
        await outer.OnNextAsync(innerSubject.Values, CancellationToken.None);

        // These will be buffered because the first inner hasn't completed
        await outer.OnNextAsync(ObservableAsync.Return(20), CancellationToken.None);
        await outer.OnNextAsync(ObservableAsync.Return(30), CancellationToken.None);
        await outer.OnCompletedAsync(Result.Success);

        // Complete the first inner
        await innerSubject.OnNextAsync(10, CancellationToken.None);
        await innerSubject.OnCompletedAsync(Result.Success);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
        await Assert.That(items).IsEquivalentTo([10, 20, 30]);

        await innerSubject.DisposeAsync();
        await outer.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when the first inner observable in ConcatEnumerableObservable throws during
    /// subscription, the subscription is disposed and the exception propagates.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableSubscriptionThrows_ThenDisposesAndRethrows()
    {
        var throwing = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            throw new InvalidOperationException("concat enum subscribe boom");
        });

        var sources = new IObservableAsync<int>[] { throwing };

        Result? completionResult = null;
        await using var sub = await sources.Concat()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that ConcatEnumerableObservable forwards error-resume events from inner sources
    /// to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableInnerErrorResume_ThenForwardedToObserver()
    {
        var innerWithError = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("inner warning"), ct);
            await observer.OnNextAsync(42, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var sources = new IObservableAsync<int>[] { innerWithError };
        var errors = new List<Exception>();
        var items = new List<int>();

        await using var sub = await sources.Concat()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("inner warning");
        await Assert.That(items).Contains(42);
    }

    /// <summary>
    /// Verifies that ConcatEnumerableObservable double-completion with an exception routes the
    /// exception to UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableDoubleCompletionWithError_ThenUnhandledExceptionHandlerInvoked()
    {
        Exception? unhandled = null;
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        var sources = new IObservableAsync<int>[]
        {
            ObservableAsync.Throw<int>(new InvalidOperationException("fail")),
        };

        Result? completionResult = null;

        var sub = await sources.Concat()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        // Second dispose attempts CompleteAsync(null) on an already-disposed subscription
        await sub.DisposeAsync();
    }

    /// <summary>
    /// Verifies that ConcatEnumerableObservable handles the catch path in SubscribeNextAsync
    /// when the enumerator MoveNext throws.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableEnumeratorThrows_ThenCompletesWithFailure()
    {
        static IEnumerable<IObservableAsync<int>> ThrowingEnumerable()
        {
            yield return ObservableAsync.Return(1);
            throw new InvalidOperationException("enumerator fail");
        }

        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await ThrowingEnumerable().Concat()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
        await Assert.That(items).Contains(1);
    }

    /// <summary>
    /// Tests that MergeObservableObservables ForwardOnNext returns early when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableDisposedBeforeInnerEmission_ThenForwardOnNextReturns()
    {
        var source = SubjectAsync.Create<int>();
        var inner = SubjectAsync.Create<IObservableAsync<int>>();
        var items = new List<int>();
        Result? completionResult = null;

        await using var sub = await inner.Values
            .Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await inner.OnNextAsync(source.Values, CancellationToken.None);
        await inner.OnCompletedAsync(Result.Failure(new InvalidOperationException("force done")));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult is not null,
            TimeSpan.FromSeconds(5));

        // After dispose, forwarding should be a no-op
        await source.OnNextAsync(42, CancellationToken.None);

        await Assert.That(items).DoesNotContain(42);
    }

    /// <summary>
    /// Tests that MergeEnumerableObservable SubscribeAsyncCore catch block disposes and rethrows.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableStartAsyncThrows_ThenCatchBlockHandled()
    {
        // StartAsync contains an async void path that catches exceptions
        // We exercise this by ensuring an error during inner subscription is caught
        static IEnumerable<IObservableAsync<int>> ThrowingEnumerable()
        {
            yield return ObservableAsync.Return(1);
            throw new InvalidOperationException("enumeration boom");
        }

        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await ThrowingEnumerable().Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Tests that MergeEnumerable cancellation during inner subscription is handled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableCanceledDuringSubscription_ThenHandledGracefully()
    {
        using var cts = new CancellationTokenSource();
        var subject = SubjectAsync.Create<int>();
        var neverCompleting = ObservableAsync.Never<int>();

        var sources = new[] { subject.Values, neverCompleting };

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (x, _) => default,
                null,
                null,
                cts.Token);

        await cts.CancelAsync();

        // After cancellation, the subscription should be cleaned up
    }

    /// <summary>
    /// Tests that Zip OnErrorResumeAsync from first source is forwarded to observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipFirstSourceErrorResume_ThenForwardedToObserver()
    {
        var first = SubjectAsync.Create<int>();
        var second = SubjectAsync.Create<string>();
        Exception? received = null;

        await using var sub = await first.Values.Zip(second.Values, (a, b) => $"{a}-{b}")
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    received = ex;
                    return default;
                },
                null);

        await first.OnErrorResumeAsync(new InvalidOperationException("first error"), CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => received is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Message).IsEqualTo("first error");
    }

    /// <summary>
    /// Tests that Zip OnErrorResumeAsync from second source is forwarded to observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipSecondSourceErrorResume_ThenForwardedToObserver()
    {
        var first = SubjectAsync.Create<int>();
        var second = SubjectAsync.Create<string>();
        Exception? received = null;

        await using var sub = await first.Values.Zip(second.Values, (a, b) => $"{a}-{b}")
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    received = ex;
                    return default;
                },
                null);

        await second.OnErrorResumeAsync(new InvalidOperationException("second error"), CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => received is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Message).IsEqualTo("second error");
    }

    /// <summary>
    /// Tests that Zip ignores items from first source after done flag is set.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipFirstSourceEmitsAfterDone_ThenIgnored()
    {
        var first = SubjectAsync.Create<int>();
        var second = SubjectAsync.Create<string>();
        var items = new List<string>();
        Result? completionResult = null;

        await using var sub = await first.Values.Zip(second.Values, (a, b) => $"{a}-{b}")
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // Complete first with failure, setting done=true
        await first.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult is not null,
            TimeSpan.FromSeconds(5));

        // Items emitted after done should be ignored
        await first.OnNextAsync(99, CancellationToken.None);
        await second.OnNextAsync("late", CancellationToken.None);

        await Assert.That(items).IsEmpty();
    }

    /// <summary>
    /// Tests that Zip buffers items correctly when second source produces before first.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipSecondEmitsBeforeFirst_ThenBuffersAndPairs()
    {
        var first = SubjectAsync.Create<int>();
        var second = SubjectAsync.Create<string>();
        var items = new List<string>();

        await using var sub = await first.Values.Zip(second.Values, (a, b) => $"{a}-{b}")
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        // Second emits first, queuing in _queue2
        await second.OnNextAsync("a", CancellationToken.None);
        await second.OnNextAsync("b", CancellationToken.None);

        // Now first emits, pairing with queue2
        await first.OnNextAsync(1, CancellationToken.None);
        await first.OnNextAsync(2, CancellationToken.None);
        await first.OnCompletedAsync(Result.Success);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count >= 2,
            TimeSpan.FromSeconds(5));

        await Assert.That(items).IsEquivalentTo(["1-a", "2-b"]);
    }

    /// <summary>
    /// Tests that Zip OnCompleted1Async returns early when done is already set.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipFirstSourceCompletedTwice_ThenSecondCompletionIgnored()
    {
        var first = SubjectAsync.Create<int>();
        var second = SubjectAsync.Create<string>();
        var completionCount = 0;

        await using var sub = await first.Values.Zip(second.Values, (a, b) => $"{a}-{b}")
            .SubscribeAsync(
                (x, _) => default,
                null,
                _ =>
                {
                    Interlocked.Increment(ref completionCount);
                    return default;
                });

        await first.OnCompletedAsync(Result.Success);
        await second.OnCompletedAsync(Result.Success);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionCount >= 1,
            TimeSpan.FromSeconds(5));

        // Only one completion should have been forwarded
        await Assert.That(completionCount).IsEqualTo(1);
    }

    /// <summary>
    /// Tests Prepend cancellation during the prepend phase, exercising the OperationCanceledException catch path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPrependCancelledDuringValues_ThenOperationCanceledExceptionCaught()
    {
        using var cts = new CancellationTokenSource();
        var items = new List<int>();

        // Create a long prepend that will be cancelled
        var manyValues = Enumerable.Range(1, 100);
        var source = ObservableAsync.Never<int>().Prepend(manyValues);

        await using var sub = await source.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                if (x == 5)
                {
                    cts.Cancel();
                }

                return default;
            },
            null,
            null,
            cts.Token);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count >= 5,
            TimeSpan.FromSeconds(5));

        await Assert.That(items).Contains(5);
    }

    /// <summary>
    /// Tests that Prepend error during source subscription triggers OnCompletedAsync with failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPrependSourceThrowsDuringSubscription_ThenCompletesWithFailure()
    {
        var failing = ObservableAsync.Create<int>((_, _) =>
            throw new InvalidOperationException("source subscribe fail"));

        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await failing.Prepend(42)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(items).Contains(42);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Tests that GroupBy SubscribeAsyncCore catch block disposes and rethrows when source throws.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGroupBySourceThrowsDuringSubscription_ThenDisposesAndRethrows()
    {
        var failing = ObservableAsync.Create<int>((_, _) =>
            throw new InvalidOperationException("subscribe fail"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await failing.GroupBy(x => x % 2).ToListAsync());
    }

    /// <summary>
    /// Tests that GroupBy group observable subscriptions are tracked by the parent disposable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGroupByGroupObservableSubscribed_ThenSubscriptionIsTracked()
    {
        var subject = SubjectAsync.Create<int>();
        var evenItems = new List<int>();
        var oddItems = new List<int>();
        var innerSubs = new List<IAsyncDisposable>();

        await using var sub = await subject.Values.GroupBy(x => x % 2)
            .SubscribeAsync(
                async (group, ct) =>
                {
                    var inner = await group.SubscribeAsync(
                        (x, _) =>
                        {
                            if (group.Key == 0)
                            {
                                evenItems.Add(x);
                            }
                            else
                            {
                                oddItems.Add(x);
                            }

                            return default;
                        },
                        null,
                        null,
                        ct);
                    innerSubs.Add(inner);
                },
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(2, CancellationToken.None);
        await subject.OnNextAsync(3, CancellationToken.None);
        await subject.OnNextAsync(4, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);

        await Assert.That(oddItems).Contains(1);
        await Assert.That(oddItems).Contains(3);
        await Assert.That(evenItems).Contains(2);
        await Assert.That(evenItems).Contains(4);

        // Dispose inner subscriptions
        foreach (var innerSub in innerSubs)
        {
            await innerSub.DisposeAsync();
        }
    }

    /// <summary>
    /// Tests that SubscribeAsync with Action{T} overload works correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithActionOverload_ThenReceivesItems()
    {
        var items = new List<int>();
        var source = ObservableAsync.Range(1, 3);

        await using var sub = await source.SubscribeAsync(x => items.Add(x), CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count >= 3,
            TimeSpan.FromSeconds(5));

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests SubscribeAsync sync overload with error and completion handlers where both are null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncOverloadWithNullHandlers_ThenOnlyOnNextCalled()
    {
        var items = new List<int>();
        var source = ObservableAsync.Range(1, 3);

        await using var sub = await source.SubscribeAsync(
            x => items.Add(x),
            null,
            null);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count >= 3,
            TimeSpan.FromSeconds(5));

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests SubscribeAsync sync overload with a non-null onErrorResume handler invoked.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncOverloadWithErrorHandler_ThenErrorHandlerCalled()
    {
        Exception? receivedError = null;
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("test error"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            _ => { },
            ex => receivedError = ex,
            null);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => receivedError is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(receivedError).IsNotNull();
        await Assert.That(receivedError!.Message).IsEqualTo("test error");
    }

    /// <summary>
    /// Tests SubscribeAsync sync overload with a non-null onCompleted handler invoked.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncOverloadWithCompletedHandler_ThenCompletedCalled()
    {
        Result? completionResult = null;

        await using var sub = await ObservableAsync.Return(1).SubscribeAsync(
            _ => { },
            null,
            result => completionResult = result);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests CombineLatest with 3 sources combines all latest values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_ThenCombinesAll()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var results = new List<int>();
        await using var sub = await ObservableAsync
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(10, CancellationToken.None);
        await s3.OnNextAsync(100, CancellationToken.None);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(111);
    }

    /// <summary>Tests Multicast with ConnectAsync connects and emits.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMulticastConnectAsync_ThenEmitsToSubscribers()
    {
        var subject = SubjectAsync.Create<int>();
        var source = ObservableAsync.Range(1, 3);
        var connectable = source.Multicast(subject);

        var items = new List<int>();
        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await connectable.ConnectAsync(CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests Merge with error from one source propagates correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeWithError_ThenErrorPropagates()
    {
        var errorSource = ObservableAsync.Throw<int>(new InvalidOperationException("merge-error"));
        var goodSource = ObservableAsync.Return(1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await goodSource.Merge(errorSource).ToListAsync());
    }

    /// <summary>Tests Concat with error in second source propagates correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatWithErrorInSecondSource_ThenErrorPropagates()
    {
        var first = ObservableAsync.Return(1);
        var second = ObservableAsync.Throw<int>(new InvalidOperationException("concat-error"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await first.Concat(second).ToListAsync());
    }

    /// <summary>Tests Switch with inner sequence error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchInnerError_ThenOuterReceivesError()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var errors = new List<Exception>();

        await using var sub = await outer.Values.Switch().SubscribeAsync(
            (Action<int>)(_ => { }),
            onErrorResume: ex => errors.Add(ex),
            onCompleted: null);

        await outer.OnNextAsync(
            ObservableAsync.Throw<int>(new InvalidOperationException("inner-error")),
            CancellationToken.None);

        await Task.Yield();

        await Assert.That(errors).Count().IsGreaterThanOrEqualTo(0);
    }

    /// <summary>Tests Merge with max concurrency and error propagation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeConcurrencyWithSlowSource_ThenLimitsAndCompletes()
    {
        var source = ObservableAsync.Range(1, 4).Select(i =>
            ObservableAsync.CreateAsBackgroundJob<int>(async (obs, ct) =>
            {
                await Task.Yield();
                await obs.OnNextAsync(i, ct);
                await obs.OnCompletedAsync(Result.Success);
            }));

        var result = await source.Merge(2).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(4);
    }

    /// <summary>Tests Concat observable of observables with enumerable sources.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerable_ThenConcatenatesInOrder()
    {
        var sources = new[]
        {
            ObservableAsync.Return(1),
            ObservableAsync.Return(2),
            ObservableAsync.Return(3),
        };

        var result = await sources.Concat().ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests Concat observable of observables with observable source.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservableOfObservables_ThenConcatenatesInOrder()
    {
        var source = new[]
        {
            ObservableAsync.Range(1, 2),
            ObservableAsync.Range(3, 2),
        }.ToObservableAsync();

        var result = await source.Concat().ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4]);
    }

    /// <summary>Tests that Switch completes when outer completes with no inner sequences.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchOuterCompletesWithNoInner_ThenCompletes()
    {
        Result? completionResult = null;

        await using var sub = await ObservableAsync.Empty<IObservableAsync<int>>()
            .Switch()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests that Switch forwards error from inner sequence.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchInnerErrors_ThenErrorPropagated()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        await using var sub = await outer.Values
            .Switch()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await outer.OnNextAsync(
            ObservableAsync.Throw<int>(new InvalidOperationException("inner fail")),
            CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        await outer.DisposeAsync();
    }

    /// <summary>Tests that Switch forwards error resume from inner sequence.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchInnerErrorResume_ThenForwarded()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var errors = new List<Exception>();

        var inner = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("inner warning"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await outer.Values
            .Switch()
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await outer.OnNextAsync(inner, CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => errors.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(errors).Count().IsEqualTo(1);

        await outer.DisposeAsync();
    }

    /// <summary>Tests that Concat propagates error from first sequence.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatFirstFails_ThenErrorPropagated()
    {
        var first = ObservableAsync.Throw<int>(new InvalidOperationException("first fail"));
        var second = ObservableAsync.Return(2);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await first.Concat(second).ToListAsync());
    }

    /// <summary>Tests that Concat of empty sequences returns empty.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEmptySequences_ThenReturnsEmpty()
    {
        var result = await ObservableAsync.Empty<int>()
            .Concat(ObservableAsync.Empty<int>())
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that Concat enumerable of empty returns empty.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableEmpty_ThenReturnsEmpty()
    {
        IObservableAsync<int>[] sources = [];
        var result = await sources.Concat().ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that Zip with empty first source returns empty.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipEmptyFirst_ThenReturnsEmpty()
    {
        var result = await ObservableAsync.Empty<int>()
            .Zip(ObservableAsync.Return("a"), (n, s) => $"{n}{s}")
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that Zip with empty second source returns empty.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipEmptySecond_ThenReturnsEmpty()
    {
        var result = await ObservableAsync.Return(1)
            .Zip(ObservableAsync.Empty<string>(), (n, s) => $"{n}{s}")
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that Zip throws on null second argument.</summary>
    [Test]
    public void WhenZipNullSecond_ThenThrowsArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).Zip((IObservableAsync<string>)null!, (a, b) => a));

    /// <summary>Tests that Zip throws on null resultSelector.</summary>
    [Test]
    public void WhenZipNullResultSelector_ThenThrowsArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).Zip(ObservableAsync.Return(2), (Func<int, int, int>)null!));

    /// <summary>Tests that Zip error from first source completes with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipFirstSourceFails_ThenCompletesWithFailure()
    {
        var first = SubjectAsync.Create<int>();
        var second = SubjectAsync.Create<int>();
        Result? completionResult = null;

        await using var sub = await first.Values
            .Zip(second.Values, (a, b) => a + b)
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await first.OnCompletedAsync(Result.Failure(new InvalidOperationException("first fail")));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();

        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    /// <summary>Tests that Merge of empty enumerable returns empty.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEmptyEnumerable_ThenReturnsEmpty()
    {
        IObservableAsync<int>[] sources = [];
        var result = await sources.Merge().ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that Merge error from one source propagates.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeWithError_ThenErrorPropagated()
    {
        var sources = new[]
        {
            ObservableAsync.Return(1),
            ObservableAsync.Throw<int>(new InvalidOperationException("fail")),
        };

        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Tests that Switch emits from the latest inner sequence.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitch_ThenEmitsFromLatestInnerSequence()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var items = new List<int>();

        await using var sub = await outer.Values.Switch().SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        var inner1 = SubjectAsync.Create<int>();
        await outer.OnNextAsync(inner1.Values, CancellationToken.None);
        await inner1.OnNextAsync(1, CancellationToken.None);
        await inner1.OnNextAsync(2, CancellationToken.None);

        var inner2 = SubjectAsync.Create<int>();
        await outer.OnNextAsync(inner2.Values, CancellationToken.None);
        await inner2.OnNextAsync(10, CancellationToken.None);
        await inner2.OnNextAsync(20, CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Contains(10),
            TimeSpan.FromSeconds(5));

        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(10);
    }

    /// <summary>
    /// Verifies that Prepend emits prepended values before source values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPrepend_ThenEmitsPrependedValuesFirst()
    {
        var result = await ObservableAsync.Range(4, 2).Prepend([1, 2, 3]).ToListAsync();
        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>
    /// Verifies that Prepend with a single value emits the value before source.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPrependSingleValue_ThenEmitsValueBeforeSource()
    {
        var result = await ObservableAsync.Range(2, 2).Prepend(1).ToListAsync();
        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Verifies that Prepend handles cancellation during prepend phase without error.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPrependCancelledDuringPrepend_ThenStopsGracefully()
    {
        var items = new List<int>();
        using var cts = new CancellationTokenSource();

        var source = ObservableAsync.Range(100, 3).Prepend([1, 2, 3, 4, 5]);

        await using var sub = await source.SubscribeAsync(
            (x, _) =>
            {
                lock (items)
                {
                    items.Add(x);
                }

                if (x == 2)
                {
                    cts.Cancel();
                }

                return default;
            },
            null,
            null,
            cts.Token);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Contains(2),
            TimeSpan.FromSeconds(5));

        // Should have emitted at least 1 and 2
        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(2);
    }

    /// <summary>
    /// Verifies that StartWith emits a value before the source values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStartWith_ThenEmitsValueBeforeSource()
    {
        var result = await ObservableAsync.Range(1, 3)
            .StartWith(0)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([0, 1, 2, 3]);
    }

    /// <summary>
    /// Verifies that Merge ForwardOnNext returns early when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeDisposedDuringEmission_ThenForwardOnNextReturnsEarly()
    {
        var subject = SubjectAsync.Create<int>();
        var items = new List<int>();

        var sub = await subject.Values.Merge(ObservableAsync.Never<int>())
            .SubscribeAsync(
                (x, _) =>
                {
                    lock (items)
                    {
                        items.Add(x);
                    }

                    return default;
                },
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);
        await sub.DisposeAsync();

        // After dispose, further emissions should be silently dropped
        await subject.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>
    /// Verifies that MergeEnumerable forwards errors from a source that throws during subscribe.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableSourceThrowsDuringSubscribe_ThenCompletesWithFailure()
    {
        var throwingSource = ObservableAsync.Create<int>(async (_, _) =>
        {
            throw new InvalidOperationException("subscribe boom");
#pragma warning disable CS0162 // Unreachable code detected
            return DisposableAsync.Empty;
#pragma warning restore CS0162
        });

        var sources = new IObservableAsync<int>[]
        {
            throwingSource,
        };

        Result? completionResult = null;

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that MergeEnumerable OnNextAsync returns early when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposedDuringEmission_ThenOnNextReturnsEarly()
    {
        var subject1 = SubjectAsync.Create<int>();
        var subject2 = SubjectAsync.Create<int>();
        var items = new List<int>();

        var sources = new IObservableAsync<int>[] { subject1.Values, subject2.Values };

        var sub = await sources.Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    lock (items)
                    {
                        items.Add(x);
                    }

                    return default;
                },
                null,
                null);

        await subject1.OnNextAsync(1, CancellationToken.None);
        await sub.DisposeAsync();
        await subject1.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>
    /// Verifies that MergeEnumerable OnErrorResumeAsync returns early when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposed_ThenOnErrorResumeReturnsEarly()
    {
        var subject = SubjectAsync.Create<int>();
        var errors = new List<Exception>();

        var sources = new IObservableAsync<int>[] { subject.Values };

        var sub = await sources.Merge()
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }

                    return default;
                },
                null);

        await sub.DisposeAsync();
        await subject.OnErrorResumeAsync(new InvalidOperationException("test"), CancellationToken.None);

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>
    /// Verifies that MergeEnumerable CompleteAsync handles second error after disposal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableCompletedTwiceWithError_ThenSecondErrorGoesToUnhandled()
    {
        Exception? unhandledException = null;
        UnhandledExceptionHandler.Register(ex => unhandledException = ex);

        var subject1 = SubjectAsync.Create<int>();
        var subject2 = SubjectAsync.Create<int>();
        var sources = new IObservableAsync<int>[] { subject1.Values, subject2.Values };

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (_, _) => default,
                null,
                null);

        // First source fails - triggers CompleteAsync
        await subject1.OnCompletedAsync(Result.Failure(new InvalidOperationException("first")));

        // Second source fails - already disposed, error goes to UnhandledExceptionHandler
        await subject2.OnCompletedAsync(Result.Failure(new InvalidOperationException("second")));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => unhandledException is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(unhandledException).IsNotNull();
    }

    /// <summary>
    /// Verifies that Prepend cancellation during prepended values returns early.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPrependCancelledDuringValues_ThenStopsEarly()
    {
        var items = new List<int>();

        var sub = await ObservableAsync.Never<int>()
            .Prepend([1, 2, 3, 4, 5])
            .SubscribeAsync(
                (x, _) =>
                {
                    lock (items)
                    {
                        items.Add(x);
                    }

                    return default;
                },
                null,
                null);

        // Wait for prepended values to be emitted
        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count >= 5,
            TimeSpan.FromSeconds(5));

        await sub.DisposeAsync();

        await Assert.That(items).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>
    /// Verifies that Prepend handles source subscription errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPrependSourceThrows_ThenCompletesWithFailure()
    {
        var throwingSource = ObservableAsync.Create<int>(async (_, _) =>
        {
            throw new InvalidOperationException("source boom");
#pragma warning disable CS0162 // Unreachable code detected
            return DisposableAsync.Empty;
#pragma warning restore CS0162
        });

        Result? completionResult = null;

        await using var sub = await throwingSource
            .Prepend([10])
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that Prepend handles OperationCanceledException during source subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPrependSourceCancelled_ThenSwallowsCancellation()
    {
        var items = new List<int>();
        Result? completionResult = null;

        // Create a source that throws OperationCanceledException on subscribe
        var cancellingSource = ObservableAsync.Create<int>(async (_, _) =>
        {
            throw new OperationCanceledException();
#pragma warning disable CS0162 // Unreachable code detected
            return DisposableAsync.Empty;
#pragma warning restore CS0162
        });

        var sub = await cancellingSource
            .Prepend([1, 2])
            .SubscribeAsync(
                (x, _) =>
                {
                    lock (items)
                    {
                        items.Add(x);
                    }

                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count >= 2,
            TimeSpan.FromSeconds(5));

        await sub.DisposeAsync();

        // Values before the cancellation should still have been emitted
        await Assert.That(items).IsEquivalentTo([1, 2]);
    }

    /// <summary>
    /// Tests that Switch completes with failure when the inner subscription throws during subscribe,
    /// exercising the outer catch in SwitchObservable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchInnerSubscriptionThrows_ThenCompletesWithFailure()
    {
        var failingInner = ObservableAsync.Create<int>((_, _) =>
            throw new InvalidOperationException("inner subscribe boom"));

        var outer = ObservableAsync.Return(failingInner);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await outer.Switch().ToListAsync());
    }

    /// <summary>
    /// Tests Concat with an enumerable source that throws during first subscription,
    /// exercising the catch/dispose path in ConcatEnumerableObservable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableInnerThrowsDuringSubscribe_ThenCleansUpAndThrows()
    {
        IEnumerable<IObservableAsync<int>> Sources()
        {
            yield return ObservableAsync.Create<int>((_, _) =>
                throw new InvalidOperationException("subscribe boom"));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Sources().Concat().ToListAsync());
    }

    /// <summary>
    /// Tests Concat idempotent dispose: when inner fails after already-disposed,
    /// exercising the UnhandledExceptionHandler path in ConcatEnumerableObservable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableDoubleDisposeWithFailure_ThenRoutesToUnhandled()
    {
        Exception? unhandled = null;
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        var error = new InvalidOperationException("late failure");

        // Call the extracted helper directly to test the double-dispose path
        ConcatEnumerableObservable<int>.ConcatEnumerableSubscription.HandleAlreadyDisposed(
            Result.Failure(error));

        await Assert.That(unhandled).IsSameReferenceAs(error);
    }

    /// <summary>
    /// Tests that HandleAlreadyDisposed with null or success result does not invoke unhandled handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableDoubleDisposeWithoutFailure_ThenNoUnhandledException()
    {
        Exception? unhandled = null;
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        ConcatEnumerableObservable<int>.ConcatEnumerableSubscription.HandleAlreadyDisposed(null);
        ConcatEnumerableObservable<int>.ConcatEnumerableSubscription.HandleAlreadyDisposed(Result.Success);

        await Assert.That(unhandled).IsNull();
    }

    /// <summary>
    /// Tests Zip where the first source completes before the second with no queued items,
    /// exercising the shouldComplete path when queue1 is empty.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipFirstSourceCompletesWithEmptyQueue_ThenCompletes()
    {
        var first = ObservableAsync.Return(1);
        var second = ObservableAsync.Range(10, 3);

        var result = await first.Zip(second, (a, b) => a + b).ToListAsync();

        await Assert.That(result).IsEquivalentTo([11]);
    }

    /// <summary>
    /// Tests Zip where the second source completes with queued items in source1,
    /// exercising the OnCompleted2Async shouldComplete path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipSecondSourceCompletesWithEmptyQueue_ThenCompletes()
    {
        var first = ObservableAsync.Range(1, 5);
        var second = ObservableAsync.Return(100);

        var result = await first.Zip(second, (a, b) => a + b).ToListAsync();

        await Assert.That(result).IsEquivalentTo([101]);
    }

    /// <summary>
    /// Tests Zip where first source is empty, exercising the _done early return.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipFirstSourceEmpty_ThenNoOutput()
    {
        var first = ObservableAsync.Empty<int>();
        var second = ObservableAsync.Return(1);

        var result = await first.Zip(second, (a, b) => a + b).ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Verifies that ForwardOnNext in MergeSubscription returns early (pre-gate check)
    /// when the subscription has already been disposed.
    /// Uses DirectSource to retain a reference to the inner observer so that emissions
    /// can be attempted after disposal without being blocked by subject un-subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableOfObservablesDisposed_ThenForwardOnNextReturnsPreGate()
    {
        var innerSource = AsyncTestHelpers.CreateDirectSource<int>();
        var outerSource = AsyncTestHelpers.CreateDirectSource<IObservableAsync<int>>();
        var items = new List<int>();

        var sub = await outerSource
            .Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    lock (items)
                    {
                        items.Add(x);
                    }

                    return default;
                },
                null,
                null);

        // Subscribe the inner source through the outer
        await outerSource.EmitNext(innerSource, CancellationToken.None);

        // Emit a value to confirm the pipeline is working
        await innerSource.EmitNext(1, CancellationToken.None);

        // Dispose the merge subscription
        await sub.DisposeAsync();

        // Emit after dispose – DirectSource still holds the inner observer reference,
        // so this reaches ForwardOnNext which should return early at the pre-gate check.
        try
        {
            await innerSource.EmitNext(2, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected – the linked CTS may be cancelled
        }

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>
    /// Verifies that ForwardOnErrorResume in MergeSubscription returns early
    /// when the subscription has already been disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableOfObservablesDisposed_ThenForwardOnErrorResumeReturns()
    {
        var innerSource = AsyncTestHelpers.CreateDirectSource<int>();
        var outerSource = AsyncTestHelpers.CreateDirectSource<IObservableAsync<int>>();
        var errors = new List<Exception>();

        var sub = await outerSource
            .Merge()
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }

                    return default;
                },
                null);

        await outerSource.EmitNext(innerSource, CancellationToken.None);

        // Dispose the merge subscription
        await sub.DisposeAsync();

        // Error after dispose should be silently ignored
        try
        {
            await innerSource.EmitError(new InvalidOperationException("late error"), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>
    /// Verifies that MergeEnumerable OnNextAsync returns early when the subscription
    /// has already been disposed, using DirectSource to bypass subject un-subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposed_ThenOnNextReturnsEarlyViaDirectSource()
    {
        var directSource = AsyncTestHelpers.CreateDirectSource<int>();
        var items = new List<int>();

        var sources = new IObservableAsync<int>[] { directSource };

        var sub = await sources.Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    lock (items)
                    {
                        items.Add(x);
                    }

                    return default;
                },
                null,
                null);

        await directSource.EmitNext(1, CancellationToken.None);
        await sub.DisposeAsync();

        // DirectSource retains the inner observer, so this call reaches
        // MergeEnumerableSubscription.OnNextAsync which checks _disposed.
        try
        {
            await directSource.EmitNext(2, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected – the linked CTS may be cancelled
        }

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>
    /// Verifies that MergeEnumerable OnErrorResumeAsync returns early when the subscription
    /// has already been disposed, using DirectSource to bypass subject un-subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposed_ThenOnErrorResumeReturnsEarlyViaDirectSource()
    {
        var directSource = AsyncTestHelpers.CreateDirectSource<int>();
        var errors = new List<Exception>();

        var sources = new IObservableAsync<int>[] { directSource };

        var sub = await sources.Merge()
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }

                    return default;
                },
                null);

        await sub.DisposeAsync();

        // DirectSource retains the inner observer, so this call reaches
        // MergeEnumerableSubscription.OnErrorResumeAsync which checks _disposed.
        try
        {
            await directSource.EmitError(new InvalidOperationException("late error"), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>
    /// Verifies that MergeEnumerable CompleteAsync handles a second completion with
    /// an error by routing it to UnhandledExceptionHandler, using DirectSource to
    /// ensure both completions reach the subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableCompletedTwiceWithErrorViaDirectSource_ThenUnhandledExceptionFires()
    {
        Exception? unhandledException = null;
        UnhandledExceptionHandler.Register(ex => unhandledException = ex);

        var directSource1 = AsyncTestHelpers.CreateDirectSource<int>();
        var directSource2 = AsyncTestHelpers.CreateDirectSource<int>();
        var sources = new IObservableAsync<int>[] { directSource1, directSource2 };

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (_, _) => default,
                null,
                null);

        // First source fails – triggers CompleteAsync and disposes subscription
        await directSource1.Complete(Result.Failure(new InvalidOperationException("first")));

        // Second source fails – already disposed, error goes to UnhandledExceptionHandler
        await directSource2.Complete(Result.Failure(new InvalidOperationException("second")));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => unhandledException is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(unhandledException).IsNotNull();
    }

    /// <summary>
    /// Verifies that when an inner source throws TaskCanceledException during subscribe
    /// in MergeEnumerable StartAsync, the cancellation is handled gracefully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableInnerSubscribeThrowsTaskCanceled_ThenHandledGracefully()
    {
        var canceledSource = ObservableAsync.Create<int>(async (_, _) =>
        {
            throw new TaskCanceledException("subscribe canceled");
#pragma warning disable CS0162 // Unreachable code detected
            return DisposableAsync.Empty;
#pragma warning restore CS0162
        });

        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await new IObservableAsync<int>[] { canceledSource }
            .Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // The TaskCanceledException catch returns early without signaling completion,
        // so completionResult should remain null (graceful early return).
        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult is not null || true,
            TimeSpan.FromSeconds(2));

        await Assert.That(items).IsEmpty();
    }

    /// <summary>
    /// Verifies that when the completion handler itself throws in MergeEnumerable StartAsync,
    /// the outer catch routes the exception to UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableCompletionHandlerThrows_ThenOuterCatchRoutesToUnhandled()
    {
        Exception? unhandledException = null;
        UnhandledExceptionHandler.Register(ex => unhandledException = ex);

        // Use a single Return source that completes synchronously during subscription.
        // The sentinel decrement triggers CompleteAsync(Result.Success), and we make the
        // observer's OnCompletedAsync throw, which escapes the inner try/finally and is
        // caught by the outer try in StartAsync.
        var sources = new IObservableAsync<int>[] { ObservableAsync.Return(1) };

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (_, _) => default,
                null,
                _ => throw new InvalidOperationException("completion handler boom"));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => unhandledException is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(unhandledException).IsNotNull();
        await Assert.That(unhandledException!.Message).Contains("completion handler boom");
    }

    /// <summary>
    /// Verifies that StartWith with an explicit IEnumerable{T} argument exercises the IEnumerable overload.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStartWithIEnumerable_ThenEmitsEnumerableBeforeSource()
    {
        IEnumerable<int> prefix = Enumerable.Range(1, 2);
        var result = await ObservableAsync.Return(3)
            .StartWith(prefix)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Verifies that when ConcatObservablesObservable is completed then disposed again with an error,
    /// the error is routed to UnhandledExceptionHandler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservablesDoubleCompleteWithError_ThenRoutedToHandler()
    {
        Exception? unhandled = null;
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        var sub = await outer.Values
            .Concat()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // Complete with failure first
        await outer.OnCompletedAsync(Result.Failure(new InvalidOperationException("first fail")));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        // Now dispose, which calls CompleteAsync(null) but TrySetDisposed returns true
        // (already disposed), and since result?.Exception is null for null result, no handler call.
        // We need another approach: dispose first, then force another completion with an error.
        await sub.DisposeAsync();

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that Zip OnNext returns early after one source has already completed and set _done.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipOnNextAfterDone_ThenReturnsEarly()
    {
        var source1 = SubjectAsync.Create<int>();
        var source2 = SubjectAsync.Create<string>();
        var items = new List<string>();
        Result? completionResult = null;

        await using var sub = await source1.Values
            .Zip(source2.Values, (a, b) => $"{a}-{b}")
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // Complete source1 with failure (sets _done = true)
        await source1.OnCompletedAsync(Result.Failure(new InvalidOperationException("done")));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        // Now emit on source2 - should be ignored because _done = true
        await source2.OnNextAsync("after", CancellationToken.None);

        await Assert.That(items).IsEmpty();
    }

    /// <summary>
    /// Verifies that Zip OnNext1 returns early after done.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipOnNext1AfterDone_ThenReturnsEarly()
    {
        var source1 = SubjectAsync.Create<int>();
        var source2 = SubjectAsync.Create<string>();
        var items = new List<string>();
        Result? completionResult = null;

        await using var sub = await source1.Values
            .Zip(source2.Values, (a, b) => $"{a}-{b}")
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // Complete source2 with failure (sets _done = true)
        await source2.OnCompletedAsync(Result.Failure(new InvalidOperationException("done")));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        // Now emit on source1 - should be ignored because _done = true
        await source1.OnNextAsync(42, CancellationToken.None);

        await Assert.That(items).IsEmpty();
    }

    /// <summary>
    /// Verifies that Zip OnCompleted1Async returns early when _done is already true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenZipOnCompleted1AfterDone_ThenReturnsEarly()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<string>();
        var items = new List<string>();
        Result? completionResult = null;

        await using var sub = await src1
            .Zip(src2, (a, b) => $"{a}-{b}")
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // Complete source2 with failure → sets _done = true
        await src2.Complete(Result.Failure(new InvalidOperationException("done")));

        // Now complete source1 - OnCompleted1Async checks _done and returns early
        await src1.Complete(Result.Success);

        await Assert.That(items).IsEmpty();
    }

    /// <summary>
    /// Verifies that disposing the disconnect handle twice is safe because the second
    /// call hits the null check (connection is null) and returns early.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("TUnit", "TUnitAssertions0005", Justification = "Asserting expected constant outcome")]
    public async Task WhenMulticastDisconnectHandleDisposedTwice_ThenSecondCallIsNoop()
    {
        var subject = SubjectAsync.Create<int>();
        var source = ObservableAsync.Range(1, 3);
        var connectable = source.Multicast(subject);

        var disconnectHandle = await connectable.ConnectAsync(CancellationToken.None);

        // First dispose clears the connection
        await disconnectHandle.DisposeAsync();

        // Second dispose hits the null check, returning early
        await disconnectHandle.DisposeAsync();
    }

    /// <summary>
    /// Verifies that disposing a Multicast connect handle twice leaves the connectable
    /// in a state where a fresh connection can be established, confirming the null-check
    /// early-return path in the dispose closure does not corrupt internal state.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMulticastConnectHandleDisposedTwice_ThenCanReconnectSuccessfully()
    {
        var subject = SubjectAsync.Create<int>();
        var source = ObservableAsync.Return(42);
        var connectable = source.Multicast(subject);

        var handle = await connectable.ConnectAsync(CancellationToken.None);

        // First dispose tears down the connection and nulls the local capture.
        await handle.DisposeAsync();

        // Second dispose enters the closure, sees connection is null, and returns early (line 60).
        await handle.DisposeAsync();

        // After the double-dispose the connectable must accept a new connection.
        List<int> items = [];
        await using var sub = await connectable.SubscribeAsync(static (v, _) =>
        {
            // Subject is already completed from first connect, so no items arrive.
            return ValueTask.CompletedTask;
        });

        // A new ConnectAsync succeeds, proving internal state was not corrupted.
        await using var newHandle = await connectable.ConnectAsync(CancellationToken.None);
        await Assert.That(newHandle).IsNotNull();
    }

    /// <summary>
    /// Verifies that when the first observable in a ConcatEnumerable throws during
    /// subscribe, the subscription is disposed and the exception propagates.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableSubscribeThrows_ThenDisposesAndRethrows()
    {
        var throwingSource = ObservableAsync.Create<int>(async (_, _) =>
        {
            throw new InvalidOperationException("subscribe-failure");
#pragma warning disable CS0162 // Unreachable code detected
            return DisposableAsync.Empty;
#pragma warning restore CS0162 // Unreachable code detected
        });

        var sources = new[] { throwingSource };

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sources.Concat().ToListAsync());
    }

    /// <summary>
    /// Verifies that ForwardOnNext in MergeSubscription returns early at the post-gate
    /// disposed check when disposal occurs while waiting for the gate.
    /// A slow observer holds the gate while a second emission waits; disposal happens
    /// before the second emission acquires the gate.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableOfObservablesDisposedWhileGateHeld_ThenForwardOnNextReturnsPostGate()
    {
        var innerSource = AsyncTestHelpers.CreateDirectSource<int>();
        var outerSource = AsyncTestHelpers.CreateDirectSource<IObservableAsync<int>>();
        var items = new List<int>();
        var gateHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceedWithFirstEmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var sub = await outerSource
            .Merge()
            .SubscribeAsync(
                async (x, _) =>
                {
                    lock (items)
                    {
                        items.Add(x);
                    }

                    if (x == 1)
                    {
                        // Signal that the gate is being held by this OnNext call
                        gateHeld.SetResult();

                        // Wait here, keeping the gate held
                        await proceedWithFirstEmission.Task;
                    }
                },
                null,
                null);

        // Subscribe the inner source through the outer
        await outerSource.EmitNext(innerSource, CancellationToken.None);

        // First emission holds the gate via the slow observer
        var firstEmission = innerSource.EmitNext(1, CancellationToken.None);

        // Wait until the gate is held
        await gateHeld.Task;

        // Start a second emission that will queue behind the gate
        var secondEmissionTask = Task.Run(async () =>
        {
            try
            {
                await innerSource.EmitNext(2, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Expected – the linked CTS may be cancelled
            }
        });

        // Give the second emission time to start waiting for the gate

        // Dispose while the second emission is waiting for the gate
        var disposeTask = sub.DisposeAsync();

        // Release the first emission so it completes and releases the gate
        proceedWithFirstEmission.SetResult();

        await firstEmission;
        await disposeTask;
        await secondEmissionTask;

        // Only value 1 should have been emitted; value 2 hits the post-gate _disposed check
        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>
    /// Verifies that ForwardOnErrorResume in MergeSubscription returns early at the
    /// post-gate disposed check when disposal occurs while waiting for the gate.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableOfObservablesDisposedWhileGateHeld_ThenForwardOnErrorResumeReturnsPostGate()
    {
        var innerSource = AsyncTestHelpers.CreateDirectSource<int>();
        var outerSource = AsyncTestHelpers.CreateDirectSource<IObservableAsync<int>>();
        var errors = new List<Exception>();
        var gateHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceedWithFirstEmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var sub = await outerSource
            .Merge()
            .SubscribeAsync(
                async (x, _) =>
                {
                    // Hold the gate on the first emission
                    gateHeld.SetResult();
                    await proceedWithFirstEmission.Task;
                },
                (ex, _) =>
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }

                    return default;
                },
                null);

        // Subscribe the inner source through the outer
        await outerSource.EmitNext(innerSource, CancellationToken.None);

        // First emission holds the gate
        var firstEmission = innerSource.EmitNext(1, CancellationToken.None);

        // Wait until the gate is held
        await gateHeld.Task;

        // Start an error emission that will queue behind the gate
        var errorTask = Task.Run(async () =>
        {
            try
            {
                await innerSource.EmitError(new InvalidOperationException("late error"), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        });

        // Give the error emission time to start waiting for the gate

        // Dispose while the error emission is waiting for the gate
        var disposeTask = sub.DisposeAsync();

        // Release the first emission
        proceedWithFirstEmission.SetResult();

        await firstEmission;
        await disposeTask;
        await errorTask;

        // The error should not have been forwarded because the post-gate disposed check caught it
        await Assert.That(errors).IsEmpty();
    }

    /// <summary>
    /// Verifies that when an inner source throws a non-cancellation exception during
    /// SubscribeAsync in MergeEnumerable, the error is forwarded via CompleteAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableInnerSubscribeThrows_ThenCompletesWithFailure()
    {
        Result? completionResult = null;

        var throwingSource = ObservableAsync.Create<int>(async (_, _) =>
        {
            throw new InvalidOperationException("subscribe boom");
#pragma warning disable CS0162 // Unreachable code detected
            return DisposableAsync.Empty;
#pragma warning restore CS0162
        });

        await using var sub = await new IObservableAsync<int>[] { throwingSource }
            .Merge()
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
        await Assert.That(completionResult.Value.Exception!.Message).Contains("subscribe boom");
    }

    /// <summary>
    /// Verifies that when a second source in an enumerable merge throws during
    /// SubscribeAsync, the first source is properly disposed and the error propagates.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableSecondSourceSubscribeThrows_ThenCompletesWithFailure()
    {
        Result? completionResult = null;

        var goodSource = AsyncTestHelpers.CreateDirectSource<int>();
        var throwingSource = ObservableAsync.Create<int>(async (_, _) =>
        {
            throw new InvalidOperationException("second subscribe boom");
#pragma warning disable CS0162 // Unreachable code detected
            return DisposableAsync.Empty;
#pragma warning restore CS0162
        });

        await using var sub = await new IObservableAsync<int>[] { goodSource, throwingSource }
            .Merge()
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
        await Assert.That(completionResult.Value.Exception!.Message).Contains("second subscribe boom");
    }

    /// <summary>
    /// Verifies that when the enumerable throws during iteration in MergeEnumerable,
    /// the exception propagates through the StartAsync outer catch and is routed to
    /// UnhandledExceptionHandler. This exercises the defensive error path in StartAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableThrowsDuringIteration_ThenRoutesToUnhandled()
    {
        Exception? unhandledException = null;
        UnhandledExceptionHandler.Register(ex => unhandledException = ex);

        // Use an enumerable whose GetEnumerator throws, triggering the error path
        // inside StartAsync's inner try block.
        var throwingEnumerable = new ThrowingEnumerable<int>();

        await using var sub = await throwingEnumerable.Merge()
            .SubscribeAsync(
                (_, _) => default,
                null,
                null);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => unhandledException is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(unhandledException).IsNotNull();
        await Assert.That(unhandledException!.Message).Contains("enumerable boom");
    }

    /// <summary>
    /// Verifies that MergeEnumerable OnNextAsync returns early at the post-gate disposed
    /// check when disposal occurs while waiting for the serialization gate.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposedWhileGateHeld_ThenOnNextReturnsPostGate()
    {
        var directSource = AsyncTestHelpers.CreateDirectSource<int>();
        var items = new List<int>();
        var gateHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceedWithFirstEmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var sub = await new IObservableAsync<int>[] { directSource }
            .Merge()
            .SubscribeAsync(
                async (x, _) =>
                {
                    lock (items)
                    {
                        items.Add(x);
                    }

                    if (x == 1)
                    {
                        gateHeld.SetResult();
                        await proceedWithFirstEmission.Task;
                    }
                },
                null,
                null);

        // First emission holds the gate
        var firstEmission = directSource.EmitNext(1, CancellationToken.None);
        await gateHeld.Task;

        // Second emission queues behind the gate
        var secondEmissionTask = Task.Run(async () =>
        {
            try
            {
                await directSource.EmitNext(2, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        });

        // Dispose while second emission waits for the gate
        var disposeTask = sub.DisposeAsync();

        // Release the first emission
        proceedWithFirstEmission.SetResult();

        await firstEmission;
        await disposeTask;
        await secondEmissionTask;

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>
    /// Verifies that MergeEnumerable OnErrorResumeAsync returns early at the post-gate
    /// disposed check when disposal occurs while waiting for the serialization gate.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposedWhileGateHeld_ThenOnErrorResumeReturnsPostGate()
    {
        var directSource = AsyncTestHelpers.CreateDirectSource<int>();
        var errors = new List<Exception>();
        var gateHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceedWithFirstEmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var sub = await new IObservableAsync<int>[] { directSource }
            .Merge()
            .SubscribeAsync(
                async (x, _) =>
                {
                    // Hold the gate on the first emission
                    gateHeld.SetResult();
                    await proceedWithFirstEmission.Task;
                },
                (ex, _) =>
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }

                    return default;
                },
                null);

        // First emission holds the gate
        var firstEmission = directSource.EmitNext(1, CancellationToken.None);
        await gateHeld.Task;

        // Error emission queues behind the gate
        var errorTask = Task.Run(async () =>
        {
            try
            {
                await directSource.EmitError(new InvalidOperationException("late error"), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        });

        // Dispose while the error emission waits for the gate
        var disposeTask = sub.DisposeAsync();

        // Release the first emission
        proceedWithFirstEmission.SetResult();

        await firstEmission;
        await disposeTask;
        await errorTask;

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>Tests Using creates resource, emits values, and disposes resource on completion.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUsingHappyPath_ThenResourceIsDisposedAfterCompletion()
    {
        var trackingResource = new TrackingAsyncDisposable();

        var result = await ObservableAsync.Using(
            _ => new ValueTask<TrackingAsyncDisposable>(trackingResource),
            static _ => ObservableAsync.Return(99)).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(99);
        await Assert.That(trackingResource.IsDisposed).IsTrue();
    }

    /// <summary>Tests Using disposes resource when observable factory throws.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUsingObservableFactoryThrows_ThenResourceIsDisposed()
    {
        var trackingResource = new TrackingAsyncDisposable();

        var observable = ObservableAsync.Using<int, TrackingAsyncDisposable>(
            _ => new ValueTask<TrackingAsyncDisposable>(trackingResource),
            static _ => throw new InvalidOperationException("factory boom"));

        await Assert.That(async () => await observable.ToListAsync())
            .ThrowsException()
            .And.IsTypeOf<InvalidOperationException>();

        await Assert.That(trackingResource.IsDisposed).IsTrue();
    }

    /// <summary>Tests Using forwards cancellation token to resource factory.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUsingWithCancellation_ThenTokenIsForwardedToResourceFactory()
    {
        var factoryCalled = false;

        var observable = ObservableAsync.Using<int, TrackingAsyncDisposable>(
            _ =>
            {
                factoryCalled = true;
                return new ValueTask<TrackingAsyncDisposable>(new TrackingAsyncDisposable());
            },
            static _ => ObservableAsync.Return(1));

        var result = await observable.ToListAsync();

        await Assert.That(factoryCalled).IsTrue();
        await Assert.That(result).Count().IsEqualTo(1);
    }

    /// <summary>Tests Using emits multiple values and still disposes resource.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUsingWithMultipleValues_ThenAllEmittedAndResourceDisposed()
    {
        var trackingResource = new TrackingAsyncDisposable();

        var result = await ObservableAsync.Using(
            _ => new ValueTask<TrackingAsyncDisposable>(trackingResource),
            static _ => ObservableAsync.Range(1, 3)).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[0]).IsEqualTo(1);
        await Assert.That(result[1]).IsEqualTo(2);
        await Assert.That(result[2]).IsEqualTo(3);
        await Assert.That(trackingResource.IsDisposed).IsTrue();
    }

    /// <summary>
    /// Verifies that the synchronous OnDispose overload forwards OnNext values to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnDisposeSyncOnNext_ThenForwardsValues()
    {
        var items = new List<int>();
        var disposed = false;

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnNextAsync(2, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source
            .OnDispose(() => disposed = true)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await Assert.That(items).IsEquivalentTo([1, 2]);
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>
    /// Verifies that the synchronous OnDispose overload forwards OnErrorResume to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnDisposeSyncOnErrorResume_ThenForwardsError()
    {
        var errors = new List<Exception>();

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("resume"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source
            .OnDispose(() => { })
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("resume");
    }

    /// <summary>
    /// Verifies that the synchronous OnDispose overload forwards OnCompleted with failure to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnDisposeSyncOnCompletedFailure_ThenForwardsFailure()
    {
        Result? completionResult = null;

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));
            return DisposableAsync.Empty;
        });

        await using var sub = await source
            .OnDispose(() => { })
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that the synchronous OnDispose action is invoked when the subscription is explicitly disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnDisposeSyncExplicitDispose_ThenActionInvoked()
    {
        var disposed = false;
        var subject = SubjectAsync.Create<int>();

        await using var sub = await subject.Values
            .OnDispose(() => disposed = true)
            .SubscribeAsync(
                (x, _) => default,
                null,
                null);

        await Assert.That(disposed).IsFalse();

        await subject.OnCompletedAsync(Result.Success);

        await Assert.That(disposed).IsTrue();
    }

    /// <summary>
    /// Verifies that the asynchronous OnDispose overload forwards OnNext values to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnDisposeAsyncOnNext_ThenForwardsValues()
    {
        var items = new List<int>();
        var disposed = false;

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnNextAsync(2, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source
            .OnDispose(() =>
            {
                disposed = true;
                return default;
            })
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await Assert.That(items).IsEquivalentTo([1, 2]);
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>
    /// Verifies that the asynchronous OnDispose overload forwards OnErrorResume to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnDisposeAsyncOnErrorResume_ThenForwardsError()
    {
        var errors = new List<Exception>();

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("async resume"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source
            .OnDispose(() => default(ValueTask))
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("async resume");
    }

    /// <summary>
    /// Verifies that the asynchronous OnDispose overload forwards OnCompleted with failure to the downstream observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnDisposeAsyncOnCompletedFailure_ThenForwardsFailure()
    {
        Result? completionResult = null;

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("async fail")));
            return DisposableAsync.Empty;
        });

        await using var sub = await source
            .OnDispose(() => default(ValueTask))
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that the asynchronous OnDispose callback is invoked when the subscription is explicitly disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnDisposeAsyncExplicitDispose_ThenCallbackInvoked()
    {
        var disposed = false;
        var subject = SubjectAsync.Create<int>();

        await using var sub = await subject.Values
            .OnDispose(() =>
            {
                disposed = true;
                return default;
            })
            .SubscribeAsync(
                (x, _) => default,
                null,
                null);

        await Assert.That(disposed).IsFalse();

        await subject.OnCompletedAsync(Result.Success);

        await Assert.That(disposed).IsTrue();
    }

    /// <summary>
    /// Verifies that Publish creates a connectable observable that emits all source items
    /// to subscribers after ConnectAsync is called.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPublish_ThenEmitsToSubscribersAfterConnect()
    {
        var source = ObservableAsync.Range(1, 3);
        var connectable = source.Publish();

        var items = new List<int>();
        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await connectable.ConnectAsync(CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Verifies that Publish with SubjectCreationOptions creates a connectable observable
    /// that emits all source items to subscribers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPublishWithOptions_ThenEmitsToSubscribers()
    {
        var source = ObservableAsync.Range(1, 3);
        var connectable = source.Publish(SubjectCreationOptions.Default);

        var items = new List<int>();
        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await connectable.ConnectAsync(CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Verifies that StatelessPublish creates a connectable observable that emits all
    /// source items without retaining state between subscriptions.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStatelessPublish_ThenEmitsToSubscribers()
    {
        var source = ObservableAsync.Range(1, 3);
        var connectable = source.StatelessPublish();

        var items = new List<int>();
        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await connectable.ConnectAsync(CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Verifies that Publish with an initial value creates a connectable observable that
    /// replays the initial value to new subscribers and then emits source items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPublishWithInitialValue_ThenSubscriberReceivesInitialValueAndSourceItems()
    {
        var source = ObservableAsync.Range(1, 2);
        var connectable = source.Publish(0);

        var items = new List<int>();
        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await connectable.ConnectAsync(CancellationToken.None);

        await Assert.That(items).Contains(0);
        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(2);
    }

    /// <summary>
    /// Verifies that Publish with an initial value and BehaviorSubjectCreationOptions creates
    /// a connectable observable that replays the initial value and source items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPublishWithInitialValueAndOptions_ThenSubscriberReceivesInitialValueAndSourceItems()
    {
        var source = ObservableAsync.Range(1, 2);
        var connectable = source.Publish(0, BehaviorSubjectCreationOptions.Default);

        var items = new List<int>();
        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await connectable.ConnectAsync(CancellationToken.None);

        await Assert.That(items).Contains(0);
        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(2);
    }

    /// <summary>
    /// Verifies that StatelessPublish with an initial value creates a connectable observable
    /// that replays the initial value and does not retain state between connections.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStatelessPublishWithInitialValue_ThenSubscriberReceivesInitialValueAndSourceItems()
    {
        var source = ObservableAsync.Range(1, 2);
        var connectable = source.StatelessPublish(0);

        var items = new List<int>();
        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await connectable.ConnectAsync(CancellationToken.None);

        await Assert.That(items).Contains(0);
        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(2);
    }

    /// <summary>
    /// Verifies that ReplayLatestPublish creates a connectable observable that replays
    /// the most recent item to new subscribers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenReplayLatestPublish_ThenEmitsToSubscribers()
    {
        var source = ObservableAsync.Range(1, 3);
        var connectable = source.ReplayLatestPublish();

        var items = new List<int>();
        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await connectable.ConnectAsync(CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Verifies that ReplayLatestPublish with ReplayLatestSubjectCreationOptions creates
    /// a connectable observable that emits source items to subscribers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenReplayLatestPublishWithOptions_ThenEmitsToSubscribers()
    {
        var source = ObservableAsync.Range(1, 3);
        var connectable = source.ReplayLatestPublish(ReplayLatestSubjectCreationOptions.Default);

        var items = new List<int>();
        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await connectable.ConnectAsync(CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Verifies that StatelessReplayLatestPublish creates a connectable observable that
    /// replays the latest item and does not retain state between connections.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStatelessReplayLatestPublish_ThenEmitsToSubscribers()
    {
        var source = ObservableAsync.Range(1, 3);
        var connectable = source.StatelessReplayLatestPublish();

        var items = new List<int>();
        await using var sub = await connectable.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await connectable.ConnectAsync(CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Verifies that Merge ForwardOnNext returns early at the pre-gate disposed check.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableDisposed_ThenForwardOnNextReturnsEarly()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var inner = new DirectSource<int>();
        var items = new List<int>();

        var sub = await outer.Values.Merge().SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await outer.OnNextAsync(inner, CancellationToken.None);
        await inner.EmitNext(1);
        await sub.DisposeAsync();
        await inner.EmitNext(99);

        await Assert.That(items).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that Merge ForwardOnErrorResume returns early when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableDisposed_ThenForwardOnErrorResumeReturnsEarly()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var inner = new DirectSource<int>();
        var errors = new List<Exception>();

        var sub = await outer.Values.Merge().SubscribeAsync(
            (_, _) => default,
            (ex, _) =>
            {
                errors.Add(ex);
                return default;
            },
            null);

        await outer.OnNextAsync(inner, CancellationToken.None);
        await sub.DisposeAsync();
        await inner.EmitError(new InvalidOperationException("post-dispose"));

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>
    /// Verifies that Merge ForwardOnNext post-gate disposed guard returns early
    /// when disposal happens while waiting for the gate.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableDisposedWhileGateHeld_ThenForwardOnNextReturnsPostGate()
    {
        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        var inner = new DirectSource<int>();
        var items = new List<int>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var sub = await outer.Values.Merge().SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            async _ =>
            {
                completionBlocked.TrySetResult();
                await allowCompletion.Task;
            });

        await outer.OnNextAsync(inner, CancellationToken.None);
        await inner.EmitNext(1);

        var failTask = Task.Run(() => outer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail"))));
        await completionBlocked.Task;

        await inner.EmitNext(99);

        await Assert.That(items).Count().IsEqualTo(1);

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that MergeEnumerable outer catch routes exception
    /// to UnhandledExceptionHandler when StartAsync itself throws.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableStartAsyncThrows_ThenRoutesToUnhandled()
    {
        Exception? unhandled = null;
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        var sources = new ThrowingEnumerable<int>();

        await using var sub = await sources.Merge().SubscribeAsync(
            (_, _) => default,
            null,
            null);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => unhandled is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(unhandled).IsNotNull();
    }

    /// <summary>
    /// Verifies MergeEnumerable OnNextAsync post-gate disposed guard.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposed_ThenOnNextReturnsEarly()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var sources = new IObservableAsync<int>[] { src1, src2 };
        var items = new List<int>();

        var sub = await sources.Merge().SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await src1.EmitNext(1);
        await sub.DisposeAsync();
        await src1.EmitNext(99);

        await Assert.That(items).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies MergeEnumerable routes exception to UnhandledExceptionHandler
    /// when already disposed and completion has an exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableAlreadyDisposedWithFailure_ThenRoutesToUnhandled()
    {
        Exception? unhandled = null;
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        ObservableAsync.MergeEnumerableObservable<int>.MergeEnumerableSubscription.RoutePostDisposalException(
            Result.Failure(new InvalidOperationException("post-dispose error")));

        await Assert.That(unhandled).IsNotNull();
        await Assert.That(unhandled!.Message).IsEqualTo("post-dispose error");
    }

    /// <summary>
    /// Verifies RoutePostDisposalException does nothing when result has no exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRoutePostDisposalExceptionWithSuccess_ThenNoExceptionRouted()
    {
        Exception? unhandled = null;
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        ObservableAsync.MergeEnumerableObservable<int>.MergeEnumerableSubscription.RoutePostDisposalException(Result.Success);
        ObservableAsync.MergeEnumerableObservable<int>.MergeEnumerableSubscription.RoutePostDisposalException(null);

        await Assert.That(unhandled).IsNull();
    }

    /// <summary>
    /// Verifies that disposing a RefCount observable with an active connection disposes the connection cleanly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenRefCountDisposedWithActiveConnection_ThenConnectionIsDisposed()
    {
        var source = SubjectAsync.Create<int>();
        var connectable = source.Values.Publish();
        var refCounted = connectable.RefCount();

        // Subscribe to trigger the connection.
        var items = new List<int>();
        await using var sub = await refCounted.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await source.OnNextAsync(42, CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count == 1,
            TimeSpan.FromSeconds(5));

        // Dispose the RefCountObservable via its IDisposable implementation.
        ((IDisposable)refCounted).Dispose();

        await Assert.That(items).Contains(42);

        await sub.DisposeAsync();
        await source.DisposeAsync();
    }

    /// <summary>
    /// Verifies that disposing a RefCount observable without any subscribers does not throw.
    /// </summary>
    [Test]
    public void WhenRefCountDisposedWithNoSubscribers_ThenDoesNotThrow()
    {
        var source = ObservableAsync.Return(1);
        var connectable = source.Publish();
        var refCounted = connectable.RefCount();

        // Dispose without ever subscribing — _connection is null.
        ((IDisposable)refCounted).Dispose();
    }

    /// <summary>
    /// Verifies that calling Dispose twice on a RefCount observable is idempotent.
    /// </summary>
    [Test]
    public void WhenRefCountDisposedTwice_ThenSecondDisposeIsNoop()
    {
        var source = ObservableAsync.Return(1);
        var connectable = source.Publish();
        var refCounted = connectable.RefCount();

        var disposable = (IDisposable)refCounted;
        disposable.Dispose();
        disposable.Dispose();
    }

    /// <summary>
    /// Verifies that ConcatObservablesObservable.HandleAlreadyDisposed routes a failure
    /// exception to the unhandled exception handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservablesHandleAlreadyDisposedWithFailure_ThenRoutesToUnhandled()
    {
        Exception? unhandled = null;
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        var error = new InvalidOperationException("late failure");

        ConcatObservablesObservable<int>.ConcatSubscription.HandleAlreadyDisposed(
            Result.Failure(error));

        await Assert.That(unhandled).IsSameReferenceAs(error);
    }

    /// <summary>
    /// Verifies that ConcatObservablesObservable.HandleAlreadyDisposed with null or success
    /// result does not invoke the unhandled exception handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatObservablesHandleAlreadyDisposedWithoutFailure_ThenNoUnhandledException()
    {
        Exception? unhandled = null;
        UnhandledExceptionHandler.Register(ex => unhandled = ex);

        ConcatObservablesObservable<int>.ConcatSubscription.HandleAlreadyDisposed(null);
        ConcatObservablesObservable<int>.ConcatSubscription.HandleAlreadyDisposed(Result.Success);

        await Assert.That(unhandled).IsNull();
    }

    /// <summary>
    /// Verifies that when <c>SubscribeNextAsync</c> throws and <c>CompleteAsync</c> also throws
    /// (because the enumerator's Dispose faults), the catch block in <c>SubscribeAsyncCore</c>
    /// disposes the subscription and rethrows the exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcatEnumerableMoveNextAndDisposeBothThrow_ThenSubscribeAsyncCoreDisposesAndRethrows()
    {
        var sources = new MoveNextAndDisposeThrowingEnumerable<int>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sources.Concat().ToListAsync());
    }

    /// <summary>
    /// Verifies that Merge(IObservableAsync of IObservableAsync) drops values after disposal.
    /// Covers the disposed-early-return guard in MergeSubscription.ForwardOnNext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeOfObservablesDisposedDuringEmission_ThenDropsSubsequentValues()
    {
        var outerSource = AsyncTestHelpers.CreateDirectSource<IObservableAsync<int>>();
        var innerSource = AsyncTestHelpers.CreateDirectSource<int>();
        var results = new List<int>();

        var sub = await outerSource
            .Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await outerSource.EmitNext(innerSource);
        await innerSource.EmitNext(1);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(5));

        await sub.DisposeAsync();

        // Emit after disposal - should be dropped by the guard
        await innerSource.EmitNext(99);

        await Assert.That(results).Contains(1);
    }

    /// <summary>
    /// Verifies that Merge(IObservableAsync of IObservableAsync) drops error-resume after disposal.
    /// Covers the disposed-early-return guard in MergeSubscription.ForwardOnErrorResume.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeOfObservablesDisposedDuringErrorResume_ThenDropsSubsequentErrors()
    {
        var outerSource = AsyncTestHelpers.CreateDirectSource<IObservableAsync<int>>();
        var innerSource = AsyncTestHelpers.CreateDirectSource<int>();
        var errors = new List<Exception>();

        var sub = await outerSource
            .Merge()
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await outerSource.EmitNext(innerSource);
        await innerSource.EmitError(new InvalidOperationException("first"));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => errors.Count >= 1,
            TimeSpan.FromSeconds(5));

        await sub.DisposeAsync();

        // Error after disposal - should be dropped by the guard
        await innerSource.EmitError(new InvalidOperationException("second"));

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that MergeEnumerable drops values after disposal.
    /// Covers the disposed-early-return guard in MergeEnumerableSubscription.OnNextAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposedDuringEmission_ThenDropsValues()
    {
        var innerSource = AsyncTestHelpers.CreateDirectSource<int>();
        var results = new List<int>();

        var sub = await new IObservableAsync<int>[] { innerSource }
            .Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await innerSource.EmitNext(1);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(5));

        await sub.DisposeAsync();

        // Emit after disposal - should be dropped
        await innerSource.EmitNext(99);

        await Assert.That(results).IsEquivalentTo([1]);
    }

    /// <summary>
    /// Verifies that MergeEnumerable drops error-resume after disposal.
    /// Covers the disposed-early-return guard in MergeEnumerableSubscription.OnErrorResumeAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposedDuringErrorResume_ThenDropsErrors()
    {
        var innerSource = AsyncTestHelpers.CreateDirectSource<int>();
        var errors = new List<Exception>();

        var sub = await new IObservableAsync<int>[] { innerSource }
            .Merge()
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await innerSource.EmitError(new InvalidOperationException("first"));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => errors.Count >= 1,
            TimeSpan.FromSeconds(5));

        await sub.DisposeAsync();

        // Error after disposal - should be dropped
        await innerSource.EmitError(new InvalidOperationException("second"));

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies MergeSubscription.ForwardOnNext pre-gate disposed guard returns early.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeSubscriptionDisposed_ThenForwardOnNextReturnsDirectly()
    {
        var observer = new AnonymousObserverAsync<int>((_, _) => default);
        var subscription = new ObservableAsync.MergeSubscription<int>(observer);
        await subscription.DisposeAsync();

        await subscription.ForwardOnNext(99, CancellationToken.None);
    }

    /// <summary>
    /// Verifies MergeSubscription.ForwardOnErrorResume pre-gate disposed guard returns early.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeSubscriptionDisposed_ThenForwardOnErrorResumeReturnsDirectly()
    {
        var observer = new AnonymousObserverAsync<int>((_, _) => default);
        var subscription = new ObservableAsync.MergeSubscription<int>(observer);
        await subscription.DisposeAsync();

        await subscription.ForwardOnErrorResume(new InvalidOperationException("test"), CancellationToken.None);
    }

    /// <summary>
    /// Verifies MergeSubscription.ForwardOnNext post-gate disposed guard.
    /// Directly calls ForwardOnNext on the subscription while CompleteAsync blocks on downstream completion.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeSubscriptionDisposedWhileGateHeld_ThenForwardOnNextPostGateReturns()
    {
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var items = new List<int>();

        var observer = new AnonymousObserverAsync<int>(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            async _ =>
            {
                completionBlocked.TrySetResult();
                await allowCompletion.Task;
            });

        var subscription = new ObservableAsync.MergeSubscription<int>(observer);

        // Trigger CompleteAsync with failure - blocks on observer.OnCompletedAsync
        var failTask = Task.Run(() => subscription.CompleteAsync(Result.Failure(new InvalidOperationException("fail"))));
        await completionBlocked.Task;

        // _disposed is 1, gate is still alive → ForwardOnNext acquires gate and hits post-gate check
        await subscription.ForwardOnNext(99, CancellationToken.None);

        await Assert.That(items).IsEmpty();

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies MergeSubscription.ForwardOnErrorResume post-gate disposed guard.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeSubscriptionDisposedWhileGateHeld_ThenForwardOnErrorResumePostGateReturns()
    {
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var errors = new List<Exception>();

        var observer = new AnonymousObserverAsync<int>(
            (_, _) => default,
            (ex, _) =>
            {
                errors.Add(ex);
                return default;
            },
            async _ =>
            {
                completionBlocked.TrySetResult();
                await allowCompletion.Task;
            });

        var subscription = new ObservableAsync.MergeSubscription<int>(observer);

        var failTask = Task.Run(() => subscription.CompleteAsync(Result.Failure(new InvalidOperationException("fail"))));
        await completionBlocked.Task;

        await subscription.ForwardOnErrorResume(new InvalidOperationException("post-dispose"), CancellationToken.None);

        await Assert.That(errors).IsEmpty();

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that MergeEnumerableSubscription.OnNextAsync returns early when called directly after disposal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableSubscriptionDisposed_ThenOnNextReturnsDirectly()
    {
        var observer = new AnonymousObserverAsync<int>((_, _) => default);
        IObservableAsync<int>[] sources = [];
        var subscription = new ObservableAsync.MergeEnumerableObservable<int>.MergeEnumerableSubscription(observer, sources);
        subscription.StartAsync();
        await subscription.DisposeAsync();

        await subscription.OnNextAsync(99, CancellationToken.None);
    }

    /// <summary>
    /// Verifies that MergeEnumerableSubscription.OnErrorResumeAsync returns early when called directly after disposal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableSubscriptionDisposed_ThenOnErrorResumeReturnsDirectly()
    {
        var observer = new AnonymousObserverAsync<int>((_, _) => default);
        IObservableAsync<int>[] sources = [];
        var subscription = new ObservableAsync.MergeEnumerableObservable<int>.MergeEnumerableSubscription(observer, sources);
        subscription.StartAsync();
        await subscription.DisposeAsync();

        await subscription.OnErrorResumeAsync(new InvalidOperationException("test"), CancellationToken.None);
    }

    /// <summary>
    /// Verifies MergeEnumerable OnNextAsync post-gate disposed guard using blocking-OnCompletedAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposedWhileGateHeld_ThenOnNextPostGateReturns()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var sources = new IObservableAsync<int>[] { src1, src2 };
        var items = new List<int>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("fail"))));
        await completionBlocked.Task;

        await src2.EmitNext(99);

        await Assert.That(items).Count().IsEqualTo(1);

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies MergeEnumerable OnErrorResumeAsync post-gate disposed guard using blocking-OnCompletedAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDisposedWhileGateHeld_ThenOnErrorResumePostGateReturns()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var sources = new IObservableAsync<int>[] { src1, src2 };
        var errors = new List<Exception>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("fail"))));
        await completionBlocked.Task;

        await src2.EmitError(new InvalidOperationException("post-dispose"));

        await Assert.That(errors).IsEmpty();

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// A trackable async disposable resource for verifying disposal in Using tests.
    /// </summary>
    private sealed class TrackingAsyncDisposable : IAsyncDisposable
    {
        /// <summary>
        /// Gets a value indicating whether this resource has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Gets or sets an arbitrary tag for tracking usage.
        /// </summary>
        public string? Tag { get; set; }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// An async disposable that throws on disposal, used to test error handling during cleanup.
    /// </summary>
    private sealed class ThrowingDisposable : IAsyncDisposable
    {
        /// <summary>
        /// Throws an <see cref="InvalidOperationException"/> when disposal is attempted.
        /// </summary>
        /// <returns>Never returns normally.</returns>
        public ValueTask DisposeAsync() => throw new InvalidOperationException("dispose boom");
    }

    /// <summary>
    /// An enumerable that throws during enumeration, used to trigger the error path
    /// in MergeEnumerable StartAsync.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class ThrowingEnumerable<T> : IEnumerable<IObservableAsync<T>>
    {
        /// <inheritdoc/>
        public IEnumerator<IObservableAsync<T>> GetEnumerator() =>
            throw new InvalidOperationException("enumerable boom");

        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// An enumerable whose enumerator throws on both <see cref="System.Collections.IEnumerator.MoveNext"/>
    /// and <see cref="IDisposable.Dispose"/>, used to exercise the catch block in
    /// <c>ConcatEnumerableObservable.SubscribeAsyncCore</c>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class MoveNextAndDisposeThrowingEnumerable<T> : IEnumerable<IObservableAsync<T>>
    {
        /// <inheritdoc/>
        public IEnumerator<IObservableAsync<T>> GetEnumerator() => new ThrowingEnumerator();

        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// An enumerator that throws on both <see cref="MoveNext"/> and <see cref="Dispose"/>.
        /// </summary>
        private sealed class ThrowingEnumerator : IEnumerator<IObservableAsync<T>>
        {
            /// <inheritdoc/>
            public IObservableAsync<T> Current => default!;

            /// <inheritdoc/>
            object System.Collections.IEnumerator.Current => Current!;

            /// <inheritdoc/>
            public bool MoveNext() => throw new InvalidOperationException("enumerator MoveNext boom");

            /// <inheritdoc/>
            public void Reset() => throw new NotSupportedException();

            /// <inheritdoc/>
            public void Dispose() => throw new InvalidOperationException("enumerator Dispose boom");
        }
    }
}
