// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides Aggregate (fold/reduce) extension methods for asynchronous observable sequences.
/// </summary>
/// <remarks>Aggregate applies an accumulator function over each element of the observable sequence
/// and returns the final accumulated value when the sequence completes. This is equivalent to a fold
/// or reduce operation.</remarks>
public static partial class ObservableAsync
{
    extension<T>(IObservableAsync<T> @this)
    {
        /// <summary>
        /// Applies an asynchronous accumulator function over the observable sequence, returning the
        /// final accumulated value when the sequence completes.
        /// </summary>
        /// <typeparam name="TAcc">The type of the accumulated value.</typeparam>
        /// <param name="seed">The initial accumulator value.</param>
        /// <param name="accumulator">An asynchronous accumulator function to invoke on each element. Receives the
        /// current accumulated value, the current element, and a cancellation token.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the final accumulated value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="accumulator"/> is null.</exception>
        public async ValueTask<TAcc> AggregateAsync<TAcc>(TAcc seed, Func<TAcc, T, CancellationToken, ValueTask<TAcc>> accumulator, CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(accumulator);
#else
            if (accumulator is null)
            {
                throw new ArgumentNullException(nameof(accumulator));
            }
#endif

            var observer = new AggregateAsyncObserver<T, TAcc>(seed, accumulator, cancellationToken);
            _ = await @this.SubscribeAsync(observer, cancellationToken);
            return await observer.WaitValueAsync();
        }

        /// <summary>
        /// Applies an accumulator function over the observable sequence, returning the final accumulated
        /// value when the sequence completes.
        /// </summary>
        /// <typeparam name="TAcc">The type of the accumulated value.</typeparam>
        /// <param name="seed">The initial accumulator value.</param>
        /// <param name="accumulator">An accumulator function to invoke on each element. Receives the current
        /// accumulated value and the current element.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the final accumulated value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="accumulator"/> is null.</exception>
        public ValueTask<TAcc> AggregateAsync<TAcc>(TAcc seed, Func<TAcc, T, TAcc> accumulator, CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(accumulator);
#else
            if (accumulator is null)
            {
                throw new ArgumentNullException(nameof(accumulator));
            }
#endif

            return @this.AggregateAsync(seed, (acc, x, _) => new ValueTask<TAcc>(accumulator(acc, x)), cancellationToken);
        }

        /// <summary>
        /// Applies an accumulator function over the observable sequence with a seed value, then applies
        /// a result selector to the final accumulated value.
        /// </summary>
        /// <typeparam name="TAcc">The type of the intermediate accumulated value.</typeparam>
        /// <typeparam name="TResult">The type of the result value.</typeparam>
        /// <param name="seed">The initial accumulator value.</param>
        /// <param name="accumulator">An accumulator function to invoke on each element.</param>
        /// <param name="resultSelector">A function to transform the final accumulated value into the result value.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the transformed result.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="accumulator"/> or
        /// <paramref name="resultSelector"/> is null.</exception>
        public async ValueTask<TResult> AggregateAsync<TAcc, TResult>(TAcc seed, Func<TAcc, T, TAcc> accumulator, Func<TAcc, TResult> resultSelector, CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(accumulator);
            ArgumentNullException.ThrowIfNull(resultSelector);
#else
            if (accumulator is null)
            {
                throw new ArgumentNullException(nameof(accumulator));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }
#endif

            var acc = await @this.AggregateAsync(seed, accumulator, cancellationToken);
            return resultSelector(acc);
        }
    }

    private sealed class AggregateAsyncObserver<T, TAcc>(TAcc seed, Func<TAcc, T, CancellationToken, ValueTask<TAcc>> accumulator, CancellationToken cancellationToken) : TaskObserverAsyncBase<T, TAcc>(cancellationToken)
    {
        private TAcc _acc = seed;

        protected override async ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => _acc = await accumulator(_acc, value, cancellationToken);

        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => TrySetException(error);

        protected override ValueTask OnCompletedAsyncCore(Result result) =>
            result.IsSuccess ? TrySetCompleted(_acc) : TrySetException(result.Exception);
    }
}
