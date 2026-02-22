// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
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
public class TakeUntilOperatorTests
{
    // ==========================================
    // TakeUntil(IObservableAsync<TOther>)
    // ==========================================

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
    public void WhenTakeUntilObservableNullOther_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).TakeUntil((IObservableAsync<string>)null!));
    }

    /// <summary>Tests that source completing normally passes through to subscriber.</summary>
    [Test]
    public async Task WhenTakeUntilObservableSourceCompletes_ThenCompletionPassesThrough()
    {
        var result = await ObservableAsync.Range(1, 3)
            .TakeUntil(ObservableAsync.Never<string>())
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests that other error with SourceFailsWhenOtherFails=true completes with failure.</summary>
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
        await Task.Delay(100);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Tests that other error with SourceFailsWhenOtherFails=false (default) completes with success.</summary>
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
        await Task.Delay(100);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests that other success completion does not trigger source completion.</summary>
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
        await Task.Delay(50);

        // Other completed with success — according to OtherObserver.OnCompletedAsyncCore, success returns default (no-op)
        // Source should still be active
        await source.OnNextAsync(2, CancellationToken.None);
        await Task.Delay(50);

        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(2);
    }

    /// <summary>Tests that error resume from other is forwarded.</summary>
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
        await Task.Delay(100);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("warning");
    }

    /// <summary>Tests that error resume from source is forwarded.</summary>
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
        await Task.Delay(100);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("src warning");
    }

    /// <summary>Tests that disposal stops emissions from source.</summary>
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
        await Task.Delay(50);

        await source.OnNextAsync(2, CancellationToken.None);
        await Task.Delay(50);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    // ==========================================
    // TakeUntil(Task)
    // ==========================================

    /// <summary>Tests that TakeUntil(Task) with null source throws.</summary>
    [Test]
    public void WhenTakeUntilTaskNullSource_ThenThrowsArgumentNull()
    {
        IObservableAsync<int> source = null!;
        Assert.Throws<ArgumentNullException>(() =>
            source.TakeUntil(Task.CompletedTask));
    }

    /// <summary>Tests that task failure with SourceFailsWhenOtherFails=true completes with failure.</summary>
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
        await Task.Delay(200);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Tests that task failure with default options sends error resume instead of failure.</summary>
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
        await Task.Delay(200);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that an already-completed task completes the sequence immediately.</summary>
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

        await Task.Delay(200);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests disposal of TakeUntil(Task) stops emissions.</summary>
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
        await Task.Delay(50);

        await source.OnNextAsync(2, CancellationToken.None);
        await Task.Delay(50);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    /// <summary>Tests that source error resume is forwarded through TakeUntil(Task).</summary>
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
        await Task.Delay(100);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    // ==========================================
    // TakeUntil(CancellationToken)
    // ==========================================

    /// <summary>Tests that already-canceled token completes immediately.</summary>
    [Test]
    public async Task WhenTakeUntilAlreadyCanceledToken_ThenCompletesImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

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

        await Task.Delay(200);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests that source error resume is forwarded through TakeUntil(CancellationToken).</summary>
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
        await Task.Delay(100);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that source completion is forwarded through TakeUntil(CancellationToken).</summary>
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
        await Task.Delay(100);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests that disposal of TakeUntil(CancellationToken) stops emissions.</summary>
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
        await Task.Delay(50);

        await source.OnNextAsync(2, CancellationToken.None);
        await Task.Delay(50);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    // ==========================================
    // TakeUntil(predicate)
    // ==========================================

    /// <summary>Tests that predicate never returning true emits all elements.</summary>
    [Test]
    public async Task WhenTakeUntilPredicateNeverTrue_ThenEmitsAllElements()
    {
        var result = await ObservableAsync.Range(1, 5)
            .TakeUntil(x => false)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    /// <summary>Tests that predicate returning true on first element emits nothing.</summary>
    [Test]
    public async Task WhenTakeUntilPredicateTrueOnFirst_ThenEmitsNothing()
    {
        var result = await ObservableAsync.Range(1, 5)
            .TakeUntil(x => true)
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that source error resume is forwarded through TakeUntil(predicate).</summary>
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

        await Task.Delay(200);

        await Assert.That(items).IsEquivalentTo(new[] { 1, 2 });
        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that source completion with failure is forwarded through TakeUntil(predicate).</summary>
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

        await Task.Delay(200);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    // ==========================================
    // TakeUntil(asyncPredicate)
    // ==========================================

    /// <summary>Tests that async predicate never returning true emits all elements.</summary>
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

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    /// <summary>Tests that async predicate returning true on first element emits nothing.</summary>
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

        await Task.Delay(200);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that source failure is forwarded through TakeUntil(asyncPredicate).</summary>
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

        await Task.Delay(200);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    // ==========================================
    // TakeUntil(CompletionObservableDelegate)
    // ==========================================

    /// <summary>Tests that TakeUntil with CompletionObservableDelegate throws on null.</summary>
    [Test]
    public void WhenTakeUntilCompletionDelegateNull_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).TakeUntil((CompletionObservableDelegate)null!));
    }

    /// <summary>Tests that CompletionObservableDelegate success signal completes the sequence.</summary>
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
        await Task.Delay(200);

        await Assert.That(items).Contains(1);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests that CompletionObservableDelegate failure signal with SourceFailsWhenOtherFails=true completes with failure.</summary>
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
        await Task.Delay(200);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Tests that CompletionObservableDelegate failure signal with default options sends error resume.</summary>
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
        await Task.Delay(200);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that source error resume is forwarded through TakeUntil(CompletionObservableDelegate).</summary>
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
        await Task.Delay(100);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that source completion is forwarded through TakeUntil(CompletionObservableDelegate).</summary>
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
        await Task.Delay(100);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests disposal of TakeUntil(CompletionObservableDelegate) stops emissions.</summary>
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
        await Task.Delay(50);

        await source.OnNextAsync(2, CancellationToken.None);
        await Task.Delay(50);

        await Assert.That(items).Contains(1);
        await Assert.That(items).DoesNotContain(2);
    }

    // ==========================================
    // TakeUntilOptions
    // ==========================================

    /// <summary>Tests TakeUntilOptions default has SourceFailsWhenOtherFails false.</summary>
    [Test]
    public async Task WhenTakeUntilOptionsDefault_ThenSourceFailsWhenOtherFailsIsFalse()
    {
        var options = TakeUntilOptions.Default;

        await Assert.That(options.SourceFailsWhenOtherFails).IsFalse();
    }

    /// <summary>Tests TakeUntilOptions with SourceFailsWhenOtherFails set to true.</summary>
    [Test]
    public async Task WhenTakeUntilOptionsSourceFailsWhenOtherFailsTrue_ThenPropertyIsTrue()
    {
        var options = new TakeUntilOptions { SourceFailsWhenOtherFails = true };

        await Assert.That(options.SourceFailsWhenOtherFails).IsTrue();
    }
}
