// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace ReactiveUI.Extensions.Async.Internals;

internal abstract class TaskObserverAsyncBase<T, TTaskValue>(CancellationToken cancellationToken) : ObserverAsync<T>
{
    private readonly TaskCompletionSource<TTaskValue> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationToken _cancellationToken = cancellationToken;

    public async ValueTask<TTaskValue> WaitValueAsync()
    {
        try
        {
            using var ct = _cancellationToken.Register(
                static x =>
            {
                var @this = (TaskObserverAsyncBase<T, TTaskValue>)x!;
                @this._tcs.TrySetException(new OperationCanceledException(@this._cancellationToken));
            },
                this);

            return await _tcs.Task;
        }
        finally
        {
            await DisposeAsync();
        }
    }

    [DebuggerStepThrough]
    protected async ValueTask TrySetCompleted(TTaskValue value)
    {
        try
        {
            _tcs.TrySetResult(value);
        }
        finally
        {
            await DisposeAsync();
        }
    }

    protected async ValueTask TrySetException(Exception e)
    {
        try
        {
            _tcs.TrySetException(e);
        }
        finally
        {
            await DisposeAsync();
        }
    }
}
