// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Represents an asynchronous execution context that encapsulates a specific SynchronizationContext or TaskScheduler
/// for controlling the scheduling of asynchronous operations.
/// </summary>
/// <remarks>Use AsyncContext to capture and restore a particular synchronization or task scheduling environment
/// when running asynchronous code. This is useful for ensuring that continuations or asynchronous callbacks execute on
/// a desired context, such as a UI thread or a custom scheduler. An AsyncContext can be created from either a
/// SynchronizationContext or a TaskScheduler, but not both at the same time. The Default context represents the absence
/// of a specific synchronization or scheduling context, and typically corresponds to the default task
/// scheduler.</remarks>
public record AsyncContext
{
    private AsyncContext()
    {
    }

    /// <summary>
    /// Gets the default instance of the AsyncContext class.
    /// </summary>
    /// <remarks>Use this property to access a shared, default AsyncContext instance when a custom context is
    /// not required.</remarks>
    public static AsyncContext Default { get; } = new();

    /// <summary>
    /// Gets the synchronization context to use for marshaling callbacks and continuations.
    /// </summary>
    /// <remarks>If this property is set, callbacks and continuations will be posted to the specified
    /// synchronization context. If null, the default context is used, which may result in execution on a thread pool
    /// thread. This property is typically used to ensure that asynchronous operations resume on a specific thread or
    /// context, such as a UI thread.</remarks>
    public SynchronizationContext? SynchronizationContext { get; init; }

    /// <summary>
    /// Gets the task scheduler to use for scheduling tasks, or null to use the default scheduler.
    /// </summary>
    public TaskScheduler? TaskScheduler { get; init; }

    /// <summary>
    /// Gets a value indicating whether the current context uses the default task scheduler and no synchronization
    /// context.
    /// </summary>
    internal bool IsDefaultContext => SynchronizationContext is null && (TaskScheduler is null || TaskScheduler == TaskScheduler.Default);

    /// <summary>
    /// Creates a new AsyncContext that uses the specified SynchronizationContext for asynchronous operations.
    /// </summary>
    /// <remarks>The returned AsyncContext will have its TaskScheduler property set to null. Use this method
    /// when you want to control asynchronous execution using a specific SynchronizationContext, such as for UI thread
    /// synchronization.</remarks>
    /// <param name="synchronizationContext">The SynchronizationContext to associate with the AsyncContext. Cannot be null.</param>
    /// <returns>An AsyncContext instance configured to use the provided SynchronizationContext.</returns>
    /// <exception cref="ArgumentNullException">Thrown if synchronizationContext is null.</exception>
    public static AsyncContext From(SynchronizationContext synchronizationContext)
    {
        ArgumentExceptionHelper.ThrowIfNull(synchronizationContext, nameof(synchronizationContext));

        return new()
        {
            SynchronizationContext = synchronizationContext,
            TaskScheduler = null
        };
    }

    /// <summary>
    /// Creates a new AsyncContext that uses the specified TaskScheduler for task execution.
    /// </summary>
    /// <param name="taskScheduler">The TaskScheduler to associate with the new AsyncContext. Cannot be null.</param>
    /// <returns>An AsyncContext instance configured to use the specified TaskScheduler. The SynchronizationContext property of
    /// the returned instance is set to null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if taskScheduler is null.</exception>
    public static AsyncContext From(TaskScheduler taskScheduler)
    {
        ArgumentExceptionHelper.ThrowIfNull(taskScheduler, nameof(taskScheduler));

        return new()
        {
            SynchronizationContext = null,
            TaskScheduler = taskScheduler
        };
    }

    /// <summary>
    /// Creates a new AsyncContext using the specified scheduler for task and synchronization context management.
    /// </summary>
    /// <remarks>If the provided scheduler directly implements <see cref="SynchronizationContext"/> or
    /// <see cref="TaskScheduler"/>, those instances are used directly. Otherwise, the scheduler is wrapped
    /// in a <see cref="TaskScheduler"/> adapter that delegates task execution to the scheduler.</remarks>
    /// <param name="scheduler">The scheduler to use for configuring the AsyncContext.</param>
    /// <returns>An AsyncContext instance configured with the provided scheduler.</returns>
    /// <exception cref="ArgumentNullException">Thrown if scheduler is null.</exception>
    public static AsyncContext From(IScheduler scheduler)
    {
        ArgumentExceptionHelper.ThrowIfNull(scheduler, nameof(scheduler));

        if (scheduler is SynchronizationContext sc)
        {
            return From(sc);
        }

        return new()
        {
            SynchronizationContext = null,
            TaskScheduler = scheduler as TaskScheduler ?? new SchedulerTaskScheduler(scheduler)
        };
    }

    /// <summary>
    /// Gets the current asynchronous context associated with the calling thread.
    /// </summary>
    /// <remarks>Use this method to capture the context for scheduling asynchronous operations that should
    /// continue on the same logical thread or synchronization context. This is commonly used to ensure code executes on
    /// the appropriate context, such as a UI thread in desktop applications.</remarks>
    /// <returns>An <see cref="AsyncContext"/> representing the current asynchronous context. If a <see
    /// cref="SynchronizationContext"/> is present, it is used; otherwise, the current <see cref="TaskScheduler"/> is
    /// used.</returns>
    public static AsyncContext GetCurrent()
    {
        var currentSc = SynchronizationContext.Current;
        return currentSc is not null ? From(currentSc) : From(TaskScheduler.Current);
    }

    /// <summary>
    /// Creates an awaitable that switches execution to the associated asynchronous context.
    /// </summary>
    /// <param name="forceYielding">true to always yield execution to the context, even if already in the correct context; otherwise, false to avoid
    /// yielding if already in the context.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the context switch operation.</param>
    /// <returns>An awaitable that completes when execution has switched to the asynchronous context.</returns>
    public AsyncContextSwitcherAwaitable SwitchContextAsync(bool forceYielding, CancellationToken cancellationToken) => new(this, forceYielding, cancellationToken);

    /// <summary>
    /// Provides an awaitable that switches execution to a specified asynchronous context, optionally forcing a yield
    /// and supporting cancellation.
    /// </summary>
    /// <remarks>Use this struct to ensure that code after an await resumes on a specific asynchronous
    /// context, such as a particular SynchronizationContext or TaskScheduler. If cancellation is requested before the
    /// continuation is scheduled, the continuation is invoked immediately and an OperationCanceledException will be
    /// thrown when GetResult is called. This type is intended for advanced scenarios where precise control over
    /// asynchronous context switching is required.</remarks>
    /// <param name="AsyncContext">The asynchronous context to which execution should be switched when awaited.</param>
    /// <param name="ForceYielding">true to always yield execution even if already in the target context; otherwise, false to avoid yielding if
    /// already in the specified context.</param>
    /// <param name="CancellationToken">A cancellation token that can be used to cancel the await operation before the continuation is scheduled.</param>
    public readonly record struct AsyncContextSwitcherAwaitable(AsyncContext AsyncContext, bool ForceYielding, CancellationToken CancellationToken) : INotifyCompletion
    {
        /// <summary>
        /// Gets a value indicating whether the asynchronous operation has completed in the current context.
        /// </summary>
        public bool IsCompleted => !ForceYielding && AsyncContext.IsSameAsCurrentAsyncContext();

        /// <summary>
        /// Checks whether the associated cancellation token has had cancellation requested and throws an exception if
        /// so.
        /// </summary>
        /// <remarks>This method is typically used to observe cancellation requests and respond by
        /// throwing an OperationCanceledException if cancellation has been signaled. If cancellation has not been
        /// requested, the method returns normally.</remarks>
        public void GetResult() => CancellationToken.ThrowIfCancellationRequested();

        /// <summary>
        /// Returns an awaiter for this AsyncContextSwitcherAwaitable instance, enabling use of the await keyword to
        /// asynchronously switch execution context.
        /// </summary>
        /// <returns>An awaiter that can be used to await this instance and perform an asynchronous context switch.</returns>
        public AsyncContextSwitcherAwaitable GetAwaiter() => this;

        /// <summary>
        /// Schedules the specified continuation action to be invoked when the operation has completed.
        /// </summary>
        /// <remarks>If a synchronization context is available, the continuation is posted to it;
        /// otherwise, the continuation is scheduled on the associated task scheduler or the default task scheduler. If
        /// the operation has already been canceled, the continuation is invoked immediately on the current
        /// thread.</remarks>
        /// <param name="continuation">The action to execute when the operation is complete. Cannot be null.</param>
        public void OnCompleted(Action continuation)
        {
            ArgumentExceptionHelper.ThrowIfNull(continuation, nameof(continuation));

            if (CancellationToken.IsCancellationRequested)
            {
                continuation();
                return;
            }

            var sc = AsyncContext.SynchronizationContext;
            if (sc is not null)
            {
                sc.Post(c => ((Action)c!).Invoke(), continuation);
                return;
            }

            var ts = AsyncContext.TaskScheduler ?? TaskScheduler.Default;
            Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
        }
    }

    /// <summary>
    /// Provides a custom TaskScheduler that schedules tasks using the specified IScheduler.
    /// </summary>
    /// <remarks>This TaskScheduler enables integration of Task-based asynchronous code with reactive or
    /// custom scheduling strategies by delegating task execution to the provided IScheduler. Tasks scheduled through
    /// this TaskScheduler will be executed according to the policies of the specified IScheduler. This class is
    /// intended for advanced scenarios where control over task scheduling is required.</remarks>
    /// <param name="scheduler">The IScheduler used to schedule and execute tasks. Cannot be null.</param>
    private sealed class SchedulerTaskScheduler(IScheduler scheduler) : TaskScheduler
    {
        /// <inheritdoc/>
        protected override IEnumerable<Task>? GetScheduledTasks() => null;

        /// <inheritdoc/>
        protected override void QueueTask(Task task) =>
            scheduler.Schedule(task, (_, t) =>
            {
                TryExecuteTask(t);
                return Disposable.Empty;
            });

        /// <inheritdoc/>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;
    }
}
