// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Per-call allocation profile for the public async mixin helpers:
/// <see cref="SubjectMixins.AsObserverAsync{T}"/>, <see cref="SubjectMixins.MapValues{T}"/>,
/// and <see cref="DisposableAsyncMixins.ToDisposableAsync"/>. Each is a thin factory; the bench
/// loops them to surface the per-construction cost.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class AsyncMixinsBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int SmallInvocationCount = 1_000;

    /// <summary>High end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int LargeInvocationCount = 10_000;

    /// <summary>Identity mapper used for <c>MapValues</c>; static so no per-call capture allocation.</summary>
    private static readonly Func<IObservableAsync<int>, IObservableAsync<int>> _identityMapper = static x => x;

    /// <summary>No-op disposable reused by every <c>ToDisposableAsync</c> bench call.</summary>
    private readonly NoopDisposable _disposable = new();

    /// <summary>Subject under test for the <c>AsObserverAsync</c> / <c>MapValues</c> calls.</summary>
    private SerialStatelessSubjectAsync<int> _subject = null!;

    /// <summary>Gets or sets the number of invocations per benchmark iteration.</summary>
    [Params(SmallInvocationCount, LargeInvocationCount)]
    public int InvocationCount { get; set; }

    /// <summary>Constructs the subject reused across mixin calls.</summary>
    [GlobalSetup]
    public void Setup() => _subject = new SerialStatelessSubjectAsync<int>();

    /// <summary>Tears the subject down asynchronously.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync() => await _subject.DisposeAsync().ConfigureAwait(false);

    /// <summary>Loops <see cref="SubjectMixins.AsObserverAsync{T}"/> over the same subject.</summary>
    [Benchmark]
    public void AsObserverAsync_PerCall()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            _ = _subject.AsObserverAsync();
        }
    }

    /// <summary>Loops <see cref="SubjectMixins.MapValues{T}"/> with a static identity mapper.</summary>
    [Benchmark]
    public void MapValues_PerCall()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            _ = _subject.MapValues(_identityMapper);
        }
    }

    /// <summary>Loops <see cref="DisposableAsyncMixins.ToDisposableAsync"/> over a no-op disposable.</summary>
    [Benchmark]
    public void ToDisposableAsync_PerCall()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            _ = _disposable.ToDisposableAsync();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Drains async teardown so <see cref="IDisposable.Dispose"/> can return synchronously.</summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
    [SuppressMessage(
        "Major Bug",
        "S4462:Calls to async methods should not be blocking",
        Justification = "IDisposable.Dispose is synchronous by contract; benchmark teardown must wait for async cleanup before returning.")]
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _disposable.Dispose();
        CleanupAsync().GetAwaiter().GetResult();
    }

    /// <summary>No-op <see cref="IDisposable"/> reused across <c>ToDisposableAsync</c> bench calls.</summary>
    private sealed class NoopDisposable : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
