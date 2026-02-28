// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for transformation operators: Select, SelectMany, Scan, Do, Cast, OfType.
/// </summary>
public class TransformationOperatorTests
{
    /// <summary>Tests sync Select projects each element.</summary>
    [Test]
    public async Task WhenSelectSyncSelector_ThenProjectsEachElement()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Select(x => x * 10)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 10, 20, 30 });
    }

    /// <summary>Tests async Select projects each element.</summary>
    [Test]
    public async Task WhenSelectAsyncSelector_ThenProjectsEachElement()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Select(async (x, ct) =>
            {
                await Task.Yield();
                return x.ToString();
            })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "1", "2", "3" });
    }

    /// <summary>Tests sync SelectMany flattens inner sequences.</summary>
    [Test]
    public async Task WhenSelectManySync_ThenFlattensInnerSequences()
    {
        var result = await ObservableAsync.Range(1, 3)
            .SelectMany(x => ObservableAsync.Range(x * 10, 2))
            .ToListAsync();

        await Assert.That(result).Count().IsEqualTo(6);
        await Assert.That(result).Contains(10);
        await Assert.That(result).Contains(30);
    }

    /// <summary>Tests async SelectMany flattens inner sequences.</summary>
    [Test]
    public async Task WhenSelectManyAsync_ThenFlattensInnerSequences()
    {
        var result = await ObservableAsync.Range(1, 2)
            .SelectMany(async (x, ct) =>
            {
                await Task.Yield();
                return ObservableAsync.Return(x * 100);
            })
            .ToListAsync();

        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result).Contains(100);
        await Assert.That(result).Contains(200);
    }

    /// <summary>Tests SelectMany with result selector projects pairs.</summary>
    [Test]
    public async Task WhenSelectManyWithResultSelector_ThenProjectsPairs()
    {
        var result = await ObservableAsync.Range(1, 2)
            .SelectMany(
                x => ObservableAsync.Return(x * 10),
                (outer, inner) => $"{outer}:{inner}")
            .ToListAsync();

        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result).Contains("1:10");
        await Assert.That(result).Contains("2:20");
    }

    /// <summary>Tests SelectMany null selector throws.</summary>
    [Test]
    public void WhenSelectManyNullSelector_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).SelectMany((Func<int, ObservableAsync<int>>)null!));
    }

    /// <summary>Tests sync Scan emits running accumulation.</summary>
    [Test]
    public async Task WhenScanSync_ThenEmitsRunningAccumulation()
    {
        var result = await ObservableAsync.Range(1, 4)
            .Scan(0, (acc, x) => acc + x)
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 3, 6, 10 });
    }

    /// <summary>Tests async Scan emits running accumulation.</summary>
    [Test]
    public async Task WhenScanAsync_ThenEmitsRunningAccumulation()
    {
        var result = await ObservableAsync.Range(1, 3)
            .Scan(string.Empty, async (acc, x, ct) =>
            {
                await Task.Yield();
                return acc + x;
            })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "1", "12", "123" });
    }

    /// <summary>Tests Scan null accumulator throws.</summary>
    [Test]
    public void WhenScanNullAccumulator_ThenThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObservableAsync.Return(1).Scan(0, (Func<int, int, int>)null!));
    }

    /// <summary>Tests sync Do invokes side effects.</summary>
    [Test]
    public async Task WhenDoSync_ThenInvokesSideEffects()
    {
        var sideEffects = new List<int>();

        var result = await ObservableAsync.Range(1, 3)
            .Do(onNext: x => sideEffects.Add(x))
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
        await Assert.That(sideEffects).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests async Do invokes side effects.</summary>
    [Test]
    public async Task WhenDoAsync_ThenInvokesSideEffects()
    {
        var sideEffects = new List<int>();

        var result = await ObservableAsync.Range(1, 3)
            .Do(async (x, ct) =>
            {
                await Task.Yield();
                sideEffects.Add(x);
            })
            .ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { 1, 2, 3 });
        await Assert.That(sideEffects).IsEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>Tests Do with completion handler invokes on completed.</summary>
    [Test]
    public async Task WhenDoWithCompletionHandler_ThenInvokesOnCompleted()
    {
        Result? completion = null;

        await ObservableAsync.Empty<int>()
            .Do(onCompleted: r => completion = r)
            .WaitCompletionAsync();

        await Assert.That(completion).IsNotNull();
        await Assert.That(completion!.Value.IsSuccess).IsTrue();
    }

    /// <summary>Tests Cast with compatible type casts correctly.</summary>
    [Test]
    public async Task WhenCastCompatibleType_ThenCasts()
    {
        var source = ObservableAsync.Return<object>("hello");

        var result = await source.Cast<object, string>().ToListAsync();

        await Assert.That(result).IsEquivalentTo(new[] { "hello" });
    }

    /// <summary>Tests OfType with matching type filters correctly.</summary>
    [Test]
    public async Task WhenOfTypeMatchingType_ThenFiltersCorrectly()
    {
        var items = new object[] { 1, "two", 3, "four" };
        var source = items.ToObservableAsync();

        var strings = await source.OfType<object, string>().ToListAsync();

        await Assert.That(strings).IsEquivalentTo(new[] { "two", "four" });
    }

    /// <summary>Tests OfType with no matches emits nothing.</summary>
    [Test]
    public async Task WhenOfTypeNoMatches_ThenEmitsNothing()
    {
        var source = new object[] { 1, 2, 3 }.ToObservableAsync();

        var strings = await source.OfType<object, string>().ToListAsync();

        await Assert.That(strings).IsEmpty();
    }
}
