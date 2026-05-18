// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Tests.Operators;

/// <summary>Edge-case coverage for <c>Partition</c> backed by
/// <c>PartitionObservable&lt;T&gt;</c> — both-sides routing, single-side disposal,
/// error broadcast, completion broadcast, and re-subscription after both sides drop.</summary>
public partial class PartitionObservableTests
{
    /// <summary>Synthetic error message attached to source errors.</summary>
    private const string SourceErrorMessage = "source error";

    /// <summary>Even-modulus divisor.</summary>
    private const int Two = 2;

    /// <summary>First odd value.</summary>
    private const int One = 1;

    /// <summary>Second odd value.</summary>
    private const int Three = 3;

    /// <summary>Second even value.</summary>
    private const int Four = 4;

    /// <summary>Verifies that elements route to the correct side based on the predicate.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPartitionWithBothSidesSubscribed_ThenRoutesByPredicate()
    {
        var subject = new Subject<int>();
        var (evens, odds) = subject.Partition(static x => x % Two == 0);
        var evenResults = new List<int>();
        var oddResults = new List<int>();

        using var evenSub = evens.Subscribe(evenResults.Add);
        using var oddSub = odds.Subscribe(oddResults.Add);

        subject.OnNext(One);
        subject.OnNext(Two);
        subject.OnNext(Three);
        subject.OnNext(Four);

        await Assert.That(evenResults).IsCollectionEqualTo([Two, Four]);
        await Assert.That(oddResults).IsCollectionEqualTo([One, Three]);
    }

    /// <summary>Verifies that a value with no observer on its side is silently dropped.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPartitionOnlyOneSideSubscribed_ThenOtherSideValuesDropped()
    {
        var subject = new Subject<int>();
        var (evens, _) = subject.Partition(static x => x % Two == 0);
        var evenResults = new List<int>();

        using var evenSub = evens.Subscribe(evenResults.Add);

        subject.OnNext(One);
        subject.OnNext(Two);

        await Assert.That(evenResults).IsCollectionEqualTo([Two]);
    }

    /// <summary>Verifies that errors are broadcast to both sides.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPartitionSourceErrors_ThenBroadcastsToBothSides()
    {
        var subject = new Subject<int>();
        var (evens, odds) = subject.Partition(static x => x % Two == 0);
        Exception? evenError = null;
        Exception? oddError = null;
        var expected = new InvalidOperationException(SourceErrorMessage);

        using var evenSub = evens.Subscribe(static _ => { }, ex => evenError = ex);
        using var oddSub = odds.Subscribe(static _ => { }, ex => oddError = ex);

        subject.OnError(expected);

        await Assert.That(evenError).IsSameReferenceAs(expected);
        await Assert.That(oddError).IsSameReferenceAs(expected);
    }

    /// <summary>Verifies that completion is broadcast to both sides.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPartitionSourceCompletes_ThenBroadcastsToBothSides()
    {
        var subject = new Subject<int>();
        var (evens, odds) = subject.Partition(static x => x % Two == 0);
        var evenCompleted = false;
        var oddCompleted = false;

        using var evenSub = evens.Subscribe(static _ => { }, () => evenCompleted = true);
        using var oddSub = odds.Subscribe(static _ => { }, () => oddCompleted = true);

        subject.OnCompleted();

        await Assert.That(evenCompleted).IsTrue();
        await Assert.That(oddCompleted).IsTrue();
    }

    /// <summary>Verifies that disposing one side stops it from receiving further emissions
    /// while the other side keeps receiving.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPartitionOneSideDisposed_ThenOnlyOtherSideReceives()
    {
        var subject = new Subject<int>();
        var (evens, odds) = subject.Partition(static x => x % Two == 0);
        var evenResults = new List<int>();
        var oddResults = new List<int>();

        var evenSub = evens.Subscribe(evenResults.Add);
        using var oddSub = odds.Subscribe(oddResults.Add);

        subject.OnNext(Two);
        evenSub.Dispose();
        subject.OnNext(Four);
        subject.OnNext(Three);

        await Assert.That(evenResults).IsCollectionEqualTo([Two]);
        await Assert.That(oddResults).IsCollectionEqualTo([Three]);
    }

    /// <summary>Verifies that the partition can be resubscribed after all sides drop —
    /// the source subscription is re-established.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenPartitionResubscribedAfterAllSidesDropped_ThenSourceRebound()
    {
        var subject = new Subject<int>();
        var (evens, _) = subject.Partition(static x => x % Two == 0);

        var firstSub = evens.Subscribe(static _ => { });
        firstSub.Dispose();

        var secondResults = new List<int>();
        using var secondSub = evens.Subscribe(secondResults.Add);

        subject.OnNext(Two);

        await Assert.That(secondResults).IsCollectionEqualTo([Two]);
    }
}
