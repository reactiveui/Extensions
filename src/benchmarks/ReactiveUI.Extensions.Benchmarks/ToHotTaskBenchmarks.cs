// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Cost of <c>ToHotTask</c> (backed by <c>FirstAsTaskHelper.FirstAsTask</c>) — each iteration
/// subscribes to a fresh-ish source, the source synchronously emits one value, the task resolves.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ToHotTaskBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="ResolveCount"/> parameter sweep.</summary>
    private const int SmallResolveCount = 1_000;

    /// <summary>High end of the <see cref="ResolveCount"/> parameter sweep.</summary>
    private const int LargeResolveCount = 10_000;

    /// <summary>The value resolved on every iteration.</summary>
    private const int ResolvedValue = 42;

    /// <summary>Pre-built single-value observable; <see cref="FirstAsTaskHelper"/> subscribes anew each call.</summary>
    private readonly SingleValueObservable<int> _source = new(ResolvedValue);

    /// <summary>Gets or sets the number of subscribe + resolve cycles per benchmark invocation.</summary>
    [Params(SmallResolveCount, LargeResolveCount)]
    public int ResolveCount { get; set; }

    /// <summary>Resolves the task <see cref="ResolveCount"/> times in a row.</summary>
    [Benchmark]
    public void ToHotTask_ResolveFirstValue()
    {
        for (var i = 0; i < ResolveCount; i++)
        {
            _ = _source.ToHotTask();
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
    }
}
