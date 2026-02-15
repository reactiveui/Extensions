// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async.Disposables;

/// <summary>
/// Provides factory methods for creating and working with implementations of <see cref="IAsyncDisposable"/>.
/// </summary>
/// <remarks>This class offers utility members to simplify the creation of asynchronous disposables, such as
/// wrapping a delegate in an <see cref="IAsyncDisposable"/> implementation or providing a no-op disposable instance.
/// All members are thread-safe and can be used to facilitate resource management in asynchronous scenarios.</remarks>
public static class DisposableAsync
{
    /// <summary>
    /// Gets an <see cref="IAsyncDisposable"/> instance that performs no action when disposed asynchronously.
    /// </summary>
    /// <remarks>Use this property when an <see cref="IAsyncDisposable"/> is required but no disposal logic is
    /// necessary. This can be useful as a default or placeholder implementation.</remarks>
    public static IAsyncDisposable Empty { get; } = new EmptyAsyncDisposable();

    /// <summary>
    /// Creates a new asynchronous disposable object that invokes the specified delegate when disposed asynchronously.
    /// </summary>
    /// <param name="disposeAsync">A delegate that is called to perform asynchronous disposal logic when the returned object is disposed. Cannot be
    /// null.</param>
    /// <returns>An <see cref="IAsyncDisposable"/> instance that invokes the specified delegate when disposed asynchronously.</returns>
    public static IAsyncDisposable Create(Func<ValueTask> disposeAsync) => new AnonymousAsyncDisposable(disposeAsync);

    private sealed class AnonymousAsyncDisposable(Func<ValueTask> disposeAsync) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync() => Interlocked.Exchange(ref _disposed, 1) == 1 ? default : disposeAsync();
    }

    private sealed class EmptyAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => default;
    }
}
