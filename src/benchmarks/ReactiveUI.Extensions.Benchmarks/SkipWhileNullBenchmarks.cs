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
/// Measures the per-emission cost of <c>SkipWhileNull</c> after the gate has opened (a non-null
/// value has been seen during <see cref="SetupAsync"/>). Every benchmark-body emission therefore
/// flows through the operator. Currently implemented as <c>SkipWhile(null).Select(value!)</c> —
/// a fusion candidate analogous to <c>WhereIsNotNull</c>.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SkipWhileNullBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Shared non-null reference reused across every emission.</summary>
    private const string SharedNonNull = "x";

    /// <summary>Shared no-op sink so no allocations leak in from the benchmark itself.</summary>
    private readonly BenchmarkNoopObserver<string> _sink = new();

    /// <summary>Source feeding the <c>SkipWhileNull</c> pipeline.</summary>
    private SerialStatelessSubjectAsync<string?> _source = null!;

    /// <summary>Subscription on the <c>SkipWhileNull</c> pipeline.</summary>
    private IAsyncDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the pipeline and primes the gate so the benchmark body measures only the steady-state pass-through.</summary>
    /// <returns>A task that completes when the pipeline is subscribed and primed.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new SerialStatelessSubjectAsync<string?>();
        _subscription = await _source.SkipWhileNull()
            .SubscribeAsync(_sink, default).ConfigureAwait(false);

        // Push one non-null value so the SkipWhile gate latches off; subsequent emissions pass through.
        await _source.OnNextAsync(SharedNonNull, default).ConfigureAwait(false);
    }

    /// <summary>Tears the pipeline down.</summary>
    /// <returns>A task that completes when teardown is done.</returns>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _subscription.DisposeAsync().ConfigureAwait(false);
        await _source.DisposeAsync().ConfigureAwait(false);
        await _sink.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Drives non-null references through the post-prime pipeline.</summary>
    /// <returns>A task that completes when every value has been propagated.</returns>
    [Benchmark]
    public async Task SkipWhileNull_PostPrime()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            await _source.OnNextAsync(SharedNonNull, default).ConfigureAwait(false);
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
