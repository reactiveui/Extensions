// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-emission cost of the sync <see cref="ReactiveExtensions.SkipWhileNull{T}"/>
/// after the gate has opened (a non-null value primes the latch during setup). Every benchmark-body
/// emission therefore flows through the operator. Previously composed via System.Reactive's
/// <c>SkipWhile(x =&gt; x == null)</c>; replaced with a typed <c>SkipWhileNullObservable&lt;T&gt;</c>.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class SyncSkipWhileNullBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Shared non-null reference reused across every emission.</summary>
    private const string SharedNonNull = "x";

    /// <summary>Source feeding the pipeline; declared as the non-nullable element type because <c>SkipWhileNull</c>
    /// is constrained on a reference-type generic.</summary>
    private readonly CurrentValueSubject<string> _source = new(SharedNonNull);

    /// <summary>Sync no-op observer reused as the downstream sink.</summary>
    private readonly NoopObserver _sink = new();

    /// <summary>Subscription on the pipeline.</summary>
    private IDisposable _subscription = null!;

    /// <summary>Gets or sets the number of emissions pushed through the pipeline per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Wires the pipeline and primes the gate by emitting one non-null value.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _subscription = _source.SkipWhileNull().Subscribe(_sink);
        _source.OnNext(SharedNonNull);
    }

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

    /// <summary>Drives a shared non-null reference through <c>SkipWhileNull</c>; every value passes.</summary>
    [Benchmark]
    public void SkipWhileNull_PostLatch()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _source.OnNext(SharedNonNull);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Drains the synchronous teardown.</summary>
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
    private sealed class NoopObserver : IObserver<string>
    {
        /// <inheritdoc/>
        public void OnNext(string value)
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
