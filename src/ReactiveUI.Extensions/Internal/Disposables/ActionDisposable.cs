// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Internal.Disposables;

/// <summary>
/// An <see cref="IDisposable"/> that runs the supplied <see cref="Action"/> exactly once on
/// <see cref="Dispose"/>. Replaces <c>Disposable.Create(Action)</c>.
/// </summary>
internal sealed class ActionDisposable : IDisposable
{
    /// <summary>
    /// The action to invoke once on dispose.
    /// </summary>
    private Action? _action;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionDisposable"/> class.
    /// </summary>
    /// <param name="action">The action to invoke once on dispose.</param>
    public ActionDisposable(Action action) => _action = action;

    /// <inheritdoc/>
    public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
}
