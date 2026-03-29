// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions;

/// <summary>
/// Represents either a staleness indicator or a value update from an observable stream.
/// </summary>
/// <typeparam name="T">The type of the update value.</typeparam>
internal class Stale<T> : IStale<T>
{
    /// <summary>
    /// The update value, or <see langword="default"/> when the instance represents a stale signal.
    /// </summary>
    private readonly T? _update;

    /// <summary>
    /// Initializes a new instance of the <see cref="Stale{T}"/> class representing a stale signal.
    /// </summary>
    public Stale()
        : this(true, default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Stale{T}"/> class representing a value update.
    /// </summary>
    /// <param name="update">The update value.</param>
    public Stale(T? update)
        : this(false, update)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Stale{T}"/> class.
    /// </summary>
    /// <param name="isStale">Whether this instance represents a stale signal.</param>
    /// <param name="update">The update value, or default if this is a stale signal.</param>
    private Stale(bool isStale, T? update)
    {
        IsStale = isStale;
        _update = update;
    }

    /// <inheritdoc/>
    public bool IsStale { get; }

    /// <inheritdoc/>
    public T? Update => IsStale ? throw new InvalidOperationException("Stale instance has no update.") : _update;
}
