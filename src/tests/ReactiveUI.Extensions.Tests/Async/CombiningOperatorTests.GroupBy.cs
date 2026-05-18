// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Tests.Async;

/// <content>
/// GroupBy tests for combining operators.
/// </content>
public partial class CombiningOperatorTests
{
    /// <summary>
    /// Tests that GroupBy SubscribeAsyncCore catch block disposes and rethrows when source throws.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGroupBySourceThrowsDuringSubscription_ThenDisposesAndRethrows()
    {
        var failing = ObservableAsync.Create<int>((_, _) =>
            throw new InvalidOperationException("subscribe fail"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await failing.GroupBy(x => x % SampleValue2).ToListAsync());
    }

    /// <summary>
    /// Tests that GroupBy group observable subscriptions are tracked by the parent disposable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGroupByGroupObservableSubscribed_ThenSubscriptionIsTracked()
    {
        var subject = SubjectAsync.Create<int>();
        var evenItems = new List<int>();
        var oddItems = new List<int>();
        var innerSubs = new List<IAsyncDisposable>();

        await using var sub = await subject.Values.GroupBy(x => x % 2)
            .SubscribeAsync(
                async (group, ct) =>
                {
                    var inner = await group.SubscribeAsync(
                        (x, _) =>
                        {
                            if (group.Key == 0)
                            {
                                evenItems.Add(x);
                            }
                            else
                            {
                                oddItems.Add(x);
                            }

                            return default;
                        },
                        null,
                        null,
                        ct);
                    innerSubs.Add(inner);
                },
                null);

        await subject.OnNextAsync(1, CancellationToken.None);
        await subject.OnNextAsync(SampleValue2, CancellationToken.None);
        await subject.OnNextAsync(SampleValue3, CancellationToken.None);
        await subject.OnNextAsync(SampleValue4, CancellationToken.None);
        await subject.OnCompletedAsync(Result.Success);

        await Assert.That(oddItems).Contains(1);
        await Assert.That(oddItems).Contains(SampleValue3);
        await Assert.That(evenItems).Contains(SampleValue2);
        await Assert.That(evenItems).Contains(SampleValue4);

        // Dispose inner subscriptions
        foreach (var innerSub in innerSubs)
        {
            await innerSub.DisposeAsync();
        }
    }
}
