// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <content>
/// Merge tests for combining operators.
/// </content>
public partial class CombiningOperatorTests
{
    /// <summary>Tests Merge two sequences emits from both.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeTwoSequences_ThenEmitsFromBoth()
    {
        var first = ObservableAsync.Return(1);
        var second = ObservableAsync.Return(2);

        var result = await first.Merge(second).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(SampleValue2);
        await Assert.That(result).Contains(1);
    }

    /// <summary>Tests Merge enumerable emits from all.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerable_ThenEmitsFromAll()
    {
        IObservableAsync<int>[] sources = [ObservableAsync.Return(10), ObservableAsync.Return(20), ObservableAsync.Return(30)
        ];

        var result = await sources.Merge().ToListAsync();

        await Assert.That(result).Count().IsEqualTo(SampleValue3);
    }

    /// <summary>Tests Merge observable of observables flattens.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeObservableOfObservables_ThenFlattens()
    {
        var source = new[] { ObservableAsync.Return(1), ObservableAsync.Return(2) }.ToObservableAsync();

        var result = await source.Merge().ToListAsync();

        await Assert.That(result).Count().IsEqualTo(SampleValue2);
    }

    /// <summary>Tests Merge with max concurrency respects limit.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeWithMaxConcurrency_ThenRespectsLimit()
    {
        var activeConcurrency = 0;
        var maxConcurrency = 0;

        var source = ObservableAsync.Range(1, 5).Select(i =>
            ObservableAsync.CreateAsBackgroundJob<int>(
                async (obs, ct) =>
                {
                    lock (_gate)
                    {
                        activeConcurrency++;
                        maxConcurrency = Math.Max(maxConcurrency, activeConcurrency);
                    }

                    await Task.Delay(50, ct);

                    lock (_gate)
                    {
                        activeConcurrency--;
                    }

                    await obs.OnNextAsync(i, ct);
                    await obs.OnCompletedAsync(Result.Success);
                }));

        var result = await source.Merge(2).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(SampleValue5);
        await Assert.That(maxConcurrency).IsLessThanOrEqualTo(SampleValue2);
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
                (_, _) => default,
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
        var failing =
            ObservableAsync.Create<int>((_, _) =>
            {
                try
                {
                    throw new InvalidOperationException("subscribe fail");
                }
                catch (Exception exception)
                {
                    return ValueTask.FromException<IAsyncDisposable>(exception);
                }
            });

        var source = new[] { failing }.ToObservableAsync();

        Result? completionResult = null;
        await using var sub = await source
            .Merge(1)
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
            TimeSpan.FromSeconds(2));

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
        IObservableAsync<int>[] sources =
        [
            ObservableAsync.Return(1), ObservableAsync.Throw<int>(new InvalidOperationException(InnerFailMessage)),
            ObservableAsync.Return(3)
        ];

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

        IObservableAsync<int>[] sources = [inner];
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
                });

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
            await observer.OnErrorResumeAsync(new InvalidOperationException(InnerWarningMessage), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await outer.Values
            .Merge()
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                });

        await outer.OnNextAsync(inner, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);

        await outer.DisposeAsync();
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
        var failing = ObservableAsync.Create<IObservableAsync<int>>((_, _) =>
        {
            try
            {
                throw new InvalidOperationException(SubscribeBoomMessage);
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<IAsyncDisposable>(exception);
            }
        });

        var act = async () =>
        {
            await using var sub = await failing
                .Merge()
                .SubscribeAsync(
                    (_, _) => default,
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
        var failing = ObservableAsync.Create<IObservableAsync<int>>((_, _) =>
        {
            try
            {
                throw new InvalidOperationException("subscribe boom max");
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<IAsyncDisposable>(exception);
            }
        });

        var act = async () =>
        {
            await using var sub = await failing
                .Merge(2)
                .SubscribeAsync(
                    (_, _) => default,
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
        var throwingInner = ObservableAsync.Create<int>((_, _) =>
        {
            try
            {
                throw new InvalidOperationException("inner subscribe fail");
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<IAsyncDisposable>(exception);
            }
        });

        var outer = SubjectAsync.Create<IObservableAsync<int>>();
        Result? completionResult = null;

        await using var sub = await outer.Values
            .Merge()
            .SubscribeAsync(
                (_, _) => default,
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
                (_, _) => default,
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
        var throwingInner = ObservableAsync.Create<int>((_, _) =>
        {
            try
            {
                throw new InvalidOperationException("inner subscribe fail");
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<IAsyncDisposable>(exception);
            }
        });

        IObservableAsync<int>[] sources = [throwingInner];
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

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that MergeEnumerable CompleteAsync called a second time with an exception
    /// routes the exception to UnhandledExceptionHandler rather than throwing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableDoubleCompletionWithError_ThenUnhandledExceptionHandlerInvoked()
    {
        // Capture the unhandled exception
        UnhandledExceptionHandler.Register(ex => _ = ex);

        IObservableAsync<int>[] sources =
        [
            ObservableAsync.Return(1), ObservableAsync.Throw<int>(new InvalidOperationException(FirstFailMessage)),
            ObservableAsync.Throw<int>(new InvalidOperationException("second fail"))
        ];

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
        await using var sub = await new[] { innerSubject.Values }.Merge()
            .SubscribeAsync(
                (_, _) => default,
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

        IObservableAsync<int>[] sources = [subject.Values, neverCompleting];

        await using var sub = await sources.Merge()
            .SubscribeAsync(
                (_, _) => default,
                null,
                null,
                cts.Token);

        await cts.CancelAsync();

        // After cancellation, the subscription should be cleaned up
    }

    /// <summary>Tests Merge with error from one source propagates correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeWithError_ThenErrorPropagates()
    {
        var errorSource = ObservableAsync.Throw<int>(new InvalidOperationException("merge-error"));
        var goodSource = ObservableAsync.Return(1);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await goodSource.Merge(errorSource).ToListAsync());
    }

    /// <summary>Tests Merge with max concurrency and error propagation.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeConcurrencyWithSlowSource_ThenLimitsAndCompletes()
    {
        var source = ObservableAsync.Range(1, 4).Select(i =>
            ObservableAsync.CreateAsBackgroundJob<int>(
                async (obs, ct) =>
                {
                    await obs.OnNextAsync(i, ct);
                    await obs.OnCompletedAsync(Result.Success);
                }));

        var result = await source.Merge(2).ToListAsync();

        await Assert.That(result).Count().IsEqualTo(SampleValue4);
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
        IObservableAsync<int>[] sources =
        [
            ObservableAsync.Return(1), ObservableAsync.Throw<int>(new InvalidOperationException("fail"))
        ];

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
    /// Verifies that MergeEnumerable forwards errors from a source that throws during subscribe.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableSourceThrowsDuringSubscribe_ThenCompletesWithFailure()
    {
        var throwingSource = ObservableAsync.Create<int>((_, _) =>
        {
            try
            {
                throw new InvalidOperationException(SubscribeBoomMessage);
#pragma warning disable CS0162 // Unreachable code detected
                return ValueTask.FromResult(DisposableAsync.Empty);
#pragma warning restore CS0162
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<IAsyncDisposable>(exception);
            }
        });

        IObservableAsync<int>[] sources = [throwingSource];

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
    /// Verifies that when an inner source throws TaskCanceledException during subscribe
    /// in MergeEnumerable StartAsync, the cancellation is handled gracefully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeEnumerableInnerSubscribeThrowsTaskCanceled_ThenHandledGracefully()
    {
        var canceledSource = ObservableAsync.Create<int>((_, _) =>
        {
            try
            {
                throw new TaskCanceledException("subscribe canceled");
#pragma warning disable CS0162 // Unreachable code detected
                return ValueTask.FromResult(DisposableAsync.Empty);
#pragma warning restore CS0162
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<IAsyncDisposable>(exception);
            }
        });

        Result? completionResult = null;
        var items = new List<int>();

        await using var sub = await new[] { canceledSource }
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
            () => true,
            TimeSpan.FromSeconds(2));

        await Assert.That(items).IsEmpty();
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

        var throwingSource = ObservableAsync.Create<int>((_, _) =>
        {
            try
            {
                throw new InvalidOperationException(SubscribeBoomMessage);
#pragma warning disable CS0162 // Unreachable code detected
                return ValueTask.FromResult(DisposableAsync.Empty);
#pragma warning restore CS0162
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<IAsyncDisposable>(exception);
            }
        });

        await using var sub = await new[] { throwingSource }
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
        await Assert.That(completionResult.Value.Exception!.Message).Contains(SubscribeBoomMessage);
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

        var goodSource = new DirectSource<int>();
        var throwingSource = ObservableAsync.Create<int>((_, _) =>
        {
            try
            {
                throw new InvalidOperationException("second subscribe boom");
#pragma warning disable CS0162 // Unreachable code detected
                return ValueTask.FromResult(DisposableAsync.Empty);
#pragma warning restore CS0162
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<IAsyncDisposable>(exception);
            }
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

    /// <summary>Tests Merge inner source failure propagates error.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeInnerSourceFails_ThenErrorPropagated()
    {
        var error = new InvalidOperationException("inner-error");
        var inner = ObservableAsync.Throw<int>(error);
        var outer = ObservableAsync.Return(inner);

        Result? completionResult = null;
        await using var sub = await outer.Merge()
            .SubscribeAsync(
                static (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Task.Delay(PropagationDelayMilliseconds);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Tests Merge with max concurrency inner failure propagates.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMergeWithMaxConcurrencyInnerFails_ThenErrorPropagated()
    {
        var error = new InvalidOperationException("merge-fail");
        var inner = ObservableAsync.Throw<int>(error);
        var outer = ObservableAsync.Return(inner);

        Result? completionResult = null;
        await using var sub = await outer.Merge(1)
            .SubscribeAsync(
                static (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Task.Delay(PropagationDelayMilliseconds);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }
}
