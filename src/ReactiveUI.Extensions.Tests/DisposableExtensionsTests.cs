// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables.Fluent;

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
    public async Task GivenNull_WhenDisposeWith_ThenExceptionThrown()
    {
        // Given
        var sut = Disposable.Create(() => { });

        // When
        var result = Assert.Throws<ArgumentNullException>(() => sut.DisposeWith(null!));

        // Then
        await Assert.That(result).IsTypeOf<ArgumentNullException>();
    }

    /// <summary>
    /// Tests DisposeWith disposes the underlying disposable.
    /// </summary>
    [Test]
    public async Task GivenDisposable_WhenDisposeWith_ThenDisposed()
    {
        // Given
        var sut = new CompositeDisposable();
        var compositeDisposable = new CompositeDisposable();
        sut.DisposeWith(compositeDisposable);

        // When
        compositeDisposable.Dispose();

        // Then
        await Assert.That(sut.IsDisposed).IsTrue();
    }

    /// <summary>
    /// Tests DisposeWith returns the original disposable.
    /// </summary>
    [Test]
    public async Task GivenDisposable_WhenDisposeWith_ThenReturnsDisposable()
    {
        // Given, When
        var sut = new CompositeDisposable();
        var compositeDisposable = new CompositeDisposable();
        var result = sut.DisposeWith(compositeDisposable);

        // Then
        await Assert.That(result).IsEquivalentTo(sut);
    }
}
