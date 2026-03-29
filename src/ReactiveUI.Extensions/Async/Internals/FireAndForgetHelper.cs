// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace ReactiveUI.Extensions.Async.Internals;

/// <summary>
/// Provides a helper for executing async actions as fire-and-forget with exception swallowing.
/// Used for async void callbacks (e.g. cancellation token registrations, signal handlers)
/// where exceptions cannot propagate to a caller.
/// </summary>
[ExcludeFromCodeCoverage]
public static class FireAndForgetHelper
{
    /// <summary>
    /// Executes an async action as fire-and-forget, swallowing all exceptions.
    /// </summary>
    /// <param name="action">The async action to execute.</param>
    public static async void Run(Func<ValueTask> action)
    {
        ArgumentExceptionHelper.ThrowIfNull(action, nameof(action));

        try
        {
            await action();
        }
        catch
        {
            // Intentionally swallowed - fire-and-forget context has no caller to propagate to
        }
    }
}
