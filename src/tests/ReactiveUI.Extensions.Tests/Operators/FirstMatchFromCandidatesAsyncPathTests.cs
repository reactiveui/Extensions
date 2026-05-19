// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Tests.Operators;

/// <summary>Coverage for the asynchronous-projection path of
/// <c>FirstMatchFromCandidates</c> backed by <c>FirstMatchFromCandidatesObservable</c>
/// — empty candidate list, async-projection match, async-projection no-match falls back,
/// async-projection error skips, and dispose during the async walk.</summary>
public class FirstMatchFromCandidatesAsyncPathTests
{
    /// <summary>Fallback value emitted when no candidate matches.</summary>
    private const string Fallback = "fallback";

    /// <summary>Verifies that an empty candidate list emits the fallback and completes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenCandidatesEmpty_ThenEmitsFallbackAndCompletes()
    {
        var results = new List<string>();
        var completed = false;

        using var sub = Array.Empty<string>()
            .FirstMatchFromCandidates<string, string, string>(
                static _ => Observable.Empty<string>(),
                static raw => raw,
                static value => value.Length > 0,
                Fallback)
            .Subscribe(results.Add, () => completed = true);

        await Assert.That(results).IsCollectionEqualTo([Fallback]);
        await Assert.That(completed).IsTrue();
    }

    /// <summary>Verifies that an async projection whose value matches the predicate emits the
    /// matching value and completes — exercises the <c>AsyncSink.OnNext</c> match path.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncProjectionMatches_ThenEmitsMatch()
    {
        string[] keys = ["miss", "hit"];
        var emissionGate = new Subject<string>();
        var results = new List<string>();
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = ((IReadOnlyList<string>)keys)
            .FirstMatchFromCandidates<string, string, string>(
                key => key == "hit" ? emissionGate : Observable.Empty<string>(),
                static raw => raw,
                static value => value == "hit",
                Fallback)
            .Subscribe(results.Add, () => completed.TrySetResult(true));

        emissionGate.OnNext("hit");
        emissionGate.OnCompleted();

        var done = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(done).IsTrue();
        await Assert.That(results).IsCollectionEqualTo(["hit"]);
    }

    /// <summary>Verifies that an async projection that never matches falls through to the
    /// fallback when its source completes — exercises the async <c>OnCompleted</c> path.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncProjectionNeverMatches_ThenFallback()
    {
        string[] keys = ["only"];
        var subject = new Subject<string>();
        var results = new List<string>();
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = ((IReadOnlyList<string>)keys)
            .FirstMatchFromCandidates<string, string, string>(
                _ => subject,
                static raw => raw,
                static value => value == "match-impossible",
                Fallback)
            .Subscribe(results.Add, () => completed.TrySetResult(true));

        subject.OnNext("nope");
        subject.OnCompleted();

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(results).IsCollectionEqualTo([Fallback]);
    }

    /// <summary>Verifies that an async projection error is swallowed and the walk continues
    /// to the next candidate — exercises the async <c>OnError</c> path.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncProjectionErrors_ThenSkipsToNextCandidate()
    {
        string[] keys = ["bad", "good"];
        var badSubject = new Subject<string>();
        var goodSubject = new Subject<string>();
        var results = new List<string>();
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = ((IReadOnlyList<string>)keys)
            .FirstMatchFromCandidates<string, string, string>(
                key => key == "bad" ? badSubject : goodSubject,
                static raw => raw,
                static value => value == "good",
                Fallback)
            .Subscribe(results.Add, () => completed.TrySetResult(true));

        badSubject.OnError(new InvalidOperationException("bad failed"));
        goodSubject.OnNext("good");
        goodSubject.OnCompleted();

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(results).IsCollectionEqualTo(["good"]);
    }

    /// <summary>Verifies that disposing during the async walk stops further candidate processing.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenDisposedDuringAsyncWalk_ThenStops()
    {
        string[] keys = ["k1", "k2"];
        var firstSubject = new Subject<string>();
        var results = new List<string>();
        var completed = false;

        var sub = ((IReadOnlyList<string>)keys)
            .FirstMatchFromCandidates<string, string, string>(
                _ => firstSubject,
                static raw => raw,
                static _ => true,
                Fallback)
            .Subscribe(results.Add, () => completed = true);

        sub.Dispose();
        firstSubject.OnNext("late");
        firstSubject.OnCompleted();

        await Assert.That(results).IsEmpty();
        await Assert.That(completed).IsFalse();
    }

    /// <summary>Verifies that when the synchronous transform throws for one candidate the next
    /// candidate is tried — exercises the <c>catch { continue; }</c> path in the sync fast path.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSyncTransformThrows_ThenContinuesToNextCandidate()
    {
        string[] keys = ["throw", "hit"];
        var results = new List<string>();
        var completed = false;

        using var sub = ((IReadOnlyList<string>)keys)
            .FirstMatchFromCandidates<string, string, string>(
                static key => Observable.Return(key),
                static raw => raw == "throw"
                    ? throw new InvalidOperationException("transform-throws")
                    : raw,
                static value => value == "hit",
                Fallback)
            .Subscribe(results.Add, () => completed = true);

        await Assert.That(results).IsCollectionEqualTo(["hit"]);
        await Assert.That(completed).IsTrue();
    }

    /// <summary>Verifies that a second async candidate emission arriving after a match has already
    /// fired is silently dropped via the <c>_done</c> guard in <c>AsyncSink.OnNext</c>.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncCandidateEmitsAfterMatch_ThenDroppedByDoneGuard()
    {
        string[] keys = ["hit"];
        var subject = new Subject<string>();
        var results = new List<string>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = ((IReadOnlyList<string>)keys)
            .FirstMatchFromCandidates<string, string, string>(
                _ => subject,
                static raw => raw,
                static value => value == "hit",
                Fallback)
            .Subscribe(results.Add, () => completed.TrySetResult());

        subject.OnNext("hit");
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        subject.OnNext("ignored-late");
        subject.OnError(new InvalidOperationException("ignored-late"));
        subject.OnCompleted();

        await Assert.That(results).IsCollectionEqualTo(["hit"]);
    }
}
