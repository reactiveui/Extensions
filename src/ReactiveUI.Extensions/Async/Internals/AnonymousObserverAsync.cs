// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async.Internals;

internal sealed class AnonymousObserverAsync<T>(Func<T, CancellationToken, ValueTask> onNextAsync,
                                                Func<Exception, CancellationToken, ValueTask>? onErrorResumeAsync = null,
                                                Func<Result, ValueTask>? onCompletedAsync = null) : ObserverAsync<T>
{
    protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => onNextAsync(value, cancellationToken);

    protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken)
    {
        if (onErrorResumeAsync is null)
        {
            UnhandledExceptionHandler.OnUnhandledException(error);
            return default;
        }

        return onErrorResumeAsync.Invoke(error, cancellationToken);
    }

    protected override ValueTask OnCompletedAsyncCore(Result result)
    {
        if (onCompletedAsync is null)
        {
            var exception = result.Exception;
            if (exception is not null)
            {
                UnhandledExceptionHandler.OnUnhandledException(exception);
            }

            return default;
        }

        return onCompletedAsync.Invoke(result);
    }
}
