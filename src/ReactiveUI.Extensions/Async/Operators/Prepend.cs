// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with asynchronous observable sequences.
/// </summary>
/// <remarks>The ObservableAsync class offers utility methods that enable manipulation and composition of
/// asynchronous observables, such as prepending values to a sequence. These methods facilitate common operations when
/// building reactive, asynchronous workflows.</remarks>
public static partial class ObservableAsync
{
    extension<T>(ObservableAsync<T> @this)
    {
        /// <summary>
        /// Returns a new observable sequence that begins with the specified value, followed by the elements of the
        /// current sequence.
        /// </summary>
        /// <param name="value">The value to prepend to the beginning of the sequence.</param>
        /// <returns>An observable sequence with the specified value prepended to the original sequence.</returns>
        public ObservableAsync<T> Prepend(T value) => @this.Prepend([value]);

        /// <summary>
        /// Returns a new observable sequence that emits the specified values before the emissions from the current
        /// sequence.
        /// </summary>
        /// <remarks>The values in the provided collection are emitted in order before any items from the
        /// original sequence. If the sequence is unsubscribed before completion, remaining values may not be
        /// emitted.</remarks>
        /// <param name="values">The collection of values to emit before the original sequence. Cannot be null.</param>
        /// <returns>An observable sequence that emits the specified values first, followed by the items from the current
        /// sequence.</returns>
        public ObservableAsync<T> Prepend(IEnumerable<T> values) => Create<T>((observer, _) =>
        {
            var cts = new CancellationTokenSource();
            SingleAssignmentDisposableAsync subscriptionDisposable = new();
            AsyncLocal<bool> reentrant = new();
            var task = Core(cts.Token);
            async Task Core(CancellationToken cancellationToken)
            {
                try
                {
                    reentrant.Value = true;
                    foreach (var value in values)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        await observer.OnNextAsync(value, cancellationToken);
                    }

                    var subscription = await @this.SubscribeAsync(observer.Wrap(), cancellationToken);
                    await subscriptionDisposable.SetDisposableAsync(subscription);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    try
                    {
                        await observer.OnCompletedAsync(Result.Failure(e));
                    }
                    catch (Exception exception)
                    {
                        UnhandledExceptionHandler.OnUnhandledException(exception);
                    }
                }
            }

            var subcription = DisposableAsync.Create(async () =>
            {
                await subscriptionDisposable.DisposeAsync();
                if (!reentrant.Value)
                {
                    cts.Cancel();
                    await task;
                }

                cts.Dispose();
            });
            return new ValueTask<IAsyncDisposable>(subcription);
        });
    }
}
