// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;
using AsyncObs = ReactiveUI.Extensions.Async.ObservableAsync;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Generated tests for CombineLatest arities 2-8 covering catch blocks,
/// disposed guards in OnNextCombined/OnErrorResume, and OnNext_N early returns.
/// </summary>
public class CombineLatestArityTests
{

    /// <summary>
    /// Verifies that CombineLatest2 disposes on subscription failure (catch block).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2SubscriptionThrows_ThenDisposesAndRethrows()
    {
        var s1 = SubjectAsync.Create<int>();
        var throwingSrc = AsyncObs.Create<int>((_, _) => throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await AsyncObs.CombineLatest(s1.Values, throwingSrc, (v1, v2) => v1 + v2)
                .SubscribeAsync((_, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that CombineLatest2 OnNextCombined guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2DisposedBeforeCombine_ThenOnNextCombinedIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var results = new List<int>();

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (v1, v2) => v1 + v2)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);

        await sub.DisposeAsync();

        await s1.OnNextAsync(99, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest2 OnErrorResume guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2DisposedBeforeError_ThenOnErrorResumeIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        Exception? receivedError = null;

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (v1, v2) => v1 + v2)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                null);

        await sub.DisposeAsync();

        await s1.OnErrorResumeAsync(new InvalidOperationException("post-dispose error"), CancellationToken.None);

        await Assert.That(receivedError).IsNull();
    }

    /// <summary>
    /// Verifies that CombineLatest2 OnNext for the last source returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2LastSourceEmitsFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (v1, v2) => v1 + v2)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s2.OnNextAsync(42, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest3 disposes on subscription failure (catch block).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3SubscriptionThrows_ThenDisposesAndRethrows()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var throwingSrc = AsyncObs.Create<int>((_, _) => throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await AsyncObs.CombineLatest(s1.Values, s2.Values, throwingSrc, (v1, v2, v3) => v1 + v2 + v3)
                .SubscribeAsync((_, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that CombineLatest3 OnNextCombined guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3DisposedBeforeCombine_ThenOnNextCombinedIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var results = new List<int>();

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (v1, v2, v3) => v1 + v2 + v3)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);

        await sub.DisposeAsync();

        await s1.OnNextAsync(99, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest3 OnErrorResume guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3DisposedBeforeError_ThenOnErrorResumeIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        Exception? receivedError = null;

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (v1, v2, v3) => v1 + v2 + v3)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                null);

        await sub.DisposeAsync();

        await s1.OnErrorResumeAsync(new InvalidOperationException("post-dispose error"), CancellationToken.None);

        await Assert.That(receivedError).IsNull();
    }

    /// <summary>
    /// Verifies that CombineLatest3 OnNext for the last source returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3LastSourceEmitsFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (v1, v2, v3) => v1 + v2 + v3)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s3.OnNextAsync(42, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest3 OnNext for the middle source returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3MiddleSourceEmitsFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (v1, v2, v3) => v1 + v2 + v3)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s2.OnNextAsync(42, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest3 OnNext_1 calls OnNextCombined when source 1 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3Source1ReEmits_ThenOnNextCombinedViaOnNext1()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
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

        await s1.OnNextAsync(2, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(112);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest3 OnNext_2 calls OnNextCombined when source 2 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3Source2ReEmits_ThenOnNextCombinedViaOnNext2()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
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

        await s2.OnNextAsync(20, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(121);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest4 disposes on subscription failure (catch block).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4SubscriptionThrows_ThenDisposesAndRethrows()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var throwingSrc = AsyncObs.Create<int>((_, _) => throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, throwingSrc, (v1, v2, v3, v4) => v1 + v2 + v3 + v4)
                .SubscribeAsync((_, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that CombineLatest4 OnNextCombined guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4DisposedBeforeCombine_ThenOnNextCombinedIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var results = new List<int>();

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (v1, v2, v3, v4) => v1 + v2 + v3 + v4)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);

        await sub.DisposeAsync();

        await s1.OnNextAsync(99, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest4 OnErrorResume guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4DisposedBeforeError_ThenOnErrorResumeIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        Exception? receivedError = null;

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (v1, v2, v3, v4) => v1 + v2 + v3 + v4)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                null);

        await sub.DisposeAsync();

        await s1.OnErrorResumeAsync(new InvalidOperationException("post-dispose error"), CancellationToken.None);

        await Assert.That(receivedError).IsNull();
    }

    /// <summary>
    /// Verifies that CombineLatest4 OnNext for the last source returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4LastSourceEmitsFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (v1, v2, v3, v4) => v1 + v2 + v3 + v4)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s4.OnNextAsync(42, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest4 OnNext for middle sources returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4MiddleSourcesEmitFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (v1, v2, v3, v4) => v1 + v2 + v3 + v4)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s2.OnNextAsync(42, CancellationToken.None);
        await s3.OnNextAsync(43, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest4 OnNext_1 calls OnNextCombined when source 1 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Source1ReEmits_ThenOnNextCombinedViaOnNext1()
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

        await s1.OnNextAsync(2, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(1112);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest4 OnNext_2 calls OnNextCombined when source 2 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Source2ReEmits_ThenOnNextCombinedViaOnNext2()
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

        await s2.OnNextAsync(20, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(1121);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest4 OnNext_3 calls OnNextCombined when source 3 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Source3ReEmits_ThenOnNextCombinedViaOnNext3()
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

        await s3.OnNextAsync(200, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(1211);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest5 disposes on subscription failure (catch block).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5SubscriptionThrows_ThenDisposesAndRethrows()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var throwingSrc = AsyncObs.Create<int>((_, _) => throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, throwingSrc, (v1, v2, v3, v4, v5) => v1 + v2 + v3 + v4 + v5)
                .SubscribeAsync((_, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that CombineLatest5 OnNextCombined guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5DisposedBeforeCombine_ThenOnNextCombinedIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var results = new List<int>();

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (v1, v2, v3, v4, v5) => v1 + v2 + v3 + v4 + v5)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);

        await sub.DisposeAsync();

        await s1.OnNextAsync(99, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest5 OnErrorResume guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5DisposedBeforeError_ThenOnErrorResumeIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        Exception? receivedError = null;

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (v1, v2, v3, v4, v5) => v1 + v2 + v3 + v4 + v5)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                null);

        await sub.DisposeAsync();

        await s1.OnErrorResumeAsync(new InvalidOperationException("post-dispose error"), CancellationToken.None);

        await Assert.That(receivedError).IsNull();
    }

    /// <summary>
    /// Verifies that CombineLatest5 OnNext for the last source returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5LastSourceEmitsFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (v1, v2, v3, v4, v5) => v1 + v2 + v3 + v4 + v5)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s5.OnNextAsync(42, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest5 OnNext for middle sources returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5MiddleSourcesEmitFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (v1, v2, v3, v4, v5) => v1 + v2 + v3 + v4 + v5)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s2.OnNextAsync(42, CancellationToken.None);
        await s3.OnNextAsync(43, CancellationToken.None);
        await s4.OnNextAsync(44, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest5 OnNext_1 calls OnNextCombined when source 1 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Source1ReEmits_ThenOnNextCombinedViaOnNext1()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
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
        await s5.OnNextAsync(10000, CancellationToken.None);

        await s1.OnNextAsync(2, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(11112);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest5 OnNext_2 calls OnNextCombined when source 2 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Source2ReEmits_ThenOnNextCombinedViaOnNext2()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
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
        await s5.OnNextAsync(10000, CancellationToken.None);

        await s2.OnNextAsync(20, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(11121);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest5 OnNext_3 calls OnNextCombined when source 3 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Source3ReEmits_ThenOnNextCombinedViaOnNext3()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
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
        await s5.OnNextAsync(10000, CancellationToken.None);

        await s3.OnNextAsync(200, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(11211);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest5 OnNext_4 calls OnNextCombined when source 4 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Source4ReEmits_ThenOnNextCombinedViaOnNext4()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
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
        await s5.OnNextAsync(10000, CancellationToken.None);

        await s4.OnNextAsync(2000, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(12111);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest6 disposes on subscription failure (catch block).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6SubscriptionThrows_ThenDisposesAndRethrows()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var throwingSrc = AsyncObs.Create<int>((_, _) => throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, throwingSrc, (v1, v2, v3, v4, v5, v6) => v1 + v2 + v3 + v4 + v5 + v6)
                .SubscribeAsync((_, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that CombineLatest6 OnNextCombined guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6DisposedBeforeCombine_ThenOnNextCombinedIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var results = new List<int>();

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (v1, v2, v3, v4, v5, v6) => v1 + v2 + v3 + v4 + v5 + v6)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);

        await sub.DisposeAsync();

        await s1.OnNextAsync(99, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest6 OnErrorResume guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6DisposedBeforeError_ThenOnErrorResumeIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        Exception? receivedError = null;

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (v1, v2, v3, v4, v5, v6) => v1 + v2 + v3 + v4 + v5 + v6)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                null);

        await sub.DisposeAsync();

        await s1.OnErrorResumeAsync(new InvalidOperationException("post-dispose error"), CancellationToken.None);

        await Assert.That(receivedError).IsNull();
    }

    /// <summary>
    /// Verifies that CombineLatest6 OnNext for the last source returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6LastSourceEmitsFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (v1, v2, v3, v4, v5, v6) => v1 + v2 + v3 + v4 + v5 + v6)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s6.OnNextAsync(42, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest6 OnNext for middle sources returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6MiddleSourcesEmitFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (v1, v2, v3, v4, v5, v6) => v1 + v2 + v3 + v4 + v5 + v6)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s2.OnNextAsync(42, CancellationToken.None);
        await s3.OnNextAsync(43, CancellationToken.None);
        await s4.OnNextAsync(44, CancellationToken.None);
        await s5.OnNextAsync(45, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest6 OnNext_1 calls OnNextCombined when source 1 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Source1ReEmits_ThenOnNextCombinedViaOnNext1()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);

        await s1.OnNextAsync(2, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(111112);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest6 OnNext_2 calls OnNextCombined when source 2 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Source2ReEmits_ThenOnNextCombinedViaOnNext2()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);

        await s2.OnNextAsync(20, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(111121);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest6 OnNext_3 calls OnNextCombined when source 3 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Source3ReEmits_ThenOnNextCombinedViaOnNext3()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);

        await s3.OnNextAsync(200, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(111211);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest6 OnNext_4 calls OnNextCombined when source 4 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Source4ReEmits_ThenOnNextCombinedViaOnNext4()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);

        await s4.OnNextAsync(2000, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(112111);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest6 OnNext_5 calls OnNextCombined when source 5 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Source5ReEmits_ThenOnNextCombinedViaOnNext5()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);

        await s5.OnNextAsync(20000, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(121111);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest7 disposes on subscription failure (catch block).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7SubscriptionThrows_ThenDisposesAndRethrows()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var throwingSrc = AsyncObs.Create<int>((_, _) => throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, throwingSrc, (v1, v2, v3, v4, v5, v6, v7) => v1 + v2 + v3 + v4 + v5 + v6 + v7)
                .SubscribeAsync((_, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnNextCombined guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7DisposedBeforeCombine_ThenOnNextCombinedIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var results = new List<int>();

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (v1, v2, v3, v4, v5, v6, v7) => v1 + v2 + v3 + v4 + v5 + v6 + v7)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);

        await sub.DisposeAsync();

        await s1.OnNextAsync(99, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnErrorResume guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7DisposedBeforeError_ThenOnErrorResumeIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        Exception? receivedError = null;

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (v1, v2, v3, v4, v5, v6, v7) => v1 + v2 + v3 + v4 + v5 + v6 + v7)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                null);

        await sub.DisposeAsync();

        await s1.OnErrorResumeAsync(new InvalidOperationException("post-dispose error"), CancellationToken.None);

        await Assert.That(receivedError).IsNull();
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnNext for the last source returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7LastSourceEmitsFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (v1, v2, v3, v4, v5, v6, v7) => v1 + v2 + v3 + v4 + v5 + v6 + v7)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s7.OnNextAsync(42, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnNext for middle sources returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7MiddleSourcesEmitFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (v1, v2, v3, v4, v5, v6, v7) => v1 + v2 + v3 + v4 + v5 + v6 + v7)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s2.OnNextAsync(42, CancellationToken.None);
        await s3.OnNextAsync(43, CancellationToken.None);
        await s4.OnNextAsync(44, CancellationToken.None);
        await s5.OnNextAsync(45, CancellationToken.None);
        await s6.OnNextAsync(46, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnNext_1 calls OnNextCombined when source 1 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Source1ReEmits_ThenOnNextCombinedViaOnNext1()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);

        await s1.OnNextAsync(2, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(1111112);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnNext_2 calls OnNextCombined when source 2 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Source2ReEmits_ThenOnNextCombinedViaOnNext2()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);

        await s2.OnNextAsync(20, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(1111121);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnNext_3 calls OnNextCombined when source 3 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Source3ReEmits_ThenOnNextCombinedViaOnNext3()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);

        await s3.OnNextAsync(200, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(1111211);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnNext_4 calls OnNextCombined when source 4 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Source4ReEmits_ThenOnNextCombinedViaOnNext4()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);

        await s4.OnNextAsync(2000, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(1112111);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnNext_5 calls OnNextCombined when source 5 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Source5ReEmits_ThenOnNextCombinedViaOnNext5()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);

        await s5.OnNextAsync(20000, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(1121111);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnNext_6 calls OnNextCombined when source 6 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Source6ReEmits_ThenOnNextCombinedViaOnNext6()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);

        await s6.OnNextAsync(200000, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(1211111);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest8 disposes on subscription failure (catch block).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8SubscriptionThrows_ThenDisposesAndRethrows()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var throwingSrc = AsyncObs.Create<int>((_, _) => throw new InvalidOperationException("subscribe failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, throwingSrc, (v1, v2, v3, v4, v5, v6, v7, v8) => v1 + v2 + v3 + v4 + v5 + v6 + v7 + v8)
                .SubscribeAsync((_, _) => default, null, null));
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnNextCombined guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8DisposedBeforeCombine_ThenOnNextCombinedIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();
        var results = new List<int>();

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (v1, v2, v3, v4, v5, v6, v7, v8) => v1 + v2 + v3 + v4 + v5 + v6 + v7 + v8)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s8.OnNextAsync(8, CancellationToken.None);

        await sub.DisposeAsync();

        await s1.OnNextAsync(99, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnErrorResume guard returns when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8DisposedBeforeError_ThenOnErrorResumeIsGuarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();
        Exception? receivedError = null;

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (v1, v2, v3, v4, v5, v6, v7, v8) => v1 + v2 + v3 + v4 + v5 + v6 + v7 + v8)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                null);

        await sub.DisposeAsync();

        await s1.OnErrorResumeAsync(new InvalidOperationException("post-dispose error"), CancellationToken.None);

        await Assert.That(receivedError).IsNull();
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnNext for the last source returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8LastSourceEmitsFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (v1, v2, v3, v4, v5, v6, v7, v8) => v1 + v2 + v3 + v4 + v5 + v6 + v7 + v8)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s8.OnNextAsync(42, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnNext for middle sources returns early when not all sources have values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8MiddleSourcesEmitFirst_ThenNoEmission()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (v1, v2, v3, v4, v5, v6, v7, v8) => v1 + v2 + v3 + v4 + v5 + v6 + v7 + v8)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s2.OnNextAsync(42, CancellationToken.None);
        await s3.OnNextAsync(43, CancellationToken.None);
        await s4.OnNextAsync(44, CancellationToken.None);
        await s5.OnNextAsync(45, CancellationToken.None);
        await s6.OnNextAsync(46, CancellationToken.None);
        await s7.OnNextAsync(47, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnNext_1 calls OnNextCombined when source 1 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Source1ReEmits_ThenOnNextCombinedViaOnNext1()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);
        await s8.OnNextAsync(10000000, CancellationToken.None);

        await s1.OnNextAsync(2, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(11111112);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnNext_2 calls OnNextCombined when source 2 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Source2ReEmits_ThenOnNextCombinedViaOnNext2()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);
        await s8.OnNextAsync(10000000, CancellationToken.None);

        await s2.OnNextAsync(20, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(11111121);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnNext_3 calls OnNextCombined when source 3 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Source3ReEmits_ThenOnNextCombinedViaOnNext3()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);
        await s8.OnNextAsync(10000000, CancellationToken.None);

        await s3.OnNextAsync(200, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(11111211);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnNext_4 calls OnNextCombined when source 4 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Source4ReEmits_ThenOnNextCombinedViaOnNext4()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);
        await s8.OnNextAsync(10000000, CancellationToken.None);

        await s4.OnNextAsync(2000, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(11112111);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnNext_5 calls OnNextCombined when source 5 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Source5ReEmits_ThenOnNextCombinedViaOnNext5()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);
        await s8.OnNextAsync(10000000, CancellationToken.None);

        await s5.OnNextAsync(20000, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(11121111);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnNext_6 calls OnNextCombined when source 6 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Source6ReEmits_ThenOnNextCombinedViaOnNext6()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);
        await s8.OnNextAsync(10000000, CancellationToken.None);

        await s6.OnNextAsync(200000, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(11211111);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnNext_7 calls OnNextCombined when source 7 re-emits after all values are present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Source7ReEmits_ThenOnNextCombinedViaOnNext7()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();
        var results = new List<int>();

        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
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
        await s5.OnNextAsync(10000, CancellationToken.None);
        await s6.OnNextAsync(100000, CancellationToken.None);
        await s7.OnNextAsync(1000000, CancellationToken.None);
        await s8.OnNextAsync(10000000, CancellationToken.None);

        await s7.OnNextAsync(2000000, CancellationToken.None);

        await Assert.That(results[^1]).IsEqualTo(12111111);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
    }

    /// <summary>
    /// Verifies that CombineLatest2 OnNextCombined guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2DisposedViaError_ThenOnNextCombinedGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var results = new List<int>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, (a, b) => a + b)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src2.EmitNext(99);

        await Assert.That(results).Count().IsEqualTo(1);

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest2 OnErrorResume guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2DisposedViaError_ThenOnErrorResumeGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        Exception? receivedError = null;
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, (a, b) => a + b)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src2.EmitError(new InvalidOperationException("ignored"));

        await Assert.That(receivedError).IsNull();

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest3 OnNextCombined guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3DisposedViaError_ThenOnNextCombinedGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var results = new List<int>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src3.EmitNext(99);

        await Assert.That(results).Count().IsEqualTo(1);

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest3 OnErrorResume guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3DisposedViaError_ThenOnErrorResumeGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        Exception? receivedError = null;
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src3.EmitError(new InvalidOperationException("ignored"));

        await Assert.That(receivedError).IsNull();

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest4 OnNextCombined guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4DisposedViaError_ThenOnNextCombinedGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var src4 = new DirectSource<int>();
        var results = new List<int>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, src4, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);
        await src4.EmitNext(4);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src4.EmitNext(99);

        await Assert.That(results).Count().IsEqualTo(1);

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest4 OnErrorResume guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4DisposedViaError_ThenOnErrorResumeGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var src4 = new DirectSource<int>();
        Exception? receivedError = null;
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, src4, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);
        await src4.EmitNext(4);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src4.EmitError(new InvalidOperationException("ignored"));

        await Assert.That(receivedError).IsNull();

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest5 OnNextCombined guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5DisposedViaError_ThenOnNextCombinedGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var src4 = new DirectSource<int>();
        var src5 = new DirectSource<int>();
        var results = new List<int>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, src4, src5, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);
        await src4.EmitNext(4);
        await src5.EmitNext(5);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src5.EmitNext(99);

        await Assert.That(results).Count().IsEqualTo(1);

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest5 OnErrorResume guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5DisposedViaError_ThenOnErrorResumeGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var src4 = new DirectSource<int>();
        var src5 = new DirectSource<int>();
        Exception? receivedError = null;
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, src4, src5, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);
        await src4.EmitNext(4);
        await src5.EmitNext(5);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src5.EmitError(new InvalidOperationException("ignored"));

        await Assert.That(receivedError).IsNull();

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest6 OnNextCombined guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6DisposedViaError_ThenOnNextCombinedGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var src4 = new DirectSource<int>();
        var src5 = new DirectSource<int>();
        var src6 = new DirectSource<int>();
        var results = new List<int>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, src4, src5, src6, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);
        await src4.EmitNext(4);
        await src5.EmitNext(5);
        await src6.EmitNext(6);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src6.EmitNext(99);

        await Assert.That(results).Count().IsEqualTo(1);

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest6 OnErrorResume guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6DisposedViaError_ThenOnErrorResumeGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var src4 = new DirectSource<int>();
        var src5 = new DirectSource<int>();
        var src6 = new DirectSource<int>();
        Exception? receivedError = null;
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, src4, src5, src6, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);
        await src4.EmitNext(4);
        await src5.EmitNext(5);
        await src6.EmitNext(6);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src6.EmitError(new InvalidOperationException("ignored"));

        await Assert.That(receivedError).IsNull();

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnNextCombined guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7DisposedViaError_ThenOnNextCombinedGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var src4 = new DirectSource<int>();
        var src5 = new DirectSource<int>();
        var src6 = new DirectSource<int>();
        var src7 = new DirectSource<int>();
        var results = new List<int>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, src4, src5, src6, src7, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);
        await src4.EmitNext(4);
        await src5.EmitNext(5);
        await src6.EmitNext(6);
        await src7.EmitNext(7);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src7.EmitNext(99);

        await Assert.That(results).Count().IsEqualTo(1);

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest7 OnErrorResume guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7DisposedViaError_ThenOnErrorResumeGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var src4 = new DirectSource<int>();
        var src5 = new DirectSource<int>();
        var src6 = new DirectSource<int>();
        var src7 = new DirectSource<int>();
        Exception? receivedError = null;
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, src4, src5, src6, src7, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);
        await src4.EmitNext(4);
        await src5.EmitNext(5);
        await src6.EmitNext(6);
        await src7.EmitNext(7);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src7.EmitError(new InvalidOperationException("ignored"));

        await Assert.That(receivedError).IsNull();

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnNextCombined guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8DisposedViaError_ThenOnNextCombinedGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var src4 = new DirectSource<int>();
        var src5 = new DirectSource<int>();
        var src6 = new DirectSource<int>();
        var src7 = new DirectSource<int>();
        var src8 = new DirectSource<int>();
        var results = new List<int>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, src4, src5, src6, src7, src8, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);
        await src4.EmitNext(4);
        await src5.EmitNext(5);
        await src6.EmitNext(6);
        await src7.EmitNext(7);
        await src8.EmitNext(8);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src8.EmitNext(99);

        await Assert.That(results).Count().IsEqualTo(1);

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatest8 OnErrorResume guard returns early when disposal is triggered
    /// by an error on source 1 while the downstream OnCompletedAsync is still running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8DisposedViaError_ThenOnErrorResumeGuardHits()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var src4 = new DirectSource<int>();
        var src5 = new DirectSource<int>();
        var src6 = new DirectSource<int>();
        var src7 = new DirectSource<int>();
        var src8 = new DirectSource<int>();
        Exception? receivedError = null;
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await AsyncObs
            .CombineLatest(src1, src2, src3, src4, src5, src6, src7, src8, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    receivedError = ex;
                    return default;
                },
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);
        await src4.EmitNext(4);
        await src5.EmitNext(5);
        await src6.EmitNext(6);
        await src7.EmitNext(7);
        await src8.EmitNext(8);

        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        await src8.EmitError(new InvalidOperationException("ignored"));

        await Assert.That(receivedError).IsNull();

        allowCompletion.TrySetResult();
        await failTask;
    }
}
