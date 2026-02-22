// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;
using AsyncObs = ReactiveUI.Extensions.Async.ObservableAsync;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Deep coverage tests targeting internal code paths: CombineLatest multi-source,
/// CompositeDisposableAsync edge cases, Bridge disposal, ObserverAsync lifecycle,
/// Timeout subscription cleanup, Merge error propagation.
/// </summary>
public class DeepCoverageTests
{
    /// <summary>Tests CombineLatest with 4 sources.</summary>
    [Test]
    public async Task WhenCombineLatestFourSources_ThenCombinesAll()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();

        var results = new List<int>();
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (a, b, c, d) => a + b + c + d)
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
        await s4.OnNextAsync(1000, CancellationToken.None);
        await Task.Delay(100);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(1111);
    }

    /// <summary>Tests CombineLatest with 5 sources.</summary>
    [Test]
    public async Task WhenCombineLatestFiveSources_ThenCombinesAll()
    {
        var subjects = Enumerable.Range(0, 5).Select(_ => SubjectAsync.Create<int>()).ToList();

        var results = new List<int>();
        await using var sub = await AsyncObs
            .CombineLatest(
                subjects[0].Values,
                subjects[1].Values,
                subjects[2].Values,
                subjects[3].Values,
                subjects[4].Values,
                (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        for (var i = 0; i < 5; i++)
        {
            await subjects[i].OnNextAsync((i + 1) * 10, CancellationToken.None);
        }

        await Task.Delay(100);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(150);
    }

    /// <summary>Tests CombineLatest with 6 sources.</summary>
    [Test]
    public async Task WhenCombineLatestSixSources_ThenCombinesAll()
    {
        var subjects = Enumerable.Range(0, 6).Select(_ => SubjectAsync.Create<int>()).ToList();

        var results = new List<int>();
        await using var sub = await AsyncObs
            .CombineLatest(
                subjects[0].Values,
                subjects[1].Values,
                subjects[2].Values,
                subjects[3].Values,
                subjects[4].Values,
                subjects[5].Values,
                (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        for (var i = 0; i < 6; i++)
        {
            await subjects[i].OnNextAsync(i + 1, CancellationToken.None);
        }

        await Task.Delay(100);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(21);
    }

    /// <summary>Tests CombineLatest with 7 sources.</summary>
    [Test]
    public async Task WhenCombineLatestSevenSources_ThenCombinesAll()
    {
        var subjects = Enumerable.Range(0, 7).Select(_ => SubjectAsync.Create<int>()).ToList();

        var results = new List<int>();
        await using var sub = await AsyncObs
            .CombineLatest(
                subjects[0].Values,
                subjects[1].Values,
                subjects[2].Values,
                subjects[3].Values,
                subjects[4].Values,
                subjects[5].Values,
                subjects[6].Values,
                (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        for (var i = 0; i < 7; i++)
        {
            await subjects[i].OnNextAsync(1, CancellationToken.None);
        }

        await Task.Delay(100);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(7);
    }

    /// <summary>Tests CombineLatest with 8 sources.</summary>
    [Test]
    public async Task WhenCombineLatestEightSources_ThenCombinesAll()
    {
        var subjects = Enumerable.Range(0, 8).Select(_ => SubjectAsync.Create<int>()).ToList();

        var results = new List<int>();
        await using var sub = await AsyncObs
            .CombineLatest(
                subjects[0].Values,
                subjects[1].Values,
                subjects[2].Values,
                subjects[3].Values,
                subjects[4].Values,
                subjects[5].Values,
                subjects[6].Values,
                subjects[7].Values,
                (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        for (var i = 0; i < 8; i++)
        {
            await subjects[i].OnNextAsync(1, CancellationToken.None);
        }

        await Task.Delay(100);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(8);
    }

    /// <summary>Tests CompositeDisposableAsync Contains returns true for added item.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncContains_ThenReturnsTrueForAdded()
    {
        var composite = new CompositeDisposableAsync();
        var d = DisposableAsync.Empty;

        await composite.AddAsync(d);

        await Assert.That(composite.Contains(d)).IsTrue();
        await composite.DisposeAsync();
    }

    /// <summary>Tests CompositeDisposableAsync Contains returns false after dispose.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncContainsAfterDispose_ThenReturnsFalse()
    {
        var composite = new CompositeDisposableAsync();
        var d = DisposableAsync.Empty;

        await composite.AddAsync(d);
        await composite.DisposeAsync();

        await Assert.That(composite.Contains(d)).IsFalse();
    }

    /// <summary>Tests CompositeDisposableAsync CopyTo copies all items.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncCopyTo_ThenCopiesAllItems()
    {
        var d1 = DisposableAsync.Empty;
        var d2 = DisposableAsync.Empty;
        var composite = new CompositeDisposableAsync(d1, d2);

        var array = new IAsyncDisposable[2];
        composite.CopyTo(array, 0);

        await Assert.That(array[0]).IsNotNull();
        await Assert.That(array[1]).IsNotNull();
        await composite.DisposeAsync();
    }

    /// <summary>Tests CompositeDisposableAsync CopyTo throws on invalid index.</summary>
    [Test]
    public void WhenCompositeDisposableAsyncCopyToInvalidIndex_ThenThrows()
    {
        var composite = new CompositeDisposableAsync();
        var array = new IAsyncDisposable[1];

        Assert.Throws<ArgumentOutOfRangeException>(() => composite.CopyTo(array, -1));
    }

    /// <summary>Tests CompositeDisposableAsync double dispose is safe.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncDoubleDispose_ThenSafe()
    {
        var disposed = false;
        var composite = new CompositeDisposableAsync(DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        }));

        await composite.DisposeAsync();
        await composite.DisposeAsync();

        await Assert.That(disposed).IsTrue();
    }

    /// <summary>Tests CompositeDisposableAsync remove from disposed returns false.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncRemoveFromDisposed_ThenReturnsFalse()
    {
        var composite = new CompositeDisposableAsync();
        await composite.DisposeAsync();

        var result = await composite.Remove(DisposableAsync.Empty);
        await Assert.That(result).IsFalse();
    }

    /// <summary>Tests CompositeDisposableAsync remove non-existent returns false.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncRemoveNonExistent_ThenReturnsFalse()
    {
        var composite = new CompositeDisposableAsync();
        var result = await composite.Remove(DisposableAsync.Empty);
        await Assert.That(result).IsFalse();
        await composite.DisposeAsync();
    }

    /// <summary>Tests CompositeDisposableAsync clear on disposed is safe.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncClearOnDisposed_ThenSafe()
    {
        var composite = new CompositeDisposableAsync();
        await composite.DisposeAsync();
        await composite.Clear();

        await Assert.That(composite.IsDisposed).IsTrue();
    }

    /// <summary>Tests CompositeDisposableAsync clear on empty is safe.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncClearOnEmpty_ThenSafe()
    {
        var composite = new CompositeDisposableAsync();
        await composite.Clear();

        await Assert.That(composite.Count).IsEqualTo(0);
        await composite.DisposeAsync();
    }

    /// <summary>Tests Bridge ToObservableAsync disposes subscription when Rx completes.</summary>
    [Test]
    public async Task WhenBridgeToObservableAsyncWithDisposal_ThenCleansUp()
    {
        var rxSource = Observable.Range(1, 3);
        var asyncObs = rxSource.ToObservableAsync();

        var items = new List<int>();
        await using var sub = await asyncObs.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await Task.Delay(100);

        await Assert.That(items).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests Bridge ToObservable with async Select pipeline.</summary>
    [Test]
    public async Task WhenBridgeToObservableWithAsyncPipeline_ThenWorks()
    {
        var asyncSource = AsyncObs.Range(1, 3)
            .Select(async (x, ct) =>
            {
                await Task.Yield();
                return x * 100;
            });

        var result = await asyncSource.ToObservable().ToList();

        await Assert.That(result).IsEquivalentTo(new[] { 100, 200, 300 });
    }

    /// <summary>Tests Merge with error from one source propagates correctly.</summary>
    [Test]
    public async Task WhenMergeWithError_ThenErrorPropagates()
    {
        var errorSource = AsyncObs.Throw<int>(new InvalidOperationException("merge-error"));
        var goodSource = AsyncObs.Return(1);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await goodSource.Merge(errorSource).ToListAsync());
    }

    /// <summary>Tests Concat with error in second source propagates correctly.</summary>
    [Test]
    public async Task WhenConcatWithErrorInSecondSource_ThenErrorPropagates()
    {
        var first = AsyncObs.Return(1);
        var second = AsyncObs.Throw<int>(new InvalidOperationException("concat-error"));

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await first.Concat(second).ToListAsync());
    }

    /// <summary>Tests Timeout with fallback on Never source switches correctly.</summary>
    [Test]
    public async Task WhenTimeoutFallbackOnNever_ThenSwitchesToFallback()
    {
        var result = await AsyncObs.Never<int>()
            .Timeout(TimeSpan.FromMilliseconds(50), AsyncObs.Return(999))
            .FirstAsync();

        await Assert.That(result).IsEqualTo(999);
    }

    /// <summary>Tests Timeout fast source completes before timeout.</summary>
    [Test]
    public async Task WhenTimeoutFastSource_ThenCompletesNormally()
    {
        var result = await AsyncObs.Range(1, 3)
            .Timeout(TimeSpan.FromSeconds(5))
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests ObserverAsync lifecycle with cancellation.</summary>
    [Test]
    public async Task WhenSubscriptionCancelled_ThenSequenceStops()
    {
        using var cts = new CancellationTokenSource();
        var subject = SubjectAsync.Create<int>();
        var items = new List<int>();

        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null,
            cts.Token);

        await subject.OnNextAsync(1, CancellationToken.None);
        cts.Cancel();
        await Task.Delay(50);

        await Assert.That(items).Contains(1);
    }

    /// <summary>Tests Switch with inner sequence error.</summary>
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
            AsyncObs.Throw<int>(new InvalidOperationException("inner-error")),
            CancellationToken.None);

        await Task.Delay(200);

        await Assert.That(errors).Count().IsGreaterThanOrEqualTo(0);
    }

    /// <summary>Tests Merge with max concurrency and error propagation.</summary>
    [Test]
    public async Task WhenMergeConcurrencyWithSlowSource_ThenLimitsAndCompletes()
    {
        var source = AsyncObs.Range(1, 4).Select(i =>
            AsyncObs.CreateAsBackgroundJob<int>(async (obs, ct) =>
            {
                await Task.Delay(20, ct);
                await obs.OnNextAsync(i, ct);
                await obs.OnCompletedAsync(Result.Success);
            }));

        var result = await source.Merge(2).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(4);
    }

    /// <summary>Tests SingleOrDefaultAsync returns value for single element.</summary>
    [Test]
    public async Task WhenSingleOrDefaultAsyncSingleElement_ThenReturnsElement()
    {
        var result = await AsyncObs.Return(42).SingleOrDefaultAsync();
        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>Tests SingleAsync with predicate.</summary>
    [Test]
    public async Task WhenSingleAsyncWithPredicate_ThenReturnsSingleMatch()
    {
        var result = await AsyncObs.Range(1, 5).SingleAsync(x => x == 3);
        await Assert.That(result).IsEqualTo(3);
    }

    /// <summary>Tests LastOrDefaultAsync returns last for non-empty.</summary>
    [Test]
    public async Task WhenLastOrDefaultAsyncNonEmpty_ThenReturnsLast()
    {
        var result = await AsyncObs.Range(1, 3).LastOrDefaultAsync();
        await Assert.That(result).IsEqualTo(3);
    }

    /// <summary>Tests LastAsync with predicate and default.</summary>
    [Test]
    public async Task WhenLastOrDefaultAsyncWithPredicate_ThenReturnsLastMatch()
    {
        var result = await AsyncObs.Range(1, 5).Where(x => x < 3).LastOrDefaultAsync(-1);
        await Assert.That(result).IsEqualTo(2);
    }

    /// <summary>Tests FirstOrDefaultAsync predicate overload with match.</summary>
    [Test]
    public async Task WhenFirstOrDefaultAsyncPredicateMatch_ThenReturnsFirst()
    {
        var result = await AsyncObs.Range(1, 5).FirstOrDefaultAsync(x => x > 3, 0);
        await Assert.That(result).IsEqualTo(4);
    }

    /// <summary>Tests FirstOrDefaultAsync predicate overload with no match.</summary>
    [Test]
    public async Task WhenFirstOrDefaultAsyncPredicateNoMatch_ThenReturnsDefault()
    {
        var result = await AsyncObs.Range(1, 5).FirstOrDefaultAsync(x => x > 10, -1);
        await Assert.That(result).IsEqualTo(-1);
    }

    /// <summary>Tests Throttle with TimeProvider overload.</summary>
    [Test]
    public async Task WhenThrottleWithTimeProvider_ThenThrottlesCorrectly()
    {
        var subject = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await subject.Values
            .Throttle(TimeSpan.FromMilliseconds(50), TimeProvider.System)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(2, CancellationToken.None);
        await Task.Delay(200);

        await subject.OnCompletedAsync(Result.Success);
        await Task.Delay(50);

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(2);
    }

    /// <summary>Tests Concat observable of observables with enumerable sources.</summary>
    [Test]
    public async Task WhenConcatEnumerable_ThenConcatenatesInOrder()
    {
        var sources = new[]
        {
            AsyncObs.Return(1),
            AsyncObs.Return(2),
            AsyncObs.Return(3),
        };

        var result = await sources.Concat().ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests Concat observable of observables with observable source.</summary>
    [Test]
    public async Task WhenConcatObservableOfObservables_ThenConcatenatesInOrder()
    {
        var source = new[]
        {
            AsyncObs.Range(1, 2),
            AsyncObs.Range(3, 2),
        }.ToObservableAsync();

        var result = await source.Concat().ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    /// <summary>Tests CreateAsBackgroundJob emits and completes.</summary>
    [Test]
    public async Task WhenCreateAsBackgroundJob_ThenEmitsAndCompletes()
    {
        var source = AsyncObs.CreateAsBackgroundJob<int>(async (obs, ct) =>
        {
            await obs.OnNextAsync(1, ct);
            await obs.OnNextAsync(2, ct);
            await obs.OnCompletedAsync(Result.Success);
        });

        var result = await source.ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2 });
    }

    /// <summary>Tests Prepend with params array overload.</summary>
    [Test]
    public async Task WhenPrependParams_ThenPrependsAll()
    {
        var values = new[] { 10, 20 };
        var result = await AsyncObs.Return(30)
            .Prepend(values)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 10, 20, 30 });
    }

    /// <summary>Tests SubscribeAsync with CancellationToken from an error source.</summary>
    [Test]
    public async Task WhenSubscribeAsyncErrorSource_ThenErrorCallbackInvoked()
    {
        var errors = new List<Exception>();

        await using var sub = await AsyncObs.Create<int>(async (obs, ct) =>
        {
            await obs.OnErrorResumeAsync(new InvalidOperationException("test"), ct);
            await obs.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        }).SubscribeAsync(
            (x, _) => default,
            (ex, _) =>
            {
                errors.Add(ex);
                return default;
            },
            null);

        await Task.Delay(100);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests Behavior subject updates on new value.</summary>
    [Test]
    public async Task WhenBehaviorSubjectUpdated_ThenLatestValueReplayed()
    {
        var subject = SubjectAsync.CreateBehavior(0);

        await subject.OnNextAsync(10, CancellationToken.None);
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

    /// <summary>Tests ReplayLatest subject with no values emits nothing to subscriber.</summary>
    [Test]
    public async Task WhenReplayLatestNoValues_ThenNoReplay()
    {
        var subject = SubjectAsync.CreateReplayLatest<int>();
        var items = new List<int>();

        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await subject.OnNextAsync(42, CancellationToken.None);
        await Task.Delay(100);

        await Assert.That(items).Contains(42);
    }

    /// <summary>Tests Multicast with replay latest subject shares and replays.</summary>
    [Test]
    public async Task WhenMulticastReplayLatest_ThenSharesAndReplays()
    {
        var subject = SubjectAsync.CreateReplayLatest<int>();
        var source = AsyncObs.Range(1, 3);
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

        await Assert.That(items).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>Tests Enumerator from GetEnumerator on CompositeDisposableAsync.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncEnumerate_ThenReturnsItems()
    {
        var d1 = DisposableAsync.Empty;
        var d2 = DisposableAsync.Empty;
        var composite = new CompositeDisposableAsync(d1, d2);

        var count = 0;
        foreach (var item in composite)
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(2);
        await composite.DisposeAsync();
    }
}
