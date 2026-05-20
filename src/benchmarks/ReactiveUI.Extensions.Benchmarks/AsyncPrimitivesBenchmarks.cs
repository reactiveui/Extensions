// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the cost of the small public async primitives that have no other benchmark home:
/// <c>Result.Failure</c>, the three <c>AsyncContext.From</c> overloads and <c>GetCurrent</c>,
/// <c>IsSameAsCurrentAsyncContext</c>, <c>Optional.TryGetValue</c>, <c>DisposableAsyncSlot.IsDisposed</c>,
/// <c>UnhandledExceptionHandler.Register</c>, <c>AsyncContext.SwitchContextAsync</c>, and
/// <c>Result.TryThrow</c>. These are construction / inspection helpers, so the numbers are floors that
/// guard against accidental allocation creeping into the hot primitives.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "BenchmarkDotNet drives benchmarks through an instance; the methods cannot be static.")]
public class AsyncPrimitivesBenchmarks
{
    /// <summary>Cached error so <see cref="ResultFailure_Construct"/> measures the wrap, not the throw.</summary>
    private static readonly InvalidOperationException Error = new("benchmark");

    /// <summary>Cached synchronization context for the <c>From(SynchronizationContext)</c> overload.</summary>
    private static readonly SynchronizationContext SyncContext = new();

    /// <summary>Cached async context for the <c>IsSameAsCurrentAsyncContext</c> check.</summary>
    private static readonly AsyncContext Context = AsyncContext.From(TaskScheduler.Default);

    /// <summary>Cached populated optional for the <c>TryGetValue</c> benchmark.</summary>
    private static readonly Optional<int> SomeValue = new(42);

    /// <summary>Cached non-sentinel disposable for the <c>IsDisposed</c> benchmark.</summary>
    private static readonly IAsyncDisposable LiveSlot = DisposableAsync.Empty;

    /// <summary>Constructs a failure <see cref="Result"/> from a cached exception.</summary>
    /// <returns>The failure result.</returns>
    [Benchmark]
    public Result ResultFailure_Construct() => Result.Failure(Error);

    /// <summary>Builds an <see cref="AsyncContext"/> from a <see cref="SynchronizationContext"/>.</summary>
    /// <returns>The constructed context.</returns>
    [Benchmark]
    public AsyncContext AsyncContextFrom_SynchronizationContext() => AsyncContext.From(SyncContext);

    /// <summary>Builds an <see cref="AsyncContext"/> from a <see cref="TaskScheduler"/>.</summary>
    /// <returns>The constructed context.</returns>
    [Benchmark]
    public AsyncContext AsyncContextFrom_TaskScheduler() => AsyncContext.From(TaskScheduler.Default);

    /// <summary>Builds an <see cref="AsyncContext"/> from an <see cref="IScheduler"/>.</summary>
    /// <returns>The constructed context.</returns>
    [Benchmark]
    public AsyncContext AsyncContextFrom_Scheduler() => AsyncContext.From(Scheduler.Default);

    /// <summary>Captures the current <see cref="AsyncContext"/>.</summary>
    /// <returns>The current context.</returns>
    [Benchmark]
    public AsyncContext AsyncContext_GetCurrent() => AsyncContext.GetCurrent();

    /// <summary>Compares a cached context against the current async context.</summary>
    /// <returns><see langword="true"/> if they match.</returns>
    [Benchmark]
    public bool IsSameAsCurrentAsyncContext_Check() => Context.IsSameAsCurrentAsyncContext();

    /// <summary>Reads the value out of a populated <see cref="Optional{T}"/>.</summary>
    /// <returns><see langword="true"/> when a value is present.</returns>
    [Benchmark]
    public bool OptionalTryGetValue_Some() => SomeValue.TryGetValue(out _);

    /// <summary>Inspects a live (non-disposed) slot via <c>DisposableAsyncSlot.IsDisposed</c>.</summary>
    /// <returns><see langword="true"/> if the slot holds the disposed sentinel.</returns>
    [Benchmark]
    public bool DisposableAsyncSlotIsDisposed_Live() => DisposableAsyncSlot.IsDisposed(LiveSlot);

    /// <summary>Registers a no-op unhandled-exception handler (replaces the global handler).</summary>
    [Benchmark]
    public void UnhandledExceptionHandler_Register() => UnhandledExceptionHandler.Register(static _ => { });

    /// <summary>Switches onto a cached async context (no forced yielding) and awaits the awaitable.</summary>
    /// <returns>A task that completes when the context switch resolves.</returns>
    [Benchmark]
    public async Task SwitchContextAsync_NoYield() =>
        await Context.SwitchContextAsync(false, CancellationToken.None);

    /// <summary>Invokes <c>TryThrow</c> on a success result (the no-throw fast path).</summary>
    [Benchmark]
    public void ResultTryThrow_Success() => Result.Success.TryThrow();
}
