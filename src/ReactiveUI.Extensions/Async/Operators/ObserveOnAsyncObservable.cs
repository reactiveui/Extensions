// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// An observable that switches the notification context of a source observable to a specified async context.
/// </summary>
/// <typeparam name="T">The type of elements in the observable sequence.</typeparam>
/// <param name="source">The source observable whose notifications will be context-switched.</param>
/// <param name="asyncContext">The async context to switch notifications onto.</param>
/// <param name="forceYielding">Whether to force yielding even if already on the target context.</param>
internal sealed class ObserveOnAsyncObservable<T>(IObservableAsync<T> source, AsyncContext asyncContext, bool forceYielding) : ObservableAsync<T>
{
    /// <inheritdoc/>
    protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken)
    {
        var observeOnObserver = new ObserveOnObserver(observer, asyncContext, forceYielding);
        return await source.SubscribeAsync(observeOnObserver, cancellationToken);
    }

    /// <summary>
    /// An observer that switches each notification onto the specified async context before forwarding.
    /// </summary>
    /// <param name="observer">The downstream observer to forward notifications to.</param>
    /// <param name="asyncContext">The async context to switch onto.</param>
    /// <param name="forceYielding">Whether to force yielding even if already on the target context.</param>
    internal sealed class ObserveOnObserver(IObserverAsync<T> observer, AsyncContext asyncContext, bool forceYielding) : ObserverAsync<T>
    {
        /// <inheritdoc/>
        protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            await asyncContext.SwitchContextAsync(forceYielding, cancellationToken);
            await observer.OnNextAsync(value, cancellationToken);
        }

        /// <inheritdoc/>
        protected override async ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
        {
            await asyncContext.SwitchContextAsync(forceYielding, cancellationToken);
            await observer.OnErrorResumeAsync(error, cancellationToken);
        }

        /// <inheritdoc/>
        protected override async ValueTask OnCompletedAsyncCore(Result result)
        {
            await asyncContext.SwitchContextAsync(forceYielding, CancellationToken.None);
            await observer.OnCompletedAsync(result);
        }
    }
}
