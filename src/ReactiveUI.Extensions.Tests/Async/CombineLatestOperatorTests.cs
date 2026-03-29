// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Subjects;
using AsyncObs = ReactiveUI.Extensions.Async.ObservableAsync;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Comprehensive tests for the CombineLatest operator covering all arities (2-8),
/// error propagation, completion, error resume, disposal, and edge cases.
/// </summary>
public class CombineLatestOperatorTests
{
    /// <summary>No emission until both sources have produced a value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_NoEmissionUntilBothHaveValues()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var results = new List<int>();
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        await s1.OnNextAsync(1, CancellationToken.None);

        await Assert.That(results).IsEmpty();

        await s2.OnNextAsync(10, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(11);
    }

    /// <summary>Multiple emissions use the latest value from each source.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_ThenEmitsOnEachNewValue()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var results = new List<int>();
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
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
        await s1.OnNextAsync(2, CancellationToken.None);
        await s2.OnNextAsync(20, CancellationToken.None);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(3);
        await Assert.That(results[0]).IsEqualTo(11);
        await Assert.That(results[1]).IsEqualTo(12);
        await Assert.That(results[2]).IsEqualTo(22);
    }

    /// <summary>Error from source 1 completes the combined sequence with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_ErrorFromSrc1_ThenCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s1.OnCompletedAsync(Result.Failure(new InvalidOperationException("src1 error")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
        await Assert.That(completionResult.Value.Exception is InvalidOperationException).IsTrue();
    }

    /// <summary>Error from source 2 completes the combined sequence with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_ErrorFromSrc2_ThenCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s2.OnCompletedAsync(Result.Failure(new InvalidOperationException("src2 error")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Combined sequence completes only when both sources complete successfully.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_BothComplete_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s1.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNull();

        await s2.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Partial completion does not complete the combined sequence.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_OnlyOneCompletes_ThenNotComplete()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s1.OnCompletedAsync(Result.Success);

        await Assert.That(completionResult).IsNull();
    }

    /// <summary>Disposal stops further emissions.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_Disposed_ThenNoMoreEmissions()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var results = new List<int>();
        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
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
        var countBefore = results.Count;

        await sub.DisposeAsync();

        await s1.OnNextAsync(2, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(countBefore);
    }

    /// <summary>Double disposal is safe and does not throw.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_DoubleDispose_ThenSafe()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (_, _) => default,
                null,
                null);

        await sub.DisposeAsync();
        await sub.DisposeAsync();
    }

    /// <summary>Error resume from source 1 is forwarded to the observer.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_ErrorResume_ThenForwarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var errors = new List<Exception>();
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        var expected = new InvalidOperationException("resume error");
        await s1.OnErrorResumeAsync(expected, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsEquivalentTo(expected);
    }

    /// <summary>Error resume from source 2 is also forwarded.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_ErrorResumeFromSrc2_ThenForwarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var errors = new List<Exception>();
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        var expected = new InvalidOperationException("src2 resume");
        await s2.OnErrorResumeAsync(expected, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsEquivalentTo(expected);
    }

    /// <summary>Tests CombineLatest with 3 sources combines correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_ThenCombinesAll()
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

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(111);
    }

    /// <summary>No emission until all three sources have values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_NoEmissionUntilAllHaveValues()
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

        await Assert.That(results).IsEmpty();
    }

    /// <summary>Error from any source propagates in 3-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_Error_ThenCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s2.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>All three sources complete successfully.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_AllComplete_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNull();

        await s3.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Error resume forwarded in 3-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_ErrorResume_ThenForwarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var errors = new List<Exception>();
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        var expected = new InvalidOperationException("resume");
        await s3.OnErrorResumeAsync(expected, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsEquivalentTo(expected);
    }

    /// <summary>Error propagation in 4-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestFourSources_Error_ThenCompletes()
    {
        var subjects = Enumerable.Range(0, 4).Select(_ => SubjectAsync.Create<int>()).ToList();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(
                subjects[0].Values,
                subjects[1].Values,
                subjects[2].Values,
                subjects[3].Values,
                (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await subjects[3].OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>All four sources complete successfully.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestFourSources_AllComplete_ThenCombinedCompletes()
    {
        var subjects = Enumerable.Range(0, 4).Select(_ => SubjectAsync.Create<int>()).ToList();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(
                subjects[0].Values,
                subjects[1].Values,
                subjects[2].Values,
                subjects[3].Values,
                (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        for (var i = 0; i < 3; i++)
        {
            await subjects[i].OnCompletedAsync(Result.Success);
        }

        await Assert.That(completionResult).IsNull();

        await subjects[3].OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Error resume forwarded in 4-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestFourSources_ErrorResume_ThenForwarded()
    {
        var subjects = Enumerable.Range(0, 4).Select(_ => SubjectAsync.Create<int>()).ToList();

        var errors = new List<Exception>();
        await using var sub = await AsyncObs
            .CombineLatest(
                subjects[0].Values,
                subjects[1].Values,
                subjects[2].Values,
                subjects[3].Values,
                (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        var expected = new InvalidOperationException("resume");
        await subjects[2].OnErrorResumeAsync(expected, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsEquivalentTo(expected);
    }

    /// <summary>Error propagation in 5-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestFiveSources_Error_ThenCompletes()
    {
        var subjects = Enumerable.Range(0, 5).Select(_ => SubjectAsync.Create<int>()).ToList();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(
                subjects[0].Values,
                subjects[1].Values,
                subjects[2].Values,
                subjects[3].Values,
                subjects[4].Values,
                (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await subjects[0].OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>All five sources complete successfully.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestFiveSources_AllComplete_ThenCombinedCompletes()
    {
        var subjects = Enumerable.Range(0, 5).Select(_ => SubjectAsync.Create<int>()).ToList();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(
                subjects[0].Values,
                subjects[1].Values,
                subjects[2].Values,
                subjects[3].Values,
                subjects[4].Values,
                (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        for (var i = 0; i < 4; i++)
        {
            await subjects[i].OnCompletedAsync(Result.Success);
        }

        await Assert.That(completionResult).IsNull();

        await subjects[4].OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Error resume forwarded in 5-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestFiveSources_ErrorResume_ThenForwarded()
    {
        var subjects = Enumerable.Range(0, 5).Select(_ => SubjectAsync.Create<int>()).ToList();

        var errors = new List<Exception>();
        await using var sub = await AsyncObs
            .CombineLatest(
                subjects[0].Values,
                subjects[1].Values,
                subjects[2].Values,
                subjects[3].Values,
                subjects[4].Values,
                (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        var expected = new InvalidOperationException("resume");
        await subjects[4].OnErrorResumeAsync(expected, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsEquivalentTo(expected);
    }

    /// <summary>Error propagation in 6-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestSixSources_Error_ThenCompletes()
    {
        var subjects = Enumerable.Range(0, 6).Select(_ => SubjectAsync.Create<int>()).ToList();

        Result? completionResult = null;
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
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await subjects[5].OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>All six sources complete successfully.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestSixSources_AllComplete_ThenCombinedCompletes()
    {
        var subjects = Enumerable.Range(0, 6).Select(_ => SubjectAsync.Create<int>()).ToList();

        Result? completionResult = null;
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
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        for (var i = 0; i < 5; i++)
        {
            await subjects[i].OnCompletedAsync(Result.Success);
        }

        await Assert.That(completionResult).IsNull();

        await subjects[5].OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Error resume forwarded in 6-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestSixSources_ErrorResume_ThenForwarded()
    {
        var subjects = Enumerable.Range(0, 6).Select(_ => SubjectAsync.Create<int>()).ToList();

        var errors = new List<Exception>();
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
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        var expected = new InvalidOperationException("resume");
        await subjects[3].OnErrorResumeAsync(expected, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsEquivalentTo(expected);
    }

    /// <summary>Error propagation in 7-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestSevenSources_Error_ThenCompletes()
    {
        var subjects = Enumerable.Range(0, 7).Select(_ => SubjectAsync.Create<int>()).ToList();

        Result? completionResult = null;
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
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await subjects[6].OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>All seven sources complete successfully.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestSevenSources_AllComplete_ThenCombinedCompletes()
    {
        var subjects = Enumerable.Range(0, 7).Select(_ => SubjectAsync.Create<int>()).ToList();

        Result? completionResult = null;
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
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        for (var i = 0; i < 6; i++)
        {
            await subjects[i].OnCompletedAsync(Result.Success);
        }

        await Assert.That(completionResult).IsNull();

        await subjects[6].OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Error resume forwarded in 7-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestSevenSources_ErrorResume_ThenForwarded()
    {
        var subjects = Enumerable.Range(0, 7).Select(_ => SubjectAsync.Create<int>()).ToList();

        var errors = new List<Exception>();
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
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        var expected = new InvalidOperationException("resume");
        await subjects[0].OnErrorResumeAsync(expected, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsEquivalentTo(expected);
    }

    /// <summary>Error propagation in 8-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEightSources_Error_ThenCompletes()
    {
        var subjects = Enumerable.Range(0, 8).Select(_ => SubjectAsync.Create<int>()).ToList();

        Result? completionResult = null;
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
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await subjects[7].OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>All eight sources complete successfully.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEightSources_AllComplete_ThenCombinedCompletes()
    {
        var subjects = Enumerable.Range(0, 8).Select(_ => SubjectAsync.Create<int>()).ToList();

        Result? completionResult = null;
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
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        for (var i = 0; i < 7; i++)
        {
            await subjects[i].OnCompletedAsync(Result.Success);
        }

        await Assert.That(completionResult).IsNull();

        await subjects[7].OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Error resume forwarded in 8-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEightSources_ErrorResume_ThenForwarded()
    {
        var subjects = Enumerable.Range(0, 8).Select(_ => SubjectAsync.Create<int>()).ToList();

        var errors = new List<Exception>();
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
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        var expected = new InvalidOperationException("resume");
        await subjects[5].OnErrorResumeAsync(expected, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsEquivalentTo(expected);
    }

    /// <summary>Error resume after disposal is ignored.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_ErrorResumeAfterDispose_ThenIgnored()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var errors = new List<Exception>();
        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await sub.DisposeAsync();

        await s1.OnErrorResumeAsync(new InvalidOperationException("after dispose"), CancellationToken.None);

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>Error from source before any values still propagates.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_ErrorBeforeAnyValues_ThenCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s1.OnCompletedAsync(Result.Failure(new InvalidOperationException("early error")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Emission continues after error resume (non-terminal).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_EmissionContinuesAfterErrorResume()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var results = new List<int>();
        var errors = new List<Exception>();
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(10, CancellationToken.None);

        await s1.OnErrorResumeAsync(new InvalidOperationException("non-terminal"), CancellationToken.None);

        await s1.OnNextAsync(2, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(results).Count().IsGreaterThanOrEqualTo(2);
        await Assert.That(results[^1]).IsEqualTo(12);
    }

    /// <summary>No emission until all N sources have values for 4-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestFourSources_NoEmissionUntilAllHaveValues()
    {
        var subjects = Enumerable.Range(0, 4).Select(_ => SubjectAsync.Create<int>()).ToList();

        var results = new List<int>();
        await using var sub = await AsyncObs
            .CombineLatest(
                subjects[0].Values,
                subjects[1].Values,
                subjects[2].Values,
                subjects[3].Values,
                (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        for (var i = 0; i < 3; i++)
        {
            await subjects[i].OnNextAsync(i + 1, CancellationToken.None);
        }

        await Assert.That(results).IsEmpty();

        await subjects[3].OnNextAsync(4, CancellationToken.None);
        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(10);
    }

    /// <summary>Disposal of 3-source variant stops emissions.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_Disposed_ThenNoMoreEmissions()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var results = new List<int>();
        var sub = await AsyncObs
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
        var countBefore = results.Count;

        await sub.DisposeAsync();

        await s1.OnNextAsync(2, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(countBefore);
    }

    /// <summary>OnNextCombined returns early when subscription is disposed mid-stream (2-source).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_DisposedDuringEmission_ThenOnNextCombinedReturnsEarly()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var results = new List<int>();
        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                null);

        // Establish initial combined values so future OnNext triggers OnNextCombined.
        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(10, CancellationToken.None);
        var countBefore = results.Count;

        // Dispose, then emit — the OnNextCombined path should hit _disposed == 1 and return.
        await sub.DisposeAsync();

        await s1.OnNextAsync(99, CancellationToken.None);
        await s2.OnNextAsync(99, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(countBefore);
    }

    /// <summary>OnNextCombined returns early when subscription is disposed mid-stream (3-source).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_DisposedDuringEmission_ThenOnNextCombinedReturnsEarly()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var results = new List<int>();
        var sub = await AsyncObs
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
        var countBefore = results.Count;

        await sub.DisposeAsync();

        await s1.OnNextAsync(99, CancellationToken.None);
        await s2.OnNextAsync(99, CancellationToken.None);
        await s3.OnNextAsync(99, CancellationToken.None);

        await Assert.That(results).Count().IsEqualTo(countBefore);
    }

    /// <summary>OnErrorResume returns early when subscription is disposed (3-source).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_ErrorResumeAfterDispose_ThenIgnored()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var errors = new List<Exception>();
        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        await sub.DisposeAsync();

        await s1.OnErrorResumeAsync(new InvalidOperationException("after dispose"), CancellationToken.None);
        await s2.OnErrorResumeAsync(new InvalidOperationException("after dispose"), CancellationToken.None);
        await s3.OnErrorResumeAsync(new InvalidOperationException("after dispose"), CancellationToken.None);

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>Error from source 1 in 3-source variant triggers completion with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_ErrorFromSrc1_ThenCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s1.OnCompletedAsync(Result.Failure(new InvalidOperationException("src1 error")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
        await Assert.That(completionResult.Value.Exception is InvalidOperationException).IsTrue();
    }

    /// <summary>Error from source 3 in 3-source variant triggers completion with failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_ErrorFromSrc3_ThenCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s3.OnCompletedAsync(Result.Failure(new InvalidOperationException("src3 error")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
        await Assert.That(completionResult.Value.Exception is InvalidOperationException).IsTrue();
    }

    /// <summary>Two-source: source 2 completes first, then source 1 completes the combined sequence.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_Src2CompletesThenSrc1Completes_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s2.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNull();

        await s1.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Three-source: sources complete in reverse order (3, 2, 1) with combined completing on last.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_SourcesCompleteInReverseOrder_ThenCombinedCompletesOnLast()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s3.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNull();

        await s2.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNull();

        await s1.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Three-source: source 2 completes last, triggering combined completion.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_Src2CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s1.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNull();

        await s2.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Three-source: source 3 completes last, triggering combined completion.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_Src3CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNull();

        await s3.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Error resume from source 1 is forwarded in 3-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_ErrorResumeFromSrc1_ThenForwarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var errors = new List<Exception>();
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        var expected = new InvalidOperationException("src1 resume");
        await s1.OnErrorResumeAsync(expected, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsEquivalentTo(expected);
    }

    /// <summary>Error resume from source 2 is forwarded in 3-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_ErrorResumeFromSrc2_ThenForwarded()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var errors = new List<Exception>();
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
                    return default;
                },
                null);

        var expected = new InvalidOperationException("src2 resume");
        await s2.OnErrorResumeAsync(expected, CancellationToken.None);

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0]).IsEquivalentTo(expected);
    }

    /// <summary>Double disposal is safe and does not throw for 3-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_DoubleDispose_ThenSafe()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                null,
                null);

        await sub.DisposeAsync();
        await sub.DisposeAsync();
    }

    /// <summary>Error from source 1 in 2-source variant while other source already completed triggers failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_Src2CompletedThenSrc1Errors_ThenFailure()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s2.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNull();

        await s1.OnCompletedAsync(Result.Failure(new InvalidOperationException("late error")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Error from source 2 in 3-source variant while others already completed triggers failure.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_TwoCompletedThenThirdErrors_ThenFailure()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s1.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNull();

        await s2.OnCompletedAsync(Result.Failure(new InvalidOperationException("late error")));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsFailure).IsTrue();
    }

    /// <summary>Two-source: emissions continue after one source completes successfully.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestTwoSources_OneSrcCompleted_ThenOtherSrcStillEmits()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var results = new List<int>();
        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(10, CancellationToken.None);
        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);

        // Complete source 1, but source 2 can still emit combined values.
        await s1.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNull();

        await s2.OnNextAsync(20, CancellationToken.None);

        // The latest s1 value (1) is still held, so 1 + 20 = 21.
        await Assert.That(results[^1]).IsEqualTo(21);
    }

    /// <summary>Three-source: emissions continue after two sources complete successfully.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestThreeSources_TwoSrcsCompleted_ThenRemainingSrcStillEmits()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var results = new List<int>();
        Result? completionResult = null;
        await using var sub = await AsyncObs
            .CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (x, _) =>
                {
                    results.Add(x);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(10, CancellationToken.None);
        await s3.OnNextAsync(100, CancellationToken.None);
        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);

        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await Assert.That(completionResult).IsNull();

        await s3.OnNextAsync(200, CancellationToken.None);

        // Latest held values: s1=1, s2=10, so 1 + 10 + 200 = 211.
        await Assert.That(results[^1]).IsEqualTo(211);
    }

    /// <summary>Multiple emissions with updated latest values for 5-source variant.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestFiveSources_MultipleEmissions_ThenUsesLatestValues()
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
            await subjects[i].OnNextAsync(1, CancellationToken.None);
        }

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(5);

        await subjects[0].OnNextAsync(10, CancellationToken.None);
        await Assert.That(results[^1]).IsEqualTo(14);
    }

    /// <summary>
    /// Verifies that CombineLatest with 2 sources propagates failure when source 1 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2Sources_Source1Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 2 sources propagates failure when source 2 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2Sources_Source2Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s2.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 2 sources completes when source 1 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2Sources_Source1CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s2.OnCompletedAsync(Result.Success);
        await s1.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 2 sources completes when source 2 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2Sources_Source2CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 2 sources handles disposal during active emission gracefully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2Sources_DisposedDuringEmission_ThenNoError()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var results = new List<int>();
        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (x, ct) =>
                    {
                        results.Add(x);
                        return default;
                    },
                null,
                r => default);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await sub.DisposeAsync();

        // Emit after disposal - should be ignored
        await s1.OnNextAsync(10, CancellationToken.None);
        await s2.OnNextAsync(20, CancellationToken.None);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest with 2 sources ignores error resume after disposal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest2Sources_ErrorResumeAfterDisposal_ThenIgnored()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();

        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, (a, b) => a + b)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r => default);

        await sub.DisposeAsync();

        // Error resume after disposal should not throw
        await s1.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s2.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
    }

    /// <summary>
    /// Verifies that CombineLatest with 3 sources propagates failure when source 1 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3Sources_Source1Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 3 sources propagates failure when source 2 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3Sources_Source2Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s2.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 3 sources propagates failure when source 3 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3Sources_Source3Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s3.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 3 sources completes when source 1 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3Sources_Source1CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s1.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 3 sources completes when source 2 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3Sources_Source2CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 3 sources completes when source 3 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3Sources_Source3CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 3 sources handles disposal during active emission gracefully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3Sources_DisposedDuringEmission_ThenNoError()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var results = new List<int>();
        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (x, ct) =>
                    {
                        results.Add(x);
                        return default;
                    },
                null,
                r => default);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await sub.DisposeAsync();

        // Emit after disposal - should be ignored
        await s1.OnNextAsync(10, CancellationToken.None);
        await s2.OnNextAsync(20, CancellationToken.None);
        await s3.OnNextAsync(30, CancellationToken.None);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest with 3 sources ignores error resume after disposal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest3Sources_ErrorResumeAfterDisposal_ThenIgnored()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();

        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, (a, b, c) => a + b + c)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r => default);

        await sub.DisposeAsync();

        // Error resume after disposal should not throw
        await s1.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s2.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s3.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
    }

    /// <summary>
    /// Verifies that CombineLatest with 4 sources propagates failure when source 1 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Sources_Source1Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 4 sources propagates failure when source 2 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Sources_Source2Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s2.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 4 sources propagates failure when source 3 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Sources_Source3Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s3.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 4 sources propagates failure when source 4 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Sources_Source4Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s4.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 4 sources completes when source 1 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Sources_Source1CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s1.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 4 sources completes when source 2 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Sources_Source2CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 4 sources completes when source 3 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Sources_Source3CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 4 sources completes when source 4 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Sources_Source4CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 4 sources handles disposal during active emission gracefully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Sources_DisposedDuringEmission_ThenNoError()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();

        var results = new List<int>();
        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, ct) =>
                    {
                        results.Add(x);
                        return default;
                    },
                null,
                r => default);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await sub.DisposeAsync();

        // Emit after disposal - should be ignored
        await s1.OnNextAsync(10, CancellationToken.None);
        await s2.OnNextAsync(20, CancellationToken.None);
        await s3.OnNextAsync(30, CancellationToken.None);
        await s4.OnNextAsync(40, CancellationToken.None);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest with 4 sources ignores error resume after disposal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest4Sources_ErrorResumeAfterDisposal_ThenIgnored()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();

        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, (a, b, c, d) => a + b + c + d)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r => default);

        await sub.DisposeAsync();

        // Error resume after disposal should not throw
        await s1.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s2.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s3.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s4.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources propagates failure when source 1 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_Source1Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources propagates failure when source 2 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_Source2Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s2.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources propagates failure when source 3 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_Source3Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s3.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources propagates failure when source 4 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_Source4Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s4.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources propagates failure when source 5 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_Source5Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s5.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources completes when source 1 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_Source1CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s1.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources completes when source 2 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_Source2CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources completes when source 3 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_Source3CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources completes when source 4 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_Source4CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources completes when source 5 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_Source5CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources handles disposal during active emission gracefully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_DisposedDuringEmission_ThenNoError()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var results = new List<int>();
        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) =>
                    {
                        results.Add(x);
                        return default;
                    },
                null,
                r => default);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await sub.DisposeAsync();

        // Emit after disposal - should be ignored
        await s1.OnNextAsync(10, CancellationToken.None);
        await s2.OnNextAsync(20, CancellationToken.None);
        await s3.OnNextAsync(30, CancellationToken.None);
        await s4.OnNextAsync(40, CancellationToken.None);
        await s5.OnNextAsync(50, CancellationToken.None);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest with 5 sources ignores error resume after disposal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest5Sources_ErrorResumeAfterDisposal_ThenIgnored()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();

        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, (a, b, c, d, e) => a + b + c + d + e)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r => default);

        await sub.DisposeAsync();

        // Error resume after disposal should not throw
        await s1.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s2.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s3.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s4.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s5.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources propagates failure when source 1 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source1Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources propagates failure when source 2 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source2Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s2.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources propagates failure when source 3 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source3Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s3.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources propagates failure when source 4 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source4Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s4.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources propagates failure when source 5 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source5Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s5.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources propagates failure when source 6 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source6Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s6.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources completes when source 1 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source1CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s1.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources completes when source 2 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source2CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources completes when source 3 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source3CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources completes when source 4 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source4CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources completes when source 5 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source5CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources completes when source 6 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_Source6CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources handles disposal during active emission gracefully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_DisposedDuringEmission_ThenNoError()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var results = new List<int>();
        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) =>
                    {
                        results.Add(x);
                        return default;
                    },
                null,
                r => default);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await sub.DisposeAsync();

        // Emit after disposal - should be ignored
        await s1.OnNextAsync(10, CancellationToken.None);
        await s2.OnNextAsync(20, CancellationToken.None);
        await s3.OnNextAsync(30, CancellationToken.None);
        await s4.OnNextAsync(40, CancellationToken.None);
        await s5.OnNextAsync(50, CancellationToken.None);
        await s6.OnNextAsync(60, CancellationToken.None);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest with 6 sources ignores error resume after disposal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest6Sources_ErrorResumeAfterDisposal_ThenIgnored()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();

        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, (a, b, c, d, e, f) => a + b + c + d + e + f)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r => default);

        await sub.DisposeAsync();

        // Error resume after disposal should not throw
        await s1.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s2.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s3.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s4.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s5.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s6.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources propagates failure when source 1 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source1Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources propagates failure when source 2 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source2Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s2.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources propagates failure when source 3 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source3Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s3.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources propagates failure when source 4 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source4Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s4.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources propagates failure when source 5 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source5Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s5.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources propagates failure when source 6 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source6Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s6.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources propagates failure when source 7 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source7Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s7.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources completes when source 1 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source1CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s1.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources completes when source 2 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source2CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources completes when source 3 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source3CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources completes when source 4 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source4CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources completes when source 5 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source5CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources completes when source 6 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source6CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources completes when source 7 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_Source7CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources handles disposal during active emission gracefully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_DisposedDuringEmission_ThenNoError()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var results = new List<int>();
        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) =>
                    {
                        results.Add(x);
                        return default;
                    },
                null,
                r => default);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await sub.DisposeAsync();

        // Emit after disposal - should be ignored
        await s1.OnNextAsync(10, CancellationToken.None);
        await s2.OnNextAsync(20, CancellationToken.None);
        await s3.OnNextAsync(30, CancellationToken.None);
        await s4.OnNextAsync(40, CancellationToken.None);
        await s5.OnNextAsync(50, CancellationToken.None);
        await s6.OnNextAsync(60, CancellationToken.None);
        await s7.OnNextAsync(70, CancellationToken.None);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest with 7 sources ignores error resume after disposal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest7Sources_ErrorResumeAfterDisposal_ThenIgnored()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();

        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, (a, b, c, d, e, f, g) => a + b + c + d + e + f + g)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r => default);

        await sub.DisposeAsync();

        // Error resume after disposal should not throw
        await s1.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s2.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s3.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s4.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s5.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s6.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s7.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources propagates failure when source 1 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source1Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources propagates failure when source 2 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source2Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s2.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources propagates failure when source 3 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source3Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s3.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources propagates failure when source 4 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source4Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s4.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources propagates failure when source 5 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source5Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s5.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources propagates failure when source 6 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source6Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s6.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources propagates failure when source 7 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source7Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s7.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources propagates failure when source 8 errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source8Errors_ThenFailurePropagates()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s8.OnCompletedAsync(Result.Failure(new InvalidOperationException("err")));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result.IsFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources completes when source 1 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source1CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s8.OnNextAsync(8, CancellationToken.None);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
        await s1.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources completes when source 2 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source2CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s8.OnNextAsync(8, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources completes when source 3 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source3CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s8.OnNextAsync(8, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources completes when source 4 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source4CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s8.OnNextAsync(8, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources completes when source 5 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source5CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s8.OnNextAsync(8, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources completes when source 6 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source6CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s8.OnNextAsync(8, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources completes when source 7 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source7CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s8.OnNextAsync(8, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources completes when source 8 is the last to complete.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_Source8CompletesLast_ThenCombinedCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r =>
                    {
                        completed.TrySetResult(r);
                        return default;
                    });

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s8.OnNextAsync(8, CancellationToken.None);
        await s1.OnCompletedAsync(Result.Success);
        await s2.OnCompletedAsync(Result.Success);
        await s3.OnCompletedAsync(Result.Success);
        await s4.OnCompletedAsync(Result.Success);
        await s5.OnCompletedAsync(Result.Success);
        await s6.OnCompletedAsync(Result.Success);
        await s7.OnCompletedAsync(Result.Success);
        await s8.OnCompletedAsync(Result.Success);

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources handles disposal during active emission gracefully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_DisposedDuringEmission_ThenNoError()
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
        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) =>
                    {
                        results.Add(x);
                        return default;
                    },
                null,
                r => default);

        await s1.OnNextAsync(1, CancellationToken.None);
        await s2.OnNextAsync(2, CancellationToken.None);
        await s3.OnNextAsync(3, CancellationToken.None);
        await s4.OnNextAsync(4, CancellationToken.None);
        await s5.OnNextAsync(5, CancellationToken.None);
        await s6.OnNextAsync(6, CancellationToken.None);
        await s7.OnNextAsync(7, CancellationToken.None);
        await s8.OnNextAsync(8, CancellationToken.None);
        await sub.DisposeAsync();

        // Emit after disposal - should be ignored
        await s1.OnNextAsync(10, CancellationToken.None);
        await s2.OnNextAsync(20, CancellationToken.None);
        await s3.OnNextAsync(30, CancellationToken.None);
        await s4.OnNextAsync(40, CancellationToken.None);
        await s5.OnNextAsync(50, CancellationToken.None);
        await s6.OnNextAsync(60, CancellationToken.None);
        await s7.OnNextAsync(70, CancellationToken.None);
        await s8.OnNextAsync(80, CancellationToken.None);

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatest with 8 sources ignores error resume after disposal.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatest8Sources_ErrorResumeAfterDisposal_ThenIgnored()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        var s3 = SubjectAsync.Create<int>();
        var s4 = SubjectAsync.Create<int>();
        var s5 = SubjectAsync.Create<int>();
        var s6 = SubjectAsync.Create<int>();
        var s7 = SubjectAsync.Create<int>();
        var s8 = SubjectAsync.Create<int>();

        var sub = await AsyncObs.CombineLatest(s1.Values, s2.Values, s3.Values, s4.Values, s5.Values, s6.Values, s7.Values, s8.Values, (a, b, c, d, e, f, g, h) => a + b + c + d + e + f + g + h)
            .SubscribeAsync(
                (x, ct) => default,
                null,
                r => default);

        await sub.DisposeAsync();

        // Error resume after disposal should not throw
        await s1.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s2.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s3.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s4.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s5.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s6.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s7.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
        await s8.OnErrorResumeAsync(new InvalidOperationException(), CancellationToken.None);
    }

    /// <summary>
    /// Tests CombineLatestEnumerable SubscribeAsyncCore catch block when a source throws during subscription.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableSourceThrowsDuringSubscribe_ThenDisposesAndRethrows()
    {
        var failing = AsyncObs.Create<int>((_, _) =>
            throw new InvalidOperationException("subscribe fail"));

        var sources = new IObservableAsync<int>[]
        {
            AsyncObs.Return(1),
            failing,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sources.CombineLatest().ToListAsync());
    }

    /// <summary>
    /// Tests CombineLatestEnumerable OnErrorResumeAsync is forwarded when disposed is not set.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableErrorResume_ThenForwardedToObserver()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        Exception? received = null;

        var sources = new IObservableAsync<int>[] { s1.Values, s2.Values };

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    received = ex;
                    return default;
                },
                null);

        await s1.OnErrorResumeAsync(new InvalidOperationException("resume"), CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => received is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Message).IsEqualTo("resume");
    }

    /// <summary>
    /// Tests CombineLatestEnumerable OnErrorResumeAsync returns early when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableErrorResumeAfterDisposed_ThenIgnored()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        Exception? received = null;

        var sources = new IObservableAsync<int>[] { s1.Values, s2.Values };

        var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (_, _) => default,
                (ex, _) =>
                {
                    received = ex;
                    return default;
                },
                null);

        await sub.DisposeAsync();

        // After disposal, error resume should be ignored
        await s1.OnErrorResumeAsync(new InvalidOperationException("late error"), CancellationToken.None);

        await Assert.That(received).IsNull();
    }

    /// <summary>
    /// Tests that CombineLatestEnumerable completes when a source completes without ever emitting a value.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableSourceCompletesWithoutEmitting_ThenCompletes()
    {
        var s1 = SubjectAsync.Create<int>();
        var s2 = SubjectAsync.Create<int>();
        Result? completionResult = null;

        var sources = new IObservableAsync<int>[] { s1.Values, s2.Values };

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (_, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // s1 completes without emitting - should trigger completion since !_values[0].HasValue
        await s1.OnCompletedAsync(Result.Success);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completionResult is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Tests that CombineLatestValuesAreAllFalse returns true for an empty source set.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestValuesAreAllFalseWithNoSources_ThenReturnsTrue()
    {
        IObservableAsync<bool>[] sources = [];
        var result = await sources
            .CombineLatestValuesAreAllFalse()
            .FirstAsync();

        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Tests CombineLatestValuesAreAllTrue with empty IEnumerable returns true (vacuous truth).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestValuesAreAllTrueEmptyList_ThenReturnsTrue()
    {
        var result = await new List<IObservableAsync<bool>>()
            .CombineLatestValuesAreAllTrue()
            .FirstAsync();

        await Assert.That(result).IsTrue();
    }

    /// <summary>Tests CombineLatest with 4 sources.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(1111);
    }

    /// <summary>Tests CombineLatest with 5 sources.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(150);
    }

    /// <summary>Tests CombineLatest with 6 sources.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(21);
    }

    /// <summary>Tests CombineLatest with 7 sources.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(7);
    }

    /// <summary>Tests CombineLatest with 8 sources.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
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

        await AsyncTestHelpers.WaitForConditionAsync(
            () => results.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(results).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(results[0]).IsEqualTo(8);
    }

    /// <summary>
    /// Tests CombineLatest enumerable returns early from SubscribeAsync when disposed during subscribe,
    /// exercising the early return path in CombineLatestEnumerable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableDisposedDuringSubscribe_ThenReturnsEarly()
    {
        var subject = SubjectAsync.Create<int>();
        var sources = new IObservableAsync<int>[] { subject.Values, AsyncObs.Never<int>() };

        var sub = await sources.CombineLatest().SubscribeAsync(
            (_, _) => default,
            null,
            null);

        // Dispose immediately, before any values
        await sub.DisposeAsync();

        // Emit after dispose - should be ignored
        await subject.OnNextAsync(1, CancellationToken.None);
    }

    /// <summary>
    /// Tests CombineLatest enumerable OnNextAsync returns early when disposed,
    /// exercising the OnNextAsync disposed guard in CombineLatestEnumerable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableOnNextAfterDispose_ThenIgnored()
    {
        var subject1 = SubjectAsync.Create<int>();
        var subject2 = SubjectAsync.Create<int>();
        var items = new List<IReadOnlyList<int>>();

        var sources = new IObservableAsync<int>[] { subject1.Values, subject2.Values };

        var sub = await sources.CombineLatest().SubscribeAsync(
            (x, _) =>
            {
                items.Add(x);
                return default;
            },
            null,
            null);

        await sub.DisposeAsync();

        // These should be ignored
        await subject1.OnNextAsync(1, CancellationToken.None);
        await subject2.OnNextAsync(2, CancellationToken.None);

        await Assert.That(items).IsEmpty();
    }

    /// <summary>
    /// Tests CombineLatest enumerable OnErrorResumeAsync returns early when disposed,
    /// exercising the OnErrorResumeAsync disposed guard in CombineLatestEnumerable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableOnErrorResumeAfterDispose_ThenIgnored()
    {
        var subject = SubjectAsync.Create<int>();
        var errors = new List<Exception>();

        var sources = new IObservableAsync<int>[] { subject.Values };

        var sub = await sources.CombineLatest().SubscribeAsync(
            (_, _) => default,
            (ex, _) =>
            {
                errors.Add(ex);
                return default;
            },
            null);

        await sub.DisposeAsync();

        // Error after dispose should be ignored
        await subject.OnErrorResumeAsync(new InvalidOperationException("err"), CancellationToken.None);

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>
    /// Tests CombineLatest enumerable OnCompletedAsync returns early when already completed for same index,
    /// exercising the already-completed guard in CombineLatestEnumerable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableAlreadyDisposed_ThenOnCompletedIgnored()
    {
        var subject1 = SubjectAsync.Create<int>();
        var subject2 = SubjectAsync.Create<int>();

        var sources = new IObservableAsync<int>[] { subject1.Values, subject2.Values };
        Result? completion = null;

        var sub = await sources.CombineLatest().SubscribeAsync(
            (_, _) => default,
            null,
            r =>
            {
                completion = r;
                return default;
            });

        // Dispose first
        await sub.DisposeAsync();

        // Complete after dispose - should be ignored
        await subject1.OnCompletedAsync(Result.Success);

        // No extra completion should have been forwarded since we disposed
        await Assert.That(completion).IsNull();
    }

    /// <summary>
    /// Tests CombineLatest enumerable completes when a source completes without emitting a value,
    /// exercising the shouldComplete path in CombineLatestEnumerable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableSourceCompletesWithoutValue_ThenCompletes()
    {
        var subject1 = SubjectAsync.Create<int>();
        var emptySource = AsyncObs.Empty<int>();

        var sources = new IObservableAsync<int>[] { subject1.Values, emptySource };
        Result? completion = null;

        await using var sub = await sources.CombineLatest().SubscribeAsync(
            (_, _) => default,
            null,
            r =>
            {
                completion = r;
                return default;
            });

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completion is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(completion).IsNotNull();
        await Assert.That(completion!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatestEnumerable OnNextAsync returns early when disposed.
    /// Uses the blocking-OnCompletedAsync technique to keep the gate alive while _disposed is set.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableOnNextAfterDispose_ThenReturnsEarly()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var sources = new IObservableAsync<int>[] { src1, src2 };
        var items = new List<IReadOnlyList<int>>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (x, _) =>
                {
                    items.Add(x);
                    return default;
                },
                null,
                async _ =>
                {
                    completionBlocked.TrySetResult();
                    await allowCompletion.Task;
                });

        // Set up initial values
        await src1.EmitNext(1);
        await src2.EmitNext(2);

        // Trigger failure on src1 → CompleteAsync → _disposed=1 → blocks on OnCompletedAsync
        var failTask = Task.Run(() => src1.Complete(Result.Failure(new InvalidOperationException("test"))));
        await completionBlocked.Task;

        // _disposed is 1, gate still alive → OnNextAsync should hit the guard
        await src2.EmitNext(99);

        await Assert.That(items).Count().IsEqualTo(1);

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatestEnumerable OnErrorResumeAsync returns early when disposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableOnErrorResumeAfterDispose_ThenReturnsEarly()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var sources = new IObservableAsync<int>[] { src1, src2 };
        var errors = new List<Exception>();
        var completionBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (x, _) => default,
                (ex, _) =>
                {
                    errors.Add(ex);
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

        // _disposed is 1, gate still alive → OnErrorResumeAsync should hit the guard
        await src2.EmitError(new InvalidOperationException("post-dispose error"));

        await Assert.That(errors).IsEmpty();

        allowCompletion.TrySetResult();
        await failTask;
    }

    /// <summary>
    /// Verifies that CombineLatestEnumerable OnCompleted returns early for an already-completed index.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableDoubleComplete_ThenSecondIsIgnored()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var sources = new IObservableAsync<int>[] { src1, src2 };
        var completionCount = 0;

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (x, _) => default,
                null,
                _ =>
                {
                    Interlocked.Increment(ref completionCount);
                    return default;
                });

        await src1.EmitNext(1);
        await src2.EmitNext(2);

        // Complete src1 (has emitted, src2 still active → no overall completion)
        await src1.Complete(Result.Success);

        // Complete src1 again - already completed[0] = true, returns early
        await src1.Complete(Result.Success);

        // Now complete src2 → overall completion
        await src2.Complete(Result.Success);

        await Assert.That(completionCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that CombineLatestEnumerable completes when a source completes without emitting.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableSourceCompletesWithoutValue_ThenCompletesImmediately()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var sources = new IObservableAsync<int>[] { src1, src2 };
        Result? completionResult = null;

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (x, _) => default,
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // src1 completes without ever emitting, so shouldComplete = !_values[0].HasValue = true
        await src1.Complete(Result.Success);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that CombineLatestEnumerable returns early when disposed during subscribe loop.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableDisposedDuringSubscribeLoop_ThenReturnsEarly()
    {
        // First source triggers disposal when subscribed
        var disposeTrigger = new TaskCompletionSource<IAsyncDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowSource = ObservableAsync.Create<int>(async (_, ct) =>
        {
            var disp = await disposeTrigger.Task.WaitAsync(ct);
            await disp.DisposeAsync();
            return DisposableAsync.Empty;
        });

        var normalSource = new DirectSource<int>();
        var sources = new IObservableAsync<int>[] { slowSource, normalSource };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            var sub = await sources.CombineLatest()
                .SubscribeAsync((x, _) => default, null, null, cts.Token);
            disposeTrigger.SetResult(sub);
            await sub.DisposeAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    /// <summary>
    /// Verifies that SubscribeAsync bails out of the source subscription loop when an
    /// earlier source completes without emitting (triggering overall completion and
    /// cancelling the dispose CTS) before remaining sources are subscribed,
    /// covering the early-return guard in CombineLatestEnumerable.SubscribeAsync.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableFirstSourceCompletesImmediately_ThenSkipsRemainingSubscriptions()
    {
        var secondSourceSubscribed = false;

        // Second source records whether it was ever subscribed.
        var trackingSource = ObservableAsync.Create<int>((_, _) =>
        {
            secondSourceSubscribed = true;
            return new ValueTask<IAsyncDisposable>(DisposableAsync.Empty);
        });

        // Empty completes immediately during subscribe, triggering overall completion
        // before the loop reaches the second source.
        var sources = new IObservableAsync<int>[] { AsyncObs.Empty<int>(), trackingSource };

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync((_, _) => default, null, null);

        await Assert.That(secondSourceSubscribed).IsFalse();
    }

    /// <summary>
    /// Verifies that OnCompletedAsync returns early when the same source index completes
    /// a second time while the subscription is still active (the _completed[index] guard),
    /// covering line 268 in CombineLatestEnumerable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableSameSourceCompletedTwice_ThenSecondCompletionIsIgnored()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var src3 = new DirectSource<int>();
        var sources = new IObservableAsync<int>[] { src1, src2, src3 };
        var completionCount = 0;

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (_, _) => default,
                null,
                _ =>
                {
                    Interlocked.Increment(ref completionCount);
                    return default;
                });

        // All sources emit so _values[i].HasValue is true for all.
        await src1.EmitNext(1);
        await src2.EmitNext(2);
        await src3.EmitNext(3);

        // src1 completes: _completed[0]=true, completedCount=1 (not 3), shouldComplete=false.
        await src1.Complete(Result.Success);

        // src1 completes again: _completed[0] is already true, returns early (line 268).
        await src1.Complete(Result.Success);

        await Assert.That(completionCount).IsEqualTo(0);

        // Finish: complete remaining sources so the subscription terminates cleanly.
        await src2.Complete(Result.Success);
        await src3.Complete(Result.Success);

        await Assert.That(completionCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that OnCompletedAsync takes the non-completing path (shouldComplete=false)
    /// when a source that has already emitted a value completes while other sources remain active,
    /// covering line 277 in CombineLatestEnumerable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableSourceWithValueCompletes_ThenDoesNotCompleteUntilAllDone()
    {
        var src1 = new DirectSource<int>();
        var src2 = new DirectSource<int>();
        var sources = new IObservableAsync<int>[] { src1, src2 };
        Result? completionResult = null;
        var emissions = new List<IReadOnlyList<int>>();

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (snapshot, _) =>
                {
                    emissions.Add(snapshot);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // Both emit so _values[i].HasValue is true for both.
        await src1.EmitNext(10);
        await src2.EmitNext(20);

        await Assert.That(emissions).Count().IsEqualTo(1);

        // src1 completes: _values[0].HasValue=true, completedCount=1 (not 2) → shouldComplete=false (line 277).
        await src1.Complete(Result.Success);

        // Subscription is still active because shouldComplete was false.
        await Assert.That(completionResult).IsNull();

        // src2 can still emit and combine with src1's last value.
        await src2.EmitNext(30);

        await Assert.That(emissions).Count().IsEqualTo(2);
        await Assert.That(emissions[1][0]).IsEqualTo(10);
        await Assert.That(emissions[1][1]).IsEqualTo(30);

        // Complete src2 → all sources completed, shouldComplete=true.
        await src2.Complete(Result.Success);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that OnCompletedAsync returns early without incrementing the completed count
    /// when the same source index has already completed, exercising the _completed[index] guard
    /// on line 268 of CombineLatestEnumerable with a single additional source still active.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableDuplicateCompletionForSameIndex_ThenIgnoredAndRemainingSourceStillEmits()
    {
        var src1 = AsyncTestHelpers.CreateDirectSource<int>();
        var src2 = AsyncTestHelpers.CreateDirectSource<int>();
        var sources = new IObservableAsync<int>[] { src1, src2 };
        var emissions = new List<IReadOnlyList<int>>();
        Result? completionResult = null;

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (snapshot, _) =>
                {
                    emissions.Add(snapshot);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // Both sources emit so all _values have values.
        await src1.EmitNext(1);
        await src2.EmitNext(2);

        await Assert.That(emissions).Count().IsEqualTo(1);

        // src1 completes: _completed[0]=true, completedCount=1, shouldComplete=false.
        await src1.Complete(Result.Success);

        await Assert.That(completionResult).IsNull();

        // Duplicate completion for index 0: _completed[0] is already true, returns default (line 268).
        await src1.Complete(Result.Success);

        // Still no overall completion because src2 hasn't completed.
        await Assert.That(completionResult).IsNull();

        // src2 can still emit; the duplicate completion did not corrupt state.
        await src2.EmitNext(5);

        await Assert.That(emissions).Count().IsEqualTo(2);
        await Assert.That(emissions[1][0]).IsEqualTo(1);
        await Assert.That(emissions[1][1]).IsEqualTo(5);

        // Clean termination.
        await src2.Complete(Result.Success);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that when one source in a three-source CombineLatest completes without ever
    /// emitting a value, the shouldComplete path triggers immediate completion and the remaining
    /// sources are torn down, exercising line 277 of CombineLatestEnumerable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestEnumerableMiddleSourceCompletesWithoutEmitting_ThenCompletesImmediately()
    {
        var src1 = AsyncTestHelpers.CreateDirectSource<int>();
        var src2 = AsyncTestHelpers.CreateDirectSource<int>();
        var src3 = AsyncTestHelpers.CreateDirectSource<int>();
        var sources = new IObservableAsync<int>[] { src1, src2, src3 };
        var emissions = new List<IReadOnlyList<int>>();
        Result? completionResult = null;

        await using var sub = await sources.CombineLatest()
            .SubscribeAsync(
                (snapshot, _) =>
                {
                    emissions.Add(snapshot);
                    return default;
                },
                null,
                result =>
                {
                    completionResult = result;
                    return default;
                });

        // src1 and src3 emit, but src2 never emits.
        await src1.EmitNext(10);
        await src3.EmitNext(30);

        // No snapshot yet because src2 has not emitted.
        await Assert.That(emissions).IsEmpty();

        // src2 completes without emitting: !_values[1].HasValue is true → shouldComplete=true (line 277).
        await src2.Complete(Result.Success);

        await Assert.That(completionResult).IsNotNull();
        await Assert.That(completionResult!.Value.IsSuccess).IsTrue();

        // No snapshots were ever emitted because not all sources had values.
        await Assert.That(emissions).IsEmpty();
    }
}
