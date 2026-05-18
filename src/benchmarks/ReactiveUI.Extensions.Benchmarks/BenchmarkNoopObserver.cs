// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Shared no-op asynchronous observer used as the terminal sink in benchmarks. Every method returns a
/// completed <see cref="ValueTask"/> so the synchronous fast paths in the production code can be measured
/// without scheduler / continuation noise.
/// </summary>
/// <typeparam name="T">The element type the sink consumes.</typeparam>
internal sealed class BenchmarkNoopObserver<T> : IObserverAsync<T>
{
    /// <inheritdoc/>
    public ValueTask OnNextAsync(T value, CancellationToken cancellationToken) => default;

    /// <inheritdoc/>
    public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken) => default;

    /// <inheritdoc/>
    public ValueTask OnCompletedAsync(Result result) => default;

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;
}
