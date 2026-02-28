// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Additional tests targeting uncovered code paths in the Async namespace to boost coverage.
/// Covers: Do, DisposableAsyncMixins, Subject variants, ObserverAsync, Multicast,
/// CombineLatest multi-source, and operator edge cases.
/// </summary>
public class CoverageBoostTests
{
    /// <summary>Tests Do async onNext callback is invoked.</summary>
    [Test]
    public async Task WhenDoAsyncOnNext_ThenCallbackInvoked()
    {
        var sideEffects = new List<int>();

        var result = await ObservableAsync.Range(1, 3)
            .Do(
                onNext: (x, ct) =>
                {
                    sideEffects.Add(x);
                    return default;
                })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
        await Assert.That(sideEffects).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests Do async with all callbacks invoked.</summary>
    [Test]
    public async Task WhenDoAsyncAllCallbacks_ThenAllInvoked()
    {
        var nextItems = new List<int>();
        Result? completedResult = null;

        await ObservableAsync.Range(1, 2)
            .Do(
                onNext: (x, ct) =>
                {
                    nextItems.Add(x);
                    return default;
                },
                onCompleted: r =>
                {
                    completedResult = r;
                    return default;
                })
            .WaitCompletionAsync();

        await Assert.That(nextItems).IsEquivalentTo(new[] { 1, 2 });
        await Assert.That(completedResult).IsNotNull();
    }

    /// <summary>Tests Do async onError callback is invoked on error.</summary>
    [Test]
    public async Task WhenDoAsyncOnError_ThenErrorCallbackInvoked()
    {
        var errors = new List<Exception>();

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("test"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source
            .Do(
                onNext: null,
                onErrorResume: (ex, ct) =>
                {
                    errors.Add(ex);
                    return default;
                })
            .SubscribeAsync(
                (x, _) => default,
                null,
                null);

        await Task.Delay(100);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests Do sync onNext callback is invoked.</summary>
    [Test]
    public async Task WhenDoSyncOnNext_ThenCallbackInvoked()
    {
        var sideEffects = new List<int>();

        var result = await ObservableAsync.Range(1, 3)
            .Do(onNext: x => sideEffects.Add(x))
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
        await Assert.That(sideEffects).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests Do sync with all callbacks invoked.</summary>
    [Test]
    public async Task WhenDoSyncAllCallbacks_ThenAllInvoked()
    {
        var nextItems = new List<int>();
        Result? completedResult = null;

        await ObservableAsync.Range(1, 2)
            .Do(
                onNext: x => nextItems.Add(x),
                onCompleted: r => completedResult = r)
            .WaitCompletionAsync();

        await Assert.That(nextItems).IsEquivalentTo(new[] { 1, 2 });
        await Assert.That(completedResult).IsNotNull();
    }

    /// <summary>Tests Do sync onError callback is invoked on error.</summary>
    [Test]
    public async Task WhenDoSyncOnError_ThenErrorCallbackInvoked()
    {
        var errors = new List<Exception>();

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("test"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source
            .Do(onNext: null, onErrorResume: ex => errors.Add(ex))
            .SubscribeAsync(
                (x, _) => default,
                null,
                null);

        await Task.Delay(100);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests DisposableAsyncMixins ToDisposableAsync wraps IDisposable correctly.</summary>
    [Test]
    public async Task WhenToDisposableAsync_ThenWrapsIDisposable()
    {
        var disposed = false;
        var syncDisposable = new TestSyncDisposable(() => disposed = true);

        var asyncDisposable = syncDisposable.ToDisposableAsync();

        await Assert.That(disposed).IsFalse();
        await asyncDisposable.DisposeAsync();
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>Tests SubjectAsync with concurrent option works.</summary>
    [Test]
    public async Task WhenSubjectAsyncConcurrent_ThenEmitsToAllSubscribers()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = false,
        };
        var subject = SubjectAsync.Create<int>(options);
        var items = new List<int>();

        await using var sub = await subject.Values.SubscribeAsync(
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
        await subject.OnNextAsync(2, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);
        await Task.Delay(100);

        await Assert.That(items).IsEquivalentTo(new[] { 1, 2 });
    }

    /// <summary>Tests SubjectAsync with serial stateless option works.</summary>
    [Test]
    public async Task WhenSubjectAsyncSerialStateless_ThenEmitsValues()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true,
        };
        var subject = SubjectAsync.Create<int>(options);
        var items = new List<int>();

        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await subject.OnNextAsync(10, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);
        await Task.Delay(50);

        await Assert.That(items).IsEquivalentTo(new[] { 10 });
    }

    /// <summary>Tests SubjectAsync with concurrent stateless option works.</summary>
    [Test]
    public async Task WhenSubjectAsyncConcurrentStateless_ThenEmitsValues()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = true,
        };
        var subject = SubjectAsync.Create<int>(options);
        var items = new List<int>();

        await using var sub = await subject.Values.SubscribeAsync(
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

        await subject.OnNextAsync(5, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);
        await Task.Delay(50);

        await Assert.That(items).IsEquivalentTo(new[] { 5 });
    }

    /// <summary>Tests Behavior subject replays initial value to late subscriber.</summary>
    [Test]
    public async Task WhenBehaviorSubject_ThenReplaysInitialValue()
    {
        var subject = SubjectAsync.CreateBehavior(42);
        var items = new List<int>();

        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await Task.Delay(100);

        await Assert.That(items).Contains(42);
    }

    /// <summary>Tests Behavior subject with concurrent options works.</summary>
    [Test]
    public async Task WhenBehaviorSubjectConcurrent_ThenReplaysInitialValue()
    {
        var options = new BehaviorSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = false,
        };
        var subject = SubjectAsync.CreateBehavior(99, options);
        var items = new List<int>();

        await using var sub = await subject.Values.SubscribeAsync(
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

        await Task.Delay(100);

        await Assert.That(items).Contains(99);
    }

    /// <summary>Tests Behavior subject with serial stateless options works.</summary>
    [Test]
    public async Task WhenBehaviorSubjectSerialStateless_ThenReplaysInitialValue()
    {
        var options = new BehaviorSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true,
        };
        var subject = SubjectAsync.CreateBehavior(7, options);
        var items = new List<int>();

        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await Task.Delay(100);

        await Assert.That(items).Contains(7);
    }

    /// <summary>Tests Behavior subject with concurrent stateless options works.</summary>
    [Test]
    public async Task WhenBehaviorSubjectConcurrentStateless_ThenReplaysInitialValue()
    {
        var options = new BehaviorSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = true,
        };
        var subject = SubjectAsync.CreateBehavior(3, options);
        var items = new List<int>();

        await using var sub = await subject.Values.SubscribeAsync(
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

        await Task.Delay(100);

        await Assert.That(items).Contains(3);
    }

    /// <summary>Tests ReplayLatest subject replays last emitted value.</summary>
    [Test]
    public async Task WhenReplayLatestSubject_ThenReplaysLastValue()
    {
        var subject = SubjectAsync.CreateReplayLatest<int>();

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(2, CancellationToken.None);

        var items = new List<int>();
        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await Task.Delay(100);

        await Assert.That(items).Contains(2);
    }

    /// <summary>Tests ReplayLatest with concurrent options works.</summary>
    [Test]
    public async Task WhenReplayLatestConcurrent_ThenReplaysLastValue()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = false,
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);

        await subject.OnNextAsync(10, CancellationToken.None);

        var items = new List<int>();
        await using var sub = await subject.Values.SubscribeAsync(
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

        await Task.Delay(100);

        await Assert.That(items).Contains(10);
    }

    /// <summary>Tests ReplayLatest with serial stateless options works.</summary>
    [Test]
    public async Task WhenReplayLatestSerialStateless_ThenReplaysLastValue()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true,
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);

        await subject.OnNextAsync(20, CancellationToken.None);

        var items = new List<int>();
        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await Task.Delay(100);

        await Assert.That(items).Contains(20);
    }

    /// <summary>Tests ReplayLatest with concurrent stateless options works.</summary>
    [Test]
    public async Task WhenReplayLatestConcurrentStateless_ThenReplaysLastValue()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = true,
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);

        await subject.OnNextAsync(30, CancellationToken.None);

        var items = new List<int>();
        await using var sub = await subject.Values.SubscribeAsync(
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

        await Task.Delay(100);

        await Assert.That(items).Contains(30);
    }

    /// <summary>Tests CombineLatest with 3 sources combines all latest values.</summary>
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
        await Task.Delay(100);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(111);
    }

    /// <summary>Tests Multicast with ConnectAsync connects and emits.</summary>
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
        await Task.Delay(200);

        await Assert.That(items).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests CompositeDisposableAsync Clear disposes and removes all.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncClear_ThenDisposesAndRemovesAll()
    {
        var count = 0;
        var composite = new CompositeDisposableAsync();

        for (var i = 0; i < 3; i++)
        {
            await composite.AddAsync(DisposableAsync.Create(() =>
            {
                Interlocked.Increment(ref count);
                return default;
            }));
        }

        await Assert.That(composite.Count).IsEqualTo(3);

        await composite.Clear();

        await Assert.That(composite.Count).IsEqualTo(0);
        await Assert.That(count).IsEqualTo(3);
    }

    /// <summary>Tests ObservableAsync.Defer defers subscription.</summary>
    [Test]
    public async Task WhenDefer_ThenFactoryCalledOnSubscription()
    {
        var factoryCallCount = 0;

        var deferred = ObservableAsync.Defer(() =>
        {
            factoryCallCount++;
            return ObservableAsync.Return(42);
        });

        await Assert.That(factoryCallCount).IsEqualTo(0);

        var result1 = await deferred.FirstAsync();
        await Assert.That(factoryCallCount).IsEqualTo(1);

        var result2 = await deferred.FirstAsync();
        await Assert.That(factoryCallCount).IsEqualTo(2);

        await Assert.That(result1).IsEqualTo(42);
        await Assert.That(result2).IsEqualTo(42);
    }

    /// <summary>Tests ObservableAsync.Defer async defers subscription.</summary>
    [Test]
    public async Task WhenDeferAsync_ThenFactoryCalledOnSubscription()
    {
        var factoryCallCount = 0;

        var deferred = ObservableAsync.Defer(async ct =>
        {
            await Task.Yield();
            factoryCallCount++;
            return ObservableAsync.Return(99);
        });

        var result = await deferred.FirstAsync();
        await Assert.That(factoryCallCount).IsEqualTo(1);
        await Assert.That(result).IsEqualTo(99);
    }

    /// <summary>Tests ObservableAsync.FromAsync wraps task factory.</summary>
    [Test]
    public async Task WhenFromAsyncWithResult_ThenReturnsResult()
    {
        var source = ObservableAsync.FromAsync(async ct =>
        {
            await Task.Yield();
            return 42;
        });

        var result = await source.FirstAsync();
        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>Tests ObservableAsync.FromAsync void wraps void task factory.</summary>
    [Test]
    public async Task WhenFromAsyncVoid_ThenCompletes()
    {
        var executed = false;
        var source = ObservableAsync.FromAsync(async ct =>
        {
            await Task.Yield();
            executed = true;
        });

        await source.WaitCompletionAsync();
        await Assert.That(executed).IsTrue();
    }

    /// <summary>Tests OfType filters by type correctly.</summary>
    [Test]
    public async Task WhenOfType_ThenFiltersCorrectly()
    {
        var source = new object[] { "hello", 1, "world", 2, "!" }.ToObservableAsync();
        var result = await source.OfType<object, string>().ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "hello", "world", "!" });
    }

    /// <summary>Tests OfType with exception type.</summary>
    [Test]
    public async Task WhenOfTypeException_ThenFiltersCorrectly()
    {
        var source = new object[] { new InvalidOperationException("a"), "skip", new ArgumentException("b") }.ToObservableAsync();
        var result = await source.OfType<object, Exception>().ToListAsync();

        await Assert.That(result).Count().IsEqualTo(2);
    }

    /// <summary>Tests Prepend with enumerable prepends values.</summary>
    [Test]
    public async Task WhenPrependEnumerableViaList_ThenPrepends()
    {
        var result = await ObservableAsync.Return(4)
            .Prepend(new List<int> { 1, 2, 3 })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    /// <summary>Tests ObserverAsync completes when observing error source.</summary>
    [Test]
    public async Task WhenSourceThrows_ThenObserverRecordsFailure()
    {
        var source = ObservableAsync.Throw<int>(new InvalidOperationException("fail"));

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.FirstAsync());
    }

    /// <summary>Tests SelectMany flattens nested sequences.</summary>
    [Test]
    public async Task WhenSelectMany_ThenFlattensSequences()
    {
        var result = await ObservableAsync.Range(1, 3)
            .SelectMany(x => new[] { x, x * 10 }.ToObservableAsync())
            .ToListAsync();

        await Assert.That(result).Count().IsEqualTo(6);
        await Assert.That(result).Contains(1);
        await Assert.That(result).Contains(10);
    }

    /// <summary>Tests subject completed prevents further emissions.</summary>
    [Test]
    public async Task WhenSubjectCompleted_ThenNoFurtherEmissions()
    {
        var subject = SubjectAsync.Create<int>();
        var items = new List<int>();

        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);

        await Task.Delay(50);

        await Assert.That(items).IsEquivalentTo(new[] { 1 });
    }

    /// <summary>Tests LongCountAsync counts elements as long.</summary>
    [Test]
    public async Task WhenLongCountAsyncOnEmpty_ThenReturnsZero()
    {
        var result = await ObservableAsync.Empty<string>().LongCountAsync();
        await Assert.That(result).IsEqualTo(0L);
    }

    /// <summary>Tests ForEachAsync with async action processes all.</summary>
    [Test]
    public async Task WhenForEachAsync_ThenProcessesAll()
    {
        var items = new List<int>();
        await ObservableAsync.Range(1, 4).ForEachAsync(x => items.Add(x));

        await Assert.That(items).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    /// <summary>Tests ToDictionaryAsync with element selector.</summary>
    [Test]
    public async Task WhenToDictionaryAsyncWithElementSelector_ThenUsesSelector()
    {
        var source = new[] { "a", "bb", "ccc" }.ToObservableAsync();
        var result = await source.ToDictionaryAsync(s => s.Length, s => s.ToUpperInvariant());

        await Assert.That(result[1]).IsEqualTo("A");
        await Assert.That(result[2]).IsEqualTo("BB");
        await Assert.That(result[3]).IsEqualTo("CCC");
    }

    /// <summary>Tests LastOrDefaultAsync with default value returns default on empty.</summary>
    [Test]
    public async Task WhenLastOrDefaultOnEmpty_ThenReturnsSpecifiedDefault()
    {
        var result = await ObservableAsync.Empty<int>().LastOrDefaultAsync(-1);
        await Assert.That(result).IsEqualTo(-1);
    }

    /// <summary>Tests SingleOrDefaultAsync on empty returns default.</summary>
    [Test]
    public async Task WhenSingleOrDefaultWithDefault_ThenReturnsSpecifiedDefault()
    {
        var result = await ObservableAsync.Empty<string>().SingleOrDefaultAsync("none");
        await Assert.That(result).IsEqualTo("none");
    }

    /// <summary>Tests FirstOrDefaultAsync with explicit default returns default on empty.</summary>
    [Test]
    public async Task WhenFirstOrDefaultWithDefault_ThenReturnsSpecifiedDefault()
    {
        var result = await ObservableAsync.Empty<int>().FirstOrDefaultAsync(-1);
        await Assert.That(result).IsEqualTo(-1);
    }

    /// <summary>
    /// Helper disposable for testing ToDisposableAsync.
    /// </summary>
    private sealed class TestSyncDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
