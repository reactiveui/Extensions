// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides the arity-10 (<c>ten</c>-source) <c>CombineLatest</c> extension method
/// and its supporting internal observable + subscription types.
/// </summary>
[SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "Has more than 7 parameters - just expected for arity-N CombineLatest operator surface.")]
public static partial class ObservableAsync
{
    /// <summary>
    /// Combines the latest values from ten asynchronous observable sources into a single
    /// sequence, projecting them through <paramref name="selector"/> whenever any source emits.
    /// </summary>
    /// <remarks>
    /// The returned sequence does not produce a value until every source has emitted at least
    /// once. After that, each new value from any source produces a fresh projection using the
    /// most recent value from each. Completion / failure of any source propagates downstream.
    /// </remarks>
    /// <typeparam name="T1">The element type of source 1.</typeparam>
    /// <typeparam name="T2">The element type of source 2.</typeparam>
    /// <typeparam name="T3">The element type of source 3.</typeparam>
    /// <typeparam name="T4">The element type of source 4.</typeparam>
    /// <typeparam name="T5">The element type of source 5.</typeparam>
    /// <typeparam name="T6">The element type of source 6.</typeparam>
    /// <typeparam name="T7">The element type of source 7.</typeparam>
    /// <typeparam name="T8">The element type of source 8.</typeparam>
    /// <typeparam name="T9">The element type of source 9.</typeparam>
    /// <typeparam name="T10">The element type of source 10.</typeparam>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="src1">Source observable 1 whose latest value is combined.</param>
    /// <param name="src2">Source observable 2 whose latest value is combined.</param>
    /// <param name="src3">Source observable 3 whose latest value is combined.</param>
    /// <param name="src4">Source observable 4 whose latest value is combined.</param>
    /// <param name="src5">Source observable 5 whose latest value is combined.</param>
    /// <param name="src6">Source observable 6 whose latest value is combined.</param>
    /// <param name="src7">Source observable 7 whose latest value is combined.</param>
    /// <param name="src8">Source observable 8 whose latest value is combined.</param>
    /// <param name="src9">Source observable 9 whose latest value is combined.</param>
    /// <param name="src10">Source observable 10 whose latest value is combined.</param>
    /// <param name="selector">Projects the latest value of every source into a result.</param>
    /// <returns>An observable sequence of projected results.</returns>
    public static IObservableAsync<TResult> CombineLatest<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(
        this IObservableAsync<T1> src1,
        IObservableAsync<T2> src2,
        IObservableAsync<T3> src3,
        IObservableAsync<T4> src4,
        IObservableAsync<T5> src5,
        IObservableAsync<T6> src6,
        IObservableAsync<T7> src7,
        IObservableAsync<T8> src8,
        IObservableAsync<T9> src9,
        IObservableAsync<T10> src10,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> selector) =>
        new CombineLatest10ObservableAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(
            new(src1, src2, src3, src4, src5, src6, src7, src8, src9, src10),
            selector);

    /// <summary>
    /// Async observable that combines the latest values from ten source sequences using a selector.
    /// </summary>
    /// <typeparam name="T1">Element type of source 1.</typeparam>
    /// <typeparam name="T2">Element type of source 2.</typeparam>
    /// <typeparam name="T3">Element type of source 3.</typeparam>
    /// <typeparam name="T4">Element type of source 4.</typeparam>
    /// <typeparam name="T5">Element type of source 5.</typeparam>
    /// <typeparam name="T6">Element type of source 6.</typeparam>
    /// <typeparam name="T7">Element type of source 7.</typeparam>
    /// <typeparam name="T8">Element type of source 8.</typeparam>
    /// <typeparam name="T9">Element type of source 9.</typeparam>
    /// <typeparam name="T10">Element type of source 10.</typeparam>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    internal sealed class CombineLatest10ObservableAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(
        CombineLatest10ObservableAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>.Sources sources,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> selector) : ObservableAsync<TResult>
    {
        /// <summary>
        /// Bundles the ten source observables so the subscription constructor stays at three
        /// parameters (observer, sources, selector) regardless of arity. Sonar S107 caps method /
        /// constructor parameter count; the bundle keeps the internal types compliant.
        /// </summary>
        /// <param name="Src1">Source observable 1.</param>
        /// <param name="Src2">Source observable 2.</param>
        /// <param name="Src3">Source observable 3.</param>
        /// <param name="Src4">Source observable 4.</param>
        /// <param name="Src5">Source observable 5.</param>
        /// <param name="Src6">Source observable 6.</param>
        /// <param name="Src7">Source observable 7.</param>
        /// <param name="Src8">Source observable 8.</param>
        /// <param name="Src9">Source observable 9.</param>
        /// <param name="Src10">Source observable 10.</param>
        internal readonly record struct Sources(
            IObservableAsync<T1> Src1,
            IObservableAsync<T2> Src2,
            IObservableAsync<T3> Src3,
            IObservableAsync<T4> Src4,
            IObservableAsync<T5> Src5,
            IObservableAsync<T6> Src6,
            IObservableAsync<T7> Src7,
            IObservableAsync<T8> Src8,
            IObservableAsync<T9> Src9,
            IObservableAsync<T10> Src10);

        /// <inheritdoc/>
        protected override ValueTask<IAsyncDisposable> SubscribeAsyncCore(
            IObserverAsync<TResult> observer,
            CancellationToken cancellationToken)
        {
            var subscription = new CombineLatestSubscription(observer, sources, selector);
            subscription.LinkExternalCancellation(cancellationToken);
            return SubscriptionHelper.SubscribeAndDisposeOnFailureAsync(
                subscription,
                () => subscription.SubscribeSourcesAsync(cancellationToken));
        }

        /// <summary>
        /// Manages subscriptions to all source sequences and emits combined values via the selector.
        /// </summary>
        internal sealed class CombineLatestSubscription : IAsyncDisposable
        {
            /// <summary>Bit owned by source 1 inside <see cref="_doneFlags"/>.</summary>
            private const int Source1Bit = 1 << 0;

            /// <summary>Bit owned by source 2 inside <see cref="_doneFlags"/>.</summary>
            private const int Source2Bit = 1 << 1;

            /// <summary>Bit owned by source 3 inside <see cref="_doneFlags"/>.</summary>
            private const int Source3Bit = 1 << 2;

            /// <summary>Bit owned by source 4 inside <see cref="_doneFlags"/>.</summary>
            private const int Source4Bit = 1 << 3;

            /// <summary>Bit owned by source 5 inside <see cref="_doneFlags"/>.</summary>
            private const int Source5Bit = 1 << 4;

            /// <summary>Bit owned by source 6 inside <see cref="_doneFlags"/>.</summary>
            private const int Source6Bit = 1 << 5;

            /// <summary>Bit owned by source 7 inside <see cref="_doneFlags"/>.</summary>
            private const int Source7Bit = 1 << 6;

            /// <summary>Bit owned by source 8 inside <see cref="_doneFlags"/>.</summary>
            private const int Source8Bit = 1 << 7;

            /// <summary>Bit owned by source 9 inside <see cref="_doneFlags"/>.</summary>
            private const int Source9Bit = 1 << 8;

            /// <summary>Bit owned by source 10 inside <see cref="_doneFlags"/>.</summary>
            private const int Source10Bit = 1 << 9;

            /// <summary>Bitmask value with every source-completion bit set; the sequence completes when
            /// <see cref="_doneFlags"/> equals this value.</summary>
            private const int AllDoneMask = Source1Bit
                | Source2Bit
                | Source3Bit
                | Source4Bit
                | Source5Bit
                | Source6Bit
                | Source7Bit
                | Source8Bit
                | Source9Bit
                | Source10Bit;

            /// <summary>Serializes downstream notifications.</summary>
            private readonly AsyncGate _gate = new();

            /// <summary>Cancellation source for disposal.</summary>
            private readonly CancellationTokenSource _disposeCts = new();

            /// <summary>Cached token from <see cref="_disposeCts"/>.</summary>
            private readonly CancellationToken _disposeCancellationToken;

            /// <summary>Lock protecting the latest-values cache and completion bitmask.</summary>
#if NET9_0_OR_GREATER
            private readonly Lock _stateLock = new();
#else
            private readonly object _stateLock = new();
#endif

            /// <summary>The downstream observer.</summary>
            private readonly IObserverAsync<TResult> _observer;

            /// <summary>Bundled source observables.</summary>
            private readonly Sources _sources;

            /// <summary>The result selector function.</summary>
            private readonly Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> _selector;

            /// <summary>Subscription disposables, indexed 0..N-1 by source position.</summary>
            private readonly IAsyncDisposable?[] _subscriptions = new IAsyncDisposable?[10];

            /// <summary>Latest value from source 1.</summary>
            private Optional<T1> _val1 = Optional<T1>.Empty;

            /// <summary>Latest value from source 2.</summary>
            private Optional<T2> _val2 = Optional<T2>.Empty;

            /// <summary>Latest value from source 3.</summary>
            private Optional<T3> _val3 = Optional<T3>.Empty;

            /// <summary>Latest value from source 4.</summary>
            private Optional<T4> _val4 = Optional<T4>.Empty;

            /// <summary>Latest value from source 5.</summary>
            private Optional<T5> _val5 = Optional<T5>.Empty;

            /// <summary>Latest value from source 6.</summary>
            private Optional<T6> _val6 = Optional<T6>.Empty;

            /// <summary>Latest value from source 7.</summary>
            private Optional<T7> _val7 = Optional<T7>.Empty;

            /// <summary>Latest value from source 8.</summary>
            private Optional<T8> _val8 = Optional<T8>.Empty;

            /// <summary>Latest value from source 9.</summary>
            private Optional<T9> _val9 = Optional<T9>.Empty;

            /// <summary>Latest value from source 10.</summary>
            private Optional<T10> _val10 = Optional<T10>.Empty;

            /// <summary>Bitmask of completed sources. Bit <c>SourceNBit</c> is set when source <c>N</c>
            /// completes. Equal to <see cref="AllDoneMask"/> when every source is done.</summary>
            private int _doneFlags;

            /// <summary>Whether this subscription has been disposed.</summary>
            private int _disposed;

            /// <summary>Registration that propagates the original subscribe-token cancellation into <see cref="_disposeCts"/>.</summary>
            private CancellationTokenRegistration _externalLinkRegistration;

            /// <summary>Latest-value snapshot taken when every source has produced at least one value.</summary>
            /// <param name="V1">Latest value from source 1.</param>
            /// <param name="V2">Latest value from source 2.</param>
            /// <param name="V3">Latest value from source 3.</param>
            /// <param name="V4">Latest value from source 4.</param>
            /// <param name="V5">Latest value from source 5.</param>
            /// <param name="V6">Latest value from source 6.</param>
            /// <param name="V7">Latest value from source 7.</param>
            /// <param name="V8">Latest value from source 8.</param>
            /// <param name="V9">Latest value from source 9.</param>
            /// <param name="V10">Latest value from source 10.</param>
            internal readonly record struct Values(
                T1 V1,
                T2 V2,
                T3 V3,
                T4 V4,
                T5 V5,
                T6 V6,
                T7 V7,
                T8 V8,
                T9 V9,
                T10 V10);

            /// <summary>
            /// Initializes a new instance of the <see cref="CombineLatestSubscription"/> class.
            /// </summary>
            /// <param name="observer">The downstream observer.</param>
            /// <param name="sources">The bundled source observables.</param>
            /// <param name="selector">The selector that projects the latest values.</param>
            public CombineLatestSubscription(
                IObserverAsync<TResult> observer,
                Sources sources,
                Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> selector)
            {
                _observer = observer;
                _sources = sources;
                _selector = selector;
                _disposeCancellationToken = _disposeCts.Token;
            }

            /// <summary>
            /// Subscribes to every source observable. Renamed from the obvious <c>SubscribeAsync</c>
            /// to avoid Sonar S3218 shadowing of <see cref="ObservableAsync{TResult}.SubscribeAsync"/>.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the subscription.</param>
            /// <returns>A task representing the asynchronous subscribe operation.</returns>
            public async ValueTask SubscribeSourcesAsync(CancellationToken cancellationToken)
            {
                for (var i = 0; i < _subscriptions.Length; i++)
                {
                    _subscriptions[i] = await SubscribeAtAsync(i, cancellationToken).ConfigureAwait(false);
                }
            }

            /// <inheritdoc/>
            public ValueTask DisposeAsync() => CompleteAsync(null);

            /// <summary>
            /// Links the original subscribe-time cancellation token into this subscription's dispose chain so
            /// per-emission methods can use <see cref="_disposeCancellationToken"/> directly instead of
            /// allocating a per-emission linked CTS.
            /// </summary>
            /// <param name="external">The subscribe-time token.</param>
            internal void LinkExternalCancellation(CancellationToken external)
            {
                if (!external.CanBeCanceled || external == _disposeCancellationToken)
                {
                    return;
                }

                if (external.IsCancellationRequested)
                {
                    _disposeCts.Cancel();
                    return;
                }

                _externalLinkRegistration = external.UnsafeRegister(
                    static state => ((CancellationTokenSource)state!).Cancel(),
                    _disposeCts);
            }

            /// <summary>Handles a new value from source 1.</summary>
            /// <param name="value">The value emitted by source 1.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal async ValueTask OnNext1(T1 value, CancellationToken cancellationToken)
            {
                lock (_stateLock)
                {
                    _val1 = new(value);
                }

                await EmitLatestAsync(cancellationToken).ConfigureAwait(false);
            }

            /// <summary>Handles completion of source 1.</summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal ValueTask OnCompleted1(Result result) => OnSourceCompleted(result, Source1Bit);

            /// <summary>Handles a new value from source 2.</summary>
            /// <param name="value">The value emitted by source 2.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal async ValueTask OnNext2(T2 value, CancellationToken cancellationToken)
            {
                lock (_stateLock)
                {
                    _val2 = new(value);
                }

                await EmitLatestAsync(cancellationToken).ConfigureAwait(false);
            }

            /// <summary>Handles completion of source 2.</summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal ValueTask OnCompleted2(Result result) => OnSourceCompleted(result, Source2Bit);

            /// <summary>Handles a new value from source 3.</summary>
            /// <param name="value">The value emitted by source 3.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal async ValueTask OnNext3(T3 value, CancellationToken cancellationToken)
            {
                lock (_stateLock)
                {
                    _val3 = new(value);
                }

                await EmitLatestAsync(cancellationToken).ConfigureAwait(false);
            }

            /// <summary>Handles completion of source 3.</summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal ValueTask OnCompleted3(Result result) => OnSourceCompleted(result, Source3Bit);

            /// <summary>Handles a new value from source 4.</summary>
            /// <param name="value">The value emitted by source 4.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal async ValueTask OnNext4(T4 value, CancellationToken cancellationToken)
            {
                lock (_stateLock)
                {
                    _val4 = new(value);
                }

                await EmitLatestAsync(cancellationToken).ConfigureAwait(false);
            }

            /// <summary>Handles completion of source 4.</summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal ValueTask OnCompleted4(Result result) => OnSourceCompleted(result, Source4Bit);

            /// <summary>Handles a new value from source 5.</summary>
            /// <param name="value">The value emitted by source 5.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal async ValueTask OnNext5(T5 value, CancellationToken cancellationToken)
            {
                lock (_stateLock)
                {
                    _val5 = new(value);
                }

                await EmitLatestAsync(cancellationToken).ConfigureAwait(false);
            }

            /// <summary>Handles completion of source 5.</summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal ValueTask OnCompleted5(Result result) => OnSourceCompleted(result, Source5Bit);

            /// <summary>Handles a new value from source 6.</summary>
            /// <param name="value">The value emitted by source 6.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal async ValueTask OnNext6(T6 value, CancellationToken cancellationToken)
            {
                lock (_stateLock)
                {
                    _val6 = new(value);
                }

                await EmitLatestAsync(cancellationToken).ConfigureAwait(false);
            }

            /// <summary>Handles completion of source 6.</summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal ValueTask OnCompleted6(Result result) => OnSourceCompleted(result, Source6Bit);

            /// <summary>Handles a new value from source 7.</summary>
            /// <param name="value">The value emitted by source 7.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal async ValueTask OnNext7(T7 value, CancellationToken cancellationToken)
            {
                lock (_stateLock)
                {
                    _val7 = new(value);
                }

                await EmitLatestAsync(cancellationToken).ConfigureAwait(false);
            }

            /// <summary>Handles completion of source 7.</summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal ValueTask OnCompleted7(Result result) => OnSourceCompleted(result, Source7Bit);

            /// <summary>Handles a new value from source 8.</summary>
            /// <param name="value">The value emitted by source 8.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal async ValueTask OnNext8(T8 value, CancellationToken cancellationToken)
            {
                lock (_stateLock)
                {
                    _val8 = new(value);
                }

                await EmitLatestAsync(cancellationToken).ConfigureAwait(false);
            }

            /// <summary>Handles completion of source 8.</summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal ValueTask OnCompleted8(Result result) => OnSourceCompleted(result, Source8Bit);

            /// <summary>Handles a new value from source 9.</summary>
            /// <param name="value">The value emitted by source 9.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal async ValueTask OnNext9(T9 value, CancellationToken cancellationToken)
            {
                lock (_stateLock)
                {
                    _val9 = new(value);
                }

                await EmitLatestAsync(cancellationToken).ConfigureAwait(false);
            }

            /// <summary>Handles completion of source 9.</summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal ValueTask OnCompleted9(Result result) => OnSourceCompleted(result, Source9Bit);

            /// <summary>Handles a new value from source 10.</summary>
            /// <param name="value">The value emitted by source 10.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal async ValueTask OnNext10(T10 value, CancellationToken cancellationToken)
            {
                lock (_stateLock)
                {
                    _val10 = new(value);
                }

                await EmitLatestAsync(cancellationToken).ConfigureAwait(false);
            }

            /// <summary>Handles completion of source 10.</summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal ValueTask OnCompleted10(Result result) => OnSourceCompleted(result, Source10Bit);

            /// <summary>
            /// Forwards an upstream error to the downstream observer under the gate.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous forward.</returns>
            internal async ValueTask OnErrorResume(Exception error, CancellationToken cancellationToken)
            {
                _ = cancellationToken;
                using (await _gate.LockAsync(_disposeCancellationToken).ConfigureAwait(false))
                {
                    if (DisposalHelper.IsDisposed(_disposed))
                    {
                        return;
                    }

                    await _observer.OnErrorResumeAsync(error, _disposeCancellationToken).ConfigureAwait(false);
                }
            }

            /// <summary>
            /// Completes the combined sequence and disposes every source subscription.
            /// </summary>
            /// <param name="result">The completion result, or null when disposing without signaling.</param>
            /// <returns>A ValueTask representing the asynchronous teardown.</returns>
            internal async ValueTask CompleteAsync(Result? result)
            {
                if (DisposalHelper.TrySetDisposed(ref _disposed))
                {
                    return;
                }

                await _disposeCts.CancelAsync().ConfigureAwait(false);

                for (var i = 0; i < _subscriptions.Length; i++)
                {
                    var d = _subscriptions[i];
                    if (d is not null)
                    {
                        await d.DisposeAsync().ConfigureAwait(false);
                    }
                }

                if (result is not null)
                {
                    await _observer.OnCompletedAsync(result.Value).ConfigureAwait(false);
                }

#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                await _externalLinkRegistration.DisposeAsync().ConfigureAwait(false);
#else
                _externalLinkRegistration.Dispose();
#endif
                _disposeCts.Dispose();
                _gate.Dispose();
            }

            /// <summary>
            /// Subscribes to a single source by 0-based index. Drives the
            /// <see cref="SubscribeSourcesAsync"/> loop without unrolled per-source code.
            /// </summary>
            /// <param name="index">0-based source index.</param>
            /// <param name="cancellationToken">A token to cancel the subscription.</param>
            /// <returns>The subscription disposable for source <paramref name="index"/>.</returns>
            [SuppressMessage(
                "Minor Code Smell",
                "S109:Magic numbers should not be used",
                Justification = "Switch dispatches on the 0..N-1 source index; naming each numeric arm would just rename the obvious.")]
            [SuppressMessage(
                "Major Code Smell",
                "S1541:Methods and properties should not be too complex",
                Justification = "Switch arm per source — the high arms-count IS the dispatch surface; splitting hurts readability more than it helps.")]
            private ValueTask<IAsyncDisposable> SubscribeAtAsync(int index, CancellationToken cancellationToken) =>
                index switch
                {
                    0 => _sources.Src1.SubscribeAsync(OnNext1, OnErrorResume, OnCompleted1, cancellationToken),
                    1 => _sources.Src2.SubscribeAsync(OnNext2, OnErrorResume, OnCompleted2, cancellationToken),
                    2 => _sources.Src3.SubscribeAsync(OnNext3, OnErrorResume, OnCompleted3, cancellationToken),
                    3 => _sources.Src4.SubscribeAsync(OnNext4, OnErrorResume, OnCompleted4, cancellationToken),
                    4 => _sources.Src5.SubscribeAsync(OnNext5, OnErrorResume, OnCompleted5, cancellationToken),
                    5 => _sources.Src6.SubscribeAsync(OnNext6, OnErrorResume, OnCompleted6, cancellationToken),
                    6 => _sources.Src7.SubscribeAsync(OnNext7, OnErrorResume, OnCompleted7, cancellationToken),
                    7 => _sources.Src8.SubscribeAsync(OnNext8, OnErrorResume, OnCompleted8, cancellationToken),
                    8 => _sources.Src9.SubscribeAsync(OnNext9, OnErrorResume, OnCompleted9, cancellationToken),
                    9 => _sources.Src10.SubscribeAsync(OnNext10, OnErrorResume, OnCompleted10, cancellationToken),
                    _ => throw new ArgumentOutOfRangeException(nameof(index)),
                };

            /// <summary>
            /// Shared completion handler. Each per-source <c>OnCompletedN</c> forwards here with its
            /// own bitmask bit; the combined sequence completes once every bit is set.
            /// </summary>
            /// <param name="result">The completion result from the upstream source.</param>
            /// <param name="doneBit">The bitmask bit owned by the completing source.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            private ValueTask OnSourceCompleted(Result result, int doneBit)
            {
                if (result.IsFailure)
                {
                    return CompleteAsync(result);
                }

                int updated;
                lock (_stateLock)
                {
                    _doneFlags |= doneBit;
                    updated = _doneFlags;
                }

                return updated == AllDoneMask ? _observer.OnCompletedAsync(result) : default;
            }

            /// <summary>
            /// Reads every source's latest value into a single snapshot. Returns <see langword="false"/>
            /// (with <paramref name="values"/> set to <see langword="default"/>) until every source has
            /// produced at least one value.
            /// </summary>
            /// <param name="values">When the method returns <see langword="true"/>, the snapshot.</param>
            /// <returns><see langword="true"/> when every source has produced a value; otherwise <see langword="false"/>.</returns>
            [SuppressMessage(
                "Major Code Smell",
                "S1541:Methods and properties should not be too complex",
                Justification = "Short-circuited && chain over every source's Optional; the high condition count IS the snapshot semantic.")]
            private bool TryReadValues(out Values values)
            {
                if (_val1.TryGetValue(out var v1)
                    && _val2.TryGetValue(out var v2)
                    && _val3.TryGetValue(out var v3)
                    && _val4.TryGetValue(out var v4)
                    && _val5.TryGetValue(out var v5)
                    && _val6.TryGetValue(out var v6)
                    && _val7.TryGetValue(out var v7)
                    && _val8.TryGetValue(out var v8)
                    && _val9.TryGetValue(out var v9)
                    && _val10.TryGetValue(out var v10))
                {
                    values = new(v1, v2, v3, v4, v5, v6, v7, v8, v9, v10);
                    return true;
                }

                values = default;
                return false;
            }

            /// <summary>
            /// Applies the selector to the current value snapshot and forwards the result to the
            /// downstream observer under the gate, respecting disposal.
            /// </summary>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous emit.</returns>
            private async ValueTask EmitLatestAsync(CancellationToken cancellationToken)
            {
                _ = cancellationToken;
                if (!TryReadValues(out var values))
                {
                    return;
                }

                using (await _gate.LockAsync(_disposeCancellationToken).ConfigureAwait(false))
                {
                    if (DisposalHelper.IsDisposed(_disposed))
                    {
                        return;
                    }

                    await _observer.OnNextAsync(
                        _selector(values.V1, values.V2, values.V3, values.V4, values.V5, values.V6, values.V7, values.V8, values.V9, values.V10),
                        _disposeCancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
