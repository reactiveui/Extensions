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
/// Measures the steady-state per-emission cost of <c>TakeUntil</c> with the three most common
/// stop-signal shapes: an observable, a sync predicate, and a <see cref="Task"/>. Stop signals
/// never fire during the benchmark body so the timings reflect the operator's per-emission
/// overhead, not its termination path.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class TakeUntilBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Source feeding the <c>TakeUntil(IObservableAsync)</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _observableSource = null!;

    /// <summary>Stop-signal observable that never fires during the benchmark body.</summary>
    private SerialStatelessSubjectAsync<int> _observableStop = null!;

    /// <summary>Subscription on the observable-stop pipeline.</summary>
    private IAsyncDisposable _observableSubscription = null!;

    /// <summary>Source feeding the <c>TakeUntil(predicate)</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _predicateSource = null!;

    /// <summary>Subscription on the predicate-stop pipeline.</summary>
    private IAsyncDisposable _predicateSubscription = null!;

    /// <summary>Source feeding the <c>TakeUntil(Task)</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _taskSource = null!;

    /// <summary>Subscription on the task-stop pipeline.</summary>
    private IAsyncDisposable _taskSubscription = null!;

    /// <summary>Cancellation source backing the never-completing stop task.</summary>
    private CancellationTokenSource _stopTaskCts = null!;

    /// <summary>Stop task that stays pending for the duration of the benchmark.</summary>
    private Task _stopTask = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires every pipeline against a stop-signal that never fires during the benchmark body.</summary>
    /// <returns>A task that completes when every pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _observableSource = new SerialStatelessSubjectAsync<int>();
        _observableStop = new SerialStatelessSubjectAsync<int>();
        _observableSubscription = await _observableSource.TakeUntil(_observableStop)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _predicateSource = new SerialStatelessSubjectAsync<int>();
        _predicateSubscription = await _predicateSource.TakeUntil(static _ => false)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _taskSource = new SerialStatelessSubjectAsync<int>();
        _stopTaskCts = new CancellationTokenSource();
        _stopTask = Task.Delay(Timeout.Infinite, _stopTaskCts.Token);
        _taskSubscription = await _taskSource.TakeUntil(_stopTask)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears every pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [SuppressMessage(
        "Critical Bug",
        "S2952:Classes should \"Dispose\" of members from the classes' own \"Dispose\" methods",
        Justification = "CleanupAsync is the async sibling of Dispose(bool) for this benchmark; Dispose() forwards into it deterministically.")]
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _observableSubscription.DisposeAsync().ConfigureAwait(false);
        await _observableSource.DisposeAsync().ConfigureAwait(false);
        await _observableStop.DisposeAsync().ConfigureAwait(false);

        await _predicateSubscription.DisposeAsync().ConfigureAwait(false);
        await _predicateSource.DisposeAsync().ConfigureAwait(false);

        await _taskSubscription.DisposeAsync().ConfigureAwait(false);
        await _taskSource.DisposeAsync().ConfigureAwait(false);
        await _stopTaskCts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _stopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected — the stop task was cancelled in teardown.
        }

        _stopTaskCts.Dispose();
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives values through a <c>TakeUntil(IObservableAsync)</c> pipeline whose stop signal never fires.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task TakeUntil_Observable_StopNeverFires()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _observableSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives values through a <c>TakeUntil(predicate)</c> pipeline whose predicate always returns <see langword="false"/>.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task TakeUntil_Predicate_NeverMatches()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _predicateSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives values through a <c>TakeUntil(Task)</c> pipeline whose stop task remains pending.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task TakeUntil_Task_StaysPending()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _taskSource.OnNextAsync(i, default).ConfigureAwait(false);
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

        CleanupAsync().GetAwaiter().GetResult();
    }
}
