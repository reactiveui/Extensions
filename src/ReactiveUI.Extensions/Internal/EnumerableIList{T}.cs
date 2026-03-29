// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Internal;

/// <summary>
/// A lightweight struct wrapper around an <see cref="IList{T}"/> that provides index-based enumeration.
/// </summary>
/// <typeparam name="T">The type of elements in the list.</typeparam>
/// <param name="list">The underlying list to wrap.</param>
internal readonly struct EnumerableIList<T>(IList<T> list) : IEnumerableIList<T>, IList<T>
{
    /// <summary>
    /// Gets an empty <see cref="EnumerableIList{T}"/> instance.
    /// </summary>
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

    /// <summary>
    /// Implicitly converts a <see cref="List{T}"/> to an <see cref="EnumerableIList{T}"/>.
    /// </summary>
    /// <param name="list">The list to convert.</param>
    public static implicit operator EnumerableIList<T>(List<T> list) => new(list);

    /// <summary>
    /// Implicitly converts an array to an <see cref="EnumerableIList{T}"/>.
    /// </summary>
    /// <param name="array">The array to convert.</param>
    public static implicit operator EnumerableIList<T>(T[] array) => new(array);

    /// <summary>
    /// Returns an index-based enumerator for the list.
    /// </summary>
    /// <returns>An <see cref="EnumeratorIList{T}"/> that iterates over the list.</returns>
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

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
}
