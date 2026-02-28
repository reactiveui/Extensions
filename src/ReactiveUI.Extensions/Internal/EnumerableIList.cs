// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Internal;

internal static class EnumerableIList
{
    /// <summary>
    /// Creates the specified list.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="list">The list.</param>
    /// <returns>Enumerable IList.</returns>
    public static EnumerableIList<T> Create<T>(IList<T> list) => new(list);
}
