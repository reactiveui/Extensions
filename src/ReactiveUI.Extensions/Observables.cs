// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Internal;

namespace ReactiveUI.Extensions;

/// <summary>
/// Factory methods that build <see cref="IObservable{T}"/> instances. Sync-side counterpart to
/// <see cref="Async.ObservableAsync"/>. The plural name avoids the resolution collision with
/// <see cref="System.Reactive.Linq.Observable"/> at call sites that import both namespaces.
/// </summary>
public static class Observables
{
    /// <summary>
    /// Returns an observable sequence that emits a single value and completes synchronously inside
    /// <see cref="IObservable{T}.Subscribe"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The value emitted to every subscriber.</param>
    /// <returns>An observable that emits <paramref name="value"/> and completes on subscribe.</returns>
    public static IObservable<T> Return<T>(T value) => new SingleValueObservable<T>(value);
}
