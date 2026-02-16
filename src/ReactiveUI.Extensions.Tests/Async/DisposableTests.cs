// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NUnit.Framework;
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
        Assert.Pass();
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

        Assert.That(disposed, Is.False);
        await disposable.DisposeAsync();
        Assert.That(disposed, Is.True);
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

        Assert.That(callCount, Is.EqualTo(1));
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

        Assert.That(composite.Count, Is.EqualTo(2));
        Assert.That(composite.IsDisposed, Is.False);

        await composite.DisposeAsync();

        Assert.That(disposed1, Is.True);
        Assert.That(disposed2, Is.True);
        Assert.That(composite.IsDisposed, Is.True);
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
        Assert.That(composite.Count, Is.EqualTo(0));

        var disposed = false;
        await composite.AddAsync(DisposableAsync.Create(() =>
        {
            disposed = true;
            return default;
        }));
        Assert.That(composite.Count, Is.EqualTo(1));

        await composite.DisposeAsync();
        Assert.That(disposed, Is.True);
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
        Assert.That(composite.Count, Is.EqualTo(3));

        await composite.DisposeAsync();
        Assert.That(count, Is.EqualTo(3));
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
        Assert.That(disposed, Is.True);
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

        Assert.That(removed, Is.True);
        Assert.That(composite.Count, Is.EqualTo(0));
        Assert.That(disposed, Is.True);
    }

    /// <summary>Tests CompositeDisposableAsync IsReadOnly returns false.</summary>
    [Test]
    public void WhenCompositeDisposableAsyncIsReadOnly_ThenReturnsFalse()
    {
        var composite = new CompositeDisposableAsync();
        Assert.That(composite.IsReadOnly, Is.False);
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
        Assert.That(sad.IsDisposed, Is.False);

        await sad.DisposeAsync();
        Assert.That(sad.IsDisposed, Is.True);
        Assert.That(disposed, Is.True);
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
        Assert.That(disposed, Is.True);
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
    public void WhenSingleAssignmentDisposableAsyncGetBeforeSet_ThenReturnsNull()
    {
        var sad = new SingleAssignmentDisposableAsync();
        Assert.That(sad.GetDisposable(), Is.Null);
    }

    /// <summary>Tests SingleAssignment get after dispose returns non-null.</summary>
    [Test]
    public async Task WhenSingleAssignmentDisposableAsyncGetAfterDispose_ThenReturnsEmpty()
    {
        var sad = new SingleAssignmentDisposableAsync();
        await sad.DisposeAsync();
        Assert.That(sad.GetDisposable(), Is.Not.Null);
    }

    /// <summary>Tests SingleAssignment get after set returns assigned.</summary>
    [Test]
    public async Task WhenSingleAssignmentDisposableAsyncGetAfterSet_ThenReturnsAssigned()
    {
        var sad = new SingleAssignmentDisposableAsync();
        var original = DisposableAsync.Empty;
        await sad.SetDisposableAsync(original);
        Assert.That(sad.GetDisposable(), Is.SameAs(original));
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
        Assert.That(disposed1, Is.False);

        await serial.SetDisposableAsync(d2);
        Assert.That(disposed1, Is.True);
        Assert.That(disposed2, Is.False);

        await serial.DisposeAsync();
        Assert.That(disposed2, Is.True);
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
        Assert.That(disposed, Is.True);
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

        Assert.That(disposed, Is.True);
    }

    /// <summary>Tests SerialDisposableAsync set null succeeds.</summary>
    [Test]
    public async Task WhenSerialDisposableAsyncSetNull_ThenSucceeds()
    {
        var serial = new SerialDisposableAsync();
        await serial.SetDisposableAsync(null);
        await serial.DisposeAsync();
        Assert.Pass();
    }
}
