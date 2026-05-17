// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Internal;

/// <summary>
/// Builds an <see cref="IObservable{T}"/> from a subscribe-time factory delegate. Cheaper than
/// the Rx <c>Observable.Create</c> path because there is no wrapping safe-observer or auto-detach
/// machinery — the factory is responsible for completing or erroring the observer itself.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="subscribe">Invoked per subscription; receives the observer and returns the disposal handle.</param>
internal sealed class DelegateObservable<T>(Func<IObserver<T>, IDisposable> subscribe) : IObservable<T>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentExceptionHelper.ThrowIfNull(observer);
        return subscribe(observer);
    }
}
