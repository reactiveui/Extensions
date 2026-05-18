// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Tests.Operators;

/// <summary>Edge-case coverage for <c>ToPropertyObservable</c> backed by
/// <c>PropertyChangedObservable&lt;T, TProperty&gt;</c> — initial-value emission,
/// matching-name forwarding, unmatched-name filtering, getter-throws forwarding,
/// and dispose detaches the handler.</summary>
public class PropertyChangedObservableTests
{
    /// <summary>Initial property value.</summary>
    private const int InitialValue = 1;

    /// <summary>Updated property value.</summary>
    private const int UpdatedValue = 42;

    /// <summary>Synthetic error message attached to getter failures.</summary>
    private const string GetterFailedMessage = "getter failed";

    /// <summary>Verifies that subscribing emits the current property value immediately.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribePropertyObservable_ThenEmitsCurrentValue()
    {
        var owner = new ObservableOwner { Value = InitialValue };
        var results = new List<int>();

        using var sub = owner.ToPropertyObservable(x => x.Value)
            .Subscribe(results.Add);

        await Assert.That(results).IsCollectionEqualTo([InitialValue]);
    }

    /// <summary>Verifies that matching-name property changes are forwarded.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenMatchingPropertyChanges_ThenForwardsValue()
    {
        var owner = new ObservableOwner { Value = InitialValue };
        var results = new List<int>();

        using var sub = owner.ToPropertyObservable(x => x.Value)
            .Subscribe(results.Add);

        owner.Value = UpdatedValue;

        await Assert.That(results).IsCollectionEqualTo([InitialValue, UpdatedValue]);
    }

    /// <summary>Verifies that non-matching property changes are ignored.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUnrelatedPropertyChanges_ThenIgnored()
    {
        var owner = new ObservableOwner { Value = InitialValue };
        var results = new List<int>();

        using var sub = owner.ToPropertyObservable(x => x.Value)
            .Subscribe(results.Add);

        owner.Other = "anything";

        await Assert.That(results).IsCollectionEqualTo([InitialValue]);
    }

    /// <summary>Verifies that disposing detaches the handler and stops forwarding.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscriptionDisposed_ThenNoFurtherEmissions()
    {
        var owner = new ObservableOwner { Value = InitialValue };
        var results = new List<int>();

        var sub = owner.ToPropertyObservable(x => x.Value)
            .Subscribe(results.Add);
        sub.Dispose();

        owner.Value = UpdatedValue;

        await Assert.That(results).IsCollectionEqualTo([InitialValue]);
    }

    /// <summary>Verifies that an exception thrown by the getter on a property-changed callback
    /// is forwarded to <c>OnError</c>. The initial subscribe read succeeds; the second read (triggered
    /// by <c>Raise</c>) throws.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGetterThrowsOnChange_ThenForwardsError()
    {
        var owner = new LatchingThrowingOwner();
        Exception? caught = null;

        using var sub = owner.ToPropertyObservable(x => x.Latched)
            .Subscribe(static _ => { }, ex => caught = ex);

        owner.ArmAndRaise();

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
    }

    /// <summary>Test owner that fires <see cref="INotifyPropertyChanged"/> on property writes.</summary>
    private sealed class ObservableOwner : INotifyPropertyChanged
    {
        /// <summary>Backing field for <see cref="Value"/>.</summary>
        private int _value;

        /// <summary>Backing field for <see cref="Other"/>.</summary>
        private string _other = string.Empty;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Gets or sets the value being observed by the test.</summary>
        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        /// <summary>Gets or sets the unrelated property used to validate name-filtering.</summary>
        public string Other
        {
            get => _other;
            set
            {
                _other = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Other)));
            }
        }
    }

    /// <summary>Test owner whose getter succeeds on the first read but throws on subsequent reads.</summary>
    private sealed class LatchingThrowingOwner : INotifyPropertyChanged
    {
        /// <summary>Failure message used by the latched getter.</summary>
        private readonly string _message = GetterFailedMessage;

        /// <summary>Set after <see cref="ArmAndRaise"/> latches the getter into throwing mode.</summary>
        private bool _armed;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Gets a property that succeeds while disarmed and throws once armed.</summary>
        public int Latched => _armed ? throw new InvalidOperationException(_message) : 0;

        /// <summary>Arms the throwing behaviour and raises a change notification.</summary>
        public void ArmAndRaise()
        {
            _armed = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Latched)));
        }
    }
}
