// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Internal;

/// <summary>
/// An index-based enumerator for <see cref="IList{T}"/> that avoids boxing and allocation overhead.
/// </summary>
/// <typeparam name="T">The type of elements in the list.</typeparam>
/// <param name="list">The list to enumerate.</param>
internal struct EnumeratorIList<T>(IList<T> list) : IEnumerator<T>
{
    /// <summary>
    /// The current position of the enumerator within the list.
    /// </summary>
    private int _index = -1;

    /// <inheritdoc/>
    public readonly T Current => list[_index];

    /// <inheritdoc/>
    readonly object? IEnumerator.Current => Current;

    /// <inheritdoc/>
    public bool MoveNext()
    {
        _index++;

        return _index < list.Count;
    }

    /// <inheritdoc/>
    public readonly void Dispose()
    {
        // Only clear if the list is not read-only (e.g., not an array)
        if (!list.IsReadOnly)
        {
            list.Clear();
        }
    }

    /// <inheritdoc/>
    public void Reset() => _index = -1;
}
