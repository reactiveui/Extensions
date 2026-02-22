// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Edge-case and error-propagation tests for operators across the ReactiveUI.Extensions.Async namespace.
/// Targets uncovered code paths for: Switch, Concat, Zip, Merge, Retry, Delay, Throttle, Timeout,
/// Select, Where, Scan, Distinct, DistinctUntilChanged, OnErrorResumeAsFailure, Wrap, GroupBy,
/// Using, OnDispose, Do, TakeWhile, SkipWhile, Take, Skip, Cast, OfType, SelectMany.
/// </summary>
public class OperatorEdgeCaseTests
{
    // ==========================================
    // Switch
    // ==========================================

    /// <summary>Tests that Switch completes when outer completes with no inner sequences.</summary>
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

        await Task.Delay(200);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests that Switch forwards error from inner sequence.</summary>
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

        await Task.Delay(200);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Tests that Switch forwards error resume from inner sequence.</summary>
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
        await Task.Delay(200);

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    // ==========================================
    // Concat
    // ==========================================

    /// <summary>Tests that Concat propagates error from first sequence.</summary>
    [Test]
    public void WhenConcatFirstFails_ThenErrorPropagated()
    {
        var first = ObservableAsync.Throw<int>(new InvalidOperationException("first fail"));
        var second = ObservableAsync.Return(2);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await first.Concat(second).ToListAsync());
    }

    /// <summary>Tests that Concat of empty sequences returns empty.</summary>
    [Test]
    public async Task WhenConcatEmptySequences_ThenReturnsEmpty()
    {
        var result = await ObservableAsync.Empty<int>()
            .Concat(ObservableAsync.Empty<int>())
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that Concat enumerable of empty returns empty.</summary>
    [Test]
    public async Task WhenConcatEnumerableEmpty_ThenReturnsEmpty()
    {
        var sources = Array.Empty<IObservableAsync<int>>();
        var result = await sources.Concat().ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    // ==========================================
    // Zip
    // ==========================================

    /// <summary>Tests that Zip with empty first source returns empty.</summary>
    [Test]
    public async Task WhenZipEmptyFirst_ThenReturnsEmpty()
    {
        var result = await ObservableAsync.Empty<int>()
            .Zip(ObservableAsync.Return("a"), (n, s) => $"{n}{s}")
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that Zip with empty second source returns empty.</summary>
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
    public void WhenZipNullSecond_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).Zip((IObservableAsync<string>)null!, (a, b) => a));
    }

    /// <summary>Tests that Zip throws on null resultSelector.</summary>
    [Test]
    public void WhenZipNullResultSelector_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).Zip(ObservableAsync.Return(2), (Func<int, int, int>)null!));
    }

    /// <summary>Tests that Zip error from first source completes with failure.</summary>
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
        await Task.Delay(100);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    // ==========================================
    // Merge
    // ==========================================

    /// <summary>Tests that Merge of empty enumerable returns empty.</summary>
    [Test]
    public async Task WhenMergeEmptyEnumerable_ThenReturnsEmpty()
    {
        var sources = Array.Empty<IObservableAsync<int>>();
        var result = await sources.Merge().ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that Merge error from one source propagates.</summary>
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

        await Task.Delay(200);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    // ==========================================
    // Retry
    // ==========================================

    /// <summary>Tests that Retry with zero retries propagates first error.</summary>
    [Test]
    public void WhenRetryZeroRetries_ThenPropagatesFirstError()
    {
        var source = ObservableAsync.Throw<int>(new InvalidOperationException("fail"));

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.Retry(0).ToListAsync());
    }

    /// <summary>Tests that Retry forwards error resume during each attempt.</summary>
    [Test]
    public async Task WhenRetryWithErrorResume_ThenErrorResumeForwarded()
    {
        var attempt = 0;
        var errors = new List<Exception>();

        var source = ObservableAsync.CreateAsBackgroundJob<int>(async (obs, ct) =>
        {
            attempt++;
            await obs.OnErrorResumeAsync(new InvalidOperationException($"warning {attempt}"), ct);
            if (attempt < 2)
            {
                await obs.OnCompletedAsync(Result.Failure(new InvalidOperationException("retry")));
                return;
            }

            await obs.OnNextAsync(42, ct);
            await obs.OnCompletedAsync(Result.Success);
        });

        var result = new List<int>();

        await using var sub = await source.Retry(3)
            .SubscribeAsync(
                (x, _) =>
                {
                    result.Add(x);
                    return default;
                },
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await Task.Delay(500);

        await Assert.That(result).Contains(42);
        await Assert.That(errors).Count().IsGreaterThanOrEqualTo(1);
    }

    // ==========================================
    // Delay
    // ==========================================

    /// <summary>Tests that Delay forwards error resume from source.</summary>
    [Test]
    public async Task WhenDelaySourceErrorResume_ThenForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("warning"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var errors = new List<Exception>();

        await using var sub = await source
            .Delay(TimeSpan.FromMilliseconds(10))
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

    /// <summary>Tests that Delay forwards failure completion from source.</summary>
    [Test]
    public async Task WhenDelaySourceFails_ThenFailureForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));
            return DisposableAsync.Empty;
        });

        Result? completionResult = null;

        await using var sub = await source
            .Delay(TimeSpan.FromMilliseconds(10))
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
    // Throttle
    // ==========================================

    /// <summary>Tests that Throttle forwards error resume from source.</summary>
    [Test]
    public async Task WhenThrottleSourceErrorResume_ThenForwarded()
    {
        var source = SubjectAsync.Create<int>();
        var errors = new List<Exception>();

        await using var sub = await source.Values
            .Throttle(TimeSpan.FromMilliseconds(50))
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

    /// <summary>Tests that Throttle forwards completion from source.</summary>
    [Test]
    public async Task WhenThrottleSourceCompletes_ThenCompletionForwarded()
    {
        var source = SubjectAsync.Create<int>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .Throttle(TimeSpan.FromMilliseconds(50))
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

    /// <summary>Tests that Throttle forwards failure from source.</summary>
    [Test]
    public async Task WhenThrottleSourceFails_ThenFailureForwarded()
    {
        var source = SubjectAsync.Create<int>();
        Result? completionResult = null;

        await using var sub = await source.Values
            .Throttle(TimeSpan.FromMilliseconds(50))
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await source.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));
        await Task.Delay(100);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    // ==========================================
    // Timeout
    // ==========================================

    /// <summary>Tests that Timeout forwards error resume from source.</summary>
    [Test]
    public async Task WhenTimeoutSourceErrorResume_ThenForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("warning"), ct);
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var errors = new List<Exception>();

        await using var sub = await source
            .Timeout(TimeSpan.FromSeconds(5))
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

    /// <summary>Tests that Timeout with fallback negative duration throws.</summary>
    [Test]
    public void WhenTimeoutWithFallbackNegativeDuration_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservableAsync.Return(1).Timeout(TimeSpan.FromMilliseconds(-1), ObservableAsync.Return(99)));
    }

    // ==========================================
    // Select
    // ==========================================

    /// <summary>Tests that Select forwards error resume from source.</summary>
    [Test]
    public async Task WhenSelectSourceErrorResume_ThenForwarded()
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
            .Select(x => x * 10)
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

        await Assert.That(items).IsEquivalentTo(new[] { 10, 20 });
        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that async Select forwards error resume from source.</summary>
    [Test]
    public async Task WhenSelectAsyncSourceErrorResume_ThenForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("warning"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var errors = new List<Exception>();

        await using var sub = await source
            .Select(async (x, ct) =>
            {
                await Task.Yield();
                return x;
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

    // ==========================================
    // Where
    // ==========================================

    /// <summary>Tests that Where forwards error resume from source.</summary>
    [Test]
    public async Task WhenWhereSourceErrorResume_ThenForwarded()
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
            .Where(x => x > 0)
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

    /// <summary>Tests that async Where forwards failure completion.</summary>
    [Test]
    public async Task WhenWhereAsyncSourceFails_ThenFailureForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));
            return DisposableAsync.Empty;
        });

        Result? completionResult = null;

        await using var sub = await source
            .Where(async (x, ct) =>
            {
                await Task.Yield();
                return true;
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
    // Scan
    // ==========================================

    /// <summary>Tests that Scan forwards error resume from source.</summary>
    [Test]
    public async Task WhenScanSourceErrorResume_ThenForwarded()
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
            .Scan(0, (acc, x) => acc + x)
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

        await Assert.That(items).IsEquivalentTo(new[] { 1, 3 });
        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that async Scan forwards failure completion.</summary>
    [Test]
    public async Task WhenScanAsyncSourceFails_ThenFailureForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));
            return DisposableAsync.Empty;
        });

        Result? completionResult = null;

        await using var sub = await source
            .Scan(0, async (acc, x, ct) =>
            {
                await Task.Yield();
                return acc + x;
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

    /// <summary>Tests that async Scan null accumulator throws.</summary>
    [Test]
    public void WhenScanAsyncNullAccumulator_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).Scan(0, (Func<int, int, CancellationToken, ValueTask<int>>)null!));
    }

    // ==========================================
    // Distinct / DistinctUntilChanged
    // ==========================================

    /// <summary>Tests that Distinct on empty returns empty.</summary>
    [Test]
    public async Task WhenDistinctOnEmpty_ThenReturnsEmpty()
    {
        var result = await ObservableAsync.Empty<int>()
            .Distinct()
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that DistinctUntilChanged on empty returns empty.</summary>
    [Test]
    public async Task WhenDistinctUntilChangedOnEmpty_ThenReturnsEmpty()
    {
        var result = await ObservableAsync.Empty<int>()
            .DistinctUntilChanged()
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that DistinctUntilChanged with single element returns it.</summary>
    [Test]
    public async Task WhenDistinctUntilChangedSingleElement_ThenReturnsIt()
    {
        var result = await ObservableAsync.Return(42)
            .DistinctUntilChanged()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 42 });
    }

    // ==========================================
    // OnErrorResumeAsFailure
    // ==========================================

    /// <summary>Tests that OnErrorResumeAsFailure passes through successful completion.</summary>
    [Test]
    public async Task WhenOnErrorResumeAsFailureSuccessfulSource_ThenPassesThrough()
    {
        var result = await ObservableAsync.Range(1, 3)
            .OnErrorResumeAsFailure()
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests that OnErrorResumeAsFailure converts error resume to failure completion.</summary>
    [Test]
    public async Task WhenOnErrorResumeAsFailureWithError_ThenCompletesWithFailure()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(new InvalidOperationException("err"), ct);
            return DisposableAsync.Empty;
        });

        Result? completionResult = null;

        await using var sub = await source
            .OnErrorResumeAsFailure()
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
    // Wrap
    // ==========================================

    /// <summary>Tests that Wrap with null observer throws.</summary>
    [Test]
    public void WhenWrapNullObserver_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Wrap<int>(null!));
    }

    // ==========================================
    // GroupBy
    // ==========================================

    /// <summary>Tests that GroupBy forwards source failure.</summary>
    [Test]
    public async Task WhenGroupBySourceFails_ThenFailureForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));
            return DisposableAsync.Empty;
        });

        Result? completionResult = null;

        await using var sub = await source
            .GroupBy(x => x % 2)
            .SubscribeAsync(
                (group, _) => default,
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

    /// <summary>Tests that GroupBy on empty source returns empty.</summary>
    [Test]
    public async Task WhenGroupByOnEmpty_ThenReturnsEmpty()
    {
        var groups = new List<int>();

        await using var sub = await ObservableAsync.Empty<int>()
            .GroupBy(x => x)
            .SubscribeAsync(
                (group, _) =>
                {
                    groups.Add(group.Key);
                    return default;
                },
                null,
                null);

        await Task.Delay(100);

        await Assert.That(groups).IsEmpty();
    }

    // ==========================================
    // Using
    // ==========================================

    /// <summary>Tests that Using disposes resource on source failure.</summary>
    [Test]
    public async Task WhenUsingSourceFails_ThenResourceDisposed()
    {
        var resourceDisposed = false;

        var source = ObservableAsync.Using(
            async ct =>
            {
                await Task.Yield();
                return new TestResource(() => resourceDisposed = true);
            },
            _ => ObservableAsync.Throw<int>(new InvalidOperationException("fail")));

        Result? completionResult = null;

        await using var sub = await source
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
        await Assert.That(resourceDisposed).IsTrue();
    }

    // ==========================================
    // OnDispose
    // ==========================================

    /// <summary>Tests that async OnDispose is called when source errors.</summary>
    [Test]
    public async Task WhenOnDisposeAsyncSourceFails_ThenCallbackInvoked()
    {
        var disposed = false;

        var source = ObservableAsync.Throw<int>(new InvalidOperationException("fail"))
            .OnDispose(() =>
            {
                disposed = true;
                return default;
            });

        Result? completionResult = null;

        await using var sub = await source
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

    /// <summary>Tests that sync OnDispose is called when source errors.</summary>
    [Test]
    public async Task WhenOnDisposeSyncSourceFails_ThenCallbackInvoked()
    {
        var disposed = false;

        var source = ObservableAsync.Throw<int>(new InvalidOperationException("fail"))
            .OnDispose(() => disposed = true);

        await using var sub = await source
            .SubscribeAsync(
                (x, _) => default,
                null,
                null);

        await Task.Delay(200);

        await Assert.That(disposed).IsTrue();
    }

    // ==========================================
    // Do
    // ==========================================

    /// <summary>Tests that Do forwards failure completion.</summary>
    [Test]
    public async Task WhenDoSourceFails_ThenFailureForwarded()
    {
        var doInvoked = false;

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnCompletedAsync(Result.Failure(new InvalidOperationException("fail")));
            return DisposableAsync.Empty;
        });

        Result? completionResult = null;

        await using var sub = await source
            .Do(onNext: x => doInvoked = true)
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Task.Delay(200);

        await Assert.That(doInvoked).IsFalse();
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Tests that async Do with error callback forwards error resume and completion.</summary>
    [Test]
    public async Task WhenDoAsyncWithAllCallbacksAndError_ThenAllInvoked()
    {
        var errors = new List<Exception>();
        Result? completedResult = null;

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnNextAsync(1, ct);
            await observer.OnErrorResumeAsync(new InvalidOperationException("err"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source
            .Do(
                onNext: (x, ct) => default,
                onErrorResume: (ex, ct) =>
                {
                    errors.Add(ex);
                    return default;
                },
                onCompleted: r =>
                {
                    completedResult = r;
                    return default;
                })
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) => default,
                null);

        await Task.Delay(200);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(completedResult).IsNotNull();
    }

    // ==========================================
    // TakeWhile
    // ==========================================

    /// <summary>Tests that TakeWhile forwards error resume from source.</summary>
    [Test]
    public async Task WhenTakeWhileSourceErrorResume_ThenForwarded()
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
            .TakeWhile(x => true)
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

    /// <summary>Tests that async TakeWhile null predicate throws.</summary>
    [Test]
    public void WhenTakeWhileAsyncNullPredicate_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).TakeWhile((Func<int, CancellationToken, ValueTask<bool>>)null!));
    }

    // ==========================================
    // SkipWhile
    // ==========================================

    /// <summary>Tests that SkipWhile forwards error resume from source.</summary>
    [Test]
    public async Task WhenSkipWhileSourceErrorResume_ThenForwarded()
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
            .SkipWhile(x => false)
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

    /// <summary>Tests that async SkipWhile null predicate throws.</summary>
    [Test]
    public void WhenSkipWhileAsyncNullPredicate_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).SkipWhile((Func<int, CancellationToken, ValueTask<bool>>)null!));
    }

    // ==========================================
    // Take / Skip edge cases
    // ==========================================

    /// <summary>Tests that Take forwards error resume from source.</summary>
    [Test]
    public async Task WhenTakeSourceErrorResume_ThenForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("warning"), ct);
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var errors = new List<Exception>();
        var items = new List<int>();

        await using var sub = await source
            .Take(10)
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

        await Assert.That(items).IsEquivalentTo(new[] { 1 });
        await Assert.That(errors).Count().IsEqualTo(1);
    }

    /// <summary>Tests that Skip forwards error resume from source.</summary>
    [Test]
    public async Task WhenSkipSourceErrorResume_ThenForwarded()
    {
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("warning"), ct);
            await observer.OnNextAsync(1, ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        var errors = new List<Exception>();

        await using var sub = await source
            .Skip(0)
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

    // ==========================================
    // Cast / OfType edge cases
    // ==========================================

    /// <summary>Tests that Cast on empty returns empty.</summary>
    [Test]
    public async Task WhenCastOnEmpty_ThenReturnsEmpty()
    {
        var result = await ObservableAsync.Empty<object>()
            .Cast<object, string>()
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that OfType on empty returns empty.</summary>
    [Test]
    public async Task WhenOfTypeOnEmpty_ThenReturnsEmpty()
    {
        var result = await ObservableAsync.Empty<object>()
            .OfType<object, string>()
            .ToListAsync();

        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that Cast with incompatible type throws.</summary>
    [Test]
    public void WhenCastIncompatibleType_ThenThrows()
    {
        var source = ObservableAsync.Return<object>(42);

        Assert.ThrowsAsync<InvalidCastException>(
            async () => await source.Cast<object, string>().ToListAsync());
    }

    // ==========================================
    // SelectMany
    // ==========================================

    /// <summary>Tests that SelectMany with async selector and null selector throws.</summary>
    [Test]
    public void WhenSelectManyAsyncNullSelector_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).SelectMany((Func<int, CancellationToken, ValueTask<IObservableAsync<int>>>)null!));
    }

    /// <summary>Tests that SelectMany with result selector null throws.</summary>
    [Test]
    public void WhenSelectManyNullResultSelector_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).SelectMany(
                x => ObservableAsync.Return(x),
                (Func<int, int, int>)null!));
    }

    /// <summary>Tests that SelectMany forwards error from inner.</summary>
    [Test]
    public async Task WhenSelectManyInnerFails_ThenErrorPropagated()
    {
        Result? completionResult = null;

        await using var sub = await ObservableAsync.Return(1)
            .SelectMany(_ => ObservableAsync.Throw<int>(new InvalidOperationException("inner fail")))
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
    // Catch
    // ==========================================

    /// <summary>Tests that Catch with null handler throws.</summary>
    [Test]
    public void WhenCatchNullHandler_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).Catch((Func<Exception, IObservableAsync<int>>)null!));
    }

    /// <summary>Tests that Catch with handler exception propagates handler failure.</summary>
    [Test]
    public async Task WhenCatchHandlerThrows_ThenPropagatesHandlerFailure()
    {
        var source = ObservableAsync.Throw<int>(new InvalidOperationException("source fail"));

        Result? completionResult = null;

        await using var sub = await source
            .Catch(new Func<Exception, IObservableAsync<int>>(ex => throw new InvalidOperationException("handler fail")))
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
    // Factory methods
    // ==========================================

    /// <summary>Tests that Throw creates a sequence that fails immediately.</summary>
    [Test]
    public void WhenThrow_ThenFailsImmediately()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ObservableAsync.Throw<int>(new InvalidOperationException("boom")).ToListAsync());
    }

    /// <summary>Tests that Never does not complete.</summary>
    [Test]
    public async Task WhenNever_ThenDoesNotComplete()
    {
        Result? completionResult = null;

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await using var sub = await ObservableAsync.Never<int>()
            .TakeUntil(cts.Token)
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await Task.Delay(300);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests that Empty completes immediately with no elements.</summary>
    [Test]
    public async Task WhenEmpty_ThenCompletesWithNoElements()
    {
        var result = await ObservableAsync.Empty<int>().ToListAsync();
        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that Return emits single value and completes.</summary>
    [Test]
    public async Task WhenReturn_ThenEmitsSingleValueAndCompletes()
    {
        var result = await ObservableAsync.Return(42).ToListAsync();
        await Assert.That(result).IsEquivalentTo(new[] { 42 });
    }

    /// <summary>Tests that Defer creates a new observable for each subscriber.</summary>
    [Test]
    public async Task WhenDefer_ThenCreatesNewObservablePerSubscriber()
    {
        var count = 0;
        var source = ObservableAsync.Defer(ct =>
        {
            count++;
            return new ValueTask<IObservableAsync<int>>(ObservableAsync.Return(count));
        });

        var first = await source.FirstAsync();
        var second = await source.FirstAsync();

        await Assert.That(first).IsEqualTo(1);
        await Assert.That(second).IsEqualTo(2);
    }

    /// <summary>Tests that Range with zero count returns empty.</summary>
    [Test]
    public async Task WhenRangeZeroCount_ThenReturnsEmpty()
    {
        var result = await ObservableAsync.Range(1, 0).ToListAsync();
        await Assert.That(result).IsEmpty();
    }

    /// <summary>Tests that ToObservableAsync converts enumerable to observable.</summary>
    [Test]
    public async Task WhenToObservableAsync_ThenConvertsEnumerableToObservable()
    {
        var items = new[] { 10, 20, 30 };
        var result = await items.ToObservableAsync().ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 10, 20, 30 });
    }

    /// <summary>Tests that ToObservableAsync empty array returns empty.</summary>
    [Test]
    public async Task WhenToObservableAsyncEmpty_ThenReturnsEmpty()
    {
        var result = await Array.Empty<int>().ToObservableAsync().ToListAsync();
        await Assert.That(result).IsEmpty();
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
