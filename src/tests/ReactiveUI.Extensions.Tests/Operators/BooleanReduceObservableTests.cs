// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Tests.Operators;

/// <summary>Edge-case coverage for the boolean-reduce operators backed by
/// <c>BooleanReduceObservable</c> — empty-source short-circuit, partial-value
/// suppression, target match/mismatch, error broadcast.</summary>
public class BooleanReduceObservableTests
{
    /// <summary>Synthetic error message attached to source errors.</summary>
    private const string SourceErrorMessage = "source error";

    /// <summary>Verifies that an empty input emits a single <c>true</c> and completes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCombineLatestValuesAreAllTrueWithEmptySources_ThenEmitsTrueAndCompletes()
    {
        var results = new List<bool>();
        var completed = false;

        using var sub = Array.Empty<IObservable<bool>>()
            .CombineLatestValuesAreAllTrue()
            .Subscribe(results.Add, () => completed = true);

        await Assert.That(results).IsCollectionEqualTo([true]);
        await Assert.That(completed).IsTrue();
    }

    /// <summary>Verifies that partial sources do not emit until every source has a value.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAllTruePartialSources_ThenSuppressesEmission()
    {
        var a = new Subject<bool>();
        var b = new Subject<bool>();
        var results = new List<bool>();

        using var sub = new IObservable<bool>[] { a, b }
            .CombineLatestValuesAreAllTrue()
            .Subscribe(results.Add);

        a.OnNext(true);

        await Assert.That(results).IsEmpty();
    }

    /// <summary>Verifies that the operator emits <c>true</c> only when every latest value is <c>true</c>.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAllTrueTransitions_ThenEmitsExpectedSequence()
    {
        var a = new Subject<bool>();
        var b = new Subject<bool>();
        var results = new List<bool>();

        using var sub = new IObservable<bool>[] { a, b }
            .CombineLatestValuesAreAllTrue()
            .Subscribe(results.Add);

        a.OnNext(true);
        b.OnNext(false);
        b.OnNext(true);
        a.OnNext(false);

        await Assert.That(results).IsCollectionEqualTo([false, true, false]);
    }

    /// <summary>Verifies that <c>AllFalse</c> emits <c>true</c> only when every latest value is <c>false</c>.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAllFalseTransitions_ThenEmitsExpectedSequence()
    {
        var a = new Subject<bool>();
        var b = new Subject<bool>();
        var results = new List<bool>();

        using var sub = new IObservable<bool>[] { a, b }
            .CombineLatestValuesAreAllFalse()
            .Subscribe(results.Add);

        a.OnNext(false);
        b.OnNext(false);
        b.OnNext(true);

        await Assert.That(results).IsCollectionEqualTo([true, false]);
    }

    /// <summary>Verifies that a source error propagates downstream.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAllTrueSourceErrors_ThenForwardsError()
    {
        var a = new Subject<bool>();
        var b = new Subject<bool>();
        Exception? caught = null;
        var expected = new InvalidOperationException(SourceErrorMessage);

        using var sub = new IObservable<bool>[] { a, b }
            .CombineLatestValuesAreAllTrue()
            .Subscribe(static _ => { }, ex => caught = ex);

        a.OnError(expected);

        await Assert.That(caught).IsSameReferenceAs(expected);
    }
}
