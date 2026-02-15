// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class contains static methods that extend the functionality of asynchronous
/// observables, enabling additional operations such as type casting and sequence manipulation. These methods are
/// intended to be used with the ObservableAsync{T} type to facilitate reactive programming patterns in asynchronous
/// scenarios.</remarks>
public static partial class ObservableAsync
{
    extension<T>(ObservableAsync<T> @this)
    {
        /// <summary>
        /// Projects each element of the observable sequence to the specified result type by performing a runtime cast.
        /// </summary>
        /// <remarks>If an element in the source sequence cannot be cast to <typeparamref
        /// name="TResult"/>, the sequence completes with a failure containing the exception. This method is useful for
        /// working with sequences of objects when the actual element type is known at runtime.</remarks>
        /// <typeparam name="TResult">The type to which the elements of the sequence are cast.</typeparam>
        /// <returns>An observable sequence whose elements are the result of casting each element of the source sequence to
        /// <typeparamref name="TResult"/>.</returns>
        public ObservableAsync<TResult> Cast<TResult>() => Create<TResult>(async (observer, subscribeToken) => await @this.SubscribeAsync(
                async (x, token) =>
                {
                    try
                    {
                        var v = (TResult)(object?)x!;
                        await observer.OnNextAsync(v, token);
                    }
                    catch (Exception e)
                    {
                        await observer.OnCompletedAsync(Result.Failure(e));
                    }
                },
                observer.OnErrorResumeAsync,
                observer.OnCompletedAsync,
                subscribeToken));
    }
}
