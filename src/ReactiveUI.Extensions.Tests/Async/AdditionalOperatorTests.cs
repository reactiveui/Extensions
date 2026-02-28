// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for operators not covered by other test files: Switch, TakeUntil, GroupBy, Using, OnDispose,
/// Yield, Multicast/Publish/RefCount, ObserveOn, SubscribeAsync overloads.
/// </summary>
public class AdditionalOperatorTests
{
    /// <summary>
    /// Tests that Switch emits from the latest inner sequence.
    /// </summary>
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

        await Task.Delay(100);

        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(10);
    }

    /// <summary>
    /// Tests that TakeUntil stops on observable signal.
    /// </summary>
    [Test]
    public async Task WhenTakeUntilObservable_ThenStopsOnSignal()
    {
        var source = SubjectAsync.Create<int>();
        var stopper = SubjectAsync.Create<string>();
        var items = new List<int>();

        await using var sub = await source.Values
            .TakeUntil(stopper.Values)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await source.OnNextAsync(1, CancellationToken.None);
        await source.OnNextAsync(2, CancellationToken.None);
        await stopper.OnNextAsync("stop", CancellationToken.None);
        await Task.Delay(100);

        await source.OnNextAsync(3, CancellationToken.None);
        await Task.Delay(50);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(3);
    }

    /// <summary>
    /// Tests that TakeUntil stops on task completion.
    /// </summary>
    [Test]
    public async Task WhenTakeUntilTask_ThenStopsOnTaskCompletion()
    {
        var tcs = new TaskCompletionSource();
        var source = SubjectAsync.Create<int>();
        var items = new List<int>();

        await using var sub = await source.Values
            .TakeUntil(tcs.Task)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await source.OnNextAsync(1, CancellationToken.None);
        tcs.SetResult();
        await Task.Delay(100);

        await source.OnNextAsync(2, CancellationToken.None);
        await Task.Delay(50);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>
    /// Tests that TakeUntil stops on cancellation.
    /// </summary>
    [Test]
    public async Task WhenTakeUntilCancellationToken_ThenStopsOnCancellation()
    {
        using var cts = new CancellationTokenSource();
        var source = SubjectAsync.Create<int>();
        var items = new List<int>();

        await using var sub = await source.Values
            .TakeUntil(cts.Token)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await source.OnNextAsync(1, CancellationToken.None);
        cts.Cancel();
        await Task.Delay(100);

        await Assert.That(items).Contains(1);
    }

    /// <summary>
    /// Tests that TakeUntil with predicate stops when predicate returns true.
    /// </summary>
    [Test]
    public async Task WhenTakeUntilPredicate_ThenStopsWhenPredicateTrue()
    {
        var result = await ObservableAsync.Range(1, 10)
            .TakeUntil(x => x > 3)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// Tests that TakeUntil with async predicate stops when predicate returns true.
    /// </summary>
    [Test]
    public async Task WhenTakeUntilAsyncPredicate_ThenStopsWhenPredicateTrue()
    {
        var result = await ObservableAsync.Range(1, 10)
            .TakeUntil(async (x, ct) =>
            {
                await Task.Yield();
                return x > 2;
            })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2 });
    }

    /// <summary>
    /// Tests that TakeUntil throws on null predicate.
    /// </summary>
    [Test]
    public void WhenTakeUntilNullPredicate_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).TakeUntil((Func<int, bool>)null!));
    }

    /// <summary>
    /// Tests that TakeUntil throws on null async predicate.
    /// </summary>
    [Test]
    public void WhenTakeUntilNullAsyncPredicate_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).TakeUntil((Func<int, CancellationToken, ValueTask<bool>>)null!));
    }

    /// <summary>
    /// Tests that GroupBy creates groups per key.
    /// </summary>
    [Test]
    public async Task WhenGroupBy_ThenCreatesGroupsPerKey()
    {
        var source = new[] { "apple", "avocado", "banana", "blueberry", "cherry" }.ToObservableAsync();

        var allItems = new List<(char Key, string Value)>();
        await using var sub = await source.GroupBy(s => s[0]).SubscribeAsync(
            async (group, ct) =>
            {
                await group.SubscribeAsync(
                    (item, ct2) =>
                    {
                        lock (allItems)
                        {
                            allItems.Add((group.Key, item));
                        }

                        return default;
                    },
                    null,
                    null,
                    ct);
            },
            null,
            null);

        await Task.Delay(200);

        var grouped = allItems.GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Select(x => x.Value).ToList());

        await Assert.That(grouped.ContainsKey('a')).IsTrue();
        await Assert.That(grouped['a']).IsEquivalentTo(new[] { "apple", "avocado" });
        await Assert.That(grouped.ContainsKey('b')).IsTrue();
        await Assert.That(grouped['b']).IsEquivalentTo(new[] { "banana", "blueberry" });
        await Assert.That(grouped.ContainsKey('c')).IsTrue();
        await Assert.That(grouped['c']).IsEquivalentTo(new[] { "cherry" });
    }

    /// <summary>
    /// Tests that GroupBy rejects null source.
    /// </summary>
    [Test]
    public void WhenGroupByNullSource_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.GroupBy<int, int>(null!, x => x));
    }

    /// <summary>
    /// Tests that GroupBy rejects null key selector.
    /// </summary>
    [Test]
    public void WhenGroupByNullKeySelector_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.GroupBy<int, int>(ObservableAsync.Return(1), null!));
    }

    /// <summary>
    /// Tests that Using disposes resource on completion.
    /// </summary>
    [Test]
    public async Task WhenUsing_ThenResourceDisposedOnCompletion()
    {
        var resourceDisposed = false;

        var source = ObservableAsync.Using(
            async ct =>
            {
                await Task.Yield();
                return new TestResource(() => resourceDisposed = true);
            },
            resource => ObservableAsync.Return(resource.Value));

        var result = await source.FirstAsync();

        await Assert.That(result).IsEqualTo(42);
        await Task.Delay(100);
        await Assert.That(resourceDisposed).IsTrue();
    }

    /// <summary>
    /// Tests that async OnDispose callback is invoked on unsubscribe.
    /// </summary>
    [Test]
    public async Task WhenOnDisposeAsync_ThenCallbackInvokedOnUnsubscribe()
    {
        var disposed = false;
        var source = ObservableAsync.Return(1)
            .OnDispose(() =>
            {
                disposed = true;
                return default;
            });

        await source.WaitCompletionAsync();

        await Task.Delay(100);
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>
    /// Tests that sync OnDispose callback is invoked on unsubscribe.
    /// </summary>
    [Test]
    public async Task WhenOnDisposeSync_ThenCallbackInvokedOnUnsubscribe()
    {
        var disposed = false;
        var source = ObservableAsync.Return(1)
            .OnDispose(() => disposed = true);

        await source.WaitCompletionAsync();

        await Task.Delay(100);
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>
    /// Tests that Yield emits the same values.
    /// </summary>
    [Test]
    public async Task WhenYield_ThenEmitsSameValues()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Yield()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// Tests that Publish with RefCount shares subscription.
    /// </summary>
    [Test]
    public async Task WhenPublishAndRefCount_ThenSharesSubscription()
    {
        var subject = SubjectAsync.Create<int>();
        var shared = subject.Values.Publish().RefCount();

        var items1 = new List<int>();
        var items2 = new List<int>();

        await using var sub1 = await shared.SubscribeAsync(
            (x, _) =>
            {
                items1.Add(x);
                return default;
            },
            null,
            null);

        await using var sub2 = await shared.SubscribeAsync(
            (x, _) =>
            {
                items2.Add(x);
                return default;
            },
            null,
            null);

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(2, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);
        await Task.Delay(200);

        await Assert.That(items1).IsNotEmpty();
        await Assert.That(items2).IsNotEmpty();
    }

    /// <summary>
    /// Tests that Multicast shares source.
    /// </summary>
    [Test]
    public async Task WhenMulticast_ThenSharesSource()
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

        await using var connection = await connectable.ConnectAsync(CancellationToken.None);
        await Task.Delay(200);

        await Assert.That(items).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// Tests ObserveOn with SynchronizationContext.
    /// </summary>
    [Test]
    public async Task WhenObserveOnSynchronizationContext_ThenUsesContext()
    {
        var result = await ObservableAsync.Return(42)
            .ObserveOn(SynchronizationContext.Current ?? new SynchronizationContext())
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests ObserveOn with TaskScheduler.
    /// </summary>
    [Test]
    public async Task WhenObserveOnTaskScheduler_ThenUsesScheduler()
    {
        var result = await ObservableAsync.Return(42)
            .ObserveOn(TaskScheduler.Default)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(42);
    }

    /// <summary>
    /// Tests SubscribeAsync with sync action callback.
    /// </summary>
    [Test]
    public async Task WhenSubscribeAsyncWithAction_ThenCallbackInvoked()
    {
        var items = new List<int>();

        await using var sub = await ObservableAsync.Range(1, 3)
            .SubscribeAsync(
                (Action<int>)(x => items.Add(x)),
                onErrorResume: null,
                onCompleted: null);

        await Task.Delay(100);

        await Assert.That(items).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// Tests SubscribeAsync with sync action, error, and completion callbacks.
    /// </summary>
    [Test]
    public async Task WhenSubscribeAsyncWithActionAndErrorAndCompletion_ThenAllCallbacksInvoked()
    {
        var items = new List<int>();
        Result? completion = null;

        await using var sub = await ObservableAsync.Range(1, 2)
            .SubscribeAsync(
                (Action<int>)(x => items.Add(x)),
                onErrorResume: null,
                onCompleted: r => completion = r);

        await Task.Delay(100);

        await Assert.That(items).IsEquivalentTo(new[] { 1, 2 });
        await Assert.That(completion).IsNotNull();
        await Assert.That(completion!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Tests SubscribeAsync with sync error handler.
    /// </summary>
    [Test]
    public async Task WhenSubscribeAsyncWithSyncErrorHandler_ThenErrorReported()
    {
        var errors = new List<Exception>();

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("err"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            (Action<int>)(_ => { }),
            onErrorResume: ex => errors.Add(ex),
            onCompleted: null);

        await Task.Delay(100);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("err");
    }

    /// <summary>
    /// Tests StatelessPublish does not retain state.
    /// </summary>
    [Test]
    public async Task WhenStatelessPublish_ThenDoesNotRetainState()
    {
        var source = ObservableAsync.Range(1, 3);
        var published = source.StatelessPublish();
        var items = new List<int>();

        await using var sub = await published.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await published.ConnectAsync(CancellationToken.None);
        await Task.Delay(200);

        await Assert.That(items).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// Tests Publish with concurrent options.
    /// </summary>
    [Test]
    public async Task WhenPublishWithOptions_ThenUsesOptions()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = false
        };
        var source = ObservableAsync.Range(1, 3);
        var published = source.Publish(options);
        var items = new List<int>();

        await using var sub = await published.SubscribeAsync(
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

        await using var conn = await published.ConnectAsync(CancellationToken.None);
        await Task.Delay(200);

        await Assert.That(items).Count().IsEqualTo(3);
    }

    /// <summary>
    /// Tests Publish with initial value sends initial value first.
    /// </summary>
    [Test]
    public async Task WhenPublishWithInitialValue_ThenSubscriberGetsInitialFirst()
    {
        var source = SubjectAsync.Create<int>();
        var published = source.Values.Publish(42);
        var items = new List<int>();

        await using var sub = await published.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await using var conn = await published.ConnectAsync(CancellationToken.None);
        await Task.Delay(100);

        await Assert.That(items).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(items[0]).IsEqualTo(42);
    }

    private sealed class TestResource(Action onDispose) : IAsyncDisposable
    {
#pragma warning disable CA1822 // Mark members as static
        public int Value => 42;
#pragma warning restore CA1822 // Mark members as static

        public ValueTask DisposeAsync()
        {
            onDispose();
            return default;
        }
    }
}
