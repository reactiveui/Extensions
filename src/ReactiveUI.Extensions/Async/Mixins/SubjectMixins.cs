// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for working with subjects in a reactive programming context.
/// </summary>
/// <remarks>The methods in this class enable interoperability between subjects and asynchronous observer
/// patterns. These extensions are intended to simplify the integration of subjects with APIs that expect asynchronous
/// observers.</remarks>
public static class SubjectMixins
{
    /// <summary>
    /// Creates an asynchronous observer wrapper for the specified subject.
    /// </summary>
    /// <typeparam name="T">The type of the elements processed by the subject and observer.</typeparam>
    /// <param name="subject">The subject to wrap as an asynchronous observer. Cannot be null.</param>
    /// <returns>An asynchronous observer that forwards notifications to the specified subject.</returns>
    public static ObserverAsync<T> AsObserverAsync<T>(this ISubjectAsync<T> subject) => new SubjectAsyncObserver<T>(subject);

    /// <summary>
    /// Creates a new subject that applies a transformation to the values of the source subject using the specified
    /// mapping function.
    /// </summary>
    /// <remarks>The returned subject reflects the mapped values of the original subject. Subscribers to the
    /// returned subject will observe the transformed sequence as defined by the mapper function. The mapping is applied
    /// to all values published by the source subject.</remarks>
    /// <typeparam name="T">The type of the elements processed by the subject.</typeparam>
    /// <param name="this">The source subject whose values are to be mapped.</param>
    /// <param name="mapper">A function that takes an asynchronous observable of type T and returns a transformed asynchronous observable of
    /// type T. This function defines how the values are mapped.</param>
    /// <returns>A subject that emits values transformed by the specified mapping function.</returns>
    public static ISubjectAsync<T> MapValues<T>(this ISubjectAsync<T> @this, Func<ObservableAsync<T>, ObservableAsync<T>> mapper) => new MappedSubject<T>(@this, mapper);

    private sealed class MappedSubject<T>(ISubjectAsync<T> original, Func<ObservableAsync<T>, ObservableAsync<T>> mapper) : ISubjectAsync<T>
    {
        public ObservableAsync<T> Values { get; } = mapper(original.Values);

        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken) => original.OnNextAsync(value, cancellationToken);

        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken) => original.OnErrorResumeAsync(error, cancellationToken);

        public ValueTask OnCompletedAsync(Result result) => original.OnCompletedAsync(result);
    }

    private sealed class SubjectAsyncObserver<T>(ISubjectAsync<T> subject) : ObserverAsync<T>
    {
        /// <summary>
        /// Asynchronously processes the next value in the sequence.
        /// </summary>
        /// <param name="value">The value to be processed by the observer.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A ValueTask that represents the asynchronous operation.</returns>
        protected override ValueTask OnNextAsyncCore(T value, CancellationToken cancellationToken) => subject.OnNextAsync(value, cancellationToken);

        /// <summary>
        /// Handles an error by resuming asynchronous processing according to the subject's error handling strategy.
        /// </summary>
        /// <param name="error">The exception that caused the error condition. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A ValueTask that represents the asynchronous error handling operation.</returns>
        protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => subject.OnErrorResumeAsync(error, cancellationToken);

        /// <summary>
        /// Performs asynchronous completion logic when the operation has finished, using the specified result.
        /// </summary>
        /// <param name="result">The result of the completed operation, containing any relevant outcome information.</param>
        /// <returns>A ValueTask that represents the asynchronous completion operation.</returns>
        protected override ValueTask OnCompletedAsyncCore(Result result) => subject.OnCompletedAsync(result);
    }
}
