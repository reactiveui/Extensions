// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Internal.Disposables;

/// <summary>
/// A disposable holder whose inner disposable can be re-assigned. The previous inner
/// disposable is NOT disposed when replaced (in contrast to <see cref="SwapDisposable"/>).
/// Once this object is disposed, any subsequently assigned inner disposable is disposed
/// immediately. Replaces <c>MultipleAssignmentDisposable</c>.
/// </summary>
internal sealed class MutableDisposable : IDisposable
{
    /// <summary>
    /// Sentinel value indicating the object has been disposed.
    /// </summary>
    private const int DisposedSentinel = 1;

    /// <summary>
    /// The current inner disposable.
    /// </summary>
    private IDisposable? _current;

    /// <summary>
    /// Indicates whether the object has been disposed.
    /// </summary>
    private int _disposed;

    /// <summary>
    /// Gets or sets the current inner disposable.
    /// </summary>
    public IDisposable? Disposable
    {
        get => Volatile.Read(ref _current);
        set
        {
            if (Volatile.Read(ref _disposed) == DisposedSentinel)
            {
                value?.Dispose();
                return;
            }

            Interlocked.Exchange(ref _current, value);

            // Re-check in case Dispose raced us.
            if (Volatile.Read(ref _disposed) != DisposedSentinel)
            {
                return;
            }

            Interlocked.Exchange(ref _current, null)?.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, DisposedSentinel) == DisposedSentinel)
        {
            return;
        }

        Interlocked.Exchange(ref _current, null)?.Dispose();
    }
}
