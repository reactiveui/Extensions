// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async.Internals;

internal class AnonymousObservableAsync<T>(Func<IObserverAsync<T>, CancellationToken, ValueTask<IAsyncDisposable>> subscribeAsync) : ObservableAsync<T>
{
    protected override ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<T> observer, CancellationToken cancellationToken) =>
        subscribeAsync(observer, cancellationToken);
}
