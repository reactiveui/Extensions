// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Per-emission cost of <c>OnErrorRetry</c> (backed by <c>RetryForeverObservable</c>) on the
/// happy path — no errors fire, so every emission flows straight through the retry sink to the
/// downstream observer.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class RetryForeverBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Source feeding the retry pipeline; the initial-value replay fires once during setup and does not pollute the measurement loop.</summary>
    private readonly CurrentValueSubject<int> _source = new(0);

    /// <summary>No-op terminal sink.</summary>
    private readonly NoopObserver _sink = new();

    /// <summary>Subscription on the pipeline.</summary>
    private IDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the pipeline per invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the pipeline.</summary>
    [GlobalSetup]
    public void Setup() => _subscription = _source.OnErrorRetry().Subscribe(_sink);

    /// <summary>Tears the pipeline down.</summary>
    [SuppressMessage(
        "Critical Bug",
        "S2952:Classes should \"Dispose\" of members from the classes' own \"Dispose\" methods",
        Justification = "Cleanup is the BDN sibling of Dispose(bool); Dispose() forwards into it deterministically.")]
    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription.Dispose();
        _source.Dispose();
    }

    /// <summary>Drives values through the retry pipeline without ever triggering a retry.</summary>
    [Benchmark]
    public void OnErrorRetry_HappyPath()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _source.OnNext(i);
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

    /// <summary>No-op observer used as the terminal sink.</summary>
    private sealed class NoopObserver : IObserver<int>
    {
        /// <inheritdoc/>
        public void OnNext(int value)
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
