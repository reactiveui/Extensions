// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides factory methods for creating asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class contains static methods for constructing instances of asynchronous
/// observables. Use these methods to create observable sequences that support asynchronous notification
/// patterns.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Creates an observable sequence that completes immediately without emitting any items.
    /// </summary>
    /// <remarks>This method is useful for representing an empty sequence in asynchronous or reactive
    /// scenarios. The returned sequence signals completion to observers as soon as it is subscribed to.</remarks>
    /// <typeparam name="T">The type of elements in the observable sequence.</typeparam>
    /// <returns>An observable sequence of type <typeparamref name="T"/> that completes immediately without producing any values.</returns>
    public static IObservableAsync<T> Empty<T>() => Create<T>(async (observer, _) =>
                                                        {
                                                            await observer.OnCompletedAsync(Result.Success);
                                                            return DisposableAsync.Empty;
                                                        });
}
