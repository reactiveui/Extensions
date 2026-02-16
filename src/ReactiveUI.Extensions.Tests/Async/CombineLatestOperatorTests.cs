// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NUnit.Framework;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;
using AsyncObs = ReactiveUI.Extensions.Async.ObservableAsync;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Comprehensive tests for the CombineLatest operator covering all arities (2-8),
/// error propagation, completion, error resume, disposal, and edge cases.
/// </summary>
public class CombineLatestOperatorTests
{
    // ???????????????????????????? 2-source ????????????????????????????

    /// <summary>No emission until both sources have produced a value.</summary>
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
        await Task.Delay(50);

        Assert.That(results, Is.Empty);

        await s2.OnNextAsync(10, CancellationToken.None);
        await Task.Delay(50);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0], Is.EqualTo(11));
    }

    /// <summary>Multiple emissions use the latest value from each source.</summary>
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
        await Task.Delay(50);

        Assert.That(results, Has.Count.GreaterThanOrEqualTo(3));
        Assert.That(results[0], Is.EqualTo(11));
        Assert.That(results[1], Is.EqualTo(12));
        Assert.That(results[2], Is.EqualTo(22));
    }

    /// <summary>Error from source 1 completes the combined sequence with failure.</summary>
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
        await Task.Delay(50);

        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsFailure, Is.True);
        Assert.That(completionResult.Value.Exception, Is.InstanceOf<InvalidOperationException>());
    }

    /// <summary>Error from source 2 completes the combined sequence with failure.</summary>
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
        await Task.Delay(50);

        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsFailure, Is.True);
    }

    /// <summary>Combined sequence completes only when both sources complete successfully.</summary>
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
        await Task.Delay(50);
        Assert.That(completionResult, Is.Null);

        await s2.OnCompletedAsync(Result.Success);
        await Task.Delay(50);
        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsSuccess, Is.True);
    }

    /// <summary>Partial completion does not complete the combined sequence.</summary>
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
        await Task.Delay(50);

        Assert.That(completionResult, Is.Null);
    }

    /// <summary>Disposal stops further emissions.</summary>
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
        await Task.Delay(50);
        var countBefore = results.Count;

        await sub.DisposeAsync();

        await s1.OnNextAsync(2, CancellationToken.None);
        await Task.Delay(50);

        Assert.That(results, Has.Count.EqualTo(countBefore));
    }

    /// <summary>Double disposal is safe and does not throw.</summary>
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
        await Task.Delay(50);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Is.SameAs(expected));
    }

    /// <summary>Error resume from source 2 is also forwarded.</summary>
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
        await Task.Delay(50);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Is.SameAs(expected));
    }

    // ???????????????????????????? 3-source ????????????????????????????

    /// <summary>Tests CombineLatest with 3 sources combines correctly.</summary>
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
        await Task.Delay(50);

        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results[0], Is.EqualTo(111));
    }

    /// <summary>No emission until all three sources have values.</summary>
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
        await Task.Delay(50);

        Assert.That(results, Is.Empty);
    }

    /// <summary>Error from any source propagates in 3-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsFailure, Is.True);
    }

    /// <summary>All three sources complete successfully.</summary>
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
        await Task.Delay(50);
        Assert.That(completionResult, Is.Null);

        await s3.OnCompletedAsync(Result.Success);
        await Task.Delay(50);
        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsSuccess, Is.True);
    }

    /// <summary>Error resume forwarded in 3-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Is.SameAs(expected));
    }

    // ???????????????????????????? 4-source ????????????????????????????

    /// <summary>Error propagation in 4-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsFailure, Is.True);
    }

    /// <summary>All four sources complete successfully.</summary>
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

        await Task.Delay(50);
        Assert.That(completionResult, Is.Null);

        await subjects[3].OnCompletedAsync(Result.Success);
        await Task.Delay(50);
        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsSuccess, Is.True);
    }

    /// <summary>Error resume forwarded in 4-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Is.SameAs(expected));
    }

    // ???????????????????????????? 5-source ????????????????????????????

    /// <summary>Error propagation in 5-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsFailure, Is.True);
    }

    /// <summary>All five sources complete successfully.</summary>
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

        await Task.Delay(50);
        Assert.That(completionResult, Is.Null);

        await subjects[4].OnCompletedAsync(Result.Success);
        await Task.Delay(50);
        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsSuccess, Is.True);
    }

    /// <summary>Error resume forwarded in 5-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Is.SameAs(expected));
    }

    // ???????????????????????????? 6-source ????????????????????????????

    /// <summary>Error propagation in 6-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsFailure, Is.True);
    }

    /// <summary>All six sources complete successfully.</summary>
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

        await Task.Delay(50);
        Assert.That(completionResult, Is.Null);

        await subjects[5].OnCompletedAsync(Result.Success);
        await Task.Delay(50);
        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsSuccess, Is.True);
    }

    /// <summary>Error resume forwarded in 6-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Is.SameAs(expected));
    }

    // ???????????????????????????? 7-source ????????????????????????????

    /// <summary>Error propagation in 7-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsFailure, Is.True);
    }

    /// <summary>All seven sources complete successfully.</summary>
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

        await Task.Delay(50);
        Assert.That(completionResult, Is.Null);

        await subjects[6].OnCompletedAsync(Result.Success);
        await Task.Delay(50);
        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsSuccess, Is.True);
    }

    /// <summary>Error resume forwarded in 7-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Is.SameAs(expected));
    }

    // ???????????????????????????? 8-source ????????????????????????????

    /// <summary>Error propagation in 8-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsFailure, Is.True);
    }

    /// <summary>All eight sources complete successfully.</summary>
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

        await Task.Delay(50);
        Assert.That(completionResult, Is.Null);

        await subjects[7].OnCompletedAsync(Result.Success);
        await Task.Delay(50);
        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsSuccess, Is.True);
    }

    /// <summary>Error resume forwarded in 8-source variant.</summary>
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
        await Task.Delay(50);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Is.SameAs(expected));
    }

    // ???????????????????????? Cross-cutting ????????????????????????

    /// <summary>Error resume after disposal is ignored.</summary>
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
        await Task.Delay(50);

        Assert.That(errors, Is.Empty);
    }

    /// <summary>Error from source before any values still propagates.</summary>
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
        await Task.Delay(50);

        Assert.That(completionResult, Is.Not.Null);
        Assert.That(completionResult!.Value.IsFailure, Is.True);
    }

    /// <summary>Emission continues after error resume (non-terminal).</summary>
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
        await Task.Delay(50);

        await s1.OnErrorResumeAsync(new InvalidOperationException("non-terminal"), CancellationToken.None);
        await Task.Delay(50);

        await s1.OnNextAsync(2, CancellationToken.None);
        await Task.Delay(50);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(results[^1], Is.EqualTo(12));
    }

    /// <summary>No emission until all N sources have values for 4-source variant.</summary>
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

        await Task.Delay(50);
        Assert.That(results, Is.Empty);

        await subjects[3].OnNextAsync(4, CancellationToken.None);
        await Task.Delay(50);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0], Is.EqualTo(10));
    }

    /// <summary>Disposal of 3-source variant stops emissions.</summary>
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
        await Task.Delay(50);
        var countBefore = results.Count;

        await sub.DisposeAsync();

        await s1.OnNextAsync(2, CancellationToken.None);
        await Task.Delay(50);

        Assert.That(results, Has.Count.EqualTo(countBefore));
    }

    /// <summary>Multiple emissions with updated latest values for 5-source variant.</summary>
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

        await Task.Delay(50);
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results[0], Is.EqualTo(5));

        await subjects[0].OnNextAsync(10, CancellationToken.None);
        await Task.Delay(50);
        Assert.That(results[^1], Is.EqualTo(14));
    }
}
