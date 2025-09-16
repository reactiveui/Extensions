// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Disposables;
using FluentAssertions;
using Xunit;

namespace ReactiveUI.Extensions.Tests;

/// <summary>
/// Tests disposable extensions.
/// </summary>
public class DisposableExtensionsTests
{
    /// <summary>
    /// Tests DisposeWith returns a disposable.
    /// </summary>
    [Fact]
    public void GivenNull_WhenDisposeWith_ThenExceptionThrown()
    {
        // Given
        var sut = Disposable.Create(() => { });

        // When
        var result = Record.Exception(() => sut.DisposeWith(null!));

        // Then
        result
            .Should()
            .BeOfType<ArgumentNullException>();
    }

    /// <summary>
    /// Tests DisposeWith disposes the underlying disposable.
    /// </summary>
    [Fact]
    public void GivenDisposable_WhenDisposeWith_ThenDisposed()
    {
        // Given
        var sut = new CompositeDisposable();
        var compositeDisposable = new CompositeDisposable();
        sut.DisposeWith(compositeDisposable);

        // When
        compositeDisposable.Dispose();

        // Then
        sut.IsDisposed
            .Should()
            .BeTrue();
    }

    /// <summary>
    /// Tests DisposeWith returns the original disposable.
    /// </summary>
    [Fact]
    public void GivenDisposable_WhenDisposeWith_ThenReturnsDisposable()
    {
        // Given, When
        var sut = new CompositeDisposable();
        var compositeDisposable = new CompositeDisposable();
        var result = sut.DisposeWith(compositeDisposable);

        // Then
        sut.Should()
            .BeEquivalentTo(result);
    }
}
