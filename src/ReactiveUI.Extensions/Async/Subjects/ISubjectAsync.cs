// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;

namespace ReactiveUI.Extensions.Async.Subjects;

/// <summary>
/// Represents an asynchronous subject that allows observers to receive values, errors, or completion notifications
/// asynchronously.
/// </summary>
/// <remarks>An asynchronous subject enables push-based notification of values, errors, or completion events to
/// multiple observers. Observers can subscribe to the subject's values stream and receive notifications as they are
/// published. This interface is typically used in scenarios where asynchronous event propagation and coordination are
/// required, such as reactive programming or event-driven architectures.</remarks>
/// <typeparam name="T">The type of the values observed and published by the subject.</typeparam>
public interface ISubjectAsync<T>
{
    /// <summary>
    /// Gets an observable sequence that asynchronously provides the current values of the collection.
    /// </summary>
    /// <remarks>The returned sequence emits updates whenever the underlying collection changes. Subscribers
    /// receive notifications asynchronously as values are added, removed, or updated.</remarks>
    ObservableAsync<T> Values { get; }

    /// <summary>
    /// Asynchronously processes the next value in the sequence.
    /// </summary>
    /// <param name="value">The value to be processed.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    ValueTask OnNextAsync(T value, CancellationToken cancellationToken);

    /// <summary>
    /// Handles the specified error and resumes asynchronous processing, if possible.
    /// </summary>
    /// <param name="error">The exception that caused the error. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the resume operation.</param>
    /// <returns>A ValueTask that represents the asynchronous operation to resume processing after an error.</returns>
    ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken);

    /// <summary>
    /// Performs asynchronous completion logic using the specified result.
    /// </summary>
    /// <param name="result">The result object that provides information required to complete the operation.</param>
    /// <returns>A ValueTask that represents the asynchronous completion operation.</returns>
    ValueTask OnCompletedAsync(Result result);
}
