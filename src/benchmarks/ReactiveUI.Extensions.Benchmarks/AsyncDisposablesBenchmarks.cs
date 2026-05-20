// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Async.Disposables;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Steady-state cost of the three async disposable primitives:
/// <see cref="CompositeDisposableAsync"/>, <see cref="SerialDisposableAsync"/>,
/// <see cref="SingleAssignmentDisposableAsync"/>. Each benchmark drives the primitive's
/// canonical add / set / clear / dispose flow over a no-op child disposable; the runs lock
/// in the per-operation overhead and surface any unexpected per-call allocations. Also covers
/// the composite's read-side inspection surface (<c>Contains</c> / <c>CopyTo</c> /
/// <c>GetEnumerator</c> / <c>Clear</c>) and the single-assignment <c>GetDisposable</c> accessor.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class AsyncDisposablesBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="OperationCount"/> parameter sweep.</summary>
    private const int SmallOperationCount = 100;

    /// <summary>High end of the <see cref="OperationCount"/> parameter sweep.</summary>
    private const int LargeOperationCount = 1_000;

    /// <summary>Reusable no-op child disposable used by every set/add operation.</summary>
    private static readonly NoopAsyncDisposable _child = new();

    /// <summary>Distinct children populating the composite for the read-side inspection benchmarks.</summary>
    private static readonly NoopAsyncDisposable[] _children = [new(), new(), new(), new()];

    /// <summary>Pre-populated composite for the read-only inspection benchmarks.</summary>
    private CompositeDisposableAsync _populatedComposite = null!;

    /// <summary>Composite reused by the Add → Clear benchmark.</summary>
    private CompositeDisposableAsync _clearComposite = null!;

    /// <summary>Single-assignment with an assigned disposable for the <c>GetDisposable</c> benchmark.</summary>
    private SingleAssignmentDisposableAsync _populatedSingle = null!;

    /// <summary>Reused destination buffer for the <c>CopyTo</c> benchmark.</summary>
    private IAsyncDisposable[] _copyToBuffer = null!;

    /// <summary>Gets or sets the number of operations per benchmark invocation.</summary>
    [Params(SmallOperationCount, LargeOperationCount)]
    public int OperationCount { get; set; }

    /// <summary>Builds the pre-populated instances used by the inspection benchmarks.</summary>
    /// <returns>A task that completes once every instance is populated.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _populatedComposite = new CompositeDisposableAsync();
        for (var i = 0; i < _children.Length; i++)
        {
            await _populatedComposite.AddAsync(_children[i]).ConfigureAwait(false);
        }

        _clearComposite = new CompositeDisposableAsync();
        _populatedSingle = new SingleAssignmentDisposableAsync();
        await _populatedSingle.SetDisposableAsync(_child).ConfigureAwait(false);
        _copyToBuffer = new IAsyncDisposable[_children.Length];
    }

    /// <summary>Disposes the pre-populated instances.</summary>
    /// <returns>A task that completes once teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _populatedComposite.DisposeAsync().ConfigureAwait(false);
        await _clearComposite.DisposeAsync().ConfigureAwait(false);
        await _populatedSingle.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Loops <see cref="OperationCount"/> Add → Dispose cycles on fresh <see cref="CompositeDisposableAsync"/> instances.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task CompositeDisposable_AddAndDispose()
    {
        for (var i = 0; i < OperationCount; i++)
        {
            var composite = new CompositeDisposableAsync();
            await composite.AddAsync(_child).ConfigureAwait(false);
            await composite.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Drives Add → Remove → Dispose cycles on a long-lived composite.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task CompositeDisposable_AddRemoveSteadyState()
    {
        var composite = new CompositeDisposableAsync();
        for (var i = 0; i < OperationCount; i++)
        {
            await composite.AddAsync(_child).ConfigureAwait(false);
            _ = await composite.Remove(_child).ConfigureAwait(false);
        }

        await composite.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Loops <see cref="OperationCount"/> Set → Dispose cycles on fresh <see cref="SerialDisposableAsync"/> instances.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task SerialDisposable_SetAndDispose()
    {
        for (var i = 0; i < OperationCount; i++)
        {
            var serial = new SerialDisposableAsync();
            await serial.SetDisposableAsync(_child).ConfigureAwait(false);
            await serial.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Drives Set → Set → Set cycles on a long-lived serial (each set disposes the prior).</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task SerialDisposable_SwapSteadyState()
    {
        var serial = new SerialDisposableAsync();
        for (var i = 0; i < OperationCount; i++)
        {
            await serial.SetDisposableAsync(_child).ConfigureAwait(false);
        }

        await serial.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Loops <see cref="OperationCount"/> Set → Dispose cycles on fresh <see cref="SingleAssignmentDisposableAsync"/> instances.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task SingleAssignmentDisposable_SetAndDispose()
    {
        for (var i = 0; i < OperationCount; i++)
        {
            var single = new SingleAssignmentDisposableAsync();
            await single.SetDisposableAsync(_child).ConfigureAwait(false);
            await single.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Zero-wrapper equivalent of <c>SerialDisposable_SetAndDispose</c> using
    /// <see cref="DisposableAsyncSlot"/> against a caller-owned <see cref="IAsyncDisposable"/> field.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task Slot_SwapAndDispose()
    {
        IAsyncDisposable? slot = null;
        for (var i = 0; i < OperationCount; i++)
        {
            slot = null;
            await DisposableAsyncSlot.SwapAsync(ref slot, _child).ConfigureAwait(false);
            await DisposableAsyncSlot.DisposeAsync(ref slot).ConfigureAwait(false);
        }
    }

    /// <summary>Zero-wrapper equivalent of <c>SingleAssignmentDisposable_SetAndDispose</c>.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task Slot_AssignAndDispose()
    {
        IAsyncDisposable? slot = null;
        for (var i = 0; i < OperationCount; i++)
        {
            slot = null;
            await DisposableAsyncSlot.AssignAsync(ref slot, _child).ConfigureAwait(false);
            await DisposableAsyncSlot.DisposeAsync(ref slot).ConfigureAwait(false);
        }
    }

    /// <summary>Loops <see cref="OperationCount"/> <c>Contains</c> hits against the populated composite.</summary>
    [Benchmark]
    public void CompositeDisposable_Contains()
    {
        for (var i = 0; i < OperationCount; i++)
        {
            _ = _populatedComposite.Contains(_children[0]);
        }
    }

    /// <summary>Loops <see cref="OperationCount"/> <c>CopyTo</c> calls into a reused buffer.</summary>
    [Benchmark]
    public void CompositeDisposable_CopyTo()
    {
        for (var i = 0; i < OperationCount; i++)
        {
            _populatedComposite.CopyTo(_copyToBuffer, 0);
        }
    }

    /// <summary>Loops <see cref="OperationCount"/> snapshot enumerations of the populated composite.</summary>
    [Benchmark]
    public void CompositeDisposable_Enumerate()
    {
        for (var i = 0; i < OperationCount; i++)
        {
            foreach (var item in _populatedComposite)
            {
                _ = item;
            }
        }
    }

    /// <summary>Loops <see cref="OperationCount"/> Add → Clear cycles on a reused composite.</summary>
    /// <returns>A task that completes when every cycle has finished.</returns>
    [Benchmark]
    public async Task CompositeDisposable_AddAndClear()
    {
        for (var i = 0; i < OperationCount; i++)
        {
            await _clearComposite.AddAsync(_child).ConfigureAwait(false);
            await _clearComposite.Clear().ConfigureAwait(false);
        }
    }

    /// <summary>Loops <see cref="OperationCount"/> <c>GetDisposable</c> reads on an assigned single-assignment.</summary>
    [Benchmark]
    public void SingleAssignmentDisposable_GetDisposable()
    {
        for (var i = 0; i < OperationCount; i++)
        {
            _ = _populatedSingle.GetDisposable();
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

    /// <summary>No-op async disposable shared across every benchmark.</summary>
    [SuppressMessage(
        "Critical Code Smell",
        "S1186:Methods should not be empty",
        Justification = "Empty no-op is the benchmark's whole point — we want zero work in DisposeAsync.")]
    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        /// <inheritdoc/>
        public ValueTask DisposeAsync() => default;
    }
}
