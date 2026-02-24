// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// The exception that is thrown when multiple concurrent calls are made to observer methods that do not support
/// concurrent execution.
/// </summary>
/// <remarks>This exception indicates that a call to OnNextAsync, OnErrorResumeAsync, or OnCompletedAsync was
/// attempted while a previous call to one of these methods is still pending. ObserverAsync{T} does not allow concurrent
/// invocations of these methods; callers should ensure that each call completes before initiating another.</remarks>
public class ConcurrentObserverCallsException() : Exception($"Concurrent calls of {nameof(ObserverAsync<>.OnNextAsync)}, {nameof(ObserverAsync<>.OnErrorResumeAsync)}, {nameof(ObserverAsync<>.OnCompletedAsync)} are not allowed. There is already a call pending");
