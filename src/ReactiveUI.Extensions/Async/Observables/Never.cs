// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides static methods for creating and composing asynchronous observable sequences.
/// </summary>
/// <remarks>This class contains factory and utility methods for working with asynchronous observables. Use these
/// methods to construct, transform, or combine observable sequences in asynchronous scenarios. All members are
/// thread-safe and can be used in concurrent environments.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Creates an observable sequence that never produces any values and never completes.
    /// </summary>
    /// <remarks>This method is useful for testing or composing observables where a sequence that remains idle
    /// is required. The returned observable will not invoke any callbacks and will not signal completion or
    /// error.</remarks>
    /// <typeparam name="T">The type of elements in the observable sequence.</typeparam>
    /// <returns>An observable sequence of type <typeparamref name="T"/> that never emits any items and never terminates.</returns>
    public static ObservableAsync<T> Never<T>() => NeverObservableAsync<T>.Instance;

    private sealed class NeverObservableAsync<T> : ObservableAsync<T>
    {
        public static NeverObservableAsync<T> Instance { get; } = new();

        protected override ValueTask<IAsyncDisposable> SubscribeAsyncCore(ObserverAsync<T> observer, CancellationToken cancellationToken) =>
            new(DisposableAsync.Empty);
    }
}
