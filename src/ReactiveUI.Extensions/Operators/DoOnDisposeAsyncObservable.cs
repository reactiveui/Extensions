// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Operator that ensures an asynchronous action is executed when the source sequence completes or is disposed.
/// </summary>
/// <typeparam name="T">The type of elements in the source sequence.</typeparam>
/// <param name="source">The source observable.</param>
/// <param name="disposeAction">The asynchronous action to execute upon disposal or completion.</param>
internal sealed class DoOnDisposeAsyncObservable<T>(
    IObservable<T> source,
    Func<Task> disposeAction) : IObservable<T>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(source);
        InvalidOperationExceptionHelper.ThrowIfNull(disposeAction);
        ArgumentExceptionHelper.ThrowIfNull(observer);

        var sub = source.Subscribe(observer);
        return new ActionDisposable(() =>
        {
            try
            {
                sub.Dispose();
            }
            finally
            {
                _ = disposeAction();
            }
        });
    }
}
