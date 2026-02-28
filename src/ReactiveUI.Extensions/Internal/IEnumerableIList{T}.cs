// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Internal;

/// <summary>
/// A enumerable that also contains the enumerable list.
/// </summary>
/// <typeparam name="T">The type of items.</typeparam>
internal interface IEnumerableIList<T> : IEnumerable<T>
{
    /// <summary>
    /// Gets the enumerator.
    /// </summary>
    /// <returns>The enumerator.</returns>
    new EnumeratorIList<T> GetEnumerator();
}
