// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async.Internals
{
    internal sealed class WrappedObserverAsync<T>(ObserverAsync<T> observer) : ObserverAsync<T>
    {
        protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => observer.OnNextAsync(value, cancellationToken);

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => observer.OnErrorResumeAsync(error, cancellationToken);

        protected override ValueTask OnCompletedAsyncCore(Result result) => observer.OnCompletedAsync(result);
    }
}
