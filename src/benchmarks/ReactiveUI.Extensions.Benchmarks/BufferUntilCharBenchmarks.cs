// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-character cost of <c>BufferUntil(start, end)</c>, which buffers characters
/// between matching delimiters and emits a string on each match. Drives a fixed pattern through
/// the operator (one open-delimiter + payload + close-delimiter per cycle) so the steady-state
/// allocation profile reflects exactly one string emission per cycle.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class BufferUntilCharBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="CycleCount"/> parameter sweep.</summary>
    private const int SmallCycleCount = 100;

    /// <summary>High end of the <see cref="CycleCount"/> parameter sweep.</summary>
    private const int LargeCycleCount = 1_000;

    /// <summary>Open delimiter character.</summary>
    private const char OpenDelimiter = '[';

    /// <summary>Close delimiter character.</summary>
    private const char CloseDelimiter = ']';

    /// <summary>Payload character inside each delimited group.</summary>
    private const char PayloadChar = 'x';

    /// <summary>Source feeding the BufferUntil pipeline.</summary>
    private readonly Subject<char> _source = new();

    /// <summary>No-op sink absorbing the emitted strings.</summary>
    private readonly NoopObserver<string> _sink = new();

    /// <summary>Subscription on the BufferUntil pipeline.</summary>
    private IDisposable _subscription = null!;

    /// <summary>Gets or sets the number of open/payload/close cycles pushed per benchmark invocation.</summary>
    [Params(SmallCycleCount, LargeCycleCount)]
    public int CycleCount { get; set; }

    /// <summary>Wires the BufferUntil pipeline.</summary>
    [GlobalSetup]
    public void Setup() => _subscription = _source.BufferUntil(OpenDelimiter, CloseDelimiter).Subscribe(_sink);

    /// <summary>Tears the pipeline down.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _subscription.Dispose();
        _source.Dispose();
    }

    /// <summary>Drives <see cref="CycleCount"/> delimited groups through the BufferUntil pipeline.</summary>
    [Benchmark]
    public void BufferUntil_PerDelimitedGroup()
    {
        for (var i = 0; i < CycleCount; i++)
        {
            _source.OnNext(OpenDelimiter);
            _source.OnNext(PayloadChar);
            _source.OnNext(CloseDelimiter);
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
