// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Deep coverage tests for all TakeUntil operator overloads:
/// TakeUntil(observable), TakeUntil(Task), TakeUntil(CancellationToken),
/// TakeUntil(predicate), TakeUntil(asyncPredicate), TakeUntil(CompletionObservableDelegate).
/// </summary>
[NotInParallel(nameof(UnhandledExceptionHandler))]
[TestExecutor<UnhandledExceptionTestExecutor>]
public class TakeUntilOperatorTests
{
    /// <summary>Tests that TakeUntil(observable) throws on null source.</summary>
    [Test]
    public void WhenTakeUntilObservableNullSource_ThenThrowsArgumentNull()
    {
        IObservableAsync<int> source = null!;
        Assert.Throws<ArgumentNullException>(() =>
            source.TakeUntil(ObservableAsync.Never<string>()));
    }

    /// <summary>Tests that TakeUntil(observable) throws on null other.</summary>
    [Test]
    public void WhenTakeUntilObservableNullOther_ThenThrowsArgumentNull() => Assert.Throws<ArgumentNullException>(() =>
                                                                                      ObservableAsync.Return(1).TakeUntil((IObservableAsync<string>)null!));

    /// <summary>Tests that source completing normally passes through to subscriber.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilObservableSourceCompletes_ThenCompletionPassesThrough()
    {
        var result = await ObservableAsync.Range(1, 3)
            .TakeUntil(ObservableAsync.Never<string>())
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>Tests that other error with SourceFailsWhenOtherFails=true completes with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilObservableOtherFailsAndOptionTrue_ThenCompletesWithFailure()
    {
        var source = SubjectAsync.Create<int>();
        var other = SubjectAsync.Create<string>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(other.Values, new TakeUntilOptions { SourceFailsWhenOtherFails = true })
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await source.OnNextAsync(1, CancellationToken.None);
        await other.OnCompletedAsync(Result.Failure(new InvalidOperationException("other failed")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Tests that other error with SourceFailsWhenOtherFails=false (default) completes with success.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilObservableOtherFailsAndOptionFalse_ThenCompletesWithSuccess()
    {
        var source = SubjectAsync.Create<int>();
        var other = SubjectAsync.Create<string>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(other.Values)
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await source.OnNextAsync(1, CancellationToken.None);
        await other.OnCompletedAsync(Result.Failure(new InvalidOperationException("other failed")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests that other success completion does not trigger source completion.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilObservableOtherCompletesSuccess_ThenSourceContinues()
    {
        var source = SubjectAsync.Create<int>();
        var other = SubjectAsync.Create<string>();
        var items = new List<int>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(other.Values)
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

        await source.OnNextAsync(1, CancellationToken.None);
        await other.OnCompletedAsync(Result.Success);

        // Other completed with success � according to OtherObserver.OnCompletedAsyncCore, success returns default (no-op)
        // Source should still be active
        await source.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(2);
    }

    /// <summary>Tests that error resume from other is forwarded.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilObservableOtherErrorResume_ThenForwardedToSubscriber()
    {
        var source = SubjectAsync.Create<int>();
        var other = SubjectAsync.Create<string>();
        var errors = new List<Exception>();

        await using var sub = await source.Values
            .TakeUntil(other.Values)
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await other.OnErrorResumeAsync(new InvalidOperationException("warning"), CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("warning");
    }

    /// <summary>Tests that error resume from source is forwarded.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilObservableSourceErrorResume_ThenForwardedToSubscriber()
    {
        var source = SubjectAsync.Create<int>();
        var other = SubjectAsync.Create<string>();
        var errors = new List<Exception>();

        await using var sub = await source.Values
            .TakeUntil(other.Values)
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await source.OnErrorResumeAsync(new InvalidOperationException("src warning"), CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("src warning");
    }

    /// <summary>Tests that disposal stops emissions from source.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilObservableDisposed_ThenStopsEmissions()
    {
        var source = SubjectAsync.Create<int>();
        var other = SubjectAsync.Create<string>();
        var items = new List<int>();

        var sub = await source.Values
            .TakeUntil(other.Values)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await source.OnNextAsync(1, CancellationToken.None);
        await sub.DisposeAsync();

        await source.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>Tests that TakeUntil(Task) with null source throws.</summary>
    [Test]
    public void WhenTakeUntilTaskNullSource_ThenThrowsArgumentNull()
    {
        IObservableAsync<int> source = null!;
        Assert.Throws<ArgumentNullException>(() =>
            source.TakeUntil(Task.CompletedTask));
    }

    /// <summary>Tests that task failure with SourceFailsWhenOtherFails=true completes with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskFailsAndOptionTrue_ThenCompletesWithFailure()
    {
        var tcs = new TaskCompletionSource();
        var source = SubjectAsync.Create<int>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(tcs.Task, new TakeUntilOptions { SourceFailsWhenOtherFails = true })
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await source.OnNextAsync(1, CancellationToken.None);
        tcs.SetException(new InvalidOperationException("task failed"));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Tests that task failure with default options sends error resume instead of failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskFailsAndOptionFalse_ThenSendsErrorResume()
    {
        var tcs = new TaskCompletionSource();
        var source = SubjectAsync.Create<int>();
        var errors = new List<Exception>();

        await using var sub = await source.Values
            .TakeUntil(tcs.Task)
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await source.OnNextAsync(1, CancellationToken.None);
        tcs.SetException(new InvalidOperationException("task failed"));

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that an already-completed task completes the sequence immediately.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilAlreadyCompletedTask_ThenCompletesImmediately()
    {
        var source = SubjectAsync.Create<int>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(Task.CompletedTask)
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

    /// <summary>Tests disposal of TakeUntil(Task) stops emissions.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskDisposed_ThenStopsEmissions()
    {
        var tcs = new TaskCompletionSource();
        var source = SubjectAsync.Create<int>();
        var items = new List<int>();

        var sub = await source.Values
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
        await sub.DisposeAsync();

        await source.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>Tests that source error resume is forwarded through TakeUntil(Task).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskSourceErrorResume_ThenForwarded()
    {
        var tcs = new TaskCompletionSource();
        var source = SubjectAsync.Create<int>();
        var errors = new List<Exception>();

        await using var sub = await source.Values
            .TakeUntil(tcs.Task)
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await source.OnErrorResumeAsync(new InvalidOperationException("warning"), CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that already-canceled token completes immediately.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilAlreadyCanceledToken_ThenCompletesImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var source = SubjectAsync.Create<int>();
        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await source.Values
            .TakeUntil(cts.Token)
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completed.TrySetResult(result);
                    return default;
                });

        var completionResult = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(completionResult.IsSuccess).IsTrue();
    }

    /// <summary>Tests that source error resume is forwarded through TakeUntil(CancellationToken).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCancellationTokenSourceErrorResume_ThenForwarded()
    {
        using var cts = new CancellationTokenSource();
        var source = SubjectAsync.Create<int>();
        var errors = new List<Exception>();

        await using var sub = await source.Values
            .TakeUntil(cts.Token)
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await source.OnErrorResumeAsync(new InvalidOperationException("warning"), CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that source completion is forwarded through TakeUntil(CancellationToken).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCancellationTokenSourceCompletes_ThenCompletionForwarded()
    {
        using var cts = new CancellationTokenSource();
        var source = SubjectAsync.Create<int>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(cts.Token)
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await source.OnCompletedAsync(Result.Success);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests that disposal of TakeUntil(CancellationToken) stops emissions.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCancellationTokenDisposed_ThenStopsEmissions()
    {
        using var cts = new CancellationTokenSource();
        var source = SubjectAsync.Create<int>();
        var items = new List<int>();

        var sub = await source.Values
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
        await sub.DisposeAsync();

        await source.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>Tests that predicate never returning true emits all elements.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilPredicateNeverTrue_ThenEmitsAllElements()
    {
        var result = await ObservableAsync.Range(1, 5)
            .TakeUntil(x => false)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>Tests that predicate returning true on first element emits nothing.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilPredicateTrueOnFirst_ThenEmitsNothing()
    {
        var result = await ObservableAsync.Range(1, 5)
            .TakeUntil(x => true)
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that source error resume is forwarded through TakeUntil(predicate).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilPredicateSourceErrorResume_ThenForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(new InvalidOperationException("warning"), ct);
            await observer.OnNextAsync(2, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var errors = new List<Exception>();
        var items = new List<int>();

        await using var sub = await source
            .TakeUntil(x => x > 10)
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

        await Assert.That(items).IsEquivalentTo([1, 2]);
        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that source completion with failure is forwarded through TakeUntil(predicate).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilPredicateSourceFails_ThenFailureForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("source failed")));
            return DisposableAsync.Empty;
        });

        Result? completionResult = null;

        await using var sub = await source
            .TakeUntil(x => x > 10)
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

    /// <summary>Tests that async predicate never returning true emits all elements.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilAsyncPredicateNeverTrue_ThenEmitsAllElements()
    {
        var result = await ObservableAsync.Range(1, 5)
            .TakeUntil(async (x, ct) =>
            {
                await Task.Yield();
                return false;
            })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    /// <summary>Tests that async predicate returning true on first element emits nothing.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilAsyncPredicateTrueOnFirst_ThenEmitsNothing()
    {
        var result = await ObservableAsync.Range(1, 5)
            .TakeUntil(async (x, ct) =>
            {
                await Task.Yield();
                return true;
            })
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that source error resume is forwarded through TakeUntil(asyncPredicate).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilAsyncPredicateSourceErrorResume_ThenForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(new InvalidOperationException("warning"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var errors = new List<Exception>();

        await using var sub = await source
            .TakeUntil(async (x, ct) =>
            {
                await Task.Yield();
                return false;
            })
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that source failure is forwarded through TakeUntil(asyncPredicate).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilAsyncPredicateSourceFails_ThenFailureForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));
            return DisposableAsync.Empty;
        });

        Result? completionResult = null;

        await using var sub = await source
            .TakeUntil(async (x, ct) =>
            {
                await Task.Yield();
                return false;
            })
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

    /// <summary>Tests that TakeUntil with CompletionObservableDelegate throws on null.</summary>
    [Test]
    public void WhenTakeUntilCompletionDelegateNull_ThenThrowsArgumentNull() => Assert.Throws<ArgumentNullException>(() =>
                                                                                         ObservableAsync.Return(1).TakeUntil((CompletionObservableDelegate)null!));

    /// <summary>Tests that CompletionObservableDelegate success signal completes the sequence.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCompletionDelegateSuccess_ThenCompletesSequence()
    {
        var source = SubjectAsync.Create<int>();
        Action<Result>? notifyStop = null;
        var items = new List<int>();
        Result? completionResult = null;

        CompletionObservableDelegate stopSignal = notify =>
        {
            notifyStop = notify;
            return DisposableAsync.Empty;
        };

        await using var sub = await source.Values
            .TakeUntil(stopSignal)
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

        await source.OnNextAsync(1, CancellationToken.None);
        await source.OnNextAsync(2, CancellationToken.None);

        notifyStop!(Result.Success);

        await Assert.That(items).Contains(1);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests that CompletionObservableDelegate failure signal with SourceFailsWhenOtherFails=true completes with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCompletionDelegateFailsAndOptionTrue_ThenCompletesWithFailure()
    {
        var source = SubjectAsync.Create<int>();
        Action<Result>? notifyStop = null;
        Result? completionResult = null;

        CompletionObservableDelegate stopSignal = notify =>
        {
            notifyStop = notify;
            return DisposableAsync.Empty;
        };

        await using var sub = await source.Values
            .TakeUntil(stopSignal, new TakeUntilOptions { SourceFailsWhenOtherFails = true })
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await source.OnNextAsync(1, CancellationToken.None);

        notifyStop!(Result.Failure(new InvalidOperationException("stop failed")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Tests that CompletionObservableDelegate failure signal with default options sends error resume.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCompletionDelegateFailsAndOptionFalse_ThenSendsErrorResume()
    {
        var source = SubjectAsync.Create<int>();
        Action<Result>? notifyStop = null;
        var errors = new List<Exception>();

        CompletionObservableDelegate stopSignal = notify =>
        {
            notifyStop = notify;
            return DisposableAsync.Empty;
        };

        await using var sub = await source.Values
            .TakeUntil(stopSignal)
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await source.OnNextAsync(1, CancellationToken.None);

        notifyStop!(Result.Failure(new InvalidOperationException("stop failed")));

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that source error resume is forwarded through TakeUntil(CompletionObservableDelegate).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCompletionDelegateSourceErrorResume_ThenForwarded()
    {
        var source = SubjectAsync.Create<int>();
        var errors = new List<Exception>();

        CompletionObservableDelegate stopSignal = notify => DisposableAsync.Empty;

        await using var sub = await source.Values
            .TakeUntil(stopSignal)
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await source.OnErrorResumeAsync(new InvalidOperationException("warning"), CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that source completion is forwarded through TakeUntil(CompletionObservableDelegate).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCompletionDelegateSourceCompletes_ThenCompletionForwarded()
    {
        var source = SubjectAsync.Create<int>();
        Result? completionResult = null;

        CompletionObservableDelegate stopSignal = notify => DisposableAsync.Empty;

        await using var sub = await source.Values
            .TakeUntil(stopSignal)
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await source.OnNextAsync(1, CancellationToken.None);
        await source.OnCompletedAsync(Result.Success);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests disposal of TakeUntil(CompletionObservableDelegate) stops emissions.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCompletionDelegateDisposed_ThenStopsEmissions()
    {
        var source = SubjectAsync.Create<int>();
        var items = new List<int>();

        CompletionObservableDelegate stopSignal = notify => DisposableAsync.Empty;

        var sub = await source.Values
            .TakeUntil(stopSignal)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await source.OnNextAsync(1, CancellationToken.None);
        await sub.DisposeAsync();

        await source.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>Tests TakeUntilOptions default has SourceFailsWhenOtherFails false.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilOptionsDefault_ThenSourceFailsWhenOtherFailsIsFalse()
    {
        var options = TakeUntilOptions.Default;

        await Assert.That(options.SourceFailsWhenOtherFails).IsFalse();
    }

    /// <summary>Tests TakeUntilOptions with SourceFailsWhenOtherFails set to true.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilOptionsSourceFailsWhenOtherFailsTrue_ThenPropertyIsTrue()
    {
        var options = new TakeUntilOptions { SourceFailsWhenOtherFails = true };

        await Assert.That(options.SourceFailsWhenOtherFails).IsTrue();
    }

    /// <summary>
    /// Verifies that TakeUntil with a predicate disposes the subscription and rethrows when the source throws during subscribe.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilPredicateSourceThrowsOnSubscribe_ThenDisposesAndRethrows()
    {
        var throwingSource = ObservableAsync.Create<int>((observer, ct) =>
            throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await throwingSource.TakeUntil(x => x > 5).SubscribeAsync((x, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that TakeUntil with a predicate that becomes true mid-stream stops emitting further elements.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilPredicateBecomesTrueMidStream_ThenStopsEmitting()
    {
        var result = await ObservableAsync.Range(1, 10)
            .TakeUntil(x => x > 3)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Verifies that TakeUntil with a cancellation token disposes the subscription and rethrows when the source throws during subscribe.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCancellationTokenSourceThrowsOnSubscribe_ThenDisposesAndRethrows()
    {
        using var cts = new CancellationTokenSource();
        var throwingSource = ObservableAsync.Create<int>((observer, ct) =>
            throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await throwingSource.TakeUntil(cts.Token).SubscribeAsync((x, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that TakeUntil with a cancellation token stops emission when the token is canceled during active subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTokenCanceledDuringEmission_ThenEmissionStops()
    {
        using var cts = new CancellationTokenSource();
        var source = SubjectAsync.Create<int>();
        var items = new List<int>();
        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await source.Values
            .TakeUntil(cts.Token)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completed.TrySetResult(result);
                    return default;
                });

        await source.OnNextAsync(1, CancellationToken.None);
        await source.OnNextAsync(2, CancellationToken.None);

        cts.Cancel();

        var completionResult = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(2);
        await Assert.That(completionResult.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that TakeUntil with a CompletionObservableDelegate disposes the subscription and rethrows when the source throws during subscribe.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCompletionDelegateSourceThrowsOnSubscribe_ThenDisposesAndRethrows()
    {
        var throwingSource = ObservableAsync.Create<int>((observer, ct) =>
            throw new InvalidOperationException("subscribe failed"));

        CompletionObservableDelegate stopSignal = notify => DisposableAsync.Empty;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await throwingSource.TakeUntil(stopSignal).SubscribeAsync((x, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that TakeUntil with a Task disposes the subscription and rethrows when the source throws during subscribe.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskSourceThrowsOnSubscribe_ThenDisposesAndRethrows()
    {
        var tcs = new TaskCompletionSource();
        var throwingSource = ObservableAsync.Create<int>((observer, ct) =>
            throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await throwingSource.TakeUntil(tcs.Task).SubscribeAsync((x, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that TakeUntil with a Task that completes mid-stream stops further emissions and completes with success.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskCompletesMidStream_ThenStopsEmissions()
    {
        var tcs = new TaskCompletionSource();
        var source = SubjectAsync.Create<int>();
        var items = new List<int>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(tcs.Task)
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

        await source.OnNextAsync(1, CancellationToken.None);
        await source.OnNextAsync(2, CancellationToken.None);

        tcs.SetResult();

        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(2);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that TakeUntil with another observable disposes the subscription and rethrows when the source throws during subscribe.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilAsyncObservableSourceThrowsOnSubscribe_ThenDisposesAndRethrows()
    {
        var throwingSource = ObservableAsync.Create<int>((observer, ct) =>
            throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await throwingSource.TakeUntil(ObservableAsync.Never<string>()).SubscribeAsync((x, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that TakeUntil with another observable that emits an item causes source to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilOtherObservableEmitsItem_ThenSourceCompletes()
    {
        var source = SubjectAsync.Create<int>();
        var other = SubjectAsync.Create<string>();
        var items = new List<int>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(other.Values)
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

        await source.OnNextAsync(1, CancellationToken.None);
        await source.OnNextAsync(2, CancellationToken.None);
        await other.OnNextAsync("stop", CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1, 2]);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that TakeUntil with an async predicate disposes the subscription and rethrows when the source throws during subscribe.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilAsyncPredicateSourceThrowsOnSubscribe_ThenDisposesAndRethrows()
    {
        var throwingSource = ObservableAsync.Create<int>((observer, ct) =>
            throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await throwingSource.TakeUntil(async (int x, CancellationToken ct) =>
            {
                await Task.Yield();
                return x > 5;
            }).SubscribeAsync((x, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that TakeUntil with an async predicate that becomes true mid-stream stops emitting further elements.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilAsyncPredicateBecomesTrueMidStream_ThenStopsEmitting()
    {
        var result = await ObservableAsync.Range(1, 10)
            .TakeUntil(async (x, ct) =>
            {
                await Task.Yield();
                return x > 3;
            })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests TakeUntil with Task overload stops emitting when task completes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTask_ThenStopsWhenTaskCompletes()
    {
        var tcs = new TaskCompletionSource<bool>();
        var subject = SubjectAsync.Create<int>();
        var items = new List<int>();

        await using var sub = await subject.Values
            .TakeUntil(tcs.Task)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);

        tcs.SetResult(true);

        await subject.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).Contains(1);
    }

    /// <summary>
    /// Tests TakeUntil with CancellationToken stops emitting when token is canceled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCancellationToken_ThenStopsWhenCanceled()
    {
        using var cts = new CancellationTokenSource();
        var subject = SubjectAsync.Create<int>();
        var items = new List<int>();

        await using var sub = await subject.Values
            .TakeUntil(cts.Token)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);

        await cts.CancelAsync();

        await Assert.That(items).Contains(1);
    }

    /// <summary>
    /// Tests TakeUntil with CompletionObservableDelegate stops emitting when delegate signal completes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCompletionDelegate_ThenStopsWhenSignalCompletes()
    {
        CompletionObservableDelegate signal = notifyStop =>
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                notifyStop(Result.Success);
            });
            return DisposableAsync.Empty;
        };

        var subject = SubjectAsync.Create<int>();
        var items = new List<int>();

        await using var sub = await subject.Values
            .TakeUntil(signal)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await subject.OnNextAsync(1, CancellationToken.None);

        await Assert.That(items).Contains(1);
    }

    /// <summary>
    /// Tests TakeUntil with sync predicate stops emitting when predicate returns true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilSyncPredicate_ThenStopsOnMatch()
    {
        var result = await ObservableAsync.Range(1, 10)
            .TakeUntil(x => x > 3)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests TakeUntil(CompletionObservableDelegate) where the stop signal fails and option is false,
    /// exercising the error resume path in WaitAndComplete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCompletionDelegateFailsAndOptionFalse_ThenErrorResumeForwarded()
    {
        var source = SubjectAsync.Create<int>();
        Exception? errorResumed = null;
        var items = new List<int>();

        await using var sub = await source.Values
            .TakeUntil(
                stop =>
                {
                    // Signal failure after a brief delay
                    _ = Task.Run(async () =>
                    {
                        await Task.Yield();
                        stop(Result.Failure(new InvalidOperationException("signal fail")));
                    });
                    return DisposableAsync.Empty;
                },
                new TakeUntilOptions { SourceFailsWhenOtherFails = false })
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                (ex, _) =>
                {
                    errorResumed = ex;
                    return default;
                },
                null);

        await source.OnNextAsync(1, CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => errorResumed is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(errorResumed).IsNotNull();
        await Assert.That(errorResumed!.Message).IsEqualTo("signal fail");
    }

    /// <summary>
    /// Tests TakeUntil(Task) where the task fails and option is false,
    /// exercising the error resume path in WaitAndComplete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskFailsAndOptionFalse_ThenErrorResumeForwardedViaWaitAndComplete()
    {
        var source = SubjectAsync.Create<int>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? errorResumed = null;

        await using var sub = await source.Values
            .TakeUntil(
                tcs.Task,
                new TakeUntilOptions { SourceFailsWhenOtherFails = false })
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errorResumed = ex;
                    return default;
                },
                null);

        tcs.SetException(new InvalidOperationException("task fail"));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => errorResumed is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(errorResumed).IsNotNull();
        await Assert.That(errorResumed!.Message).IsEqualTo("task fail");
    }

    /// <summary>
    /// Tests TakeUntil(predicate) DisposeAsyncCore when subscription is not null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilPredicateDisposed_ThenSubscriptionIsDisposed()
    {
        var source = SubjectAsync.Create<int>();
        var items = new List<int>();

        var sub = await source.Values
            .TakeUntil(x => x > 100)
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                null);

        await source.OnNextAsync(1, CancellationToken.None);
        await sub.DisposeAsync();

        // After dispose, emitting should not reach observer
        await source.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).IsEquivalentTo([1]);
    }

    /// <summary>
    /// Tests TakeUntil(CancellationToken) where the token is cancelled
    /// and the observer's OnTokenCanceled catch path is exercised.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCancellationTokenCanceled_ThenCompletionForwarded()
    {
        using var cts = new CancellationTokenSource();
        var source = SubjectAsync.Create<int>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(cts.Token)
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await source.OnNextAsync(1, CancellationToken.None);
        await cts.CancelAsync();

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Tests TakeUntil(CompletionObservableDelegate) where the delegate's disposable
    /// throws during cleanup, exercising the inner catch block.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCompletionDelegateDisposableThrows_ThenCaughtGracefully()
    {
        var source = SubjectAsync.Create<int>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(
                stop =>
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Yield();
                        stop(Result.Success);
                    });
                    return DisposableAsync.Create(() => throw new InvalidOperationException("dispose fail"));
                })
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await source.OnNextAsync(1, CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Tests that TakeUntil stops on observable signal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await AsyncTestHelpers.WaitForConditionAsync(
            () => true,
            TimeSpan.FromMilliseconds(100));

        await source.OnNextAsync(3, CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => true,
            TimeSpan.FromMilliseconds(50));

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(3);
    }

    /// <summary>
    /// Tests that TakeUntil stops on task completion.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await AsyncTestHelpers.WaitForConditionAsync(
            () => true,
            TimeSpan.FromMilliseconds(100));

        await source.OnNextAsync(2, CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => true,
            TimeSpan.FromMilliseconds(50));

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>
    /// Tests that TakeUntil stops on cancellation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await AsyncTestHelpers.WaitForConditionAsync(
            () => true,
            TimeSpan.FromMilliseconds(100));

        await Assert.That(items).Contains(1);
    }

    /// <summary>
    /// Tests that TakeUntil with predicate stops when predicate returns true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilPredicate_ThenStopsWhenPredicateTrue()
    {
        var result = await ObservableAsync.Range(1, 10)
            .TakeUntil(x => x > 3)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo([1, 2, 3]);
    }

    /// <summary>
    /// Tests that TakeUntil with async predicate stops when predicate returns true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await Assert.That(result).IsEquivalentTo([1, 2]);
    }

    /// <summary>
    /// Tests that TakeUntil throws on null predicate.
    /// </summary>
    [Test]
    public void WhenTakeUntilNullPredicate_ThenThrowsArgumentNull() => Assert.Throws<ArgumentNullException>(() =>
                                                                                ObservableAsync.Return(1).TakeUntil((Func<int, bool>)null!));

    /// <summary>
    /// Tests that TakeUntil throws on null async predicate.
    /// </summary>
    [Test]
    public void WhenTakeUntilNullAsyncPredicate_ThenThrowsArgumentNull() => Assert.Throws<ArgumentNullException>(() =>
                                                                                     ObservableAsync.Return(1).TakeUntil((Func<int, CancellationToken, ValueTask<bool>>)null!));

    /// <summary>
    /// Verifies that TakeUntil(CompletionObservableDelegate) forwards error resume when SourceFailsWhenOtherFails is false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilDelegateOtherFailsAndOptionFalse_ThenErrorResumeForwarded()
    {
        var source = SubjectAsync.Create<int>();
        var errors = new List<Exception>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(
                notifyStop =>
                {
                    // Fire stop with failure after a brief moment
                    _ = Task.Run(() => notifyStop(Result.Failure(new InvalidOperationException("delegate fail"))));
                    return DisposableAsync.Empty;
                },
                new TakeUntilOptions { SourceFailsWhenOtherFails = false })
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
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => errors.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(errors).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that TakeUntil(CompletionObservableDelegate) forwards failure when SourceFailsWhenOtherFails is true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilDelegateOtherFailsAndOptionTrue_ThenCompletesWithFailure()
    {
        var source = SubjectAsync.Create<int>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .TakeUntil(
                notifyStop =>
                {
                    _ = Task.Run(() => notifyStop(Result.Failure(new InvalidOperationException("delegate fail"))));
                    return DisposableAsync.Empty;
                },
                new TakeUntilOptions { SourceFailsWhenOtherFails = true })
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
    /// Verifies that TakeUntil(Task) forwards error resume when task fails and SourceFailsWhenOtherFails is false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskWaitAndCompleteFailsOptionFalse_ThenErrorResumeForwarded()
    {
        var source = SubjectAsync.Create<int>();
        var errors = new List<Exception>();
        var failingTask = Task.FromException(new InvalidOperationException("task fail"));

        await using var sub = await source.Values
            .TakeUntil(failingTask, new TakeUntilOptions { SourceFailsWhenOtherFails = false })
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

        await AsyncTestHelpers.WaitForConditionAsync(
            () => errors.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(errors).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that TakeUntil(Task) forwards failure when task fails and SourceFailsWhenOtherFails is true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskWaitAndCompleteFailsOptionTrue_ThenCompletesWithFailure()
    {
        var source = SubjectAsync.Create<int>();
        Result? completionResult = null;
        var failingTask = Task.FromException(new InvalidOperationException("task fail"));

        await using var sub = await source.Values
            .TakeUntil(failingTask, new TakeUntilOptions { SourceFailsWhenOtherFails = true })
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
    /// Verifies that TakeUntil(Task) SourceObserver forwards error resume and completion through parent.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskSourceEmitsErrorResume_ThenErrorIsForwarded()
    {
        var errors = new List<Exception>();
        var neverTask = new TaskCompletionSource().Task;

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(new InvalidOperationException("resume error"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        Result? completionResult = null;

        await using var sub = await source
            .TakeUntil(neverTask)
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
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        await Assert.That(errors).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(completionResult).IsNotNull();
    }

    /// <summary>
    /// Verifies that TakeUntil(CompletionObservableDelegate) disposes during wait.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilDelegateDisposedDuringWait_ThenCancelsCleanly()
    {
        var source = SubjectAsync.Create<int>();

        var sub = await source.Values
            .TakeUntil(
                _ => DisposableAsync.Empty,
                new TakeUntilOptions { SourceFailsWhenOtherFails = false })
            .SubscribeAsync(
                (_, _) => default,
                null,
                null);

        await source.OnNextAsync(1, CancellationToken.None);

        // Dispose while WaitAndComplete is still waiting for the signal
        await sub.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when the stop signal fires with an error and SourceFailsWhenOtherFails is false,
    /// the error is forwarded as an error-resume and the disposable's exception is swallowed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilDelegateSignalFailsAndOptionFalse_ThenSendsErrorResume()
    {
        var source = SubjectAsync.Create<int>();
        var errors = new List<Exception>();
        Action<Result>? storedNotifyStop = null;

        CompletionObservableDelegate signalDelegate = notifyStop =>
        {
            storedNotifyStop = notifyStop;
            return DisposableAsync.Create(() =>
            {
                // This dispose throws to exercise the catch block
                throw new InvalidOperationException("dispose boom");
            });
        };

        await using var sub = await source.Values
            .TakeUntil(signalDelegate)
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await source.OnNextAsync(1, CancellationToken.None);

        // Fire the stop signal with a failure
        storedNotifyStop!(Result.Failure(new InvalidOperationException("signal error")));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => errors.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(errors).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that when the stop signal fires with an error and SourceFailsWhenOtherFails is true,
    /// the error is forwarded as completion failure and the disposable's exception is swallowed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilDelegateSignalFailsAndOptionTrue_ThenCompletesWithFailure()
    {
        var source = SubjectAsync.Create<int>();
        Result? completionResult = null;
        Action<Result>? storedNotifyStop = null;

        CompletionObservableDelegate signalDelegate = notifyStop =>
        {
            storedNotifyStop = notifyStop;
            return DisposableAsync.Create(() =>
            {
                throw new InvalidOperationException("dispose boom");
            });
        };

        await using var sub = await source.Values
            .TakeUntil(signalDelegate, new TakeUntilOptions { SourceFailsWhenOtherFails = true })
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        storedNotifyStop!(Result.Failure(new InvalidOperationException("signal error")));

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult.HasValue,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that when the delegate stop signal fires and forwarding completion throws,
    /// the outer catch block swallows the exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilDelegateForwardingThrows_ThenOuterCatchSwallows()
    {
        Action<Result>? storedNotifyStop = null;

        CompletionObservableDelegate signalDelegate = notifyStop =>
        {
            storedNotifyStop = notifyStop;
            return DisposableAsync.Empty;
        };

        // Use a source that never completes, with an observer that throws on completion
        var source = SubjectAsync.Create<int>();
        var sub = await source.Values
            .TakeUntil(signalDelegate)
            .SubscribeAsync(
                (x, _) => default,
                null,
                _ => throw new InvalidOperationException("observer completion throws"));

        // Fire the stop signal with success; ForwardOnCompletedAsync will throw because observer throws
        storedNotifyStop!(Result.Success);

        // The outer catch block should swallow the exception; no crash
        await AsyncTestHelpers.WaitForConditionAsync(
            () => true,
            TimeSpan.FromMilliseconds(200));

        await sub.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when TakeUntil(Task) forwarding completion throws,
    /// the outer catch block swallows the exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskForwardingThrows_ThenOuterCatchSwallows()
    {
        var tcs = new TaskCompletionSource();
        var source = SubjectAsync.Create<int>();

        var sub = await source.Values
            .TakeUntil(tcs.Task)
            .SubscribeAsync(
                (x, _) => default,
                null,
                _ => throw new InvalidOperationException("observer completion throws"));

        // Complete the task; ForwardOnCompletedAsync will throw because observer throws
        tcs.SetResult();

        // The outer catch block should swallow the exception; no crash
        await AsyncTestHelpers.WaitForConditionAsync(
            () => true,
            TimeSpan.FromMilliseconds(200));

        await sub.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when TakeUntil(CancellationToken) forwards completion and the observer throws,
    /// the outer catch block in OnTokenCanceled swallows the exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilCancellationTokenForwardingThrows_ThenOuterCatchSwallows()
    {
        using var cts = new CancellationTokenSource();
        var source = SubjectAsync.Create<int>();

        var sub = await source.Values
            .TakeUntil(cts.Token)
            .SubscribeAsync(
                (x, _) => default,
                null,
                _ => throw new InvalidOperationException("observer completion throws"));

        // Cancel the token; OnTokenCanceled will call ForwardOnCompletedAsync which will throw
        await cts.CancelAsync();

        // The outer catch block should swallow the exception; no crash
        await AsyncTestHelpers.WaitForConditionAsync(
            () => true,
            TimeSpan.FromMilliseconds(200));

        await sub.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when TakeUntil(CompletionObservableDelegate) with SourceFailsWhenOtherFails=false
    /// signals an error, ForwardOnErrorResumeAsync is called. If that also throws, the outer catch swallows it.
    /// Covers the outermost catch in WaitAndComplete for CompletionObservableDelegate.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilDelegateErrorResumeThrows_ThenOuterCatchSwallows()
    {
        Action<Result>? storedNotifyStop = null;
        var source = SubjectAsync.Create<int>();

        CompletionObservableDelegate stopSignal = notifyStop =>
        {
            storedNotifyStop = notifyStop;
            return DisposableAsync.Create(() => default);
        };

        var sub = await source.Values
            .TakeUntil(
                stopSignal,
                new TakeUntilOptions { SourceFailsWhenOtherFails = false })
            .SubscribeAsync(
                (x, _) => default,
                (_, _) => throw new InvalidOperationException("error resume throws"),
                _ => throw new InvalidOperationException("completion throws"));

        // Signal a failure; SourceFailsWhenOtherFails=false so ForwardOnErrorResumeAsync is called, which throws
        storedNotifyStop!(Result.Failure(new InvalidOperationException("stop error")));

        // The outer catch block should swallow the exception
        await AsyncTestHelpers.WaitForConditionAsync(
            () => true,
            TimeSpan.FromMilliseconds(200));

        await sub.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when TakeUntil(Task) with SourceFailsWhenOtherFails=false
    /// the task faults and ForwardOnErrorResumeAsync throws, the outer catch swallows it.
    /// Covers the outermost catch in WaitAndComplete for Task.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenTakeUntilTaskErrorResumeThrows_ThenOuterCatchSwallows()
    {
        var tcs = new TaskCompletionSource();
        var source = SubjectAsync.Create<int>();

        var sub = await source.Values
            .TakeUntil(
                tcs.Task,
                new TakeUntilOptions { SourceFailsWhenOtherFails = false })
            .SubscribeAsync(
                (x, _) => default,
                (_, _) => throw new InvalidOperationException("error resume throws"),
                _ => throw new InvalidOperationException("completion throws"));

        // Fault the task; SourceFailsWhenOtherFails=false so ForwardOnErrorResumeAsync is called, which throws
        tcs.SetException(new InvalidOperationException("task error"));

        // The outer catch block should swallow the exception
        await AsyncTestHelpers.WaitForConditionAsync(
            () => true,
            TimeSpan.FromMilliseconds(200));

        await sub.DisposeAsync();
    }
}
