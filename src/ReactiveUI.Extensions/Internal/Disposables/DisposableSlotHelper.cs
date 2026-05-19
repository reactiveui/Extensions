// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace ReactiveUI.Extensions.Internal.Disposables;

/// <summary>
/// Pure-plumbing helpers for the swap-disposable-slot pattern shared by
/// <see cref="MutableDisposable"/> and <see cref="SwapDisposable"/>. Centralizes the
/// TOCTOU race-defensive recheck so the call-site setters stay branchless one-liners.
/// Marked <see cref="ExcludeFromCodeCoverageAttribute"/> — the recheck protects against
/// an extremely narrow concurrent-Dispose race that cannot be deterministically triggered
/// in unit tests, in the same spirit as <see cref="ArgumentExceptionHelper"/>'s
/// throw-helpers.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class DisposableSlotHelper
{
    /// <summary>Sentinel value indicating the holder has been disposed.</summary>
    public const int DisposedSentinel = 1;

    /// <summary>
    /// Reassigns an inner disposable slot WITHOUT disposing the previous value (mutable-assign
    /// semantics, matching the <see cref="MutableDisposable"/> contract). If the holder is
    /// already disposed, the incoming value is disposed immediately; if Dispose races between
    /// the pre-check and the store, the just-stored value is disposed to avoid leaking it.
    /// </summary>
    /// <param name="slot">The reference to the current-inner field.</param>
    /// <param name="disposed">The reference to the disposed-flag field.</param>
    /// <param name="value">The incoming value (or <see langword="null"/>).</param>
    public static void AssignWithoutDisposingPrevious(
        ref IDisposable? slot,
        ref int disposed,
        IDisposable? value)
    {
        if (Volatile.Read(ref disposed) == DisposedSentinel)
        {
            value?.Dispose();
            return;
        }

        Interlocked.Exchange(ref slot, value);

        if (Volatile.Read(ref disposed) != DisposedSentinel)
        {
            return;
        }

        Interlocked.Exchange(ref slot, null)?.Dispose();
    }

    /// <summary>
    /// Reassigns an inner disposable slot and disposes the previous value (swap semantics,
    /// matching the <see cref="SwapDisposable"/> contract). If the holder is already disposed,
    /// the incoming value is disposed immediately; if Dispose races between the swap and the
    /// recheck, the just-stored value is also disposed.
    /// </summary>
    /// <param name="slot">The reference to the current-inner field.</param>
    /// <param name="disposed">The reference to the disposed-flag field.</param>
    /// <param name="value">The incoming value (or <see langword="null"/>).</param>
    public static void SwapAndDisposePrevious(
        ref IDisposable? slot,
        ref int disposed,
        IDisposable? value)
    {
        if (Volatile.Read(ref disposed) == DisposedSentinel)
        {
            value?.Dispose();
            return;
        }

        var previous = Interlocked.Exchange(ref slot, value);
        previous?.Dispose();

        if (Volatile.Read(ref disposed) != DisposedSentinel)
        {
            return;
        }

        Interlocked.Exchange(ref slot, null)?.Dispose();
    }

    /// <summary>
    /// Performs the standard idempotent dispose step: latches the disposed flag and disposes
    /// the current inner (if any). Returns <see langword="true"/> if this was the first call
    /// and the caller should clean up; <see langword="false"/> if a prior dispose has already
    /// done the work.
    /// </summary>
    /// <param name="slot">The reference to the current-inner field.</param>
    /// <param name="disposed">The reference to the disposed-flag field.</param>
    /// <returns>
    /// <see langword="true"/> if the current invocation latched the flag; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public static bool TryDispose(ref IDisposable? slot, ref int disposed)
    {
        if (Interlocked.Exchange(ref disposed, DisposedSentinel) == DisposedSentinel)
        {
            return false;
        }

        Interlocked.Exchange(ref slot, null)?.Dispose();
        return true;
    }
}
