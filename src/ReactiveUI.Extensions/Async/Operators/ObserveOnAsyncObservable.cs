// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

internal sealed class ObserveOnAsyncObservable<T>(ObservableAsync<T> source, AsyncContext asyncContext, bool forceYielding) : ObservableAsync<T>
{
    protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(ObserverAsync<T> observer, CancellationToken cancellationToken)
    {
        var observeOnObserver = new ObserveOnObserver(observer, asyncContext, forceYielding);
        return await source.SubscribeAsync(observeOnObserver, cancellationToken);
    }

    internal sealed class ObserveOnObserver(ObserverAsync<T> observer, AsyncContext asyncContext, bool forceYielding) : ObserverAsync<T>
    {
        protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken)
        {
            await asyncContext.SwitchContextAsync(forceYielding, cancellationToken);
            await observer.OnNextAsync(value, cancellationToken);
        }

        protected override async ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
        {
            await asyncContext.SwitchContextAsync(forceYielding, cancellationToken);
            await observer.OnErrorResumeAsync(error, cancellationToken);
        }

        protected override async ValueTask OnCompletedAsyncCore(Result result)
        {
            await asyncContext.SwitchContextAsync(forceYielding, CancellationToken.None);
            await observer.OnCompletedAsync(result);
        }
    }
}
