// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Subscribe-and-dispose cost for the remaining scheduler/retry-driven sync operators that don't
/// yield meaningful per-emission measurements without real wall-clock time: the retry family
/// (<c>RetryWithBackoff</c>, <c>RetryWithDelay</c>, <c>RetryWithFixedDelay</c>,
/// <c>RetryForeverWithDelay</c>), the idle-buffer family (<c>BufferUntilIdle</c>,
/// <c>BufferUntilInactive</c>), <c>DebounceImmediate</c>, <c>ThrottleOnScheduler</c>,
/// <c>ThrottleUntilTrue</c>, <c>DetectStale</c>, and the <c>SyncTimer</c> factory. Measures the
/// per-subscription wrapper allocation; the source never errors / emits during the bench so no
/// retry or timer work fires.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class RetryAndBufferSubscribeBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int SmallInvocationCount = 100;

    /// <summary>High end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int LargeInvocationCount = 1_000;

    /// <summary>Retry cap for the retry-family benchmarks.</summary>
    private const int RetryCount = 3;

    /// <summary>Long-enough window so no scheduled work fires during the subscribe-and-dispose lifetime.</summary>
    private static readonly TimeSpan _longWindow = TimeSpan.FromSeconds(60);

    /// <summary>Static fixed-delay selector for <c>RetryWithDelay</c>; avoids per-call capture.</summary>
    private static readonly Func<int, TimeSpan> _delaySelector = static _ => _longWindow;

    /// <summary>Static always-false predicate for <c>ThrottleUntilTrue</c>.</summary>
    private static readonly Func<int, bool> _falsePredicate = static _ => false;

    /// <summary>Source feeding every pipeline.</summary>
    private readonly Subject<int> _source = new();

    /// <summary>No-op int sink.</summary>
    private readonly NoopObserver<int> _intSink = new();

    /// <summary>No-op list sink for the buffering operators.</summary>
    private readonly NoopObserver<IList<int>> _listSink = new();

    /// <summary>No-op stale sink for <c>DetectStale</c>.</summary>
    private readonly NoopObserver<Stale<int>> _staleSink = new();

    /// <summary>No-op DateTime sink for <c>SyncTimer</c>.</summary>
    private readonly NoopObserver<DateTime> _dateSink = new();

    /// <summary>Gets or sets the number of subscribe-and-dispose cycles per benchmark invocation.</summary>
    [Params(SmallInvocationCount, LargeInvocationCount)]
    public int InvocationCount { get; set; }

    /// <summary>Loops <c>RetryWithBackoff</c> subscribe → dispose (source never errors).</summary>
    [Benchmark]
    public void RetryWithBackoff_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.RetryWithBackoff(RetryCount, _longWindow).Subscribe(_intSink);
        }
    }

    /// <summary>Loops <c>RetryWithDelay</c> subscribe → dispose.</summary>
    [Benchmark]
    public void RetryWithDelay_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.RetryWithDelay(RetryCount, _delaySelector).Subscribe(_intSink);
        }
    }

    /// <summary>Loops <c>RetryWithFixedDelay</c> subscribe → dispose.</summary>
    [Benchmark]
    public void RetryWithFixedDelay_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.RetryWithFixedDelay(RetryCount, _longWindow).Subscribe(_intSink);
        }
    }

    /// <summary>Loops <c>RetryForeverWithDelay</c> subscribe → dispose.</summary>
    [Benchmark]
    public void RetryForeverWithDelay_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.RetryForeverWithDelay(_longWindow).Subscribe(_intSink);
        }
    }

    /// <summary>Loops <c>BufferUntilIdle</c> subscribe → dispose.</summary>
    [Benchmark]
    public void BufferUntilIdle_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.BufferUntilIdle(_longWindow, Scheduler.Default).Subscribe(_listSink);
        }
    }

    /// <summary>Loops <c>BufferUntilInactive</c> subscribe → dispose.</summary>
    [Benchmark]
    public void BufferUntilInactive_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.BufferUntilInactive(_longWindow, Scheduler.Default).Subscribe(_listSink);
        }
    }

    /// <summary>Loops <c>DebounceImmediate</c> subscribe → dispose.</summary>
    [Benchmark]
    public void DebounceImmediate_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.DebounceImmediate(_longWindow, Scheduler.Default).Subscribe(_intSink);
        }
    }

    /// <summary>Loops <c>ThrottleOnScheduler</c> subscribe → dispose.</summary>
    [Benchmark]
    public void ThrottleOnScheduler_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.ThrottleOnScheduler(_longWindow, Scheduler.Default).Subscribe(_intSink);
        }
    }

    /// <summary>Loops <c>ThrottleUntilTrue</c> subscribe → dispose.</summary>
    [Benchmark]
    public void ThrottleUntilTrue_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.ThrottleUntilTrue(_longWindow, _falsePredicate).Subscribe(_intSink);
        }
    }

    /// <summary>Loops <c>DetectStale</c> subscribe → dispose.</summary>
    [Benchmark]
    public void DetectStale_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = _source.DetectStale(_longWindow, Scheduler.Default).Subscribe(_staleSink);
        }
    }

    /// <summary>Loops <c>SyncTimer</c> subscribe → dispose (timer never fires within the cycle).</summary>
    [Benchmark]
    public void SyncTimer_SubscribeAndDispose()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            using var sub = ReactiveExtensions.SyncTimer(_longWindow, Scheduler.Default).Subscribe(_dateSink);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Drains synchronous teardown.</summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _source.Dispose();
    }

    /// <summary>No-op observer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class NoopObserver<T> : IObserver<T>
    {
        /// <inheritdoc/>
        public void OnNext(T value)
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }
    }
}
