// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ReactiveUI.Extensions.Benchmarks;

/// <summary>
/// Measures the per-change cost of <c>ToPropertyObservable</c>: a property getter is bridged into an
/// <see cref="IObservable{T}"/> that emits whenever the named property raises
/// <see cref="INotifyPropertyChanged.PropertyChanged"/>. The benchmark drives change notifications
/// through the subscribed observable, capturing the per-notification overhead (name match + compiled
/// getter invocation + emit) rather than the one-time expression compilation done at subscribe time.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ToPropertyObservableBenchmarks : IDisposable
{
    /// <summary>Low end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int SmallEmissionCount = 1_000;

    /// <summary>High end of the <see cref="EmissionCount"/> parameter sweep.</summary>
    private const int LargeEmissionCount = 10_000;

    /// <summary>Synchronous no-op sink subscribed to the property observable.</summary>
    private readonly NoopObserver<int> _sink = new();

    /// <summary>INPC source whose <see cref="ObservableModel.Value"/> property the observable tracks.</summary>
    private readonly ObservableModel _model = new();

    /// <summary>Subscription on the property observable.</summary>
    private IDisposable _subscription = null!;

    /// <summary>Gets or sets the number of property changes raised per benchmark invocation.</summary>
    [Params(SmallEmissionCount, LargeEmissionCount)]
    public int EmissionCount { get; set; }

    /// <summary>Builds the property observable and attaches the sink.</summary>
    [GlobalSetup]
    public void Setup() =>
        _subscription = _model.ToPropertyObservable(static x => x.Value).Subscribe(_sink);

    /// <summary>Tears the subscription down.</summary>
    [GlobalCleanup]
    public void Cleanup() => _subscription.Dispose();

    /// <summary>Raises <see cref="EmissionCount"/> property changes through the observable.</summary>
    [Benchmark]
    public void ToPropertyObservable_PerChange()
    {
        for (var i = 0; i < EmissionCount; i++)
        {
            _model.Value = i;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Drains synchronous teardown.</summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Cleanup();
    }

    /// <summary>Minimal <see cref="INotifyPropertyChanged"/> model with a single tracked property.</summary>
    private sealed class ObservableModel : INotifyPropertyChanged
    {
        /// <summary>Cached event args so each raise does not allocate — keeps the benchmark measuring
        /// the operator's per-change overhead rather than the model's notification allocation.</summary>
        private static readonly PropertyChangedEventArgs ValueChangedArgs = new(nameof(Value));

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Gets or sets the tracked value; setting it raises <see cref="PropertyChanged"/>.</summary>
        public int Value
        {
            get;
            set
            {
                field = value;
                PropertyChanged?.Invoke(this, ValueChangedArgs);
            }
        }
    }

    /// <summary>No-op synchronous observer used as the terminal sink.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class NoopObserver<T> : IObserver<T>
    {
        /// <inheritdoc/>
        public void OnNext(T value)
        {
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
        }
    }
}
