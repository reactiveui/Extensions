// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for controlling the execution context of asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class allow you to specify the context on which observer callbacks are invoked
/// for an asynchronous observable sequence. This is useful for ensuring that notifications are delivered on a
/// particular synchronization or task context, such as a UI thread or a custom scheduler.</remarks>
public static partial class ObservableAsync
{
    extension<T>(ObservableAsync<T> @this)
    {
        public ObservableAsync<T> ObserveOn(AsyncContext asyncContext, bool forceYielding = false)
        {
            return new ObserveOnAsyncObservable<T>(@this, asyncContext, forceYielding);
        }

        public ObservableAsync<T> ObserveOn(SynchronizationContext synchronizationContext, bool forceYielding = false)
        {
            var asyncContext = AsyncContext.From(synchronizationContext);
            return new ObserveOnAsyncObservable<T>(@this, asyncContext, forceYielding);
        }

        public ObservableAsync<T> ObserveOn(TaskScheduler taskScheduler, bool forceYielding = false)
        {
            var asyncContext = AsyncContext.From(taskScheduler);
            return new ObserveOnAsyncObservable<T>(@this, asyncContext, forceYielding);
        }
    }
}
