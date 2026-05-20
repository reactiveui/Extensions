// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the cost of the synchronous subscription helpers in
/// <see cref="ObservableSubscriptionExtensions"/>: <c>SubscribeGetValue</c>,
/// <c>SubscribeGetError</c>, <c>SubscribeAndComplete</c>, <c>WaitForValue</c>,
/// <c>WaitForCompletion</c>, and <c>WaitForError</c>. Each call subscribes a one-shot blocking
/// observer; the source is a synchronously-completing single-value observable so the helpers do
/// not wait on a real event.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ObservableSubscriptionBenchmarks
{
    /// <summary>Low end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int SmallInvocationCount = 100;

    /// <summary>High end of the <see cref="InvocationCount"/> parameter sweep.</summary>
    private const int LargeInvocationCount = 1_000;

    /// <summary>Constant value emitted by the synchronous source.</summary>
    private const int SampledValue = 42;

    /// <summary>Pre-built synchronously-terminating int source.</summary>
    private readonly IObservable<int> _intSource = new InlineCompletingObservable<int>(SampledValue);

    /// <summary>Pre-built synchronously-terminating unit source.</summary>
    private readonly IObservable<Unit> _unitSource = new InlineCompletingObservable<Unit>(Unit.Default);

    /// <summary>Pre-built synchronously-erroring source for <see cref="WaitForError_OnError"/>.</summary>
    private readonly IObservable<int> _erroringSource = new InlineErroringObservable<int>();

    /// <summary>Gets or sets the number of invocations per benchmark iteration.</summary>
    [Params(SmallInvocationCount, LargeInvocationCount)]
    public int InvocationCount { get; set; }

    /// <summary>Measures the cost of <see cref="ObservableSubscriptionExtensions.SubscribeGetValue{T}"/>.</summary>
    [Benchmark]
    public void SubscribeGetValue_SingleValueSource()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            _ = _intSource.SubscribeGetValue();
        }
    }

    /// <summary>Measures the cost of <see cref="ObservableSubscriptionExtensions.SubscribeGetError{T}(IObservable{T})"/> on a sync-erroring source.</summary>
    [Benchmark]
    public void SubscribeGetError_ErroringSource()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            _ = _erroringSource.SubscribeGetError();
        }
    }

    /// <summary>Measures the cost of <see cref="ObservableSubscriptionExtensions.SubscribeAndComplete"/>.</summary>
    [Benchmark]
    public void SubscribeAndComplete_UnitSource()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            _unitSource.SubscribeAndComplete();
        }
    }

    /// <summary>Measures the cost of <see cref="ObservableSubscriptionExtensions.WaitForValue{T}(IObservable{T})"/> on a sync-completing source.</summary>
    [Benchmark]
    public void WaitForValue_SyncSource()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            _ = _intSource.WaitForValue();
        }
    }

    /// <summary>Measures the cost of <see cref="ObservableSubscriptionExtensions.WaitForCompletion(IObservable{Unit})"/> on a sync-completing source.</summary>
    [Benchmark]
    public void WaitForCompletion_SyncSource()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            _unitSource.WaitForCompletion();
        }
    }

    /// <summary>Measures the cost of <see cref="ObservableSubscriptionExtensions.WaitForError{T}(IObservable{T})"/> on a sync-erroring source.</summary>
    [Benchmark]
    public void WaitForError_OnError()
    {
        for (var i = 0; i < InvocationCount; i++)
        {
            _ = _erroringSource.WaitForError();
        }
    }

    /// <summary>Synchronously emits the configured value and completes inside the subscribe call.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The value emitted on every subscribe.</param>
    private sealed class InlineCompletingObservable<T>(T value) : IObservable<T>
    {
        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnNext(value);
            observer.OnCompleted();
            return EmptyDisposable.Instance;
        }
    }

    /// <summary>Synchronously emits an error inside the subscribe call.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class InlineErroringObservable<T> : IObservable<T>
    {
        /// <summary>Shared error instance to avoid per-call allocations.</summary>
        private static readonly InvalidOperationException SharedError = new("benchmark");

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnError(SharedError);
            return EmptyDisposable.Instance;
        }
    }

    /// <summary>Singleton no-op disposable.</summary>
    private sealed class EmptyDisposable : IDisposable
    {
        /// <summary>Singleton instance.</summary>
        public static readonly EmptyDisposable Instance = new();

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
