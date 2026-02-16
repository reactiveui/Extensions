// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for combining multiple asynchronous observable sequences into a single sequence that
/// emits values based on the latest values from each source.
/// </summary>
/// <remarks>The methods in this class allow you to combine up to eight ObservableAsync sources using the
/// CombineLatest pattern. Each resulting sequence emits a value whenever any source emits, after all sources have
/// produced at least one value. The emitted value is determined by a selector function that receives the latest values
/// from each source. These methods are useful for scenarios where you need to react to changes across multiple
/// asynchronous streams and produce a new value based on their combined state. Thread safety and cancellation are
/// handled internally, and subscriptions are disposed when the combined sequence completes or is cancelled.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Combines two asynchronous observable sequences and emits a result each time either sequence produces a value,
    /// using the specified selector function.
    /// </summary>
    /// <remarks>The returned sequence emits a value each time either source sequence produces a new value,
    /// after both sequences have produced at least one value. The selector function is invoked with the most recent
    /// values from each sequence. If either source sequence completes, the combined sequence completes as
    /// well.</remarks>
    /// <typeparam name="T1">The type of the elements in the first observable sequence.</typeparam>
    /// <typeparam name="T2">The type of the elements in the second observable sequence.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the selector function.</typeparam>
    /// <param name="src1">The first asynchronous observable sequence to combine.</param>
    /// <param name="src2">The second asynchronous observable sequence to combine.</param>
    /// <param name="selector">A function that projects a result value from the latest values of both observable sequences.</param>
    /// <returns>An asynchronous observable sequence that emits values resulting from applying the selector function to the
    /// latest values from both source sequences.</returns>
    public static IObservableAsync<TResult> CombineLatest<T1, T2, TResult>(
        this IObservableAsync<T1> src1,
        IObservableAsync<T2> src2,
        Func<T1, T2, TResult> selector) => new CombineLatest2ObservableAsync<T1, T2, TResult>(src1, src2, selector);

    /// <summary>
    /// Combines the latest values from three asynchronous observable sequences and projects them into a new result
    /// using the specified selector function.
    /// </summary>
    /// <remarks>The returned sequence does not emit a value until all source sequences have produced at least
    /// one value. Subsequent emissions occur whenever any source sequence produces a new value, using the most recent
    /// values from all sources.</remarks>
    /// <typeparam name="T1">The type of the elements in the first source observable sequence.</typeparam>
    /// <typeparam name="T2">The type of the elements in the second source observable sequence.</typeparam>
    /// <typeparam name="T3">The type of the elements in the third source observable sequence.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the selector function.</typeparam>
    /// <param name="src1">The first source observable sequence whose latest value will be combined.</param>
    /// <param name="src2">The second source observable sequence whose latest value will be combined.</param>
    /// <param name="src3">The third source observable sequence whose latest value will be combined.</param>
    /// <param name="selector">A function that receives the latest values from each source sequence and returns a projected result.</param>
    /// <returns>An asynchronous observable sequence that emits a result each time any of the source sequences produces a new
    /// value, using the latest values from all sources.</returns>
    public static IObservableAsync<TResult> CombineLatest<T1, T2, T3, TResult>(
        this IObservableAsync<T1> src1,
        IObservableAsync<T2> src2,
        IObservableAsync<T3> src3,
        Func<T1, T2, T3, TResult> selector) => new CombineLatest3ObservableAsync<T1, T2, T3, TResult>(src1, src2, src3, selector);

    /// <summary>
    /// Combines the latest values from four asynchronous observable sequences and projects them into a new result using
    /// the specified selector function.
    /// </summary>
    /// <remarks>The returned sequence emits a new result each time any of the source sequences produces a
    /// value, after all sources have produced at least one value. The selector function is invoked with the most recent
    /// values from each source. If any source sequence completes or fails, the resulting sequence will also complete or
    /// fail accordingly.</remarks>
    /// <typeparam name="T1">The type of the elements in the first source observable sequence.</typeparam>
    /// <typeparam name="T2">The type of the elements in the second source observable sequence.</typeparam>
    /// <typeparam name="T3">The type of the elements in the third source observable sequence.</typeparam>
    /// <typeparam name="T4">The type of the elements in the fourth source observable sequence.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the selector function.</typeparam>
    /// <param name="src1">The first source observable sequence whose latest value will be combined.</param>
    /// <param name="src2">The second source observable sequence whose latest value will be combined.</param>
    /// <param name="src3">The third source observable sequence whose latest value will be combined.</param>
    /// <param name="src4">The fourth source observable sequence whose latest value will be combined.</param>
    /// <param name="selector">A function that takes the latest values from each source sequence and projects them into a result.</param>
    /// <returns>An asynchronous observable sequence containing the results of combining the latest values from the four source
    /// sequences using the selector function.</returns>
    public static IObservableAsync<TResult> CombineLatest<T1, T2, T3, T4, TResult>(
        this IObservableAsync<T1> src1,
        IObservableAsync<T2> src2,
        IObservableAsync<T3> src3,
        IObservableAsync<T4> src4,
        Func<T1, T2, T3, T4, TResult> selector) => new CombineLatest4ObservableAsync<T1, T2, T3, T4, TResult>(src1, src2, src3, src4, selector);

    /// <summary>
    /// Combines the latest values from five asynchronous observable sequences into a single sequence using the
    /// specified selector function.
    /// </summary>
    /// <remarks>The resulting sequence emits a new value each time any of the source sequences produces a
    /// value, after all sources have emitted at least one value. If any source sequence completes or fails, the
    /// resulting sequence will also complete or fail accordingly.</remarks>
    /// <typeparam name="T1">The type of the elements in the first source observable sequence.</typeparam>
    /// <typeparam name="T2">The type of the elements in the second source observable sequence.</typeparam>
    /// <typeparam name="T3">The type of the elements in the third source observable sequence.</typeparam>
    /// <typeparam name="T4">The type of the elements in the fourth source observable sequence.</typeparam>
    /// <typeparam name="T5">The type of the elements in the fifth source observable sequence.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the selector function.</typeparam>
    /// <param name="src1">The first source observable sequence whose latest value will be combined.</param>
    /// <param name="src2">The second source observable sequence whose latest value will be combined.</param>
    /// <param name="src3">The third source observable sequence whose latest value will be combined.</param>
    /// <param name="src4">The fourth source observable sequence whose latest value will be combined.</param>
    /// <param name="src5">The fifth source observable sequence whose latest value will be combined.</param>
    /// <param name="selector">A function that combines the latest values from each source sequence into a result value.</param>
    /// <returns>An asynchronous observable sequence that emits values resulting from applying the selector function to the
    /// latest values from each source sequence.</returns>
    public static IObservableAsync<TResult> CombineLatest<T1, T2, T3, T4, T5, TResult>(
        this IObservableAsync<T1> src1,
        IObservableAsync<T2> src2,
        IObservableAsync<T3> src3,
        IObservableAsync<T4> src4,
        IObservableAsync<T5> src5,
        Func<T1, T2, T3, T4, T5, TResult> selector) => new CombineLatest5ObservableAsync<T1, T2, T3, T4, T5, TResult>(src1, src2, src3, src4, src5, selector);

    /// <summary>
    /// Combines the latest values from six asynchronous observable sources into a single observable sequence, using the
    /// specified selector function to produce results.
    /// </summary>
    /// <remarks>The resulting observable will not emit any values until all six source observables have
    /// produced at least one value. Subsequent emissions occur whenever any source produces a new value, using the
    /// latest values from all sources. If any source completes or fails, the resulting observable will complete or fail
    /// accordingly.</remarks>
    /// <typeparam name="T1">The type of the elements in the first source observable.</typeparam>
    /// <typeparam name="T2">The type of the elements in the second source observable.</typeparam>
    /// <typeparam name="T3">The type of the elements in the third source observable.</typeparam>
    /// <typeparam name="T4">The type of the elements in the fourth source observable.</typeparam>
    /// <typeparam name="T5">The type of the elements in the fifth source observable.</typeparam>
    /// <typeparam name="T6">The type of the elements in the sixth source observable.</typeparam>
    /// <typeparam name="TResult">The type of the result elements produced by the selector function.</typeparam>
    /// <param name="src1">The first source observable whose latest value will be combined.</param>
    /// <param name="src2">The second source observable whose latest value will be combined.</param>
    /// <param name="src3">The third source observable whose latest value will be combined.</param>
    /// <param name="src4">The fourth source observable whose latest value will be combined.</param>
    /// <param name="src5">The fifth source observable whose latest value will be combined.</param>
    /// <param name="src6">The sixth source observable whose latest value will be combined.</param>
    /// <param name="selector">A function that combines the latest values from each source observable into a result value.</param>
    /// <returns>An observable sequence that emits a result each time any of the source observables produces a new value, after
    /// all sources have emitted at least one value.</returns>
    public static IObservableAsync<TResult> CombineLatest<T1, T2, T3, T4, T5, T6, TResult>(
        this IObservableAsync<T1> src1,
        IObservableAsync<T2> src2,
        IObservableAsync<T3> src3,
        IObservableAsync<T4> src4,
        IObservableAsync<T5> src5,
        IObservableAsync<T6> src6,
        Func<T1, T2, T3, T4, T5, T6, TResult> selector) => new CombineLatest6ObservableAsync<T1, T2, T3, T4, T5, T6, TResult>(src1, src2, src3, src4, src5, src6, selector);

    /// <summary>
    /// Combines the latest values from seven asynchronous observable sources into a single observable sequence, using
    /// the specified selector function to produce results whenever any source emits a new value.
    /// </summary>
    /// <remarks>The resulting sequence does not emit a value until each source has produced at least one
    /// value. Subsequent emissions occur whenever any source produces a new value, using the latest values from all
    /// sources. If any source completes or fails, the resulting sequence will complete or propagate the error
    /// accordingly.</remarks>
    /// <typeparam name="T1">The type of the elements emitted by the first observable source.</typeparam>
    /// <typeparam name="T2">The type of the elements emitted by the second observable source.</typeparam>
    /// <typeparam name="T3">The type of the elements emitted by the third observable source.</typeparam>
    /// <typeparam name="T4">The type of the elements emitted by the fourth observable source.</typeparam>
    /// <typeparam name="T5">The type of the elements emitted by the fifth observable source.</typeparam>
    /// <typeparam name="T6">The type of the elements emitted by the sixth observable source.</typeparam>
    /// <typeparam name="T7">The type of the elements emitted by the seventh observable source.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the selector function.</typeparam>
    /// <param name="src1">The first asynchronous observable source whose latest value will be combined.</param>
    /// <param name="src2">The second asynchronous observable source whose latest value will be combined.</param>
    /// <param name="src3">The third asynchronous observable source whose latest value will be combined.</param>
    /// <param name="src4">The fourth asynchronous observable source whose latest value will be combined.</param>
    /// <param name="src5">The fifth asynchronous observable source whose latest value will be combined.</param>
    /// <param name="src6">The sixth asynchronous observable source whose latest value will be combined.</param>
    /// <param name="src7">The seventh asynchronous observable source whose latest value will be combined.</param>
    /// <param name="selector">A function that combines the latest values from all seven sources into a result to be emitted by the resulting
    /// observable sequence.</param>
    /// <returns>An asynchronous observable sequence that emits results produced by the selector function whenever any of the
    /// seven sources emits a new value, after all sources have emitted at least one value.</returns>
    public static IObservableAsync<TResult> CombineLatest<T1, T2, T3, T4, T5, T6, T7, TResult>(
        this IObservableAsync<T1> src1,
        IObservableAsync<T2> src2,
        IObservableAsync<T3> src3,
        IObservableAsync<T4> src4,
        IObservableAsync<T5> src5,
        IObservableAsync<T6> src6,
        IObservableAsync<T7> src7,
        Func<T1, T2, T3, T4, T5, T6, T7, TResult> selector) => new CombineLatest7ObservableAsync<T1, T2, T3, T4, T5, T6, T7, TResult>(src1, src2, src3, src4, src5, src6, src7, selector);

    /// <summary>
    /// Combines the latest values from eight asynchronous observable sources into a single observable sequence, using
    /// the specified selector function to produce results.
    /// </summary>
    /// <remarks>The resulting observable will not emit a value until each source observable has produced at
    /// least one value. Subsequent emissions occur whenever any source produces a new value, using the latest values
    /// from all sources. If any source observable completes or fails, the resulting sequence will complete or fail
    /// accordingly.</remarks>
    /// <typeparam name="T1">The type of the elements in the first source observable.</typeparam>
    /// <typeparam name="T2">The type of the elements in the second source observable.</typeparam>
    /// <typeparam name="T3">The type of the elements in the third source observable.</typeparam>
    /// <typeparam name="T4">The type of the elements in the fourth source observable.</typeparam>
    /// <typeparam name="T5">The type of the elements in the fifth source observable.</typeparam>
    /// <typeparam name="T6">The type of the elements in the sixth source observable.</typeparam>
    /// <typeparam name="T7">The type of the elements in the seventh source observable.</typeparam>
    /// <typeparam name="T8">The type of the elements in the eighth source observable.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the selector function.</typeparam>
    /// <param name="src1">The first source observable whose latest value will be combined.</param>
    /// <param name="src2">The second source observable whose latest value will be combined.</param>
    /// <param name="src3">The third source observable whose latest value will be combined.</param>
    /// <param name="src4">The fourth source observable whose latest value will be combined.</param>
    /// <param name="src5">The fifth source observable whose latest value will be combined.</param>
    /// <param name="src6">The sixth source observable whose latest value will be combined.</param>
    /// <param name="src7">The seventh source observable whose latest value will be combined.</param>
    /// <param name="src8">The eighth source observable whose latest value will be combined.</param>
    /// <param name="selector">A function that combines the latest values from each source observable into a result value.</param>
    /// <returns>An observable sequence that emits a result each time any of the source observables produces a new value, after
    /// all sources have emitted at least one value.</returns>
    public static IObservableAsync<TResult> CombineLatest<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(
        this IObservableAsync<T1> src1,
        IObservableAsync<T2> src2,
        IObservableAsync<T3> src3,
        IObservableAsync<T4> src4,
        IObservableAsync<T5> src5,
        IObservableAsync<T6> src6,
        IObservableAsync<T7> src7,
        IObservableAsync<T8> src8,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> selector) => new CombineLatest8ObservableAsync<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(src1, src2, src3, src4, src5, src6, src7, src8, selector);

    private sealed class CombineLatest2ObservableAsync<T1, T2, TResult>(IObservableAsync<T1> src1, IObservableAsync<T2> src2, Func<T1, T2, TResult> selector) : ObservableAsync<TResult>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<TResult> observer, CancellationToken cancellationToken)
        {
            var subscription = new CombineLatestSubscription(observer, src1, src2, selector);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }

            return subscription;
        }

        private sealed class CombineLatestSubscription : IAsyncDisposable
        {
            private readonly AsyncGate _gate = new();
            private readonly CancellationTokenSource _disposeCts = new();
            private readonly CancellationToken _disposeCancellationToken;
            private readonly IObserverAsync<TResult> _observer;
            private readonly IObservableAsync<T1> _src1;
            private readonly IObservableAsync<T2> _src2;
            private readonly Func<T1, T2, TResult> _selector;
            private IAsyncDisposable? _d1;
            private IAsyncDisposable? _d2;

            private Optional<T1> _val1 = Optional<T1>.Empty;
            private Optional<T2> _val2 = Optional<T2>.Empty;

            private bool _done1;
            private bool _done2;
            private int _disposed;

            public CombineLatestSubscription(IObserverAsync<TResult> observer, IObservableAsync<T1> src1, IObservableAsync<T2> src2, Func<T1, T2, TResult> selector)
            {
                _observer = observer;
                _src1 = src1;
                _src2 = src2;
                _selector = selector;
                _disposeCancellationToken = _disposeCts.Token;
            }

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                _d1 = await _src1.SubscribeAsync(OnNext_1, OnErrorResume, OnCompleted_1, cancellationToken);
                _d2 = await _src2.SubscribeAsync(OnNext_2, OnErrorResume, OnCompleted_2, cancellationToken);
            }

            /// <summary>
            /// Asynchronously releases resources used by the instance.
            /// </summary>
            /// <returns>A ValueTask that represents the asynchronous dispose operation.</returns>
            public ValueTask DisposeAsync() => CompleteAsync(null);

            private ValueTask OnNext_1(T1 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val1 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2))
                {
                    return OnNextCombined(v1, v2, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_1(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done1 = true;
                    shouldComplete = _done1 && _done2;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_2(T2 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val2 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2))
                {
                    return OnNextCombined(v1, v2, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_2(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done2 = true;
                    shouldComplete = _done1 && _done2;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private async ValueTask OnNextCombined(T1 v1, T2 v2, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    var v = _selector(v1, v2);
                    await _observer.OnNextAsync(v, linkedCts.Token);
                }
            }

            private async ValueTask OnErrorResume(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private async ValueTask CompleteAsync(Result? result)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return;
                }

                _disposeCts.Cancel();

                if (_d1 is not null)
                {
                    await _d1.DisposeAsync();
                }

                if (_d2 is not null)
                {
                    await _d2.DisposeAsync();
                }

                if (result is not null)
                {
                    await _observer.OnCompletedAsync(result.Value);
                }

                _disposeCts.Dispose();
                _gate.Dispose();
            }
        }
    }

    private sealed class CombineLatest3ObservableAsync<T1, T2, T3, TResult>(IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, Func<T1, T2, T3, TResult> selector) : ObservableAsync<TResult>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<TResult> observer, CancellationToken cancellationToken)
        {
            var subscription = new CombineLatestSubscription(observer, src1, src2, src3, selector);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }

            return subscription;
        }

        private sealed class CombineLatestSubscription : IAsyncDisposable
        {
            private readonly AsyncGate _gate = new();
            private readonly CancellationTokenSource _disposeCts = new();
            private readonly CancellationToken _disposeCancellationToken;
            private readonly IObserverAsync<TResult> _observer;
            private readonly IObservableAsync<T1> _src1;
            private readonly IObservableAsync<T2> _src2;
            private readonly IObservableAsync<T3> _src3;
            private readonly Func<T1, T2, T3, TResult> _selector;
            private IAsyncDisposable? _d1;
            private IAsyncDisposable? _d2;
            private IAsyncDisposable? _d3;

            private Optional<T1> _val1 = Optional<T1>.Empty;
            private Optional<T2> _val2 = Optional<T2>.Empty;
            private Optional<T3> _val3 = Optional<T3>.Empty;

            private bool _done1;
            private bool _done2;
            private bool _done3;
            private int _disposed;

            public CombineLatestSubscription(IObserverAsync<TResult> observer, IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, Func<T1, T2, T3, TResult> selector)
            {
                _observer = observer;
                _src1 = src1;
                _src2 = src2;
                _src3 = src3;
                _selector = selector;
                _disposeCancellationToken = _disposeCts.Token;
            }

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                _d1 = await _src1.SubscribeAsync(OnNext_1, OnErrorResume, OnCompleted_1, cancellationToken);
                _d2 = await _src2.SubscribeAsync(OnNext_2, OnErrorResume, OnCompleted_2, cancellationToken);
                _d3 = await _src3.SubscribeAsync(OnNext_3, OnErrorResume, OnCompleted_3, cancellationToken);
            }

            public ValueTask DisposeAsync() => CompleteAsync(null);

            private ValueTask OnNext_1(T1 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val1 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3))
                {
                    return OnNextCombined(v1, v2, v3, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_1(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done1 = true;
                    shouldComplete = _done1 && _done2 && _done3;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_2(T2 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val2 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3))
                {
                    return OnNextCombined(v1, v2, v3, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_2(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done2 = true;
                    shouldComplete = _done1 && _done2 && _done3;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_3(T3 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val3 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3))
                {
                    return OnNextCombined(v1, v2, v3, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_3(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done3 = true;
                    shouldComplete = _done1 && _done2 && _done3;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private async ValueTask OnNextCombined(T1 v1, T2 v2, T3 v3, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    var v = _selector(v1, v2, v3);
                    await _observer.OnNextAsync(v, linkedCts.Token);
                }
            }

            private async ValueTask OnErrorResume(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private async ValueTask CompleteAsync(Result? result)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return;
                }

                _disposeCts.Cancel();

                if (_d1 is not null)
                {
                    await _d1.DisposeAsync();
                }

                if (_d2 is not null)
                {
                    await _d2.DisposeAsync();
                }

                if (_d3 is not null)
                {
                    await _d3.DisposeAsync();
                }

                if (result is not null)
                {
                    await _observer.OnCompletedAsync(result.Value);
                }

                _disposeCts.Dispose();
                _gate.Dispose();
            }
        }
    }

    private sealed class CombineLatest4ObservableAsync<T1, T2, T3, T4, TResult>(IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, IObservableAsync<T4> src4, Func<T1, T2, T3, T4, TResult> selector) : ObservableAsync<TResult>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<TResult> observer, CancellationToken cancellationToken)
        {
            var subscription = new CombineLatestSubscription(observer, src1, src2, src3, src4, selector);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }

            return subscription;
        }

        private sealed class CombineLatestSubscription : IAsyncDisposable
        {
            private readonly AsyncGate _gate = new();
            private readonly CancellationTokenSource _disposeCts = new();
            private readonly CancellationToken _disposeCancellationToken;
            private readonly IObserverAsync<TResult> _observer;
            private readonly IObservableAsync<T1> _src1;
            private readonly IObservableAsync<T2> _src2;
            private readonly IObservableAsync<T3> _src3;
            private readonly IObservableAsync<T4> _src4;
            private readonly Func<T1, T2, T3, T4, TResult> _selector;
            private IAsyncDisposable? _d1;
            private IAsyncDisposable? _d2;
            private IAsyncDisposable? _d3;
            private IAsyncDisposable? _d4;

            private Optional<T1> _val1 = Optional<T1>.Empty;
            private Optional<T2> _val2 = Optional<T2>.Empty;
            private Optional<T3> _val3 = Optional<T3>.Empty;
            private Optional<T4> _val4 = Optional<T4>.Empty;

            private bool _done1;
            private bool _done2;
            private bool _done3;
            private bool _done4;
            private int _disposed;

            public CombineLatestSubscription(IObserverAsync<TResult> observer, IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, IObservableAsync<T4> src4, Func<T1, T2, T3, T4, TResult> selector)
            {
                _observer = observer;
                _src1 = src1;
                _src2 = src2;
                _src3 = src3;
                _src4 = src4;
                _selector = selector;
                _disposeCancellationToken = _disposeCts.Token;
            }

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                _d1 = await _src1.SubscribeAsync(OnNext_1, OnErrorResume, OnCompleted_1, cancellationToken);
                _d2 = await _src2.SubscribeAsync(OnNext_2, OnErrorResume, OnCompleted_2, cancellationToken);
                _d3 = await _src3.SubscribeAsync(OnNext_3, OnErrorResume, OnCompleted_3, cancellationToken);
                _d4 = await _src4.SubscribeAsync(OnNext_4, OnErrorResume, OnCompleted_4, cancellationToken);
            }

            public ValueTask DisposeAsync() => CompleteAsync(null);

            private ValueTask OnNext_1(T1 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val1 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4))
                {
                    return OnNextCombined(v1, v2, v3, v4, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_1(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done1 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_2(T2 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val2 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4))
                {
                    return OnNextCombined(v1, v2, v3, v4, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_2(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done2 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_3(T3 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val3 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4))
                {
                    return OnNextCombined(v1, v2, v3, v4, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_3(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done3 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_4(T4 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val4 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4))
                {
                    return OnNextCombined(v1, v2, v3, v4, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_4(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done4 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private async ValueTask OnNextCombined(T1 v1, T2 v2, T3 v3, T4 v4, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    var v = _selector(v1, v2, v3, v4);
                    await _observer.OnNextAsync(v, linkedCts.Token);
                }
            }

            private async ValueTask OnErrorResume(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private async ValueTask CompleteAsync(Result? result)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return;
                }

                _disposeCts.Cancel();

                if (_d1 is not null)
                {
                    await _d1.DisposeAsync();
                }

                if (_d2 is not null)
                {
                    await _d2.DisposeAsync();
                }

                if (_d3 is not null)
                {
                    await _d3.DisposeAsync();
                }

                if (_d4 is not null)
                {
                    await _d4.DisposeAsync();
                }

                if (result is not null)
                {
                    await _observer.OnCompletedAsync(result.Value);
                }

                _disposeCts.Dispose();
                _gate.Dispose();
            }
        }
    }

    private sealed class CombineLatest5ObservableAsync<T1, T2, T3, T4, T5, TResult>(IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, IObservableAsync<T4> src4, IObservableAsync<T5> src5, Func<T1, T2, T3, T4, T5, TResult> selector) : ObservableAsync<TResult>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<TResult> observer, CancellationToken cancellationToken)
        {
            var subscription = new CombineLatestSubscription(observer, src1, src2, src3, src4, src5, selector);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }

            return subscription;
        }

        private sealed class CombineLatestSubscription : IAsyncDisposable
        {
            private readonly AsyncGate _gate = new();
            private readonly CancellationTokenSource _disposeCts = new();
            private readonly CancellationToken _disposeCancellationToken;
            private readonly IObserverAsync<TResult> _observer;
            private readonly IObservableAsync<T1> _src1;
            private readonly IObservableAsync<T2> _src2;
            private readonly IObservableAsync<T3> _src3;
            private readonly IObservableAsync<T4> _src4;
            private readonly IObservableAsync<T5> _src5;
            private readonly Func<T1, T2, T3, T4, T5, TResult> _selector;
            private IAsyncDisposable? _d1;
            private IAsyncDisposable? _d2;
            private IAsyncDisposable? _d3;
            private IAsyncDisposable? _d4;
            private IAsyncDisposable? _d5;

            private Optional<T1> _val1 = Optional<T1>.Empty;
            private Optional<T2> _val2 = Optional<T2>.Empty;
            private Optional<T3> _val3 = Optional<T3>.Empty;
            private Optional<T4> _val4 = Optional<T4>.Empty;
            private Optional<T5> _val5 = Optional<T5>.Empty;

            private bool _done1;
            private bool _done2;
            private bool _done3;
            private bool _done4;
            private bool _done5;
            private int _disposed;

            public CombineLatestSubscription(IObserverAsync<TResult> observer, IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, IObservableAsync<T4> src4, IObservableAsync<T5> src5, Func<T1, T2, T3, T4, T5, TResult> selector)
            {
                _observer = observer;
                _src1 = src1;
                _src2 = src2;
                _src3 = src3;
                _src4 = src4;
                _src5 = src5;
                _selector = selector;
                _disposeCancellationToken = _disposeCts.Token;
            }

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                _d1 = await _src1.SubscribeAsync(OnNext_1, OnErrorResume, OnCompleted_1, cancellationToken);
                _d2 = await _src2.SubscribeAsync(OnNext_2, OnErrorResume, OnCompleted_2, cancellationToken);
                _d3 = await _src3.SubscribeAsync(OnNext_3, OnErrorResume, OnCompleted_3, cancellationToken);
                _d4 = await _src4.SubscribeAsync(OnNext_4, OnErrorResume, OnCompleted_4, cancellationToken);
                _d5 = await _src5.SubscribeAsync(OnNext_5, OnErrorResume, OnCompleted_5, cancellationToken);
            }

            public ValueTask DisposeAsync() => CompleteAsync(null);

            private ValueTask OnNext_1(T1 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val1 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_1(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done1 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_2(T2 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val2 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_2(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done2 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_3(T3 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val3 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_3(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done3 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_4(T4 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val4 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_4(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done4 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_5(T5 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val5 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_5(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done5 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private async ValueTask OnNextCombined(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    var v = _selector(v1, v2, v3, v4, v5);
                    await _observer.OnNextAsync(v, linkedCts.Token);
                }
            }

            private async ValueTask OnErrorResume(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private async ValueTask CompleteAsync(Result? result)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return;
                }

                _disposeCts.Cancel();

                if (_d1 is not null)
                {
                    await _d1.DisposeAsync();
                }

                if (_d2 is not null)
                {
                    await _d2.DisposeAsync();
                }

                if (_d3 is not null)
                {
                    await _d3.DisposeAsync();
                }

                if (_d4 is not null)
                {
                    await _d4.DisposeAsync();
                }

                if (_d5 is not null)
                {
                    await _d5.DisposeAsync();
                }

                if (result is not null)
                {
                    await _observer.OnCompletedAsync(result.Value);
                }

                _disposeCts.Dispose();
                _gate.Dispose();
            }
        }
    }

    private sealed class CombineLatest6ObservableAsync<T1, T2, T3, T4, T5, T6, TResult>(IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, IObservableAsync<T4> src4, IObservableAsync<T5> src5, IObservableAsync<T6> src6, Func<T1, T2, T3, T4, T5, T6, TResult> selector) : ObservableAsync<TResult>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<TResult> observer, CancellationToken cancellationToken)
        {
            var subscription = new CombineLatestSubscription(observer, src1, src2, src3, src4, src5, src6, selector);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }

            return subscription;
        }

        private sealed class CombineLatestSubscription : IAsyncDisposable
        {
            private readonly AsyncGate _gate = new();
            private readonly CancellationTokenSource _disposeCts = new();
            private readonly CancellationToken _disposeCancellationToken;
            private readonly IObserverAsync<TResult> _observer;
            private readonly IObservableAsync<T1> _src1;
            private readonly IObservableAsync<T2> _src2;
            private readonly IObservableAsync<T3> _src3;
            private readonly IObservableAsync<T4> _src4;
            private readonly IObservableAsync<T5> _src5;
            private readonly IObservableAsync<T6> _src6;
            private readonly Func<T1, T2, T3, T4, T5, T6, TResult> _selector;
            private IAsyncDisposable? _d1;
            private IAsyncDisposable? _d2;
            private IAsyncDisposable? _d3;
            private IAsyncDisposable? _d4;
            private IAsyncDisposable? _d5;
            private IAsyncDisposable? _d6;

            private Optional<T1> _val1 = Optional<T1>.Empty;
            private Optional<T2> _val2 = Optional<T2>.Empty;
            private Optional<T3> _val3 = Optional<T3>.Empty;
            private Optional<T4> _val4 = Optional<T4>.Empty;
            private Optional<T5> _val5 = Optional<T5>.Empty;
            private Optional<T6> _val6 = Optional<T6>.Empty;

            private bool _done1;
            private bool _done2;
            private bool _done3;
            private bool _done4;
            private bool _done5;
            private bool _done6;
            private int _disposed;

            public CombineLatestSubscription(IObserverAsync<TResult> observer, IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, IObservableAsync<T4> src4, IObservableAsync<T5> src5, IObservableAsync<T6> src6, Func<T1, T2, T3, T4, T5, T6, TResult> selector)
            {
                _observer = observer;
                _src1 = src1;
                _src2 = src2;
                _src3 = src3;
                _src4 = src4;
                _src5 = src5;
                _src6 = src6;
                _selector = selector;
                _disposeCancellationToken = _disposeCts.Token;
            }

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                _d1 = await _src1.SubscribeAsync(OnNext_1, OnErrorResume, OnCompleted_1, cancellationToken);
                _d2 = await _src2.SubscribeAsync(OnNext_2, OnErrorResume, OnCompleted_2, cancellationToken);
                _d3 = await _src3.SubscribeAsync(OnNext_3, OnErrorResume, OnCompleted_3, cancellationToken);
                _d4 = await _src4.SubscribeAsync(OnNext_4, OnErrorResume, OnCompleted_4, cancellationToken);
                _d5 = await _src5.SubscribeAsync(OnNext_5, OnErrorResume, OnCompleted_5, cancellationToken);
                _d6 = await _src6.SubscribeAsync(OnNext_6, OnErrorResume, OnCompleted_6, cancellationToken);
            }

            public ValueTask DisposeAsync() => CompleteAsync(null);

            private ValueTask OnNext_1(T1 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val1 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_1(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done1 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_2(T2 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val2 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_2(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done2 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_3(T3 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val3 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_3(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done3 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_4(T4 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val4 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_4(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done4 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_5(T5 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val5 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_5(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done5 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_6(T6 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val6 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_6(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done6 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private async ValueTask OnNextCombined(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    var v = _selector(v1, v2, v3, v4, v5, v6);
                    await _observer.OnNextAsync(v, linkedCts.Token);
                }
            }

            private async ValueTask OnErrorResume(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private async ValueTask CompleteAsync(Result? result)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return;
                }

                _disposeCts.Cancel();

                if (_d1 is not null)
                {
                    await _d1.DisposeAsync();
                }

                if (_d2 is not null)
                {
                    await _d2.DisposeAsync();
                }

                if (_d3 is not null)
                {
                    await _d3.DisposeAsync();
                }

                if (_d4 is not null)
                {
                    await _d4.DisposeAsync();
                }

                if (_d5 is not null)
                {
                    await _d5.DisposeAsync();
                }

                if (_d6 is not null)
                {
                    await _d6.DisposeAsync();
                }

                if (result is not null)
                {
                    await _observer.OnCompletedAsync(result.Value);
                }

                _disposeCts.Dispose();
                _gate.Dispose();
            }
        }
    }

    private sealed class CombineLatest7ObservableAsync<T1, T2, T3, T4, T5, T6, T7, TResult>(IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, IObservableAsync<T4> src4, IObservableAsync<T5> src5, IObservableAsync<T6> src6, IObservableAsync<T7> src7, Func<T1, T2, T3, T4, T5, T6, T7, TResult> selector) : ObservableAsync<TResult>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<TResult> observer, CancellationToken cancellationToken)
        {
            var subscription = new CombineLatestSubscription(observer, src1, src2, src3, src4, src5, src6, src7, selector);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }

            return subscription;
        }

        private sealed class CombineLatestSubscription : IAsyncDisposable
        {
            private readonly AsyncGate _gate = new();
            private readonly CancellationTokenSource _disposeCts = new();
            private readonly CancellationToken _disposeCancellationToken;
            private readonly IObserverAsync<TResult> _observer;
            private readonly IObservableAsync<T1> _src1;
            private readonly IObservableAsync<T2> _src2;
            private readonly IObservableAsync<T3> _src3;
            private readonly IObservableAsync<T4> _src4;
            private readonly IObservableAsync<T5> _src5;
            private readonly IObservableAsync<T6> _src6;
            private readonly IObservableAsync<T7> _src7;
            private readonly Func<T1, T2, T3, T4, T5, T6, T7, TResult> _selector;
            private IAsyncDisposable? _d1;
            private IAsyncDisposable? _d2;
            private IAsyncDisposable? _d3;
            private IAsyncDisposable? _d4;
            private IAsyncDisposable? _d5;
            private IAsyncDisposable? _d6;
            private IAsyncDisposable? _d7;

            private Optional<T1> _val1 = Optional<T1>.Empty;
            private Optional<T2> _val2 = Optional<T2>.Empty;
            private Optional<T3> _val3 = Optional<T3>.Empty;
            private Optional<T4> _val4 = Optional<T4>.Empty;
            private Optional<T5> _val5 = Optional<T5>.Empty;
            private Optional<T6> _val6 = Optional<T6>.Empty;
            private Optional<T7> _val7 = Optional<T7>.Empty;

            private bool _done1;
            private bool _done2;
            private bool _done3;
            private bool _done4;
            private bool _done5;
            private bool _done6;
            private bool _done7;
            private int _disposed;

            public CombineLatestSubscription(IObserverAsync<TResult> observer, IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, IObservableAsync<T4> src4, IObservableAsync<T5> src5, IObservableAsync<T6> src6, IObservableAsync<T7> src7, Func<T1, T2, T3, T4, T5, T6, T7, TResult> selector)
            {
                _observer = observer;
                _src1 = src1;
                _src2 = src2;
                _src3 = src3;
                _src4 = src4;
                _src5 = src5;
                _src6 = src6;
                _src7 = src7;
                _selector = selector;
                _disposeCancellationToken = _disposeCts.Token;
            }

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                _d1 = await _src1.SubscribeAsync(OnNext_1, OnErrorResume, OnCompleted_1, cancellationToken);
                _d2 = await _src2.SubscribeAsync(OnNext_2, OnErrorResume, OnCompleted_2, cancellationToken);
                _d3 = await _src3.SubscribeAsync(OnNext_3, OnErrorResume, OnCompleted_3, cancellationToken);
                _d4 = await _src4.SubscribeAsync(OnNext_4, OnErrorResume, OnCompleted_4, cancellationToken);
                _d5 = await _src5.SubscribeAsync(OnNext_5, OnErrorResume, OnCompleted_5, cancellationToken);
                _d6 = await _src6.SubscribeAsync(OnNext_6, OnErrorResume, OnCompleted_6, cancellationToken);
                _d7 = await _src7.SubscribeAsync(OnNext_7, OnErrorResume, OnCompleted_7, cancellationToken);
            }

            public ValueTask DisposeAsync() => CompleteAsync(null);

            private ValueTask OnNext_1(T1 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val1 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_1(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done1 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_2(T2 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val2 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_2(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done2 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_3(T3 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val3 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_3(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done3 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_4(T4 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val4 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_4(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done4 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_5(T5 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val5 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_5(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done5 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_6(T6 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val6 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_6(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done6 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_7(T7 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val7 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_7(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done7 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private async ValueTask OnNextCombined(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    var v = _selector(v1, v2, v3, v4, v5, v6, v7);
                    await _observer.OnNextAsync(v, linkedCts.Token);
                }
            }

            private async ValueTask OnErrorResume(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private async ValueTask CompleteAsync(Result? result)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return;
                }

                _disposeCts.Cancel();

                if (_d1 is not null)
                {
                    await _d1.DisposeAsync();
                }

                if (_d2 is not null)
                {
                    await _d2.DisposeAsync();
                }

                if (_d3 is not null)
                {
                    await _d3.DisposeAsync();
                }

                if (_d4 is not null)
                {
                    await _d4.DisposeAsync();
                }

                if (_d5 is not null)
                {
                    await _d5.DisposeAsync();
                }

                if (_d6 is not null)
                {
                    await _d6.DisposeAsync();
                }

                if (_d7 is not null)
                {
                    await _d7.DisposeAsync();
                }

                if (result is not null)
                {
                    await _observer.OnCompletedAsync(result.Value);
                }

                _disposeCts.Dispose();
                _gate.Dispose();
            }
        }
    }

    private sealed class CombineLatest8ObservableAsync<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, IObservableAsync<T4> src4, IObservableAsync<T5> src5, IObservableAsync<T6> src6, IObservableAsync<T7> src7, IObservableAsync<T8> src8, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> selector) : ObservableAsync<TResult>
    {
        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(IObserverAsync<TResult> observer, CancellationToken cancellationToken)
        {
            var subscription = new CombineLatestSubscription(observer, src1, src2, src3, src4, src5, src6, src7, src8, selector);
            try
            {
                await subscription.SubscribeAsync(cancellationToken);
            }
            catch
            {
                await subscription.DisposeAsync();
                throw;
            }

            return subscription;
        }

        private sealed class CombineLatestSubscription : IAsyncDisposable
        {
            private readonly AsyncGate _gate = new();
            private readonly CancellationTokenSource _disposeCts = new();
            private readonly CancellationToken _disposeCancellationToken;
            private readonly IObserverAsync<TResult> _observer;
            private readonly IObservableAsync<T1> _src1;
            private readonly IObservableAsync<T2> _src2;
            private readonly IObservableAsync<T3> _src3;
            private readonly IObservableAsync<T4> _src4;
            private readonly IObservableAsync<T5> _src5;
            private readonly IObservableAsync<T6> _src6;
            private readonly IObservableAsync<T7> _src7;
            private readonly IObservableAsync<T8> _src8;
            private readonly Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> _selector;
            private IAsyncDisposable? _d1;
            private IAsyncDisposable? _d2;
            private IAsyncDisposable? _d3;
            private IAsyncDisposable? _d4;
            private IAsyncDisposable? _d5;
            private IAsyncDisposable? _d6;
            private IAsyncDisposable? _d7;
            private IAsyncDisposable? _d8;

            private Optional<T1> _val1 = Optional<T1>.Empty;
            private Optional<T2> _val2 = Optional<T2>.Empty;
            private Optional<T3> _val3 = Optional<T3>.Empty;
            private Optional<T4> _val4 = Optional<T4>.Empty;
            private Optional<T5> _val5 = Optional<T5>.Empty;
            private Optional<T6> _val6 = Optional<T6>.Empty;
            private Optional<T7> _val7 = Optional<T7>.Empty;
            private Optional<T8> _val8 = Optional<T8>.Empty;

            private bool _done1;
            private bool _done2;
            private bool _done3;
            private bool _done4;
            private bool _done5;
            private bool _done6;
            private bool _done7;
            private bool _done8;
            private int _disposed;

            public CombineLatestSubscription(IObserverAsync<TResult> observer, IObservableAsync<T1> src1, IObservableAsync<T2> src2, IObservableAsync<T3> src3, IObservableAsync<T4> src4, IObservableAsync<T5> src5, IObservableAsync<T6> src6, IObservableAsync<T7> src7, IObservableAsync<T8> src8, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> selector)
            {
                _observer = observer;
                _src1 = src1;
                _src2 = src2;
                _src3 = src3;
                _src4 = src4;
                _src5 = src5;
                _src6 = src6;
                _src7 = src7;
                _src8 = src8;
                _selector = selector;
                _disposeCancellationToken = _disposeCts.Token;
            }

            public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
            {
                _d1 = await _src1.SubscribeAsync(OnNext_1, OnErrorResume, OnCompleted_1, cancellationToken);
                _d2 = await _src2.SubscribeAsync(OnNext_2, OnErrorResume, OnCompleted_2, cancellationToken);
                _d3 = await _src3.SubscribeAsync(OnNext_3, OnErrorResume, OnCompleted_3, cancellationToken);
                _d4 = await _src4.SubscribeAsync(OnNext_4, OnErrorResume, OnCompleted_4, cancellationToken);
                _d5 = await _src5.SubscribeAsync(OnNext_5, OnErrorResume, OnCompleted_5, cancellationToken);
                _d6 = await _src6.SubscribeAsync(OnNext_6, OnErrorResume, OnCompleted_6, cancellationToken);
                _d7 = await _src7.SubscribeAsync(OnNext_7, OnErrorResume, OnCompleted_7, cancellationToken);
                _d8 = await _src8.SubscribeAsync(OnNext_8, OnErrorResume, OnCompleted_8, cancellationToken);
            }

            public ValueTask DisposeAsync() => CompleteAsync(null);

            private ValueTask OnNext_1(T1 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val1 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7) && _val8.TryGetValue(out var v8))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, v8, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_1(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done1 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7 && _done8;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_2(T2 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val2 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7) && _val8.TryGetValue(out var v8))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, v8, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_2(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done2 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7 && _done8;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_3(T3 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val3 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7) && _val8.TryGetValue(out var v8))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, v8, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_3(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done3 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7 && _done8;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_4(T4 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val4 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7) && _val8.TryGetValue(out var v8))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, v8, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_4(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done4 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7 && _done8;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_5(T5 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val5 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7) && _val8.TryGetValue(out var v8))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, v8, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_5(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done5 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7 && _done8;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_6(T6 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val6 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7) && _val8.TryGetValue(out var v8))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, v8, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_6(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done6 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7 && _done8;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_7(T7 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val7 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7) && _val8.TryGetValue(out var v8))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, v8, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_7(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done7 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7 && _done8;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private ValueTask OnNext_8(T8 value, CancellationToken cancellationToken)
            {
                lock (_disposeCts)
                {
                    _val8 = new(value);
                }

                if (_val1.TryGetValue(out var v1) && _val2.TryGetValue(out var v2) && _val3.TryGetValue(out var v3) && _val4.TryGetValue(out var v4) && _val5.TryGetValue(out var v5) && _val6.TryGetValue(out var v6) && _val7.TryGetValue(out var v7) && _val8.TryGetValue(out var v8))
                {
                    return OnNextCombined(v1, v2, v3, v4, v5, v6, v7, v8, cancellationToken);
                }

                return default;
            }

            private ValueTask OnCompleted_8(Result result)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                bool shouldComplete;
                lock (_disposeCts)
                {
                    _done8 = true;
                    shouldComplete = _done1 && _done2 && _done3 && _done4 && _done5 && _done6 && _done7 && _done8;
                }

                return shouldComplete ? _observer.OnCompletedAsync(result) : default;
            }

            private async ValueTask OnNextCombined(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    var v = _selector(v1, v2, v3, v4, v5, v6, v7, v8);
                    await _observer.OnNextAsync(v, linkedCts.Token);
                }
            }

            private async ValueTask OnErrorResume(Exception error, CancellationToken cancellationToken)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationToken);
                using (await _gate.LockAsync())
                {
                    if (_disposed == 1)
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(error, linkedCts.Token);
                }
            }

            private async ValueTask CompleteAsync(Result? result)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return;
                }

                _disposeCts.Cancel();

                if (_d1 is not null)
                {
                    await _d1.DisposeAsync();
                }

                if (_d2 is not null)
                {
                    await _d2.DisposeAsync();
                }

                if (_d3 is not null)
                {
                    await _d3.DisposeAsync();
                }

                if (_d4 is not null)
                {
                    await _d4.DisposeAsync();
                }

                if (_d5 is not null)
                {
                    await _d5.DisposeAsync();
                }

                if (_d6 is not null)
                {
                    await _d6.DisposeAsync();
                }

                if (_d7 is not null)
                {
                    await _d7.DisposeAsync();
                }

                if (_d8 is not null)
                {
                    await _d8.DisposeAsync();
                }

                if (result is not null)
                {
                    await _observer.OnCompletedAsync(result.Value);
                }

                _disposeCts.Dispose();
                _gate.Dispose();
            }
        }
    }
}
