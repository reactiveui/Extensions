// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests concurrent subject pushes values to all observers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).Count().IsEqualTo(2);
    }

    /// <summary>Tests serial stateless subject pushes values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).IsEquivalentTo(["a", "b"]);
    }

    /// <summary>Tests concurrent stateless subject pushes values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).IsEquivalentTo([5]);
    }

    /// <summary>Tests behavior subject with start value emits latest first to new subscriber.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(items[0]).IsEqualTo(42);
    }

    /// <summary>Tests concurrent behavior subject emits latest to new subscriber.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(items[0]).IsEqualTo(100);
    }

    /// <summary>Tests replay latest subject replays last value to late subscriber.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(items[0]).IsEqualTo(20);
    }

    /// <summary>Tests concurrent replay latest subject replays latest to new subscriber.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(items[0]).IsEqualTo(5);
    }

    /// <summary>Tests subject OnErrorResume delivers error to observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("test");
    }

    /// <summary>Tests subject OnCompleted delivers completion to observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests subject OnCompleted with failure delivers failure to observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Tests multiple observers all receive values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items1).IsEquivalentTo([1, 2]);
        await Assert.That(items2).IsEquivalentTo([1, 2]);
    }

    /// <summary>Tests AsObserverAsync forwards to subject.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).IsEquivalentTo([1, 2]);
    }

    /// <summary>Tests MapValues transforms observable.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).IsEquivalentTo([10, 20]);
    }

    /// <summary>Tests default SubjectCreationOptions is serial and stateful.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDefaultSubjectCreationOptions_ThenSerialAndStateful()
    {
        var options = SubjectCreationOptions.Default;

        await Assert.That(options.PublishingOption).IsEqualTo(PublishingOption.Serial);
        await Assert.That(options.IsStateless).IsFalse();
    }

    /// <summary>Tests default BehaviorSubjectCreationOptions is serial and stateful.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDefaultBehaviorSubjectCreationOptions_ThenSerialAndStateful()
    {
        var options = BehaviorSubjectCreationOptions.Default;

        await Assert.That(options.PublishingOption).IsEqualTo(PublishingOption.Serial);
        await Assert.That(options.IsStateless).IsFalse();
    }

    /// <summary>Tests default ReplayLatestSubjectCreationOptions is serial and stateful.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDefaultReplayLatestSubjectCreationOptions_ThenSerialAndStateful()
    {
        var options = ReplayLatestSubjectCreationOptions.Default;

        await Assert.That(options.PublishingOption).IsEqualTo(PublishingOption.Serial);
        await Assert.That(options.IsStateless).IsFalse();
    }

    /// <summary>Tests behavior subject stateless emits start value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(items[0]).IsEqualTo("initial");
    }

    /// <summary>Tests replay latest stateless emits latest to new subscriber.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(items[0]).IsEqualTo(7);
    }

    /// <summary>Tests concurrent stateless replay latest emits latest.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(items[0]).IsEqualTo(77);
    }

    /// <summary>Tests that OnNextAsync on a serial stateless replay-last subject replays the value to a late subscriber.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStatelessReplayLastOnNext_ThenLateSubscriberReceivesReplayedValue()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);

        await subject.OnNextAsync(42, CancellationToken.None);

        var items = new List<int>();
        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await subject.OnNextAsync(99, CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([42, 99]);
    }

    /// <summary>Tests that OnErrorResumeAsync on a serial stateless replay-last subject delivers the error to observers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStatelessReplayLastOnErrorResume_ThenObserverReceivesError()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);
        var errorTcs = new TaskCompletionSource<Exception>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errorTcs.TrySetResult(ex);
                return default;
            },
            null);

        var expected = new InvalidOperationException("stateless-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        var received = await errorTcs.Task;
        await Assert.That(received).IsEqualTo(expected);
    }

    /// <summary>Tests that OnErrorResumeAsync on a concurrent stateless replay-last subject delivers the error to observers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentStatelessReplayLastOnErrorResume_ThenObserverReceivesError()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = true
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);
        var errorTcs = new TaskCompletionSource<Exception>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errorTcs.TrySetResult(ex);
                return default;
            },
            null);

        var expected = new InvalidOperationException("concurrent-stateless-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        var received = await errorTcs.Task;
        await Assert.That(received).IsEqualTo(expected);
    }

    /// <summary>Tests that OnCompletedAsync on a serial stateless replay-last subject delivers completion and resets state.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStatelessReplayLastOnCompleted_ThenObserverReceivesCompletionAndStateResets()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);
        var resultTcs = new TaskCompletionSource<Result>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            result =>
            {
                resultTcs.TrySetResult(result);
                return default;
            });

        await subject.OnNextAsync(10, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);

        var received = await resultTcs.Task;
        await Assert.That(received.IsSuccess).IsTrue();

        // After completion, a new subscriber should NOT receive a replayed value since state was reset.
        var lateItems = new List<int>();
        await using var lateSub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                lateItems.Add(x);
                return default;
            },
            null,
            null);

        await Assert.That(lateItems).Count().IsEqualTo(0);
    }

    /// <summary>Tests that OnCompletedAsync on a concurrent stateless replay-last subject delivers completion to observers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentStatelessReplayLastOnCompleted_ThenObserverReceivesCompletion()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = true
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);
        var resultTcs = new TaskCompletionSource<Result>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            result =>
            {
                resultTcs.TrySetResult(result);
                return default;
            });

        await subject.OnCompletedAsync(Result.Success);

        var received = await resultTcs.Task;
        await Assert.That(received.IsSuccess).IsTrue();
    }

    /// <summary>Tests that DisposeAsync on a serial stateless replay-last subject completes without error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenStatelessReplayLastDispose_ThenCompletesSuccessfully()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.DisposeAsync();
    }

    /// <summary>Tests that DisposeAsync on a concurrent stateless replay-last subject completes without error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentStatelessReplayLastDispose_ThenCompletesSuccessfully()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = true
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.DisposeAsync();
    }

    /// <summary>Tests that OnErrorResumeAsync on a serial stateless subject delivers the error to the observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSerialStatelessSubjectOnErrorResume_ThenObserverReceivesError()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true
        };
        var subject = SubjectAsync.Create<int>(options);
        var errorTcs = new TaskCompletionSource<Exception>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errorTcs.TrySetResult(ex);
                return default;
            },
            null);

        var expected = new InvalidOperationException("serial-stateless-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        var received = await errorTcs.Task;
        await Assert.That(received).IsEqualTo(expected);
    }

    /// <summary>Tests that OnErrorResumeAsync on a concurrent stateless subject delivers the error to the observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentStatelessSubjectOnErrorResume_ThenObserverReceivesError()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = true
        };
        var subject = SubjectAsync.Create<int>(options);
        var errorTcs = new TaskCompletionSource<Exception>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errorTcs.TrySetResult(ex);
                return default;
            },
            null);

        var expected = new InvalidOperationException("concurrent-stateless-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        var received = await errorTcs.Task;
        await Assert.That(received).IsEqualTo(expected);
    }

    /// <summary>Tests that DisposeAsync on a serial stateless subject clears observers and completes without error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSerialStatelessSubjectDispose_ThenCompletesAndClearsObservers()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true
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

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.DisposeAsync();

        // After dispose, observers are cleared so no further values should be delivered.
        await subject.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1]);
    }

    /// <summary>Tests that DisposeAsync on a concurrent stateless subject clears observers and completes without error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentStatelessSubjectDispose_ThenCompletesAndClearsObservers()
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

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.DisposeAsync();

        // After dispose, observers are cleared so no further values should be delivered.
        await subject.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1]);
    }

    /// <summary>Tests concurrent stateless behavior emits start value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(items).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(items[0]).IsEqualTo(55);
    }

    /// <summary>Tests that OnErrorResumeAsync on a concurrent stateful subject delivers the error to observers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentSubjectOnErrorResume_ThenObserverReceivesError()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = false
        };
        var subject = SubjectAsync.Create<int>(options);
        var errorTcs = new TaskCompletionSource<Exception>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errorTcs.TrySetResult(ex);
                return default;
            },
            null);

        var expected = new InvalidOperationException("concurrent-stateful");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        var received = await errorTcs.Task;
        await Assert.That(received).IsEqualTo(expected);
    }

    /// <summary>Tests that OnErrorResumeAsync is ignored after the subject has already completed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorResumeAsyncCalledAfterCompletion_ThenIsIgnored()
    {
        var subject = SubjectAsync.Create<int>();
        var errors = new List<Exception>();
        var completionTcs = new TaskCompletionSource();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errors.Add(ex);
                return default;
            },
            _ =>
            {
                completionTcs.TrySetResult();
                return default;
            });

        await subject.OnCompletedAsync(Result.Success);
        await completionTcs.Task;

        await subject.OnErrorResumeAsync(new InvalidOperationException("should be ignored"), CancellationToken.None);

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>Tests that OnCompletedAsync is ignored on the second call after the subject has already completed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnCompletedAsyncCalledTwice_ThenSecondCallIsIgnored()
    {
        var subject = SubjectAsync.Create<int>();
        var completionCount = 0;
        var completionTcs = new TaskCompletionSource();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            _ =>
            {
                Interlocked.Increment(ref completionCount);
                completionTcs.TrySetResult();
                return default;
            });

        await subject.OnCompletedAsync(Result.Success);
        await completionTcs.Task;

        await subject.OnCompletedAsync(Result.Failure(new InvalidOperationException("second")));

        await Assert.That(completionCount).IsEqualTo(1);
    }

    /// <summary>Tests that subscribing to an already-completed subject immediately delivers the completion result.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribingToAlreadyCompletedSubject_ThenObserverReceivesCompletionImmediately()
    {
        var subject = SubjectAsync.Create<int>();
        var firstCompletionTcs = new TaskCompletionSource();

        await using var firstSub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            _ =>
            {
                firstCompletionTcs.TrySetResult();
                return default;
            });

        await subject.OnCompletedAsync(Result.Success);
        await firstCompletionTcs.Task;

        Result? lateResult = null;
        var lateTcs = new TaskCompletionSource();

        await using var lateSub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            result =>
            {
                lateResult = result;
                lateTcs.TrySetResult();
                return default;
            });

        await lateTcs.Task;

        await Assert.That(lateResult).IsNotNull();
        await Assert.That(lateResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests that OnNextAsync is ignored after the subject has already completed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnNextAsyncCalledAfterCompletion_ThenIsIgnored()
    {
        var subject = SubjectAsync.Create<int>();
        var items = new List<int>();
        var completionTcs = new TaskCompletionSource();

        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            _ =>
            {
                completionTcs.TrySetResult();
                return default;
            });

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);
        await completionTcs.Task;

        await subject.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1]);
    }

    /// <summary>Tests that OnErrorResumeAsync forwards the error to observers when the subject has not completed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnErrorResumeAsyncCalledBeforeCompletion_ThenErrorIsForwarded()
    {
        var subject = SubjectAsync.Create<int>();
        var errorTcs = new TaskCompletionSource<Exception>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errorTcs.TrySetResult(ex);
                return default;
            },
            null);

        var expected = new InvalidOperationException("forwarded");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        var received = await errorTcs.Task;

        await Assert.That(received).IsEqualTo(expected);
    }

    /// <summary>Tests that OnCompletedAsync forwards the result to observers and clears the observer list.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenOnCompletedAsyncCalled_ThenResultIsForwardedToObservers()
    {
        var subject = SubjectAsync.Create<int>();
        var resultTcs = new TaskCompletionSource<Result>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            result =>
            {
                resultTcs.TrySetResult(result);
                return default;
            });

        var failure = Result.Failure(new InvalidOperationException("done"));
        await subject.OnCompletedAsync(failure);

        var received = await resultTcs.Task;

        await Assert.That(received.IsFailure).IsTrue();
    }

    /// <summary>Tests that OnNextAsync on a replay-latest subject is ignored after the subject has completed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenReplayLatestOnNextAfterCompleted_ThenValueIsIgnored()
    {
        var subject = SubjectAsync.CreateReplayLatest<int>();
        var items = new List<int>();
        var completionTcs = new TaskCompletionSource();

        await using var sub = await subject.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            _ =>
            {
                completionTcs.TrySetResult();
                return default;
            });

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);
        await completionTcs.Task;

        await subject.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1]);
    }

    /// <summary>Tests that OnErrorResumeAsync on a replay-latest subject delivers the error to observers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenReplayLatestOnErrorResume_ThenObserverReceivesError()
    {
        var subject = SubjectAsync.CreateReplayLatest<int>();
        var errorTcs = new TaskCompletionSource<Exception>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errorTcs.TrySetResult(ex);
                return default;
            },
            null);

        var expected = new InvalidOperationException("replay-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        var received = await errorTcs.Task;
        await Assert.That(received).IsEqualTo(expected);
    }

    /// <summary>Tests that OnErrorResumeAsync on a replay-latest subject is ignored after completion.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenReplayLatestOnErrorResumeAfterCompleted_ThenErrorIsIgnored()
    {
        var subject = SubjectAsync.CreateReplayLatest<int>();
        var errors = new List<Exception>();
        var completionTcs = new TaskCompletionSource();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errors.Add(ex);
                return default;
            },
            _ =>
            {
                completionTcs.TrySetResult();
                return default;
            });

        await subject.OnCompletedAsync(Result.Success);
        await completionTcs.Task;

        await subject.OnErrorResumeAsync(new InvalidOperationException("ignored"), CancellationToken.None);

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>Tests that OnCompletedAsync on a replay-latest subject delivers completion to observers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenReplayLatestOnCompleted_ThenObserverReceivesCompletion()
    {
        var subject = SubjectAsync.CreateReplayLatest<int>();
        var completionTcs = new TaskCompletionSource<Result>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            result =>
            {
                completionTcs.TrySetResult(result);
                return default;
            });

        await subject.OnCompletedAsync(Result.Success);

        var completionResult = await completionTcs.Task;
        await Assert.That(completionResult.IsSuccess).IsTrue();
    }

    /// <summary>Tests that calling OnCompletedAsync twice on a replay-latest subject ignores the second call.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenReplayLatestOnCompletedCalledTwice_ThenSecondCallIsIgnored()
    {
        var subject = SubjectAsync.CreateReplayLatest<int>();
        var completionCount = 0;
        var completionTcs = new TaskCompletionSource();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            _ =>
            {
                Interlocked.Increment(ref completionCount);
                completionTcs.TrySetResult();
                return default;
            });

        await subject.OnCompletedAsync(Result.Success);
        await completionTcs.Task;

        await subject.OnCompletedAsync(Result.Failure(new InvalidOperationException("second")));

        await Assert.That(completionCount).IsEqualTo(1);
    }

    /// <summary>Tests that DisposeAsync on a replay-latest subject completes without error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenReplayLatestDisposeAsync_ThenCompletesSuccessfully()
    {
        var subject = SubjectAsync.CreateReplayLatest<int>();
        await subject.OnNextAsync(42, CancellationToken.None);

        await subject.DisposeAsync();
    }

    /// <summary>Tests that OnErrorResumeAsync on a serial stateless subject delivers the error to multiple observers sequentially.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSerialStatelessSubjectOnErrorResumeWithMultipleObservers_ThenAllObserversReceiveError()
    {
        var options = new SubjectCreationOptions
        {
            PublishingOption = PublishingOption.Serial,
            IsStateless = true
        };
        var subject = SubjectAsync.Create<int>(options);
        var errors1 = new List<Exception>();
        var errors2 = new List<Exception>();

        await using var sub1 = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errors1.Add(ex);
                return default;
            },
            null);

        await using var sub2 = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errors2.Add(ex);
                return default;
            },
            null);

        var expected = new InvalidOperationException("multi-observer-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);

        await Assert.That(errors1).Count().IsEqualTo(1);
        await Assert.That(errors1[0]).IsEqualTo(expected);
        await Assert.That(errors2).Count().IsEqualTo(1);
        await Assert.That(errors2[0]).IsEqualTo(expected);
    }

    /// <summary>Tests that SubjectAsync.Create throws ArgumentOutOfRangeException for an invalid options combination.</summary>
    [Test]
    public void WhenCreateWithInvalidOptions_ThenThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SubjectAsync.Create<int>(null!));
    }

    /// <summary>Tests that SubjectAsync.CreateBehavior throws ArgumentOutOfRangeException for an invalid options combination.</summary>
    [Test]
    public void WhenCreateBehaviorWithInvalidOptions_ThenThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SubjectAsync.CreateBehavior(0, null!));
    }

    /// <summary>Tests that SubjectAsync.CreateReplayLatest throws ArgumentOutOfRangeException for an invalid options combination.</summary>
    [Test]
    public void WhenCreateReplayLatestWithInvalidOptions_ThenThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SubjectAsync.CreateReplayLatest<int>(null!));
    }

    /// <summary>Tests that MappedSubject.SubscribeAsync subscribes an observer through the mapped values observable.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMappedSubjectSubscribeAsync_ThenObserverReceivesMappedValues()
    {
        var subject = SubjectAsync.Create<int>();
        var mapped = subject.MapValues(values => values.Select(x => x + 1));

        var collector = SubjectAsync.Create<int>();
        var items = new List<int>();
        await using var collectorSub = await collector.Values.SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        var observer = collector.AsObserverAsync();
        await using var sub = await mapped.SubscribeAsync(observer, CancellationToken.None);

        await mapped.OnNextAsync(10, CancellationToken.None);
        await mapped.OnCompletedAsync(Result.Success);

        await Assert.That(items).IsEquivalentTo([11]);
    }

    /// <summary>Tests that MappedSubject.OnErrorResumeAsync forwards the error to the original subject.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMappedSubjectOnErrorResumeAsync_ThenErrorIsForwardedToOriginal()
    {
        var subject = SubjectAsync.Create<int>();
        var mapped = subject.MapValues(values => values);
        var errorTcs = new TaskCompletionSource<Exception>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errorTcs.TrySetResult(ex);
                return default;
            },
            null);

        var expected = new InvalidOperationException("mapped-error");
        await mapped.OnErrorResumeAsync(expected, CancellationToken.None);

        var received = await errorTcs.Task;
        await Assert.That(received).IsEqualTo(expected);
    }

    /// <summary>Tests that MappedSubject.DisposeAsync disposes the original subject.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMappedSubjectDisposeAsync_ThenOriginalSubjectIsDisposed()
    {
        var subject = SubjectAsync.Create<int>();
        var mapped = subject.MapValues(values => values);

        // DisposeAsync on the mapped subject should dispose the underlying subject.
        await mapped.DisposeAsync();

        // Verify double-dispose is safe.
        await mapped.DisposeAsync();
    }

    /// <summary>Tests that AsObserverAsync forwards OnErrorResumeAsync to the underlying subject.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsObserverAsyncOnErrorResume_ThenErrorIsForwardedToSubject()
    {
        var subject = SubjectAsync.Create<int>();
        var observer = subject.AsObserverAsync();
        var errorTcs = new TaskCompletionSource<Exception>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errorTcs.TrySetResult(ex);
                return default;
            },
            null);

        var expected = new InvalidOperationException("observer-error");
        await observer.OnErrorResumeAsync(expected, CancellationToken.None);

        var received = await errorTcs.Task;
        await Assert.That(received).IsEqualTo(expected);
    }

    /// <summary>Tests that AsObserverAsync forwards OnCompletedAsync to the underlying subject.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsObserverAsyncOnCompleted_ThenCompletionIsForwardedToSubject()
    {
        var subject = SubjectAsync.Create<int>();
        var observer = subject.AsObserverAsync();
        var resultTcs = new TaskCompletionSource<Result>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            result =>
            {
                resultTcs.TrySetResult(result);
                return default;
            });

        await observer.OnCompletedAsync(Result.Success);

        var received = await resultTcs.Task;
        await Assert.That(received.IsSuccess).IsTrue();
    }

    /// <summary>Tests that ForwardOnErrorResumeConcurrently with an empty observer list completes immediately.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForwardOnErrorResumeConcurrentlyWithEmptyObservers_ThenCompletesImmediately()
    {
        var emptyObservers = Array.Empty<IObserverAsync<int>>();

        var task = Concurrent.ForwardOnErrorResumeConcurrently(emptyObservers, new InvalidOperationException("unused"), CancellationToken.None);

        await Assert.That(task.IsCompletedSuccessfully).IsTrue();
    }

    /// <summary>Tests that ForwardOnCompletedConcurrently with an empty observer list completes immediately.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForwardOnCompletedConcurrentlyWithEmptyObservers_ThenCompletesImmediately()
    {
        var emptyObservers = Array.Empty<IObserverAsync<int>>();

        var task = Concurrent.ForwardOnCompletedConcurrently<int>(emptyObservers, Result.Success);

        await Assert.That(task.IsCompletedSuccessfully).IsTrue();
    }

    /// <summary>Tests that OnErrorResumeAsync on a concurrent stateful replay-latest subject delivers the error to observers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentReplayLatestOnErrorResume_ThenObserverReceivesError()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = false
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);
        var errorTcs = new TaskCompletionSource<Exception>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                errorTcs.TrySetResult(ex);
                return default;
            },
            null);

        var expected = new InvalidOperationException("concurrent-stateful-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        var received = await errorTcs.Task;
        await Assert.That(received).IsEqualTo(expected);
    }

    /// <summary>Tests that OnCompletedAsync on a concurrent stateful replay-latest subject delivers completion to observers.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenConcurrentReplayLatestOnCompleted_ThenObserverReceivesCompletion()
    {
        var options = new ReplayLatestSubjectCreationOptions
        {
            PublishingOption = PublishingOption.Concurrent,
            IsStateless = false
        };
        var subject = SubjectAsync.CreateReplayLatest<int>(options);
        var resultTcs = new TaskCompletionSource<Result>();

        await using var sub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            result =>
            {
                resultTcs.TrySetResult(result);
                return default;
            });

        await subject.OnCompletedAsync(Result.Success);

        var received = await resultTcs.Task;
        await Assert.That(received.IsSuccess).IsTrue();
    }

    /// <summary>Tests that subscribing to an already-completed replay-latest subject immediately delivers completion.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeToCompletedReplayLatest_ThenObserverReceivesImmediateCompletion()
    {
        var subject = SubjectAsync.CreateReplayLatest<int>();
        var firstCompletionTcs = new TaskCompletionSource();

        await using var firstSub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            _ =>
            {
                firstCompletionTcs.TrySetResult();
                return default;
            });

        await subject.OnNextAsync(99, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);
        await firstCompletionTcs.Task;

        Result? lateResult = null;
        var lateTcs = new TaskCompletionSource();

        await using var lateSub = await subject.Values.SubscribeAsync(
            static (_, _) => default,
            null,
            result =>
            {
                lateResult = result;
                lateTcs.TrySetResult();
                return default;
            });

        await lateTcs.Task;

        await Assert.That(lateResult).IsNotNull();
        await Assert.That(lateResult!.Value.IsSuccess).IsTrue();
    }
}
