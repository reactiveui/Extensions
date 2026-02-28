// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

internal static class AsyncContextMixins
{
    /// <summary>
    /// Determines whether the specified <see cref="AsyncContext"/> represents the current asynchronous context.
    /// </summary>
    /// <remarks>This method compares the <see cref="SynchronizationContext"/> or <see cref="TaskScheduler"/>
    /// of the provided <see cref="AsyncContext"/> with the current context to determine equivalence. Use this method to
    /// check if code is executing within the intended asynchronous environment.</remarks>
    /// <param name="this">The <see cref="AsyncContext"/> instance to compare with the current context.</param>
    /// <returns><see langword="true"/> if the specified <see cref="AsyncContext"/> matches the current <see
    /// cref="SynchronizationContext"/> or <see cref="TaskScheduler"/>; otherwise, <see langword="false"/>.</returns>
    public static bool IsSameAsCurrentAsyncContext(this AsyncContext @this)
    {
        if (@this.SynchronizationContext is not null)
        {
            return @this.SynchronizationContext == SynchronizationContext.Current;
        }

        if (@this.TaskScheduler is not null)
        {
            return @this.TaskScheduler == TaskScheduler.Current;
        }

        return TaskScheduler.Current == TaskScheduler.Default;
    }
}
