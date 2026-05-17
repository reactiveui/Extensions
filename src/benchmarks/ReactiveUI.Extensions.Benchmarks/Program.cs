// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Running;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>BenchmarkDotNet runner entry point.</summary>
public static class Program
{
    /// <summary>Runs the benchmark switcher across every benchmark class in this assembly.</summary>
    /// <param name="args">Command-line arguments forwarded to BenchmarkDotNet (filters, exporters, etc.).</param>
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
