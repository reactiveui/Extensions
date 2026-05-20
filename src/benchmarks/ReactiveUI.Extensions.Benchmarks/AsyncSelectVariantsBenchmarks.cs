// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Per-emission cost of the async projection operators — <c>SelectAsyncSequential</c>,
/// <c>SelectLatestAsync</c>, <c>SelectAsyncConcurrent</c>, and the two <c>SelectAsync</c> overloads
/// (which delegate to the sequential observable). Each variant runs an
/// <c>Func&lt;TSource, Task&lt;TResult&gt;&gt;</c> projection that resolves against a cached completed
/// task so the benchmark captures the operator's per-emission overhead rather than I/O.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class AsyncSelectVariantsBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 100;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 1_000;

    /// <summary>Concurrency level passed to <c>SelectAsyncConcurrent</c>.</summary>
    private const int Concurrency = 4;

    /// <summary>Pre-completed task reused by every projection.</summary>
    private static readonly Task<int> _completedResult = Task.FromResult(0);

    /// <summary>Source for the sequential pipeline.</summary>
    private readonly Subject<int> _sequentialSource = new();

    /// <summary>Source for the latest pipeline.</summary>
    private readonly Subject<int> _latestSource = new();

    /// <summary>Source for the concurrent pipeline.</summary>
    private readonly Subject<int> _concurrentSource = new();

    /// <summary>Source for the <c>SelectAsync(Func&lt;T,Task&gt;)</c> pipeline.</summary>
    private readonly Subject<int> _selectAsyncSource = new();

    /// <summary>Source for the <c>SelectAsync(Func&lt;T,CancellationToken,Task&gt;)</c> pipeline.</summary>
    private readonly Subject<int> _selectAsyncCtSource = new();

    /// <summary>Reused sink.</summary>
    private readonly NoopObserver<int> _sink = new();

    /// <summary>Subscription on the sequential pipeline.</summary>
    private IDisposable _sequentialSubscription = null!;

    /// <summary>Subscription on the latest pipeline.</summary>
    private IDisposable _latestSubscription = null!;

    /// <summary>Subscription on the concurrent pipeline.</summary>
    private IDisposable _concurrentSubscription = null!;

    /// <summary>Subscription on the <c>SelectAsync(Func&lt;T,Task&gt;)</c> pipeline.</summary>
    private IDisposable _selectAsyncSubscription = null!;

    /// <summary>Subscription on the <c>SelectAsync(Func&lt;T,CancellationToken,Task&gt;)</c> pipeline.</summary>
    private IDisposable _selectAsyncCtSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed per invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires every pipeline.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _sequentialSubscription = _sequentialSource.SelectAsyncSequential(static _ => _completedResult).Subscribe(_sink);
        _latestSubscription = _latestSource.SelectLatestAsync(static _ => _completedResult).Subscribe(_sink);
        _concurrentSubscription = _concurrentSource
            .SelectAsyncConcurrent(static _ => _completedResult, Concurrency)
            .Subscribe(_sink);
        _selectAsyncSubscription = _selectAsyncSource.SelectAsync(static _ => _completedResult).Subscribe(_sink);
        _selectAsyncCtSubscription = _selectAsyncCtSource.SelectAsync(static (_, _) => _completedResult).Subscribe(_sink);
    }

    /// <summary>Tears every pipeline down.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _sequentialSubscription.Dispose();
        _latestSubscription.Dispose();
        _concurrentSubscription.Dispose();
        _selectAsyncSubscription.Dispose();
        _selectAsyncCtSubscription.Dispose();
        _sequentialSource.Dispose();
        _latestSource.Dispose();
        _concurrentSource.Dispose();
        _selectAsyncSource.Dispose();
        _selectAsyncCtSource.Dispose();
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through SelectAsyncSequential.</summary>
    [Benchmark]
    public void SelectAsyncSequential_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _sequentialSource.OnNext(i);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through SelectLatestAsync.</summary>
    [Benchmark]
    public void SelectLatestAsync_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _latestSource.OnNext(i);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through SelectAsyncConcurrent.</summary>
    [Benchmark]
    public void SelectAsyncConcurrent_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _concurrentSource.OnNext(i);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through <c>SelectAsync(Func&lt;T,Task&gt;)</c>.</summary>
    [Benchmark]
    public void SelectAsync_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _selectAsyncSource.OnNext(i);
        }
    }

    /// <summary>Drives <see cref="EmissionCount"/> values through <c>SelectAsync(Func&lt;T,CancellationToken,Task&gt;)</c>.</summary>
    [Benchmark]
    public void SelectAsyncWithToken_PerEmission()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _selectAsyncCtSource.OnNext(i);
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

        Cleanup();
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
