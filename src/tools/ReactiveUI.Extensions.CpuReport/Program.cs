// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace ReactiveUI.Extensions.CpuReport;

/// <summary>
/// Console entry point for the CPU-sample report tool. Reads a <c>.nettrace</c> file produced
/// by BenchmarkDotNet's <c>EventPipeProfiler(EventPipeProfile.CpuSampling)</c>, aggregates the
/// per-thread sample events, and emits a markdown table of the top-N methods and call stacks
/// by sampled count. Output is meant to be greppable / pastable so a CPU profile can be
/// evaluated without a GUI.
/// </summary>
internal static class Program
{
    /// <summary>Default number of rows to surface in each table.</summary>
    private const int DefaultTopN = 25;

    /// <summary>Exit code when a trace file is not found.</summary>
    private const int ExitCodeTraceFileNotFound = 2;

    /// <summary>Exit code when no CPU samples are found.</summary>
    private const int ExitCodeNoCpuSamples = 3;

    /// <summary>Exit code when usage is incorrect.</summary>
    private const int ExitCodeUsageError = 1;

    /// <summary>How deep to walk each managed call stack when bucketing.</summary>
    private const int StackDepth = 6;

    /// <summary>Multiplier for percentage calculations.</summary>
    private const double PercentMultiplier = 100.0;

    /// <summary>Approximate millisecond cost per CPU sample (EventPipe default sampling rate is 1 ms).</summary>
    private const double MillisecondsPerSample = 1.0;

    /// <summary>Threshold (in milliseconds) above which the duration formatter switches to seconds.</summary>
    private const double SecondsThresholdMs = 1000d;

    /// <summary>Provider name for the EventPipe sample-profiler events emitted by <c>EventPipeProfile.CpuSampling</c>.</summary>
    private const string SampleProfilerProvider = "Microsoft-DotNETCore-SampleProfiler";

    /// <summary>Opcode name on the sample-profiler thread-sample event.</summary>
    private const string SampleEventOpcode = "Sample";

    /// <summary>Console entry point.</summary>
    /// <param name="args">Command-line args: <c>&lt;trace.nettrace&gt; [topN]</c>.</param>
    /// <returns>0 on success; non-zero on usage / file / sampling error.</returns>
    private static int Main(string[] args)
    {
        if (args.Length is 0)
        {
            Console.Error.WriteLine("usage: rxext-cpureport <path-to-trace.nettrace> [topN]");
            return ExitCodeUsageError;
        }

        var tracePath = args[0];
        if (!File.Exists(tracePath))
        {
            Console.Error.WriteLine($"trace file not found: {tracePath}");
            return ExitCodeTraceFileNotFound;
        }

        var topN = args.Length >= 2 &&
                   int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
                   parsed > 0
            ? parsed
            : DefaultTopN;

        var stats = AggregateSamples(tracePath);
        if (stats.TotalSamples is 0)
        {
            Console.Error.WriteLine(
                "trace contained no CPU sample events. Make sure the benchmark was instrumented with " +
                "[EventPipeProfiler(EventPipeProfile.CpuSampling)].");
            return ExitCodeNoCpuSamples;
        }

        WriteHeader(tracePath, stats);
        WriteMethodTable(stats, topN);
        WriteStackTable(stats, topN);
        return 0;
    }

    /// <summary>Walks the trace file and aggregates self-time and inclusive-time per managed method, plus self-time per call stack.</summary>
    /// <param name="tracePath">Absolute path to the <c>.nettrace</c> file.</param>
    /// <returns>The aggregated stats.</returns>
    private static AggregatedStats AggregateSamples(string tracePath)
    {
        Dictionary<string, MethodStat> byMethod = new(StringComparer.Ordinal);
        Dictionary<string, StackStat> byStack = new(StringComparer.Ordinal);
        long totalSamples = 0;

        var etlxPath = TraceLog.CreateFromEventPipeDataFile(tracePath);
        try
        {
            using TraceLog traceLog = new(etlxPath);
            var source = traceLog.Events.GetSource();

            source.AllEvents += data =>
            {
                if (!string.Equals(data.ProviderName, SampleProfilerProvider, StringComparison.Ordinal)
                    || !string.Equals(data.OpcodeName, SampleEventOpcode, StringComparison.Ordinal))
                {
                    return;
                }

                var callStack = data.CallStack();
                if (callStack is null)
                {
                    return;
                }

                totalSamples++;
                AccumulateSelf(callStack, byMethod);
                AccumulateInclusive(callStack, byMethod);

                var stackKey = FormatStack(callStack, StackDepth);
                ref var stackStat = ref CollectionsMarshal.GetValueRefOrAddDefault(byStack, stackKey, out _);
                stackStat.Samples++;
                stackStat.LeafMethod ??= LeafMethodOf(callStack);
            };

            source.Process();
        }
        finally
        {
            if (!string.Equals(etlxPath, tracePath, StringComparison.OrdinalIgnoreCase) && File.Exists(etlxPath))
            {
                File.Delete(etlxPath);
            }
        }

        return new(byMethod, byStack, totalSamples);
    }

    /// <summary>Adds one self-time sample to the leaf method of <paramref name="stack"/>.</summary>
    /// <param name="stack">Sample call stack (leaf-first).</param>
    /// <param name="byMethod">Per-method accumulator map.</param>
    private static void AccumulateSelf(TraceCallStack stack, Dictionary<string, MethodStat> byMethod)
    {
        var name = stack.CodeAddress?.FullMethodName;
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        ref var stat = ref CollectionsMarshal.GetValueRefOrAddDefault(byMethod, ShortenFrame(name), out _);
        stat.SelfSamples++;
    }

    /// <summary>Adds inclusive-time samples (every distinct method in the stack gets one).</summary>
    /// <param name="stack">Sample call stack (leaf-first).</param>
    /// <param name="byMethod">Per-method accumulator map.</param>
    private static void AccumulateInclusive(TraceCallStack stack, Dictionary<string, MethodStat> byMethod)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        var current = stack;
        while (current is not null)
        {
            var raw = current.CodeAddress?.FullMethodName;
            current = current.Caller;
            if (string.IsNullOrEmpty(raw))
            {
                continue;
            }

            var name = ShortenFrame(raw);
            if (!seen.Add(name))
            {
                continue;
            }

            ref var stat = ref CollectionsMarshal.GetValueRefOrAddDefault(byMethod, name, out _);
            stat.InclusiveSamples++;
        }
    }

    /// <summary>Returns the leaf-most managed method name on <paramref name="stack"/>.</summary>
    /// <param name="stack">Sample call stack (leaf-first).</param>
    /// <returns>Shortened method name, or "&lt;unmanaged&gt;" when the leaf has no name.</returns>
    private static string LeafMethodOf(TraceCallStack stack)
    {
        var name = stack.CodeAddress?.FullMethodName;
        return string.IsNullOrEmpty(name) ? "<unmanaged>" : ShortenFrame(name);
    }

    /// <summary>Prints the markdown header + summary line.</summary>
    /// <param name="tracePath">Trace file name surfaced in the title.</param>
    /// <param name="stats">Aggregated stats whose totals are printed.</param>
    private static void WriteHeader(string tracePath, AggregatedStats stats)
    {
        Console.WriteLine($"# CPU Report -- {Path.GetFileName(tracePath)}");
        Console.WriteLine();
        Console.WriteLine($"- Total CPU sample events: **{stats.TotalSamples:N0}**");
        Console.WriteLine($"- Approx wall-time captured: **{FormatDuration(stats.TotalSamples)}**");
        Console.WriteLine(
            "- CPU sampling fires once per millisecond per running managed thread; absolute durations are an estimate, *relative* ranking between methods is accurate.");
        Console.WriteLine();
    }

    /// <summary>Prints the top-N methods table (self-time and inclusive-time columns).</summary>
    /// <param name="stats">Aggregated stats to render.</param>
    /// <param name="topN">Maximum rows to emit.</param>
    private static void WriteMethodTable(AggregatedStats stats, int topN)
    {
        Console.WriteLine($"## Top {topN} methods by self-time");
        Console.WriteLine();
        Console.WriteLine("| Method | Self samples | Self % | Inclusive samples | Inclusive % |");
        Console.WriteLine("|---|---:|---:|---:|---:|");
        foreach (var (method, stat) in stats.ByMethod.OrderByDescending(static kvp => kvp.Value.SelfSamples).Take(topN))
        {
            var selfPct = stats.TotalSamples == 0 ? 0d : stat.SelfSamples * PercentMultiplier / stats.TotalSamples;
            var inclPct = stats.TotalSamples == 0 ? 0d : stat.InclusiveSamples * PercentMultiplier / stats.TotalSamples;
            Console.WriteLine(
                $"| `{Escape(method)}` | {stat.SelfSamples:N0} | {selfPct:F1}% | {stat.InclusiveSamples:N0} | {inclPct:F1}% |");
        }

        Console.WriteLine();
    }

    /// <summary>Prints the top-N call stacks by sample count.</summary>
    /// <param name="stats">Aggregated stats to render.</param>
    /// <param name="topN">Maximum rows to emit.</param>
    private static void WriteStackTable(AggregatedStats stats, int topN)
    {
        Console.WriteLine($"## Top {topN} call stacks by sample count (depth {StackDepth})");
        Console.WriteLine();
        Console.WriteLine("| Leaf | Samples | % | Stack |");
        Console.WriteLine("|---|---:|---:|---|");
        foreach (var (stack, stat) in stats.ByStack.OrderByDescending(static kvp => kvp.Value.Samples).Take(topN))
        {
            var pct = stats.TotalSamples == 0 ? 0d : stat.Samples * PercentMultiplier / stats.TotalSamples;
            Console.WriteLine(
                $"| `{Escape(stat.LeafMethod ?? "<n/a>")}` | {stat.Samples:N0} | {pct:F1}% | {Escape(stack)} |");
        }
    }

    /// <summary>Formats <paramref name="samples"/> as an approximate wall-clock duration.</summary>
    /// <param name="samples">Sample count.</param>
    /// <returns>Human-readable "1.23 s" / "456 ms" string.</returns>
    private static string FormatDuration(long samples)
    {
        var ms = samples * MillisecondsPerSample;
        return ms >= SecondsThresholdMs
            ? $"{ms / SecondsThresholdMs:F2} s"
            : $"{ms:F0} ms";
    }

    /// <summary>Walks <paramref name="stack"/> top-down and formats it as a single line, truncated to <paramref name="depth"/> frames.</summary>
    /// <param name="stack">Stack to render.</param>
    /// <param name="depth">Maximum frame count to include.</param>
    /// <returns>The formatted stack line.</returns>
    private static string FormatStack(TraceCallStack stack, int depth)
    {
        List<string> frames = new(depth);
        var current = stack;
        while (current is not null && frames.Count < depth)
        {
            var name = current.CodeAddress?.FullMethodName;
            if (!string.IsNullOrEmpty(name))
            {
                frames.Add(ShortenFrame(name));
            }

            current = current.Caller;
        }

        return frames.Count == 0 ? "<no frames>" : string.Join(" <- ", frames);
    }

    /// <summary>Drops the parameter list from a frame name so the table stays scannable.</summary>
    /// <param name="fullName">Method name of the form <c>Type.Method(Params)</c>.</param>
    /// <returns>The shortened frame name.</returns>
    private static string ShortenFrame(string fullName)
    {
        var paren = fullName.IndexOf('(', StringComparison.Ordinal);
        return paren > 0 ? fullName[..paren] : fullName;
    }

    /// <summary>Escapes a value so it's safe to drop into a markdown table cell.</summary>
    /// <param name="value">Raw text.</param>
    /// <returns>Escaped text.</returns>
    private static string Escape(string value) => value
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

    /// <summary>Per-method sample accumulator (mutable struct held in dictionary).</summary>
    private record struct MethodStat
    {
        /// <summary>Number of samples whose leaf-most stack frame was this method.</summary>
        public long SelfSamples;

        /// <summary>Number of samples whose stack contained this method anywhere.</summary>
        public long InclusiveSamples;
    }

    /// <summary>Per-stack sample accumulator (mutable struct held in dictionary).</summary>
    private record struct StackStat
    {
        /// <summary>Number of samples that landed on this stack.</summary>
        public long Samples;

        /// <summary>Leaf-most managed method observed on this stack -- used as the row label.</summary>
        public string? LeafMethod;
    }

    /// <summary>Result of the trace pass.</summary>
    /// <param name="ByMethod">Per-method sample accumulators.</param>
    /// <param name="ByStack">Per-stack sample accumulators (stack key -> stat).</param>
    /// <param name="TotalSamples">Total number of CPU sample events observed.</param>
    private sealed record AggregatedStats(
        Dictionary<string, MethodStat> ByMethod,
        Dictionary<string, StackStat> ByStack,
        long TotalSamples);
}
