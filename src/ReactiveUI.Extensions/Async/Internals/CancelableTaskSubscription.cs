// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async.Internals;

internal static class CancelableTaskSubscription
{
    public static CancelableTaskSubscription<T> CreateAndStart<T>(Func<ObserverAsync<T>, CancellationToken, ValueTask> runAsyncCore, ObserverAsync<T> observer)
    {
        var ret = new AnonymousCancelableTaskSubscription<T>(runAsyncCore, observer);
        ret.Run();
        return ret;
    }

    private class AnonymousCancelableTaskSubscription<T>(Func<ObserverAsync<T>, CancellationToken, ValueTask> runAsyncCore, ObserverAsync<T> observer) : CancelableTaskSubscription<T>(observer)
    {
        protected override ValueTask RunAsyncCore(ObserverAsync<T> observer, CancellationToken cancellationToken) => runAsyncCore(observer, cancellationToken);
    }
}
