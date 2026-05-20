// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// A/B benchmark between <see cref="Continuation.Lock{T}"/> (<see cref="Task"/>-returning) and
/// <see cref="Continuation.LockValueTask{T}"/> (<see cref="ValueTask"/>-returning) on the
/// already-locked fast path — exercises the boxed-<c>Task.CompletedTask</c> wrapper that the Task
/// path materializes against the <c>default</c> ValueTask the new overload returns.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ContinuationLockBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int SmallInvocationCount = 1_000;

    /// <summary>High end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int LargeInvocationCount = 10_000;

    /// <summary>Pre-locked Continuation reused by every benchmark call so each invocation hits the
    /// already-locked fast path (returns immediately, no barrier work).</summary>
    private readonly Continuation _continuation = new();

    /// <summary>Gets or sets the number of Lock invocations per benchmark iteration.</summary>
    [Params(SmallInvocationCount, LargeInvocationCount)]
    public int InvocationCount { get; set; }

    /// <summary>Primes the continuation so subsequent Lock / LockValueTask calls hit the fast path.</summary>
    [GlobalSetup]
    public void Setup() => _ = _continuation.Lock<int>(0, observer: null);

    /// <summary>Drives <see cref="InvocationCount"/> <c>Lock</c> calls on the already-locked continuation.</summary>
    /// <returns>A task that completes when every call has been awaited.</returns>
    [Benchmark]
    public async Task Lock_AlreadyLockedFastPath()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            await _continuation.Lock<int>(i, observer: null).ConfigureAwait(false);
        }
    }

    /// <summary>Drives <see cref="InvocationCount"/> <c>LockValueTask</c> calls on the already-locked continuation.</summary>
    /// <returns>A task that completes when every call has been awaited.</returns>
    [Benchmark]
    public async Task LockValueTask_AlreadyLockedFastPath()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            await _continuation.LockValueTask<int>(i, observer: null).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the continuation.</summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _continuation.Dispose();
    }
}
