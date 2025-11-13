// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using NUnit.Framework;

namespace ReactiveUI.Extensions.Tests;

/// <summary>
/// Tests disposable extensions.
/// </summary>
public class DisposableExtensionsTests
{
    /// <summary>
    /// Tests DisposeWith returns a disposable.
    /// </summary>
    [Test]
    public void GivenNull_WhenDisposeWith_ThenExceptionThrown()
    {
        // Given
        var sut = Disposable.Create(() => { });

        // When
        var result = Assert.Catch<Exception>(() => sut.DisposeWith(null!));

        // Then
        Assert.That(result, Is.TypeOf<ArgumentNullException>());
    }

    /// <summary>
    /// Tests DisposeWith disposes the underlying disposable.
    /// </summary>
    [Test]
    public void GivenDisposable_WhenDisposeWith_ThenDisposed()
    {
        // Given
        var sut = new CompositeDisposable();
        var compositeDisposable = new CompositeDisposable();
        sut.DisposeWith(compositeDisposable);

        // When
        compositeDisposable.Dispose();

        // Then
        Assert.That(sut.IsDisposed, Is.True);
    }

    /// <summary>
    /// Tests DisposeWith returns the original disposable.
    /// </summary>
    [Test]
    public void GivenDisposable_WhenDisposeWith_ThenReturnsDisposable()
    {
        // Given, When
        var sut = new CompositeDisposable();
        var compositeDisposable = new CompositeDisposable();
        var result = sut.DisposeWith(compositeDisposable);

        // Then
        Assert.That(result, Is.SameAs(sut));
    }
}
