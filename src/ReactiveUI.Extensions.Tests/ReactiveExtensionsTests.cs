// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ReactiveUI.Extensions.Tests;

/// <summary>
/// Tests Reactive Extensions.
/// </summary>
public class ReactiveExtensionsTests
{
    /// <summary>
    /// Tests the WhereIsNotNull extension.
    /// </summary>
    [Test]
    public void GivenNull_WhenWhereIsNotNull_ThenNoNotification()
    {
        // Given, When
        bool? result = null;
        using var disposable = Observable.Return<bool?>(null).WhereIsNotNull().Subscribe(x => result = x);

        // Then
        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Tests the WhereIsNotNull extension.
    /// </summary>
    [Test]
    public void GivenValue_WhenWhereIsNotNull_ThenNotification()
    {
        // Given, When
        bool? result = null;
        using var disposable = Observable.Return<bool?>(false).WhereIsNotNull().Subscribe(x => result = x);

        // Then
        Assert.That(result, Is.False);
    }

    /// <summary>
    /// Tests the AsSignal extension.
    /// </summary>
    [Test]
    public void GivenObservable_WhenAsSignal_ThenNotifiesUnit()
    {
        // Given, When
        Unit? result = null;
        using var disposable = Observable.Return<bool?>(false).AsSignal().Subscribe(x => result = x);

        // Then
        Assert.That(result, Is.EqualTo(Unit.Default));
    }

    /// <summary>
    /// Syncronizes the asynchronous runs with asynchronous tasks in subscriptions.
    /// </summary>
    [Test]
    public void SubscribeSynchronus_RunsWithAsyncTasksInSubscriptions()
    {
        // Given, When
        var result = 0;
        var itterations = 0;
        var subject = new Subject<bool>();
        using var disposable = subject
            .SubscribeSynchronous(async x =>
            {
                if (x)
                {
                    await Task.Delay(1000);
                    result++;
                }
                else
                {
                    await Task.Delay(500);
                    result--;
                }

                itterations++;
            });

        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);

        while (itterations < 6)
        {
            Thread.Yield();
        }

        // Then
        Assert.That(result, Is.Zero);
    }

    /// <summary>
    /// Syncronizes the asynchronous runs with asynchronous tasks in subscriptions.
    /// </summary>
    [Test]
    public void SyncronizeAsync_RunsWithAsyncTasksInSubscriptions()
    {
        // Given, When
        var result = 0;
        var itterations = 0;
        var subject = new Subject<bool>();
        using var disposable = subject
            .SynchronizeAsync()
            .Subscribe(async x =>
            {
                if (x.Value)
                {
                    await Task.Delay(1000);
                    result++;
                }
                else
                {
                    await Task.Delay(500);
                    result--;
                }

                x.Sync.Dispose();
                itterations++;
            });

        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);

        while (itterations < 6)
        {
            Thread.Yield();
        }

        // Then
        Assert.That(result, Is.Zero);
    }

    /// <summary>
    /// Syncronizes the asynchronous runs with asynchronous tasks in subscriptions.
    /// </summary>
    [Test]
    public void SynchronizeSynchronous_RunsWithAsyncTasksInSubscriptions()
    {
        // Given, When
        var result = 0;
        var itterations = 0;
        var subject = new Subject<bool>();
        using var disposable = subject
            .SynchronizeSynchronous()
            .Subscribe(async x =>
            {
                if (x.Value)
                {
                    await Task.Delay(1000);
                    result++;
                }
                else
                {
                    await Task.Delay(500);
                    result--;
                }

                x.Sync.Dispose();
                itterations++;
            });

        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);

        while (itterations < 6)
        {
            Thread.Yield();
        }

        // Then
        Assert.That(result, Is.Zero);
    }

    /// <summary>
    /// Syncronizes the asynchronous runs with asynchronous tasks in subscriptions.
    /// </summary>
    [Test]
    public void SubscribeAsync_RunsWithAsyncTasksInSubscriptions()
    {
        // Given, When
        var result = 0;
        var itterations = 0;
        var subject = new Subject<bool>();
        using var disposable = subject
            .SubscribeAsync(async x =>
            {
                if (x)
                {
                    await Task.Delay(1000);
                    result++;
                }
                else
                {
                    await Task.Delay(500);
                    result--;
                }

                itterations++;
            });

        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);
        subject.OnNext(true);
        subject.OnNext(false);

        while (itterations < 6)
        {
            Thread.Yield();
        }

        // Then
        Assert.That(result, Is.Zero);
    }
}
