// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions;

internal class Heartbeat<T> : IHeartbeat<T>
{
    public Heartbeat()
        : this(true, default)
    {
    }

    public Heartbeat(T? update)
        : this(false, update)
    {
    }

    private Heartbeat(bool isHeartbeat, T? update)
    {
        IsHeartbeat = isHeartbeat;
        Update = update;
    }

    public bool IsHeartbeat { get; }

    public T? Update { get; }
}
