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
/// Measures the per-emission cost of the parity-helper error-handling operators on the happy path
/// (no upstream errors): <c>CatchIgnore</c> and <c>CatchAndReturn</c>. Both are pass-through when
/// the source doesn't fault, so the numbers here reflect operator scaffold cost.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ParityCatchBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Fallback value used by the <c>CatchAndReturn</c> benchmark; never emitted on the happy path.</summary>
    private const int Fallback = -1;

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<int> _sink = new();

    /// <summary>Source for the <c>CatchIgnore</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _catchIgnoreSource = null!;

    /// <summary>Subscription on the <c>CatchIgnore</c> pipeline.</summary>
    private IAsyncDisposable _catchIgnoreSubscription = null!;

    /// <summary>Source for the <c>CatchAndReturn</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<int> _catchAndReturnSource = null!;

    /// <summary>Subscription on the <c>CatchAndReturn</c> pipeline.</summary>
    private IAsyncDisposable _catchAndReturnSubscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through each pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires both pipelines.</summary>
    /// <returns>A task that completes when every pipeline is subscribed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _catchIgnoreSource = new SerialStatelessSubjectAsync<int>();
        _catchIgnoreSubscription = await _catchIgnoreSource.CatchIgnore()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        _catchAndReturnSource = new SerialStatelessSubjectAsync<int>();
        _catchAndReturnSubscription = await _catchAndReturnSource.CatchAndReturn(Fallback)
            .SubscribeAsync(_sink, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipelines down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _catchIgnoreSubscription.DisposeAsync().ConfigureAwait(false);
        await _catchIgnoreSource.DisposeAsync().ConfigureAwait(false);
        await _catchAndReturnSubscription.DisposeAsync().ConfigureAwait(false);
        await _catchAndReturnSource.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives values through <c>CatchIgnore</c> (no error path triggered).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task CatchIgnore_HappyPath()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _catchIgnoreSource.OnNextAsync(i, default).ConfigureAwait(false);
        }
    }

    /// <summary>Drives values through <c>CatchAndReturn</c> (no error path triggered).</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task CatchAndReturn_HappyPath()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _catchAndReturnSource.OnNextAsync(i, default).ConfigureAwait(false);
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
