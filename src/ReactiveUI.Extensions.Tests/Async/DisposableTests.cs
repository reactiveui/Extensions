// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// Tests for DisposableAsync, CompositeDisposableAsync, SingleAssignmentDisposableAsync, and SerialDisposableAsync.
/// </summary>
public class DisposableTests
{
    /// <summary>Tests DisposableAsync.Empty dispose does nothing.</summary>
    [Test]
    public async Task WhenDisposableAsyncEmpty_ThenDisposeDoesNothing()
    {
        var empty = DisposableAsync.Empty;
        await empty.DisposeAsync();
    }

    /// <summary>Tests DisposableAsync.Create callback invoked on dispose.</summary>
    [Test]
    public async Task WhenDisposableAsyncCreate_ThenCallbackInvokedOnDispose()
    {
        var disposed = false;
        var disposable = DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        });

        await Assert.That(disposed).IsFalse();
        await disposable.DisposeAsync();
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>Tests DisposableAsync.Create double dispose only calls once.</summary>
    [Test]
    public async Task WhenDisposableAsyncCreate_ThenDoubleDisposeOnlyCallsOnce()
    {
        var callCount = 0;
        var disposable = DisposableAsync.Create(() =>
        {
            callCount++;
            return default;
        });

        await disposable.DisposeAsync();
        await disposable.DisposeAsync();

        await Assert.That(callCount).IsEqualTo(1);
    }

    /// <summary>Tests CompositeDisposableAsync disposes all.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsync_ThenDisposesAll()
    {
        var disposed1 = false;
        var disposed2 = false;
        var d1 = DisposableAsync.Create(() =>
        {
            disposed1 = true;
            return default;
        });
        var d2 = DisposableAsync.Create(() =>
        {
            disposed2 = true;
            return default;
        });

        var composite = new CompositeDisposableAsync(d1, d2);

        await Assert.That(composite.Count).IsEqualTo(2);
        await Assert.That(composite.IsDisposed).IsFalse();

        await composite.DisposeAsync();

        await Assert.That(disposed1).IsTrue();
        await Assert.That(disposed2).IsTrue();
        await Assert.That(composite.IsDisposed).IsTrue();
    }

    /// <summary>Tests CompositeDisposableAsync negative capacity throws.</summary>
    [Test]
    public void WhenCompositeDisposableAsyncNegativeCapacity_ThenThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompositeDisposableAsync(-1));
    }

    /// <summary>Tests CompositeDisposableAsync with capacity works.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncWithCapacity_ThenWorks()
    {
        var composite = new CompositeDisposableAsync(10);
        await Assert.That(composite.Count).IsEqualTo(0);

        var disposed = false;
        await composite.AddAsync(DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        }));
        await Assert.That(composite.Count).IsEqualTo(1);

        await composite.DisposeAsync();
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>Tests CompositeDisposableAsync from enumerable disposes all.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncFromEnumerable_ThenDisposesAll()
    {
        var count = 0;
        var disposables = Enumerable.Range(0, 3).Select(_ => DisposableAsync.Create(() =>
        {
            Interlocked.Increment(ref count);
            return default;
        }));

        var composite = new CompositeDisposableAsync(disposables);
        await Assert.That(composite.Count).IsEqualTo(3);

        await composite.DisposeAsync();
        await Assert.That(count).IsEqualTo(3);
    }

    /// <summary>Tests CompositeDisposableAsync add after dispose disposes immediately.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncAddAfterDispose_ThenItemDisposedImmediately()
    {
        var composite = new CompositeDisposableAsync();
        await composite.DisposeAsync();

        var disposed = false;
        await composite.AddAsync(DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        }));

        await Task.Delay(50);
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>Tests CompositeDisposableAsync remove disposes and removes the item.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncRemove_ThenItemRemovedAndDisposed()
    {
        var disposed = false;
        var d = DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        });

        var composite = new CompositeDisposableAsync(d);
        var removed = await composite.Remove(d);

        await Assert.That(removed).IsTrue();
        await Assert.That(composite.Count).IsEqualTo(0);
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>Tests CompositeDisposableAsync IsDisposed returns false when active.</summary>
    [Test]
    public async Task WhenCompositeDisposableAsyncIsDisposed_ThenReturnsFalse()
    {
        var composite = new CompositeDisposableAsync();
        await Assert.That(composite.IsDisposed).IsFalse();
    }

    /// <summary>Tests SingleAssignmentDisposableAsync disposes assigned.</summary>
    [Test]
    public async Task WhenSingleAssignmentDisposableAsync_ThenDisposesAssigned()
    {
        var sad = new SingleAssignmentDisposableAsync();
        var disposed = false;

        await sad.SetDisposableAsync(DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        }));
        await Assert.That(sad.IsDisposed).IsFalse();

        await sad.DisposeAsync();
        await Assert.That(sad.IsDisposed).IsTrue();
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>Tests SingleAssignment dispose before set disposes immediately.</summary>
    [Test]
    public async Task WhenSingleAssignmentDisposableAsyncDisposeBeforeSet_ThenSetDisposedImmediately()
    {
        var sad = new SingleAssignmentDisposableAsync();
        await sad.DisposeAsync();

        var disposed = false;
        await sad.SetDisposableAsync(DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        }));
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>Tests SingleAssignment double set throws.</summary>
    [Test]
    public async Task WhenSingleAssignmentDisposableAsyncDoubleSet_ThenThrowsInvalidOperation()
    {
        var sad = new SingleAssignmentDisposableAsync();
        await sad.SetDisposableAsync(DisposableAsync.Empty);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sad.SetDisposableAsync(DisposableAsync.Empty));
    }

    /// <summary>Tests SingleAssignment get before set returns null.</summary>
    [Test]
    public async Task WhenSingleAssignmentDisposableAsyncGetBeforeSet_ThenReturnsNull()
    {
        var sad = new SingleAssignmentDisposableAsync();
        await Assert.That(sad.GetDisposable()).IsNull();
    }

    /// <summary>Tests SingleAssignment get after dispose returns non-null.</summary>
    [Test]
    public async Task WhenSingleAssignmentDisposableAsyncGetAfterDispose_ThenReturnsEmpty()
    {
        var sad = new SingleAssignmentDisposableAsync();
        await sad.DisposeAsync();
        await Assert.That(sad.GetDisposable()).IsNotNull();
    }

    /// <summary>Tests SingleAssignment get after set returns assigned.</summary>
    [Test]
    public async Task WhenSingleAssignmentDisposableAsyncGetAfterSet_ThenReturnsAssigned()
    {
        var sad = new SingleAssignmentDisposableAsync();
        var original = DisposableAsync.Empty;
        await sad.SetDisposableAsync(original);
        await Assert.That(sad.GetDisposable()).IsEquivalentTo(original);
    }

    /// <summary>Tests SerialDisposableAsync replaces and disposes previous.</summary>
    [Test]
    public async Task WhenSerialDisposableAsync_ThenReplacesAndDisposesPrevious()
    {
        var serial = new SerialDisposableAsync();
        var disposed1 = false;
        var disposed2 = false;

        var d1 = DisposableAsync.Create(() =>
        {
            disposed1 = true;
            return default;
        });
        var d2 = DisposableAsync.Create(() =>
        {
            disposed2 = true;
            return default;
        });

        await serial.SetDisposableAsync(d1);
        await Assert.That(disposed1).IsFalse();

        await serial.SetDisposableAsync(d2);
        await Assert.That(disposed1).IsTrue();
        await Assert.That(disposed2).IsFalse();

        await serial.DisposeAsync();
        await Assert.That(disposed2).IsTrue();
    }

    /// <summary>Tests SerialDisposableAsync set after dispose disposes immediately.</summary>
    [Test]
    public async Task WhenSerialDisposableAsyncSetAfterDispose_ThenDisposedImmediately()
    {
        var serial = new SerialDisposableAsync();
        await serial.DisposeAsync();

        var disposed = false;
        await serial.SetDisposableAsync(DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        }));
        await Assert.That(disposed).IsTrue();
    }

    /// <summary>Tests SerialDisposableAsync double dispose is safe.</summary>
    [Test]
    public async Task WhenSerialDisposableAsyncDoubleDispose_ThenSafe()
    {
        var serial = new SerialDisposableAsync();
        var disposed = false;
        await serial.SetDisposableAsync(DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        }));

        await serial.DisposeAsync();
        await serial.DisposeAsync();

        await Assert.That(disposed).IsTrue();
    }

    /// <summary>Tests SerialDisposableAsync set null succeeds.</summary>
    [Test]
    public async Task WhenSerialDisposableAsyncSetNull_ThenSucceeds()
    {
        var serial = new SerialDisposableAsync();
        await serial.SetDisposableAsync(null);
        await serial.DisposeAsync();
    }
}
