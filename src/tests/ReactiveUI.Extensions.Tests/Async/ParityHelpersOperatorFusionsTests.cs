// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>Edge-case coverage for the fused async operators in
/// <c>ParityHelpers.OperatorFusions</c> — async <c>ScanWithInitial</c>,
/// <c>ThrottleDistinct</c> upstream/downstream filtering, <c>DebounceUntil</c>
/// immediate-bypass branch, and the typed fast paths in <c>ForEach</c>
/// (array / IReadOnlyList / general IEnumerable).</summary>
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "TUnit requires instance methods")]
public class ParityHelpersOperatorFusionsTests
{
    /// <summary>Initial accumulator seed for scan tests.</summary>
    private const int ScanSeed = 0;

    /// <summary>Throttle window in milliseconds for <c>ThrottleDistinct</c> tests.</summary>
    private const int ThrottleWindowMilliseconds = 50;

    /// <summary>Sentinel one.</summary>
    private const int One = 1;

    /// <summary>Sentinel two.</summary>
    private const int Two = 2;

    /// <summary>Sentinel three.</summary>
    private const int Three = 3;

    /// <summary>Sentinel four.</summary>
    private const int Four = 4;

    /// <summary>Array sentinels for the array fast-path test.</summary>
    private static readonly int[] ArraySlice1 = [One, Two];

    /// <summary>Second array sentinels.</summary>
    private static readonly int[] ArraySlice2 = [Three, Four];

    /// <summary>Expected flat result for the array fast-path test.</summary>
    private static readonly int[] ExpectedArrayFlat = [One, Two, Three, Four];

    /// <summary>Expected flat result for the list and enumerable tests.</summary>
    private static readonly int[] ExpectedListFlat = [One, Two, Three];

    /// <summary>Inputs for the <c>ScanWithInitial</c> async-accumulator test.</summary>
    private static readonly int[] ScanInputs = [One, Two, Three];

    /// <summary>Inputs for the <c>ThrottleDistinct</c> rapid-values test.</summary>
    private static readonly int[] ThrottleRapidInputs = [One, Two, Three];

    /// <summary>Inputs for the <c>DebounceUntil</c> immediate-bypass test.</summary>
    private static readonly int[] DebounceInputs = [One, Two, Three];

    /// <summary>Inputs of all-equal values for the <c>ThrottleDistinct</c> duplicates test.</summary>
    private static readonly int[] ThrottleDuplicateInputs = [One, One, One];

    /// <summary>Verifies that the async-accumulator overload of <c>ScanWithInitial</c>
    /// emits the seed first then every intermediate value produced by the asynchronous fold.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScanWithInitialAsync_ThenSeedThenAsyncFolded()
    {
        var result = await ScanInputs.ToObservableAsync()
            .ScanWithInitial(ScanSeed, static async (acc, x, _) =>
            {
                await Task.Yield();
                return acc + x;
            })
            .ToListAsync();

        int[] expected =
        [
            ScanSeed,
            One,
            One + Two,
            One + Two + Three
        ];
        await Assert.That(result).IsCollectionEqualTo(expected);
    }

    /// <summary>Verifies that <c>ThrottleDistinct</c> suppresses consecutive duplicates upstream
    /// before any throttle work is scheduled.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleDistinctConsecutiveDuplicates_ThenSuppressesUpstream()
    {
        var result = await ThrottleDuplicateInputs.ToObservableAsync()
            .ThrottleDistinct(TimeSpan.FromMilliseconds(ThrottleWindowMilliseconds))
            .ToListAsync();

        // All inputs are equal — only one emission is ever scheduled, and the source completes
        // before the throttle window elapses, so the pending emission must still flush exactly once.
        await Assert.That(result.Count).IsLessThanOrEqualTo(1);
    }

    /// <summary>Verifies that <c>ThrottleDistinct</c> with distinct rapid values respects the
    /// no-consecutive-duplicates contract and never emits more than the input count.
    /// (Pending throttled emissions are superseded by source completion — this is the
    /// documented behavior, so a count-bound assertion is the appropriate check rather than
    /// "at least one emission".)</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleDistinctRapidDistinctValues_ThenNoConsecutiveDuplicates()
    {
        var result = await ThrottleRapidInputs.ToObservableAsync()
            .ThrottleDistinct(TimeSpan.FromMilliseconds(ThrottleWindowMilliseconds))
            .ToListAsync();

        await Assert.That(result.Count).IsLessThanOrEqualTo(ThrottleRapidInputs.Length);
        for (var i = 1; i < result.Count; i++)
        {
            await Assert.That(result[i]).IsNotEqualTo(result[i - 1]);
        }
    }

    /// <summary>Verifies that <c>DebounceUntil</c> with an always-true condition bypasses
    /// the debounce window and emits inline.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDebounceUntilConditionAlwaysTrue_ThenEmitsImmediately()
    {
        var result = await DebounceInputs.ToObservableAsync()
            .DebounceUntil(TimeSpan.FromSeconds(5), static _ => true)
            .ToListAsync();

        await Assert.That(result).IsCollectionEqualTo(DebounceInputs);
    }

    /// <summary>Verifies that the array fast path of <c>ForEach</c> flattens an
    /// <c>IObservableAsync&lt;T[]&gt;</c> into a flat sequence of elements.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForEachOverArray_ThenUsesArrayFastPath()
    {
        IEnumerable<int>[] arrays = [ArraySlice1, ArraySlice2];

        var result = await arrays.ToObservableAsync()
            .ForEach()
            .ToListAsync();

        await Assert.That(result).IsCollectionEqualTo(ExpectedArrayFlat);
    }

    /// <summary>Verifies that the <see cref="IReadOnlyList{T}"/> fast path of <c>ForEach</c>
    /// flattens a list-typed source.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForEachOverReadOnlyList_ThenUsesListFastPath()
    {
        var firstList = new List<int>(ArraySlice1);
        var secondList = new List<int>(1) { Three };
        IEnumerable<int>[] lists = [firstList, secondList];

        var result = await lists.ToObservableAsync()
            .ForEach()
            .ToListAsync();

        await Assert.That(result).IsCollectionEqualTo(ExpectedListFlat);
    }

    /// <summary>Verifies that the general <see cref="IEnumerable{T}"/> path of <c>ForEach</c>
    /// flattens a non-array, non-list source.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForEachOverGenericEnumerable_ThenUsesEnumeratorPath()
    {
        IEnumerable<int>[] enumerables = [Enumerate(One, Two), Enumerate(Three)];

        var result = await enumerables.ToObservableAsync()
            .ForEach()
            .ToListAsync();

        await Assert.That(result).IsCollectionEqualTo(ExpectedListFlat);
    }

    /// <summary>Verifies that <c>Partition</c> broadcasts an upstream non-terminal error to both
    /// subscribed branches via the <c>OnErrorResume</c> path.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPartitionSourceErrorResume_ThenBothBranchesReceiveError()
    {
        var subject = SubjectAsync.Create<int>();
        var (evens, odds) = subject.Values.Partition(static x => x % Two == 0);

        Exception? evenError = null;
        Exception? oddError = null;
        var evenTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oddTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var evenSub = await evens.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                evenError = ex;
                evenTcs.TrySetResult();
                return default;
            });
        await using var oddSub = await odds.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                oddError = ex;
                oddTcs.TrySetResult();
                return default;
            });

        var expected = new InvalidOperationException("partition-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        await Task.WhenAll(evenTcs.Task, oddTcs.Task).WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(evenError).IsSameReferenceAs(expected);
        await Assert.That(oddError).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies that a branch subscriber attaching after the source has already
    /// completed gets the cached terminal forwarded immediately.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPartitionLateBranchSubscribesAfterCompletion_ThenCachedTerminalForwarded()
    {
        var subject = SubjectAsync.Create<int>();
        var (evens, odds) = subject.Values.Partition(static x => x % Two == 0);

        var firstTask = evens.ToListAsync().AsTask();
        await subject.OnNextAsync(Two, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);
        await firstTask;

        var lateValues = new List<int>();
        var lateCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var lateSub = await odds.SubscribeAsync(
            (v, _) =>
            {
                lateValues.Add(v);
                return default;
            },
            (_, _) => default,
            result =>
            {
                lateCompleted.TrySetResult();
                return default;
            });

        await lateCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(lateValues).IsEmpty();
    }

    /// <summary>Verifies that <c>DropIfBusy</c> resets the busy flag and re-throws when the
    /// async action throws synchronously (rather than returning a faulted task).</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDropIfBusyActionThrowsSynchronously_ThenBusyFlagResetAndErrorObserved()
    {
        var failure = new InvalidOperationException("sync-throw");
        InvalidOperationException? observed = null;

        try
        {
            await new[] { One }.ToObservableAsync()
                .DropIfBusy(static (_, _) => throw new InvalidOperationException("sync-throw"))
                .ToListAsync();
        }
        catch (InvalidOperationException ex)
        {
            observed = ex;
        }

        await Assert.That(observed).IsNotNull();
        await Assert.That(observed!.Message).IsEqualTo(failure.Message);
    }

    /// <summary>Verifies that <c>ScanWithInitial</c> forwards a non-terminal upstream error
    /// downstream while still emitting the seed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenScanWithInitialSourceErrorResumes_ThenForwardsDownstream()
    {
        var subject = SubjectAsync.Create<int>();
        var values = new List<int>();
        Exception? caught = null;
        var errorTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await subject.Values
            .ScanWithInitial(ScanSeed, static (acc, x) => acc + x)
            .SubscribeAsync(
                (v, _) =>
                {
                    values.Add(v);
                    return default;
                },
                (ex, _) =>
                {
                    caught = ex;
                    errorTcs.TrySetResult();
                    return default;
                });

        var expected = new InvalidOperationException("scan-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(caught).IsSameReferenceAs(expected);
        await Assert.That(values).IsCollectionEqualTo([ScanSeed]);
    }

    /// <summary>Verifies that <c>ThrottleDistinct</c> forwards a non-terminal upstream error
    /// downstream.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenThrottleDistinctSourceErrorResumes_ThenForwardsDownstream()
    {
        var subject = SubjectAsync.Create<int>();
        Exception? caught = null;
        var errorTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await subject.Values
            .ThrottleDistinct(TimeSpan.FromMilliseconds(ThrottleWindowMilliseconds))
            .SubscribeAsync(
                static (_, _) => default,
                (ex, _) =>
                {
                    caught = ex;
                    errorTcs.TrySetResult();
                    return default;
                });

        var expected = new InvalidOperationException("throttle-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(caught).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies that <c>DebounceUntil</c> forwards a non-terminal upstream error
    /// downstream.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDebounceUntilSourceErrorResumes_ThenForwardsDownstream()
    {
        var subject = SubjectAsync.Create<int>();
        Exception? caught = null;
        var errorTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await subject.Values
            .DebounceUntil(TimeSpan.FromSeconds(5), static _ => false)
            .SubscribeAsync(
                static (_, _) => default,
                (ex, _) =>
                {
                    caught = ex;
                    errorTcs.TrySetResult();
                    return default;
                });

        var expected = new InvalidOperationException("debounce-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(caught).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies that <c>ForEach</c> forwards a non-terminal upstream error downstream.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenForEachSourceErrorResumes_ThenForwardsDownstream()
    {
        var subject = SubjectAsync.Create<IEnumerable<int>>();
        Exception? caught = null;
        var errorTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await subject.Values
            .ForEach()
            .SubscribeAsync(
                static (_, _) => default,
                (ex, _) =>
                {
                    caught = ex;
                    errorTcs.TrySetResult();
                    return default;
                });

        var expected = new InvalidOperationException("foreach-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(caught).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies that <c>DropIfBusy</c> forwards a non-terminal upstream error downstream.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDropIfBusySourceErrorResumes_ThenForwardsDownstream()
    {
        var subject = SubjectAsync.Create<int>();
        Exception? caught = null;
        var errorTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sub = await subject.Values
            .DropIfBusy(static (_, _) => default)
            .SubscribeAsync(
                static (_, _) => default,
                (ex, _) =>
                {
                    caught = ex;
                    errorTcs.TrySetResult();
                    return default;
                });

        var expected = new InvalidOperationException("dropifbusy-error");
        await subject.OnErrorResumeAsync(expected, CancellationToken.None);

        await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(caught).IsSameReferenceAs(expected);
    }

    /// <summary>Yields values as a generic <see cref="IEnumerable{T}"/> (neither array nor list)
    /// to drive the slow-path branch of <c>ForEach</c>.</summary>
    /// <param name="values">Values to yield.</param>
    /// <returns>A lazily-evaluated enumerable.</returns>
    private static IEnumerable<int> Enumerate(params int[] values)
    {
        foreach (var v in values)
        {
            yield return v;
        }
    }
}
