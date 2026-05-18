#!/usr/bin/env python3
"""Generate CombineLatestN.cs files for arities 2..16.

Each generated file declares a partial of ObservableAsync containing the
arity-N CombineLatest extension method, the per-arity observable type
(CombineLatestNObservableAsync<T1,...,TN,TResult>), and its inner
CombineLatestSubscription. The shared subscription infrastructure
(gate, dispose CTS, observer fan-out, completion-bitmask handling,
external-cancellation link) lives once in
ReactiveUI.Extensions.Async.Internals.CombineLatestLifecycle<TResult>;
each per-arity subscription composes one instance and forwards.

Run from the src/ folder:
    python3 tools/codegen/generate_combine_latest.py
"""
from pathlib import Path

REPO_SRC = Path(__file__).resolve().parents[2]
OUT_DIR = REPO_SRC / "ReactiveUI.Extensions" / "Async" / "Operators"


def render(n: int) -> str:
    type_params = ", ".join(f"T{i}" for i in range(1, n + 1)) + ", TResult"
    selector_sig = ", ".join(f"T{i}" for i in range(1, n + 1)) + ", TResult"
    sources_typeparams = ", ".join(f"T{i}" for i in range(1, n + 1)) + ", TResult"

    src_record_params = ",\n            ".join(
        f"IObservableAsync<T{i}> Src{i}" for i in range(1, n + 1)
    )
    src_record_doc = "\n".join(
        f"        /// <param name=\"Src{i}\">Source observable {i}.</param>"
        for i in range(1, n + 1)
    )

    values_record_params = ",\n                ".join(
        f"T{i} V{i}" for i in range(1, n + 1)
    )
    values_record_doc = "\n".join(
        f"            /// <param name=\"V{i}\">Latest value from source {i}.</param>"
        for i in range(1, n + 1)
    )

    source_bit_consts = "\n\n".join(
        f"            /// <summary>Bit owned by source {i} inside the lifecycle's completion bitmask.</summary>\n"
        f"            private const int Source{i}Bit = 1 << {i - 1};"
        for i in range(1, n + 1)
    )

    val_fields = "\n\n".join(
        f"            /// <summary>Latest value from source {i}.</summary>\n"
        f"            private Optional<T{i}> _val{i} = Optional<T{i}>.Empty;"
        for i in range(1, n + 1)
    )

    on_next_completed_methods = []
    for i in range(1, n + 1):
        on_next_completed_methods.append(f"""            /// <summary>Handles a new value from source {i}.</summary>
            /// <param name="value">The value emitted by source {i}.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal async ValueTask OnNext{i}(T{i} value, CancellationToken cancellationToken)
            {{
                _ = cancellationToken;
                lock (_valuesLock)
                {{
                    _val{i} = new(value);
                }}

                await EmitLatestAsync().ConfigureAwait(false);
            }}

            /// <summary>Handles completion of source {i}.</summary>
            /// <param name="result">The completion result.</param>
            /// <returns>A ValueTask representing the asynchronous handler.</returns>
            internal ValueTask OnCompleted{i}(Result result) => Lifecycle.OnSourceCompletedAsync(result, Source{i}Bit);""")
    on_next_completed = "\n\n".join(on_next_completed_methods)

    switch_arms = "\n".join(
        f"                    {i - 1} => _sources.Src{i}.SubscribeAsync(OnNext{i}, OnErrorResume, OnCompleted{i}, cancellationToken),"
        for i in range(1, n + 1)
    )

    try_read_conds = "\n                    && ".join(
        f"_val{i}.TryGetValue(out var v{i})" for i in range(1, n + 1)
    )
    try_read_ctor = ", ".join(f"v{i}" for i in range(1, n + 1))

    # Wrap selector call args across multiple lines for high-arity files so the
    # final emit expression stays under the 200-char line limit (Sonar S103).
    if n <= 8:
        selector_call_args = ", ".join(f"values.V{i}" for i in range(1, n + 1))
        emit_expr = (
            f"            private ValueTask EmitLatestAsync() =>\n"
            f"                TryReadValues(out var values)\n"
            f"                    ? Lifecycle.EmitDownstreamAsync(_selector({selector_call_args}))\n"
            f"                    : default;"
        )
    else:
        wrapped_args = ",\n                            ".join(
            f"values.V{i}" for i in range(1, n + 1)
        )
        emit_expr = (
            f"            private ValueTask EmitLatestAsync()\n"
            f"            {{\n"
            f"                if (!TryReadValues(out var values))\n"
            f"                {{\n"
            f"                    return default;\n"
            f"                }}\n"
            f"\n"
            f"                var projected = _selector(\n"
            f"                            {wrapped_args});\n"
            f"                return Lifecycle.EmitDownstreamAsync(projected);\n"
            f"            }}"
        )

    ctor_src_params = ",\n        ".join(
        f"IObservableAsync<T{i}> src{i}" for i in range(1, n + 1)
    )
    ctor_src_arglist = ", ".join(f"src{i}" for i in range(1, n + 1))
    ctor_src_doc = "\n".join(
        f"    /// <param name=\"src{i}\">Source observable {i} whose latest value is combined.</param>"
        for i in range(1, n + 1)
    )

    typeparam_doc = "\n".join(
        f"    /// <typeparam name=\"T{i}\">The element type of source {i}.</typeparam>"
        for i in range(1, n + 1)
    )
    inner_typeparam_doc = "\n".join(
        f"    /// <typeparam name=\"T{i}\">Element type of source {i}.</typeparam>"
        for i in range(1, n + 1)
    )

    # Word form for the 'arity-N (X-source)' header phrase.
    word_forms = {
        2: "two", 3: "three", 4: "four", 5: "five", 6: "six", 7: "seven",
        8: "eight", 9: "nine", 10: "ten", 11: "eleven", 12: "twelve",
        13: "thirteen", 14: "fourteen", 15: "fifteen", 16: "sixteen",
    }
    word = word_forms[n]

    return f"""// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides the arity-{n} (<c>{word}</c>-source) <c>CombineLatest</c> extension method
/// and its supporting internal observable + subscription types.
/// </summary>
[SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "Has more than 7 parameters - just expected for arity-N CombineLatest operator surface.")]
public static partial class ObservableAsync
{{
    /// <summary>
    /// Combines the latest values from {word} asynchronous observable sources into a single
    /// sequence, projecting them through <paramref name="selector"/> whenever any source emits.
    /// </summary>
    /// <remarks>
    /// The returned sequence does not produce a value until every source has emitted at least
    /// once. After that, each new value from any source produces a fresh projection using the
    /// most recent value from each. Completion / failure of any source propagates downstream.
    /// </remarks>
{typeparam_doc}
    /// <typeparam name="TResult">The projected element type.</typeparam>
{ctor_src_doc}
    /// <param name="selector">Projects the latest value of every source into a result.</param>
    /// <returns>An observable sequence of projected results.</returns>
    public static IObservableAsync<TResult> CombineLatest<{type_params}>(
        this {ctor_src_params},
        Func<{selector_sig}> selector) =>
        new CombineLatest{n}ObservableAsync<{type_params}>(
            new({ctor_src_arglist}),
            selector);

    /// <summary>
    /// Async observable that combines the latest values from {word} source sequences using a selector.
    /// </summary>
{inner_typeparam_doc}
    /// <typeparam name="TResult">The projected element type.</typeparam>
    internal sealed class CombineLatest{n}ObservableAsync<{sources_typeparams}>(
        CombineLatest{n}ObservableAsync<{sources_typeparams}>.Sources sources,
        Func<{selector_sig}> selector) : ObservableAsync<TResult>
    {{
        /// <summary>
        /// Bundles the {word} source observables so the subscription constructor stays at three
        /// parameters (observer, sources, selector) regardless of arity. Sonar S107 caps method /
        /// constructor parameter count; the bundle keeps the internal types compliant.
        /// </summary>
{src_record_doc}
        internal readonly record struct Sources(
            {src_record_params});

        /// <inheritdoc/>
        protected override ValueTask<IAsyncDisposable> SubscribeAsyncCore(
            IObserverAsync<TResult> observer,
            CancellationToken cancellationToken)
        {{
            var subscription = new CombineLatestSubscription(observer, sources, selector);
            subscription.Lifecycle.LinkExternalCancellation(cancellationToken);
            return SubscriptionHelper.SubscribeAndDisposeOnFailureAsync(
                subscription,
                () => subscription.SubscribeSourcesAsync(cancellationToken));
        }}

        /// <summary>
        /// Manages subscriptions to all source sequences and emits combined values via the selector.
        /// </summary>
        internal sealed class CombineLatestSubscription : IAsyncDisposable
        {{
{source_bit_consts}

            /// <summary>Lock protecting the latest-values cache.</summary>
#if NET9_0_OR_GREATER
            private readonly Lock _valuesLock = new();
#else
            private readonly object _valuesLock = new();
#endif

            /// <summary>Bundled source observables.</summary>
            private readonly Sources _sources;

            /// <summary>The result selector function.</summary>
            private readonly Func<{selector_sig}> _selector;

{val_fields}

            /// <summary>Latest-value snapshot taken when every source has produced at least one value.</summary>
{values_record_doc}
            internal readonly record struct Values(
                {values_record_params});

            /// <summary>
            /// Initializes a new instance of the <see cref="CombineLatestSubscription"/> class.
            /// </summary>
            /// <param name="observer">The downstream observer.</param>
            /// <param name="sources">The bundled source observables.</param>
            /// <param name="selector">The selector that projects the latest values.</param>
            public CombineLatestSubscription(
                IObserverAsync<TResult> observer,
                Sources sources,
                Func<{selector_sig}> selector)
            {{
                _sources = sources;
                _selector = selector;
                Lifecycle = new CombineLatestLifecycle<TResult>(observer, sourceCount: {n});
            }}

            /// <summary>Gets the shared subscription lifecycle (state + lifecycle methods).</summary>
            internal CombineLatestLifecycle<TResult> Lifecycle {{ get; }}

            /// <summary>
            /// Subscribes to every source observable. Renamed from the obvious <c>SubscribeAsync</c>
            /// to avoid Sonar S3218 shadowing of <see cref="ObservableAsync{{TResult}}.SubscribeAsync"/>.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the subscription.</param>
            /// <returns>A task representing the asynchronous subscribe operation.</returns>
            public async ValueTask SubscribeSourcesAsync(CancellationToken cancellationToken)
            {{
                var subs = Lifecycle.Subscriptions;
                for (var i = 0; i < subs.Length; i++)
                {{
                    subs[i] = await SubscribeAtAsync(i, cancellationToken).ConfigureAwait(false);
                }}
            }}

            /// <inheritdoc/>
            public ValueTask DisposeAsync() => Lifecycle.DisposeAsync();

{on_next_completed}

            /// <summary>
            /// Forwards an upstream error to the downstream observer; thin shim with the
            /// <c>(error, ct)</c> signature that <see cref="IObservableAsync{{T}}.SubscribeAsync"/> expects.
            /// </summary>
            /// <param name="error">The error to forward.</param>
            /// <param name="cancellationToken">Ignored — the lifecycle uses its own dispose token.</param>
            /// <returns>A ValueTask representing the asynchronous forward.</returns>
            internal ValueTask OnErrorResume(Exception error, CancellationToken cancellationToken)
            {{
                _ = cancellationToken;
                return Lifecycle.OnErrorResumeAsync(error);
            }}

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
                {{
{switch_arms}
                    _ => throw new ArgumentOutOfRangeException(nameof(index)),
                }};

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
            {{
                if ({try_read_conds})
                {{
                    values = new({try_read_ctor});
                    return true;
                }}

                values = default;
                return false;
            }}

            /// <summary>Reads the latest snapshot and forwards it through the selector to the lifecycle.</summary>
            /// <returns>A ValueTask representing the asynchronous emit.</returns>
{emit_expr}
        }}
    }}
}}
"""


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for n in range(2, 17):
        path = OUT_DIR / f"CombineLatest{n}.cs"
        path.write_text(render(n), encoding="utf-8")
        print(f"wrote {path.relative_to(REPO_SRC.parent)}  ({path.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
