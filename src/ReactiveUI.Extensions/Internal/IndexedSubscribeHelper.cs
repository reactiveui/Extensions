// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Internal.Disposables;

namespace ReactiveUI.Extensions.Internal;

/// <summary>
/// Shared subscription loop for sync operators that fan an indexed callback set across an N-source
/// list (e.g. <c>BooleanReduceObservable</c>, <c>MinMaxObservable</c>). Each call site previously
/// hand-rolled the same loop — index-capture, <see cref="ObservableSubscribeExtensions.SubscribeCallbacks{T}"/>
/// triple, <see cref="DisposableBag"/> aggregate — so centralising it here keeps the per-emission
/// closure shape consistent and the duplication off Sonar's CPD radar.
/// </summary>
internal static class IndexedSubscribeHelper
{
    /// <summary>
    /// Subscribes the supplied callbacks to every source in <paramref name="sources"/>, threading
    /// each source's positional index through to the <paramref name="onNext"/> and
    /// <paramref name="onCompleted"/> hooks. The returned disposable disposes every per-source
    /// subscription on dispose.
    /// </summary>
    /// <typeparam name="T">The element type of the source observables.</typeparam>
    /// <param name="sources">The source observables, indexed 0..N-1.</param>
    /// <param name="onNext">Per-source OnNext hook: <c>(index, value)</c>.</param>
    /// <param name="onError">Shared OnError hook (errors from any source forward unchanged).</param>
    /// <param name="onCompleted">Per-source OnCompleted hook: <c>(index)</c>.</param>
    /// <returns>A composite disposable that releases every per-source subscription on dispose.</returns>
    public static IDisposable SubscribeIndexed<T>(
        IReadOnlyList<IObservable<T>> sources,
        Action<int, T> onNext,
        Action<Exception> onError,
        Action<int> onCompleted)
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);
        ArgumentExceptionHelper.ThrowIfNull(onNext);
        ArgumentExceptionHelper.ThrowIfNull(onError);
        ArgumentExceptionHelper.ThrowIfNull(onCompleted);

        var composite = new DisposableBag();
        for (var i = 0; i < sources.Count; i++)
        {
            var index = i;
            composite.Add(sources[i].SubscribeCallbacks(
                value => onNext(index, value),
                onError,
                () => onCompleted(index)));
        }

        return composite;
    }
}
