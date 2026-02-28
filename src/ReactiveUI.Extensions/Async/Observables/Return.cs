// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides factory methods for creating asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class contains static methods for constructing and manipulating asynchronous
/// observables. Use these methods to create observables that emit values asynchronously, supporting scenarios such as
/// background processing or integration with asynchronous workflows.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Creates an observable sequence that emits a single value and then completes.
    /// </summary>
    /// <remarks>The returned observable sequence emits the value asynchronously and completes immediately
    /// after. This method is useful for creating simple observable sequences for testing or composing with other
    /// observables.</remarks>
    /// <typeparam name="T">The type of the value to be emitted by the observable sequence.</typeparam>
    /// <param name="value">The value to be emitted by the observable sequence.</param>
    /// <returns>An observable sequence that emits the specified value and then signals completion.</returns>
    public static IObservableAsync<T> Return<T>(T value) => CreateAsBackgroundJob<T>(
            async (obs, token) =>
        {
            await obs.OnNextAsync(value, token);
            await obs.OnCompletedAsync(Result.Success);
        },
            true);
}
