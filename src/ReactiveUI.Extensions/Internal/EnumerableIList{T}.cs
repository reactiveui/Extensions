// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Internal;

internal readonly struct EnumerableIList<T>(IList<T> list) : IEnumerableIList<T>, IList<T>
{
    public static EnumerableIList<T> Empty { get; }

    /// <inheritdoc />
    public int Count => list.Count;

    /// <inheritdoc />
    public bool IsReadOnly => list.IsReadOnly;

    /// <inheritdoc />
    public T this[int index]
    {
        get => list[index];
        set => list[index] = value;
    }

    public static implicit operator EnumerableIList<T>(List<T> list) => new(list);

    public static implicit operator EnumerableIList<T>(T[] array) => new(array);

    public EnumeratorIList<T> GetEnumerator() => new(list);

    /// <inheritdoc />
    public void Add(T item) => list.Add(item);

    /// <inheritdoc />
    public void Clear() => list.Clear();

    /// <inheritdoc />
    public bool Contains(T item) => list.Contains(item);

    /// <inheritdoc />
    public void CopyTo(T[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public int IndexOf(T item) => list.IndexOf(item);

    /// <inheritdoc />
    public void Insert(int index, T item) => list.Insert(index, item);

    /// <inheritdoc />
    public bool Remove(T item) => list.Remove(item);

    /// <inheritdoc />
    public void RemoveAt(int index) => list.RemoveAt(index);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
}
