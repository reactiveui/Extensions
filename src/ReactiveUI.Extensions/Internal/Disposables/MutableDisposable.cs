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
    /// <summary>The current inner disposable.</summary>
    private IDisposable? _current;

    /// <summary>Indicates whether the object has been disposed (0 = open, 1 = disposed).</summary>
    private int _disposed;

    /// <summary>Gets or sets the current inner disposable.</summary>
    public IDisposable? Disposable
    {
        get => Volatile.Read(ref _current);
        set => DisposableSlotHelper.AssignWithoutDisposingPrevious(ref _current, ref _disposed, value);
    }

    /// <inheritdoc/>
    public void Dispose() => DisposableSlotHelper.TryDispose(ref _current, ref _disposed);
}
