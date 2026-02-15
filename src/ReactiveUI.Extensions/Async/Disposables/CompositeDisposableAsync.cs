// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace ReactiveUI.Extensions.Async.Disposables;

/// <summary>
/// Represents a thread-safe collection of asynchronous disposable objects that are disposed together as a group.
/// Provides methods to add, remove, and asynchronously dispose contained resources as a single operation.
/// </summary>
/// <remarks>Use this class to manage the lifetime of multiple <see cref="IAsyncDisposable"/> resources, ensuring
/// that all are disposed when the collection is disposed. Once disposed, the collection cannot be used to add or remove
/// items. This class is not read-only and is safe for concurrent access from multiple threads.</remarks>
public sealed class CompositeDisposableAsync : IAsyncDisposable
{
    private const int ShrinkThreshold = 64;
    private readonly object _gate = new object();
    private List<IAsyncDisposable?> _list;
    private bool _isDisposed;
    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeDisposableAsync"/> class.
    /// </summary>
    public CompositeDisposableAsync() => _list = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeDisposableAsync"/> class with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">The number of elements that the collection can initially store. Must be greater than or equal to 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is less than 0.</exception>
    public CompositeDisposableAsync(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _list = new(capacity);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeDisposableAsync"/> class that contains the specified asynchronous.
    /// disposables.
    /// </summary>
    /// <remarks>Each disposable provided will be disposed asynchronously when the composite is disposed. The
    /// order in which disposables are disposed is the same as the order in the array.</remarks>
    /// <param name="disposables">An array of objects that implement IAsyncDisposable to be managed by the composite. Cannot be null, but may be
    /// empty.</param>
    public CompositeDisposableAsync(params IAsyncDisposable[] disposables)
    {
        _list = new(disposables);
        _count = _list.Count;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeDisposableAsync"/> class that contains the specified asynchronous.
    /// disposables.
    /// </summary>
    /// <remarks>Each disposable in the collection will be disposed asynchronously when the composite is
    /// disposed. The order in which disposables are disposed is the same as the order in the provided
    /// collection.</remarks>
    /// <param name="disposables">The collection of IAsyncDisposable instances to include in the composite. Cannot be null.</param>
    public CompositeDisposableAsync(IEnumerable<IAsyncDisposable> disposables)
    {
        _list = new(disposables);
        _count = _list.Count;
    }

    /// <summary>
    /// Gets a value indicating whether the object has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _isDisposed);

    /// <summary>
    /// Gets the number of elements contained in the collection.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Adds an asynchronous disposable item to the collection, or disposes it immediately if the collection has already
    /// been disposed.
    /// </summary>
    /// <param name="item">The item to add. The item must implement <see cref="IAsyncDisposable"/> and will be disposed asynchronously if
    /// the collection is disposed.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation. The returned task is completed if the item
    /// was added; otherwise, it represents the asynchronous disposal of the item.</returns>
    public ValueTask AddAsync(IAsyncDisposable item)
    {
        lock (_gate)
        {
            if (!_isDisposed)
            {
                _count++;
                _list.Add(item);
                return default;
            }
        }

        return item.DisposeAsync();
    }

    /// <summary>
    /// Removes the specified item from the collection and disposes it asynchronously.
    /// </summary>
    /// <remarks>If the item is not found in the collection, it is not disposed. This method is
    /// thread-safe.</remarks>
    /// <param name="item">The item to remove and dispose. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous remove operation. The task result is <see langword="true"/> if the item
    /// was found and removed; otherwise, <see langword="false"/>.</returns>
    public async ValueTask<bool> Remove(IAsyncDisposable item)
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return false;
            }

            var current = _list;

            var index = current.IndexOf(item);
            if (index == -1)
            {
                return false;
            }

            current[index] = null;

            if (current.Capacity > ShrinkThreshold && _count < current.Capacity / 2)
            {
                var fresh = new List<IAsyncDisposable?>(current.Capacity / 2);

                foreach (var d in current)
                {
                    if (d != null)
                    {
                        fresh.Add(d);
                    }
                }

                _list = fresh;
            }

            _count--;
        }

        await item.DisposeAsync();
        return true;
    }

    /// <summary>
    /// Asynchronously disposes all items in the collection and removes them.
    /// </summary>
    /// <remarks>If the collection is already empty or has been disposed, this method performs no action. Each
    /// item is disposed asynchronously before being removed from the collection. This method is thread-safe.</remarks>
    /// <returns>A task that represents the asynchronous clear operation.</returns>
    public async ValueTask Clear()
    {
        IAsyncDisposable?[] targetDisposables;
        int clearCount;
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_count == 0)
            {
                return;
            }

            targetDisposables = ArrayPool<IAsyncDisposable?>.Shared.Rent(_list.Count);
            clearCount = _list.Count;

            _list.CopyTo(targetDisposables);

            _list.Clear();
            _count = 0;
        }

        try
        {
            foreach (var item in targetDisposables.Take(clearCount))
            {
                if (item != null)
                {
                    await item.DisposeAsync();
                }
            }
        }
        finally
        {
            ArrayPool<IAsyncDisposable?>.Shared.Return(targetDisposables, clearArray: true);
        }
    }

    /// <summary>
    /// Determines whether the collection contains the specified asynchronous disposable item.
    /// </summary>
    /// <remarks>If the collection has been disposed, this method always returns false.</remarks>
    /// <param name="item">The asynchronous disposable item to locate in the collection. Can be null.</param>
    /// <returns>true if the specified item is found in the collection and the collection has not been disposed; otherwise,
    /// false.</returns>
    public bool Contains(IAsyncDisposable item)
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return false;
            }

            return _list.Contains(item);
        }
    }

    /// <summary>
    /// Copies the elements of the collection to the specified array, starting at the given array index.
    /// </summary>
    /// <param name="array">The one-dimensional array of IAsyncDisposable elements that is the destination of the elements copied from the
    /// collection. The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in the destination array at which copying begins. Must be non-negative and less than the
    /// length of the array.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when arrayIndex is less than zero, greater than or equal to the length of array, or when there is not
    /// enough space from arrayIndex to the end of array to accommodate all elements in the collection.</exception>
    public void CopyTo(IAsyncDisposable[] array, int arrayIndex)
    {
        if (arrayIndex < 0 || arrayIndex >= array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            if (arrayIndex + _count > array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            var i = 0;
            foreach (var item in _list)
            {
                if (item != null)
                {
                    array[arrayIndex + i++] = item;
                }
            }
        }
    }

    /// <summary>
    /// Asynchronously releases all resources used by the collection and disposes of each contained asynchronous
    /// disposable object.
    /// </summary>
    /// <remarks>After calling this method, the collection is considered disposed and cannot be used. This
    /// method is thread-safe and can be called multiple times; subsequent calls after the first have no
    /// effect.</remarks>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        List<IAsyncDisposable?> disposables;

        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _count = 0;
            _isDisposed = true;
            disposables = _list;
            _list = null!; // dereference.
        }

        foreach (var item in disposables)
        {
            if (item is not null)
            {
                await item.DisposeAsync();
            }
        }

        disposables.Clear();
    }

    /// <summary>
    /// Returns an enumerator that iterates through a snapshot of the collection and clears its contents.
    /// </summary>
    /// <remarks>The enumerator operates on a snapshot of the collection taken at the time of the call. After
    /// enumeration, the original collection is cleared. This method is thread-safe.</remarks>
    /// <returns>An enumerator for the collection of <see cref="IAsyncDisposable"/> items present at the time of enumeration.</returns>
    public IEnumerator<IAsyncDisposable> GetEnumerator()
    {
        lock (_gate)
        {
            // make snapshot
            return EnumerateAndClear(_list.ToArray()).GetEnumerator();
        }
    }

    private static IEnumerable<IAsyncDisposable> EnumerateAndClear(IAsyncDisposable?[] disposables)
    {
        try
        {
            foreach (var item in disposables)
            {
                if (item != null)
                {
                    yield return item;
                }
            }
        }
        finally
        {
            disposables.AsSpan().Clear();
        }
    }
}
