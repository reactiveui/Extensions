// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace ReactiveUI.Extensions.AllocReport;

/// <summary>
/// Console entry point for the allocation-report tool. Reads a <c>.nettrace</c> file produced by
/// BenchmarkDotNet's <c>EventPipeProfiler(EventPipeProfile.GcVerbose)</c>, aggregates
/// <c>GCAllocationTick</c> samples, and emits a markdown table of the top-N types and call stacks
/// by sampled bytes. The output is meant to be greppable / pastable so a profile can be evaluated
/// without a GUI.
/// </summary>
internal static class Program
{
    /// <summary>Default number of rows to surface in each table.</summary>
    private const int DefaultTopN = 25;

    /// <summary>Exit code when a trace file is not found.</summary>
    private const int ExitCodeTraceFileNotFound = 2;

    /// <summary>Exit code when no allocation samples are found.</summary>
    private const int ExitCodeNoAllocationSamples = 3;

    /// <summary>Exit code when usage is incorrect.</summary>
    private const int ExitCodeUsageError = 1;

    /// <summary>How deep to walk each managed call stack when bucketing.</summary>
    private const int StackDepth = 6;

    /// <summary>Bits to shift for Gigabytes.</summary>
    private const int GbShift = 30;

    /// <summary>Bits to shift for Megabytes.</summary>
    private const int MbShift = 20;

    /// <summary>Bits to shift for Kilobytes.</summary>
    private const int KbShift = 10;

    /// <summary>Multiplier for percentage calculations.</summary>
    private const double PercentMultiplier = 100.0;

    /// <summary>Console entry point.</summary>
    /// <param name="args">Command-line args: <c>&lt;trace.nettrace&gt; [topN]</c>.</param>
    /// <returns>0 on success; non-zero on usage / file / sampling error.</returns>
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: rxext-allocreport <path-to-trace.nettrace> [topN]");
            return ExitCodeUsageError;
        }

        var tracePath = args[0];
        if (!File.Exists(tracePath))
        {
            Console.Error.WriteLine($"trace file not found: {tracePath}");
            return ExitCodeTraceFileNotFound;
        }

        var topN = args.Length > 1 &&
                   int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : DefaultTopN;

        var stats = AggregateAllocations(tracePath);
        if (stats.TotalSamples == 0)
        {
            Console.Error.WriteLine(
                "trace contained no GC allocation tick events. Make sure the benchmark was instrumented with " +
                "[EventPipeProfiler(EventPipeProfile.GcVerbose)].");
            return ExitCodeNoAllocationSamples;
        }

        WriteHeader(tracePath, stats);
        WriteTypeTable(stats, topN);
        WriteStackTable(stats, topN);
        return 0;
    }

    /// <summary>Walks the trace, aggregating every <c>GCAllocationTick</c> by type name and managed call-stack key.</summary>
    /// <param name="tracePath">Absolute path to the <c>.nettrace</c> file.</param>
    /// <returns>The aggregated stats.</returns>
    private static AggregatedStats AggregateAllocations(string tracePath)
    {
        Dictionary<string, AllocStat> byType = new(StringComparer.Ordinal);
        Dictionary<string, StackStat> byStack = new(StringComparer.Ordinal);
        long totalBytes = 0;
        long totalSamples = 0;

        // TraceLog needs the .etlx indexed form to expose CallStack data.
        var etlxPath = TraceLog.CreateFromEventPipeDataFile(tracePath);
        try
        {
            using TraceLog traceLog = new(etlxPath);
            var source = traceLog.Events.GetSource();
            source.Clr.GCAllocationTick += data =>
            {
                var typeName = data.TypeName ?? "<unknown>";
                var bytes = data.AllocationAmount64 > 0 ? data.AllocationAmount64 : data.AllocationAmount;

                ref var typeStat = ref CollectionsMarshal.GetValueRefOrAddDefault(byType, typeName, out _);
                typeStat.Bytes += bytes;
                typeStat.Samples++;
                totalBytes += bytes;
                totalSamples++;

                var callStack = data.CallStack();
                if (callStack is null)
                {
                    return;
                }

                var stackKey = FormatStack(callStack, StackDepth);
                ref var stackStat = ref CollectionsMarshal.GetValueRefOrAddDefault(byStack, stackKey, out _);
                stackStat.Bytes += bytes;
                stackStat.Samples++;
                stackStat.TopType ??= typeName;
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

        return new(byType, byStack, totalBytes, totalSamples);
    }

    /// <summary>Prints the markdown header + summary line.</summary>
    /// <param name="tracePath">Trace file name surfaced in the title.</param>
    /// <param name="stats">Aggregated stats whose totals are printed.</param>
    private static void WriteHeader(string tracePath, AggregatedStats stats)
    {
        Console.WriteLine($"# Allocation Report -- {Path.GetFileName(tracePath)}");
        Console.WriteLine();
        Console.WriteLine($"- Total sampled allocation bytes: **{FormatBytes(stats.TotalBytes)}**");
        Console.WriteLine($"- Total sample events: **{stats.TotalSamples:N0}**");
        Console.WriteLine(
            "- GCAllocationTick samples one allocation per ~100 KB allocated; absolute bytes are an estimate, *relative* ranking is accurate.");
        Console.WriteLine();
    }

    /// <summary>Prints the top-N types-by-bytes table.</summary>
    /// <param name="stats">Aggregated stats to render.</param>
    /// <param name="topN">Maximum rows to emit.</param>
    private static void WriteTypeTable(AggregatedStats stats, int topN)
    {
        Console.WriteLine($"## Top {topN} types by sampled bytes");
        Console.WriteLine();
        Console.WriteLine("| Type | Sampled bytes | % | Samples |");
        Console.WriteLine("|---|---:|---:|---:|");
        foreach (var (type, stat) in stats.ByType.OrderByDescending(static kvp => kvp.Value.Bytes).Take(topN))
        {
            var pct = stats.TotalBytes == 0 ? 0d : stat.Bytes * PercentMultiplier / stats.TotalBytes;
            Console.WriteLine($"| `{Escape(type)}` | {FormatBytes(stat.Bytes)} | {pct:F1}% | {stat.Samples:N0} |");
        }

        Console.WriteLine();
    }

    /// <summary>Prints the top-N stack-by-bytes table.</summary>
    /// <param name="stats">Aggregated stats to render.</param>
    /// <param name="topN">Maximum rows to emit.</param>
    private static void WriteStackTable(AggregatedStats stats, int topN)
    {
        Console.WriteLine($"## Top {topN} call stacks by sampled bytes (depth {StackDepth})");
        Console.WriteLine();
        Console.WriteLine("| Top type | Sampled bytes | % | Samples | Stack |");
        Console.WriteLine("|---|---:|---:|---:|---|");
        foreach (var (stack, stat) in stats.ByStack.OrderByDescending(static kvp => kvp.Value.Bytes).Take(topN))
        {
            var pct = stats.TotalBytes == 0 ? 0d : stat.Bytes * PercentMultiplier / stats.TotalBytes;
            Console.WriteLine(
                $"| `{Escape(stat.TopType ?? "<n/a>")}` | {FormatBytes(stat.Bytes)} | {pct:F1}% | {stat.Samples:N0} | {Escape(stack)} |");
        }
    }

    /// <summary>Formats <paramref name="bytes"/> as a human-readable size string.</summary>
    /// <param name="bytes">Byte count.</param>
    /// <returns>Formatted "1.23 MB" / "456 B" string.</returns>
    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << GbShift => $"{bytes / (double)(1L << GbShift):F2} GB",
        >= 1L << MbShift => $"{bytes / (double)(1L << MbShift):F2} MB",
        >= 1L << KbShift => $"{bytes / (double)(1L << KbShift):F2} KB",
        _ => $"{bytes} B"
    };

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

    /// <summary>Per-type allocation accumulator (mutable struct held in dictionary).</summary>
    private record struct AllocStat
    {
        /// <summary>Total sampled bytes attributed to this type.</summary>
        public long Bytes;

        /// <summary>Number of GCAllocationTick samples that named this type.</summary>
        public long Samples;
    }

    /// <summary>Per-stack allocation accumulator (mutable struct held in dictionary).</summary>
    private record struct StackStat
    {
        /// <summary>Total sampled bytes attributed to this stack.</summary>
        public long Bytes;

        /// <summary>Number of samples that landed on this stack.</summary>
        public long Samples;

        /// <summary>The first allocated type seen on this stack -- used as a label in the report.</summary>
        public string? TopType;
    }

    /// <summary>Result of the trace pass.</summary>
    /// <param name="ByType">Per-type allocation accumulators.</param>
    /// <param name="ByStack">Per-stack allocation accumulators (stack key -> stat).</param>
    /// <param name="TotalBytes">Sum of every sampled allocation's byte count.</param>
    /// <param name="TotalSamples">Total number of GCAllocationTick samples observed.</param>
    private sealed record AggregatedStats(
        Dictionary<string, AllocStat> ByType,
        Dictionary<string, StackStat> ByStack,
        long TotalBytes,
        long TotalSamples);
}
