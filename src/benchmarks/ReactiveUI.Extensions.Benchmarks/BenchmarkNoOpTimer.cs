// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Singleton no-op <see cref="ITimer"/> returned by <see cref="BenchmarkImmediateFireTimeProvider"/>;
/// both <see cref="Change"/> and <see cref="Dispose"/> are no-ops, so a single instance can be
/// reused across every <c>CreateTimer</c> call without producing allocations on the benchmark hot
/// path.
/// </summary>
internal sealed class BenchmarkNoOpTimer : ITimer
{
    /// <summary>Shared instance.</summary>
    public static readonly BenchmarkNoOpTimer Instance = new();

    /// <summary>Initializes a new instance of the <see cref="BenchmarkNoOpTimer"/> class. Private to enforce the singleton pattern.</summary>
    private BenchmarkNoOpTimer()
    {
    }

    /// <inheritdoc/>
    public bool Change(TimeSpan dueTime, TimeSpan period) => true;

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;
}
