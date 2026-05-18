// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Internal;
using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Executes an action when the subscription is disposed.
/// </summary>
/// <typeparam name="T">The type of elements in the source sequence.</typeparam>
/// <param name="source">The source observable.</param>
/// <param name="disposeAction">The action to execute when the subscription is disposed.</param>
internal sealed class DoOnDisposeObservable<T>(
    IObservable<T> source,
    Action disposeAction) : IObservable<T>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(source);
        InvalidOperationExceptionHelper.ThrowIfNull(disposeAction);
        ArgumentExceptionHelper.ThrowIfNull(observer);

        var subscription = source.Subscribe(observer);
        return new ActionDisposable(() =>
        {
            try
            {
                subscription.Dispose();
            }
            finally
            {
                disposeAction();
            }
        });
    }
}
