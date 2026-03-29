// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions;

/// <summary>
/// Represents either a heartbeat signal or a value update from an observable stream.
/// </summary>
/// <typeparam name="T">The type of the update value.</typeparam>
internal class Heartbeat<T> : IHeartbeat<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Heartbeat{T}"/> class representing a heartbeat signal.
    /// </summary>
    public Heartbeat()
        : this(true, default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Heartbeat{T}"/> class representing a value update.
    /// </summary>
    /// <param name="update">The update value.</param>
    public Heartbeat(T? update)
        : this(false, update)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Heartbeat{T}"/> class.
    /// </summary>
    /// <param name="isHeartbeat">Whether this instance represents a heartbeat signal.</param>
    /// <param name="update">The update value, or default if this is a heartbeat.</param>
    private Heartbeat(bool isHeartbeat, T? update)
    {
        IsHeartbeat = isHeartbeat;
        Update = update;
    }

    /// <inheritdoc/>
    public bool IsHeartbeat { get; }

    /// <inheritdoc/>
    public T? Update { get; }
}
