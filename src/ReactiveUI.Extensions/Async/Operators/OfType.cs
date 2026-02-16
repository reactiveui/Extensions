// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class contains static methods that extend the functionality of asynchronous
/// observables, enabling advanced filtering, transformation, and composition operations. These methods are intended to
/// be used with types implementing asynchronous observable patterns, such as ObservableAsync{T}.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Projects each element of the observable sequence to the specified reference type and filters out elements
        /// that are not of that type.
        /// </summary>
        /// <remarks>Elements that are not of type TResult are ignored and not included in the resulting
        /// sequence. This method is useful for working with observable sequences containing heterogeneous types,
        /// allowing subscribers to focus on elements of a specific type.</remarks>
        /// <typeparam name="TResult">The reference type to filter and project elements to. Must be a class.</typeparam>
        /// <returns>An observable sequence containing only the elements of type TResult from the original sequence.</returns>
        public IObservableAsync<TResult> OfType<TResult>()
            where TResult : class => Create<TResult>(async (observer, subscribeToken) => await @this.SubscribeAsync(
                                                      async (x, token) =>
                                                  {
                                                      if (x is TResult v)
                                                      {
                                                          await observer.OnNextAsync(v, token);
                                                      }
                                                  },
                                                      observer.OnErrorResumeAsync,
                                                      observer.OnCompletedAsync,
                                                      subscribeToken));
    }
}
