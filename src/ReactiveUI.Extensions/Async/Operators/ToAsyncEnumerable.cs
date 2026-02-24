// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
public static partial class ObservableAsync
{
    /// <summary>
    /// Converts the specified observable sequence to an asynchronous enumerable sequence, enabling consumption using
    /// asynchronous iteration.
    /// </summary>
    /// <remarks>The returned asynchronous enumerable reflects the items and completion behavior of the source
    /// observable. The buffering and concurrency characteristics depend on the channel created by <paramref
    /// name="channelFactory"/>. If <paramref name="onErrorResume"/> is provided, it can be used to suppress or handle
    /// errors from the observable; otherwise, errors are propagated to the enumerator.</remarks>
    /// <typeparam name="T">The type of elements in the observable and resulting asynchronous enumerable sequence.</typeparam>
    /// <param name="this">The observable sequence to convert to an asynchronous enumerable.</param>
    /// <param name="channelFactory">A factory function that creates a new channel used to buffer items between the observable and the asynchronous
    /// enumerable. The channel controls the buffering and backpressure behavior.</param>
    /// <param name="onErrorResume">An optional asynchronous callback invoked when an error occurs in the observable sequence. If provided, this
    /// function can handle the exception and determine how the sequence should resume or complete. If null, the
    /// sequence completes with the error.</param>
    /// <returns>An asynchronous enumerable sequence that yields the elements produced by the observable sequence. The
    /// enumeration completes when the observable completes or an unhandled error occurs.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="this"/> or <paramref name="channelFactory"/> is null.</exception>
    public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        this IObservableAsync<T> @this,
        Func<Channel<T>> channelFactory,
        Func<Exception, CancellationToken, ValueTask>? onErrorResume = null)
    {
        if (@this is null)
        {
            throw new ArgumentNullException(nameof(@this));
        }

        if (channelFactory is null)
        {
            throw new ArgumentNullException(nameof(channelFactory));
        }

        return Impl(@this, channelFactory, onErrorResume);

        static async IAsyncEnumerable<T> Impl(
            IObservableAsync<T> @this,
            Func<Channel<T>> channelFactory,
            Func<Exception, CancellationToken, ValueTask>? onErrorResume,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var channel = channelFactory();
            var onErrorResumeAsync = onErrorResume ?? ((e, _) =>
            {
                channel.Writer.Complete(e);
                return default;
            });

            await using var subscription = await @this.SubscribeAsync(
                channel.Writer.WriteAsync,
                onErrorResumeAsync,
                result =>
                {
                    channel.Writer.Complete(result.Exception);
                    return default;
                },
                cancellationToken);

            await foreach (var x in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return x;
            }
        }
    }
}
