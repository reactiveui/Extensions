// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides the arity-2 (<c>two</c>-source) <c>CombineLatest</c> extension method
/// and its supporting internal observable + subscription types.
/// </summary>
[SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "Has more than 7 parameters - just expected for arity-N CombineLatest operator surface.")]
public static partial class ObservableAsync
{
    /// <summary>
    /// Combines the latest values from two asynchronous observable sources into a single
    /// sequence, projecting them through <paramref name="selector"/> whenever any source emits.
    /// </summary>
    /// <remarks>
    /// The returned sequence does not produce a value until every source has emitted at least
    /// once. After that, each new value from any source produces a fresh projection using the
    /// most recent value from each. Completion / failure of any source propagates downstream.
    /// </remarks>
    /// <typeparam name="T1">The element type of source 1.</typeparam>
    /// <typeparam name="T2">The element type of source 2.</typeparam>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="src1">Source observable 1 whose latest value is combined.</param>
    /// <param name="src2">Source observable 2 whose latest value is combined.</param>
    /// <param name="selector">Projects the latest value of every source into a result.</param>
    /// <returns>An observable sequence of projected results.</returns>
    public static IObservableAsync<TResult> CombineLatest<T1, T2, TResult>(
        this IObservableAsync<T1> src1,
        IObservableAsync<T2> src2,
        Func<T1, T2, TResult> selector) =>
        new CombineLatest2ObservableAsync<T1, T2, TResult>(
            new(src1, src2),
            selector);

    /// <summary>
    /// Async observable that combines the latest values from two source sequences using a selector.
    /// </summary>
    /// <typeparam name="T1">Element type of source 1.</typeparam>
    /// <typeparam name="T2">Element type of source 2.</typeparam>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    internal sealed class CombineLatest2ObservableAsync<T1, T2, TResult>(
        CombineLatest2ObservableAsync<T1, T2, TResult>.Sources sources,
        Func<T1, T2, TResult> selector) : ObservableAsync<TResult>
    {
        /// <summary>
        /// Bundles the two source observables so the subscription constructor stays at three
        /// parameters (observer, sources, selector) regardless of arity. Sonar S107 caps method /
        /// constructor parameter count; the bundle keeps the internal types compliant.
        /// </summary>
        /// <param name="Src1">Source observable 1.</param>
        /// <param name="Src2">Source observable 2.</param>
        internal readonly record struct Sources(
            IObservableAsync<T1> Src1,
            IObservableAsync<T2> Src2);

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

            /// <summary>Bitmask value with every source-completion bit set; the sequence completes when
            /// <see cref="_doneFlags"/> equals this value.</summary>
            private const int AllDoneMask = Source1Bit
                | Source2Bit;

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
            private readonly Func<T1, T2, TResult> _selector;

            /// <summary>Subscription disposables, indexed 0..N-1 by source position.</summary>
            private readonly IAsyncDisposable?[] _subscriptions = new IAsyncDisposable?[2];

            /// <summary>Latest value from source 1.</summary>
            private Optional<T1> _val1 = Optional<T1>.Empty;

            /// <summary>Latest value from source 2.</summary>
            private Optional<T2> _val2 = Optional<T2>.Empty;

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
            internal readonly record struct Values(
                T1 V1,
                T2 V2);

            /// <summary>
            /// Initializes a new instance of the <see cref="CombineLatestSubscription"/> class.
            /// </summary>
            /// <param name="observer">The downstream observer.</param>
            /// <param name="sources">The bundled source observables.</param>
            /// <param name="selector">The selector that projects the latest values.</param>
            public CombineLatestSubscription(
                IObserverAsync<TResult> observer,
                Sources sources,
                Func<T1, T2, TResult> selector)
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
                    && _val2.TryGetValue(out var v2))
                {
                    values = new(v1, v2);
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

                    await _observer.OnNextAsync(_selector(values.V1, values.V2), _disposeCancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
