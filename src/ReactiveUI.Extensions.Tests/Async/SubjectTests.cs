// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NUnit.Framework;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for SubjectAsync factory, all subject variants, and SubjectMixins.
/// </summary>
public class SubjectTests
{
    /// <summary>Tests serial subject pushes values to all observers in order.</summary>
    [Test]
    public async Task WhenSerialSubjectPushValues_ThenAllObserversReceiveInOrder()
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
        await subject.OnNextAsync(2, CancellationToken.None);
        await subject.OnNextAsync(3, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);

        await Task.Delay(100);

        Assert.That(items, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    /// <summary>Tests concurrent subject pushes values to all observers.</summary>
    [Test]
    public async Task WhenConcurrentSubjectPushValues_ThenAllObserversReceive()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = false
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

        await subject.OnNextAsync(10, CancellationToken.None);
        await subject.OnNextAsync(20, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);

        await Task.Delay(100);

        Assert.That(items, Has.Count.EqualTo(2));
    }

    /// <summary>Tests serial stateless subject pushes values.</summary>
    [Test]
    public async Task WhenSerialStatelessSubjectPushValues_ThenObserversReceive()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true
        };
        var subject = SubjectAsync.Create<string>(options);
        var items = new List<string>();

        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await subject.OnNextAsync("a", CancellationToken.None);
        await subject.OnNextAsync("b", CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);

        await Task.Delay(100);

        Assert.That(items, Is.EqualTo(new[] { "a", "b" }));
    }

    /// <summary>Tests concurrent stateless subject pushes values.</summary>
    [Test]
    public async Task WhenConcurrentStatelessSubjectPushValues_ThenObserversReceive()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = true
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

        await Task.Delay(100);

        Assert.That(items, Is.EqualTo(new[] { 5 }));
    }

    /// <summary>Tests behavior subject with start value emits latest first to new subscriber.</summary>
    [Test]
    public async Task WhenBehaviorSubjectWithStartValue_ThenNewSubscriberReceivesLatestFirst()
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

        Assert.That(items, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(items[0], Is.EqualTo(42));
    }

    /// <summary>Tests concurrent behavior subject emits latest to new subscriber.</summary>
    [Test]
    public async Task WhenBehaviorSubjectConcurrent_ThenNewSubscriberReceivesLatest()
    {
        var options = new BehaviorSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = false
        };
        var subject = SubjectAsync.CreateBehavior(100, options);
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

        Assert.That(items, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(items[0], Is.EqualTo(100));
    }

    /// <summary>Tests replay latest subject replays last value to late subscriber.</summary>
    [Test]
    public async Task WhenReplayLatestSubject_ThenLateSubscriberGetsLatestValue()
    {
        var subject = SubjectAsync.CreateReplayLatest<int>();

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

        Assert.That(items, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(items[0], Is.EqualTo(20));
    }

    /// <summary>Tests concurrent replay latest subject replays latest to new subscriber.</summary>
    [Test]
    public async Task WhenReplayLatestSubjectConcurrent_ThenLateSubscriberGetsLatest()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = false
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);

        await subject.OnNextAsync(5, CancellationToken.None);

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

        Assert.That(items, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(items[0], Is.EqualTo(5));
    }

    /// <summary>Tests subject OnErrorResume delivers error to observer.</summary>
    [Test]
    public async Task WhenSubjectOnErrorResume_ThenObserverReceivesError()
    {
        var subject = SubjectAsync.Create<int>();
        var errors = new List<Exception>();

        await using var sub = await subject.Values.SubscribeAsync(
            (_, _) => default,
            (ex, _) =>
            {
                errors.Add(ex);
                return default;
            },
            null);

        await subject.OnErrorResumeAsync(new InvalidOperationException("test"), CancellationToken.None);
        await Task.Delay(100);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0].Message, Is.EqualTo("test"));
    }

    /// <summary>Tests subject OnCompleted delivers completion to observer.</summary>
    [Test]
    public async Task WhenSubjectOnCompleted_ThenObserverReceivesCompletion()
    {
        var subject = SubjectAsync.Create<int>();
        Result? completionResult = null;

        await using var sub = await subject.Values.SubscribeAsync(
            (_, _) => default,
            null,
            result =>
            {
                completionResult = result;
                return default;
            });

        await subject.OnCompletedAsync(Result.Success);
        await Task.Delay(100);

        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsSuccess, Is.True);
    }

    /// <summary>Tests subject OnCompleted with failure delivers failure to observer.</summary>
    [Test]
    public async Task WhenSubjectOnCompletedWithFailure_ThenObserverReceivesFailure()
    {
        var subject = SubjectAsync.Create<int>();
        Result? completionResult = null;

        await using var sub = await subject.Values.SubscribeAsync(
            (_, _) => default,
            null,
            result =>
            {
                completionResult = result;
                return default;
            });

        await subject.OnCompletedAsync(Result.Failure(new InvalidOperationException("fatal")));
        await Task.Delay(100);

        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsFailure, Is.True);
    }

    /// <summary>Tests multiple observers all receive values.</summary>
    [Test]
    public async Task WhenMultipleObservers_ThenAllReceiveValues()
    {
        var subject = SubjectAsync.Create<int>();
        var items1 = new List<int>();
        var items2 = new List<int>();

        await using var sub1 = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items1.Add(x);
                return default;
            },
            null,
            null);

        await using var sub2 = await subject.Values.SubscribeAsync(
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

        await Task.Delay(100);

        Assert.That(items1, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(items2, Is.EqualTo(new[] { 1, 2 }));
    }

    /// <summary>Tests AsObserverAsync forwards to subject.</summary>
    [Test]
    public async Task WhenAsObserverAsync_ThenForwardsToSubject()
    {
        var subject = SubjectAsync.Create<int>();
        var observer = subject.AsObserverAsync();
        var items = new List<int>();

        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await observer.OnNextAsync(1, CancellationToken.None);
        await observer.OnNextAsync(2, CancellationToken.None);
        await observer.OnCompletedAsync(Result.Success);

        await Task.Delay(100);

        Assert.That(items, Is.EqualTo(new[] { 1, 2 }));
    }

    /// <summary>Tests MapValues transforms observable.</summary>
    [Test]
    public async Task WhenMapValues_ThenTransformsObservable()
    {
        var subject = SubjectAsync.Create<int>();
        var mapped = subject.MapValues(values => values.Select(x => x * 10));

        var items = new List<int>();
        await using var sub = await mapped.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await mapped.OnNextAsync(1, CancellationToken.None);
        await mapped.OnNextAsync(2, CancellationToken.None);
        await mapped.OnCompletedAsync(Result.Success);

        await Task.Delay(100);

        Assert.That(items, Is.EqualTo(new[] { 10, 20 }));
    }

    /// <summary>Tests default SubjectCreationOptions is serial and stateful.</summary>
    [Test]
    public void WhenDefaultSubjectCreationOptions_ThenSerialAndStateful()
    {
        var options = SubjectCreationOptions.Default;

        Assert.That(options.PublishingOption, Is.EqualTo(PublishingOption.Serial));
        Assert.That(options.IsStateless, Is.False);
    }

    /// <summary>Tests default BehaviorSubjectCreationOptions is serial and stateful.</summary>
    [Test]
    public void WhenDefaultBehaviorSubjectCreationOptions_ThenSerialAndStateful()
    {
        var options = BehaviorSubjectCreationOptions.Default;

        Assert.That(options.PublishingOption, Is.EqualTo(PublishingOption.Serial));
        Assert.That(options.IsStateless, Is.False);
    }

    /// <summary>Tests default ReplayLatestSubjectCreationOptions is serial and stateful.</summary>
    [Test]
    public void WhenDefaultReplayLatestSubjectCreationOptions_ThenSerialAndStateful()
    {
        var options = ReplayLatestSubjectCreationOptions.Default;

        Assert.That(options.PublishingOption, Is.EqualTo(PublishingOption.Serial));
        Assert.That(options.IsStateless, Is.False);
    }

    /// <summary>Tests behavior subject stateless emits start value.</summary>
    [Test]
    public async Task WhenBehaviorSubjectStateless_ThenEmitsStartValueToNewSubscriber()
    {
        var options = new BehaviorSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true
        };
        var subject = SubjectAsync.CreateBehavior("initial", options);
        var items = new List<string>();

        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await Task.Delay(100);

        Assert.That(items, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(items[0], Is.EqualTo("initial"));
    }

    /// <summary>Tests replay latest stateless emits latest to new subscriber.</summary>
    [Test]
    public async Task WhenReplayLatestStateless_ThenEmitsLatestToNewSubscriber()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);

        await subject.OnNextAsync(7, CancellationToken.None);

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

        Assert.That(items, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(items[0], Is.EqualTo(7));
    }

    /// <summary>Tests concurrent stateless replay latest emits latest.</summary>
    [Test]
    public async Task WhenConcurrentStatelessReplayLatest_ThenEmitsLatest()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = true
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);

        await subject.OnNextAsync(77, CancellationToken.None);

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

        Assert.That(items, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(items[0], Is.EqualTo(77));
    }

    /// <summary>Tests concurrent stateless behavior emits start value.</summary>
    [Test]
    public async Task WhenConcurrentStatelessBehavior_ThenEmitsStartValue()
    {
        var options = new BehaviorSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = true
        };
        var subject = SubjectAsync.CreateBehavior(55, options);
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

        Assert.That(items, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(items[0], Is.EqualTo(55));
    }
}
