// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Shared, cached observable singletons for frequently emitted trivial values.
/// </summary>
public static class CachedObservables
{
    /// <summary>
    /// Gets a cached observable that synchronously emits a single <see cref="Unit.Default"/> and completes.
    /// </summary>
    public static IObservable<Unit> UnitDefault { get; } = new SingleValueObservable<Unit>(Unit.Default);
}
