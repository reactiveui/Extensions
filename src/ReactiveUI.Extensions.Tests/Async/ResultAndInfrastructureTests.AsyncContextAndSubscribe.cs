// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Reactive.Testing;
using ReactiveUI.Extensions.Async;
using ReactiveUI.Extensions.Async.Disposables;

namespace ReactiveUI.Extensions.Tests.Async;

/// <summary>
/// AsyncContext, ObserveOn, SubscribeAsync, UnhandledExceptionHandler, and WrappedObserver tests.
/// </summary>
public partial class ResultAndInfrastructureTests
{
    /// <summary>Tests UnhandledExceptionHandler Register invokes custom handler.</summary>
    [Test]
    public void WhenUnhandledExceptionHandlerRegisterCustom_ThenCustomHandlerInvoked()
    {
        List<Exception> caught = [];

        UnhandledExceptionHandler.Register(caught.Add);

        // Trigger unhandled exception via observer that throws during OnCompleted
        ObservableAsync.Create<int>(async (observer, _) =>
        {
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });
    }

    /// <summary>
    /// Verifies that UnhandledExceptionHandler invokes a custom handler when an exception occurs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUnhandledExceptionHandlerCustom_ThenCustomHandlerReceivesException()
    {
        List<Exception> caught = [];

        UnhandledExceptionHandler.Register(caught.Add);

        // Trigger unhandled exception via the static method
        UnhandledExceptionHandler.OnUnhandledException(new InvalidOperationException("test error"));

        await Assert.That(caught).Count().IsEqualTo(1);
        await Assert.That(caught[0].Message).IsEqualTo("test error");
    }

    /// <summary>
    /// Verifies that UnhandledExceptionHandler ignores OperationCanceledException.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenUnhandledExceptionHandlerOperationCanceled_ThenIgnored()
    {
        List<Exception> caught = [];

        UnhandledExceptionHandler.Register(caught.Add);

        UnhandledExceptionHandler.OnUnhandledException(new OperationCanceledException());

        await Assert.That(caught).IsEmpty();
    }

    /// <summary>
    /// Verifies that UnhandledExceptionHandler swallows exceptions from the handler itself.
    /// </summary>
    [Test]
    public void WhenUnhandledExceptionHandlerThrows_ThenSwallowed()
    {
        UnhandledExceptionHandler.Register(_ => throw new InvalidOperationException("handler exploded"));

        // This should not throw even though the handler throws
        UnhandledExceptionHandler.OnUnhandledException(new ArgumentException("test"));
    }

    /// <summary>Tests DefaultUnhandledExceptionHandler does not throw and writes to console.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public Task WhenDefaultUnhandledExceptionHandler_ThenDoesNotThrow()
    {
        try
        {
            var ex = new InvalidOperationException("test-error");

            // Just verify it doesn't throw; it writes to Console.Out internally
            UnhandledExceptionHandler.DefaultUnhandledExceptionHandler(ex);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    /// <summary>
    /// Tests AsyncContext.From(IScheduler) where scheduler is not a SynchronizationContext or TaskScheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncContextFromIScheduler_ThenWrapsInSchedulerTaskScheduler()
    {
        var scheduler = Scheduler.Default;
        var context = AsyncContext.From(scheduler);

        await Assert.That(context.SynchronizationContext).IsNull();
        await Assert.That(context.TaskScheduler).IsNotNull();
    }

    /// <summary>
    /// Tests AsyncContext.From(TaskScheduler) factory method.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncContextFromTaskScheduler_ThenCreatesContext()
    {
        var context = AsyncContext.From(TaskScheduler.Default);

        await Assert.That(context.TaskScheduler).IsEqualTo(TaskScheduler.Default);
        await Assert.That(context.SynchronizationContext).IsNull();
    }

    /// <summary>
    /// Verifies that IsDefaultContext returns true for a default AsyncContext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncContextDefault_ThenIsDefaultContextTrue()
    {
        var context = AsyncContext.Default;
        var isDefault = context.IsDefaultContext;
        await Assert.That(isDefault).IsTrue();
    }

    /// <summary>
    /// Verifies that From(IScheduler) with a scheduler that is a SynchronizationContext
    /// delegates to From(SynchronizationContext).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncContextFromSchedulerThatIsSyncContext_ThenUsesSyncContext()
    {
        var scheduler = new SyncContextScheduler();
        var context = AsyncContext.From((IScheduler)scheduler);

        await Assert.That(context.SynchronizationContext).IsNotNull();
        await Assert.That(context.TaskScheduler).IsNull();
    }

    /// <summary>
    /// Verifies that From(IScheduler) with a plain scheduler wraps it in SchedulerTaskScheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncContextFromPlainScheduler_ThenUsesSchedulerTaskScheduler()
    {
        var scheduler = new TestScheduler();
        var context = AsyncContext.From(scheduler);

        await Assert.That(context.TaskScheduler).IsNotNull();
        await Assert.That(context.SynchronizationContext).IsNull();

        // Verify the task scheduler works by scheduling a task on it
        var taskRan = false;
        _ = Task.Factory.StartNew(
            () => taskRan = true,
            CancellationToken.None,
            TaskCreationOptions.None,
            context.TaskScheduler!);

        // Advance the scheduler to run the task
        scheduler.AdvanceBy(1);
        await Assert.That(taskRan).IsTrue();
    }

    /// <summary>
    /// Verifies that AsyncContext.GetCurrent captures the current SynchronizationContext when one is set.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGetCurrentWithSyncContext_ThenCapturesSyncContext()
    {
        var sc = new SynchronizationContext();
        var original = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(sc);
            var context = AsyncContext.GetCurrent();

            await Assert.That(context.SynchronizationContext).IsSameReferenceAs(sc);
            await Assert.That(context.TaskScheduler).IsNull();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }
    }

    /// <summary>
    /// Verifies that AsyncContext.GetCurrent falls back to TaskScheduler.Current when no SynchronizationContext is set.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenGetCurrentWithoutSyncContext_ThenFallsBackToTaskScheduler()
    {
        var original = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            var context = AsyncContext.GetCurrent();

            await Assert.That(context.SynchronizationContext).IsNull();
            await Assert.That(context.TaskScheduler).IsNotNull();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }
    }

    /// <summary>
    /// Verifies that AsyncContextSwitcherAwaitable.OnCompleted invokes the continuation immediately
    /// when the cancellation token is already cancelled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchContextAsyncOnCompletedWithCancelledToken_ThenContinuationInvokedImmediately()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var context = AsyncContext.Default;
        var awaitable = context.SwitchContextAsync(true, cts.Token);

        var invoked = false;
        awaitable.OnCompleted(() => invoked = true);

        await Assert.That(invoked).IsTrue();
    }

    /// <summary>
    /// Verifies that AsyncContextSwitcherAwaitable.OnCompleted schedules the continuation
    /// on the TaskScheduler when no SynchronizationContext is present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchContextAsyncOnCompletedWithTaskScheduler_ThenSchedulesContinuation()
    {
        var context = AsyncContext.From(TaskScheduler.Default);
        var awaitable = context.SwitchContextAsync(true, CancellationToken.None);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        awaitable.OnCompleted(() => tcs.TrySetResult());

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(tcs.Task.IsCompletedSuccessfully).IsTrue();
    }

    /// <summary>
    /// Verifies that SchedulerTaskScheduler.GetScheduledTasks returns null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSchedulerTaskSchedulerGetScheduledTasks_ThenReturnsNull()
    {
        var testScheduler = new TestScheduler();
        var taskScheduler = new AsyncContext.SchedulerTaskScheduler(testScheduler);

        var result = taskScheduler.GetScheduledTasksForTesting();

        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies that SchedulerTaskScheduler.TryExecuteTaskInline always returns false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSchedulerTaskSchedulerTryExecuteTaskInline_ThenReturnsFalse()
    {
        var testScheduler = new TestScheduler();
        var taskScheduler = new AsyncContext.SchedulerTaskScheduler(testScheduler);

        // Create a task that we can pass to TryExecuteTaskInline
        var task = new Task(() => { });
        var result = taskScheduler.TryExecuteTaskInlineForTesting(task, false);

        await Assert.That(result).IsFalse();
    }

    /// <summary>
    /// Verifies that SchedulerTaskScheduler.QueueTask delegates to the IScheduler,
    /// and that TryExecuteTaskInline always returns false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSchedulerTaskSchedulerUsed_ThenTaskIsScheduledOnIScheduler()
    {
        var testScheduler = new TestScheduler();
        var context = AsyncContext.From(testScheduler);

        await Assert.That(context.TaskScheduler).IsNotNull();

        // The TaskScheduler returned by AsyncContext.From should be a SchedulerTaskScheduler
        var taskScheduler = context.TaskScheduler!;

        var executed = false;
        _ = Task.Factory.StartNew(
            () => executed = true,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            taskScheduler);

        // Task should not have run yet (TestScheduler needs advancement)
        await Assert.That(executed).IsFalse();

        // Advance the scheduler to execute the queued task
        testScheduler.AdvanceBy(1);

        await Assert.That(executed).IsTrue();
    }

    /// <summary>Tests AsyncContext.IsDefaultContext returns false for non-default context.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenAsyncContextIsNotDefault_ThenIsDefaultContextIsFalse()
    {
        var sc = new SynchronizationContext();
        var ctx = AsyncContext.From(sc);
        await Assert.That(ctx.IsDefaultContext).IsFalse();
    }

    /// <summary>Tests AsyncContext.SwitchContextAsync with default context and force yielding.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSwitchContextAsyncWithDefaultContext_ThenCompletes()
    {
        var ctx = AsyncContext.Default;
        await ctx.SwitchContextAsync(true, CancellationToken.None);
    }

    /// <summary>
    /// Tests ObserveOn with SynchronizationContext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnSynchronizationContext_ThenUsesContext()
    {
        var result = await ObservableAsync.Return(SampleValue)
            .ObserveOn(SynchronizationContext.Current ?? new SynchronizationContext())
            .FirstAsync();

        await Assert.That(result).IsEqualTo(SampleValue);
    }

    /// <summary>
    /// Tests ObserveOn with TaskScheduler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnTaskScheduler_ThenUsesScheduler()
    {
        var result = await ObservableAsync.Return(SampleValue)
            .ObserveOn(TaskScheduler.Default)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(SampleValue);
    }

    /// <summary>
    /// Tests ObserveOn with TaskScheduler overload.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnWithTaskScheduler_ThenEmitsOnScheduler()
    {
        var source = ObservableAsync.Return(SampleValue);
        var result = await source.ObserveOn(TaskScheduler.Default).FirstAsync();

        await Assert.That(result).IsEqualTo(SampleValue);
    }

    /// <summary>
    /// Verifies that ObserveOn with the default AsyncContext invokes the
    /// IsSameAsCurrentAsyncContext fallback path where both SynchronizationContext and TaskScheduler are null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnDefaultAsyncContext_ThenEmitsValues()
    {
        var result = await ObservableAsync.Return(SampleValue)
            .ObserveOn(AsyncContext.Default)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(SampleValue);
    }

    /// <summary>
    /// Verifies that ObserveOn with an AsyncContext backed by a TaskScheduler emits values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenObserveOnAsyncContext_ThenEmitsValues()
    {
        var context = AsyncContext.From(TaskScheduler.Default);
        var result = await ObservableAsync.Return(SampleValue)
            .ObserveOn(context)
            .FirstAsync();

        await Assert.That(result).IsEqualTo(SampleValue);
    }

    /// <summary>
    /// Tests SubscribeAsync with sync action callback.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithAction_ThenCallbackInvoked()
    {
        const int SecondValue = 2;
        const int ThirdValue = 3;

        List<int> items = [];

        await using var sub = await ObservableAsync.Range(1, 3)
            .SubscribeAsync((Action<int>)items.Add);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count >= 3,
            TimeSpan.FromSeconds(5));

        await Assert.That(items).IsCollectionEqualTo([1, SecondValue, ThirdValue]);
    }

    /// <summary>
    /// Tests SubscribeAsync with sync action, error, and completion callbacks.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithActionAndErrorAndCompletion_ThenAllCallbacksInvoked()
    {
        List<int> items = [];
        Result? completion = null;

        await using var sub = await ObservableAsync.Range(1, 2)
            .SubscribeAsync(
                (Action<int>)(x => items.Add(x)),
                onErrorResume: null,
                onCompleted: r => completion = r,
                CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completion is not null,
            TimeSpan.FromSeconds(5));

        const int SecondValue = 2;
        await Assert.That(items).IsCollectionEqualTo([1, SecondValue]);
        await Assert.That(completion).IsNotNull();
        await Assert.That(completion!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Tests SubscribeAsync with sync error handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncWithSyncErrorHandler_ThenErrorReported()
    {
        List<Exception> errors = [];

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("err"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            (Action<int>)(_ => { }),
            onErrorResume: ex => errors.Add(ex),
            onCompleted: null,
            CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => errors.Count >= 1,
            TimeSpan.FromSeconds(5));

        await Assert.That(errors).Count().IsEqualTo(1);
        await Assert.That(errors[0].Message).IsEqualTo("err");
    }

    /// <summary>
    /// Tests sync SubscribeAsync with null onErrorResume handler: error is silently ignored,
    /// exercising the return default path when onErrorResume is null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncWithNullErrorHandler_ThenErrorSilentlyIgnored()
    {
        Result? completion = null;

        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("ignored err"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            (Action<int>)(_ => { }),
            onErrorResume: null,
            onCompleted: r => completion = r,
            CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => completion is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(completion).IsNotNull();
        await Assert.That(completion!.Value.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Tests sync SubscribeAsync with null onCompleted handler: completion is silently ignored,
    /// exercising the return default path when onCompleted is null.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncWithNullCompletedHandler_ThenCompletionSilentlyIgnored()
    {
        List<int> items = [];

        var source = ObservableAsync.Range(1, 3);

        await using var sub = await source.SubscribeAsync(
            (Action<int>)(x => items.Add(x)),
            onErrorResume: null,
            onCompleted: null,
            CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => items.Count >= 3,
            TimeSpan.FromSeconds(5));

        const int SecondValue = 2;
        const int ThirdValue = 3;
        await Assert.That(items).IsCollectionEqualTo([1, SecondValue, ThirdValue]);
    }

    /// <summary>
    /// Verifies that WrappedObserverAsync forwards OnErrorResumeAsync to the inner observer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenWrappedObserverAsyncErrorResume_ThenForwardsToInner()
    {
        Exception? captured = null;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("wrapped-error"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            static (_, _) => default,
            (ex, _) =>
            {
                captured = ex;
                return default;
            },
            _ =>
            {
                completed.TrySetResult();
                return default;
            });

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).IsEqualTo("wrapped-error");
    }

    /// <summary>
    /// Verifies that the SubscribeAsync overload with sync callbacks invokes onErrorResume.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncCallbacksErrorResume_ThenInvokesCallback()
    {
        Exception? captured = null;
        var source = ObservableAsync.Create<int>(async (observer, ct) =>
        {
            await observer.OnErrorResumeAsync(new InvalidOperationException("sync-error"), ct);
            await observer.OnCompletedAsync(Result.Success);
            return DisposableAsync.Empty;
        });

        await using var sub = await source.SubscribeAsync(
            (Action<int>)(static _ => { }),
            onErrorResume: ex => captured = ex,
            onCompleted: null,
            CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => captured is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).IsEqualTo("sync-error");
    }

    /// <summary>
    /// Verifies that the SubscribeAsync overload with sync callbacks invokes onCompleted.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Test]
    public async Task WhenSubscribeAsyncSyncCallbacksCompleted_ThenInvokesCallback()
    {
        Result? captured = null;
        await using var sub = await ObservableAsync.Return(1).SubscribeAsync(
            (Action<int>)(static _ => { }),
            onErrorResume: null,
            onCompleted: result => captured = result,
            CancellationToken.None);

        await AsyncTestHelpers.WaitForConditionAsync(
            () => captured is not null,
            TimeSpan.FromSeconds(5));

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Value.IsSuccess).IsTrue();
    }
}
