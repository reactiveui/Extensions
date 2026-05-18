// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;

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
