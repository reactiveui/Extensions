// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Internal.Disposables;

/// <summary>
/// A no-op <see cref="IDisposable"/> singleton used in place of <c>Disposable.Empty</c>.
/// </summary>
internal sealed class EmptyDisposable : IDisposable
{
    /// <summary>
    /// The shared singleton instance.
    /// </summary>
    public static readonly EmptyDisposable Instance = new();

    /// <summary>
    /// Prevents a default instance of the <see cref="EmptyDisposable"/> class from being created.
    /// </summary>
    private EmptyDisposable()
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
