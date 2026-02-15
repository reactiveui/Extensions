// Copyright (c) 2019-2025 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Extensions.Async.Disposables;
using ReactiveUI.Extensions.Async.Internals;
using ReactiveUI.Extensions.Async.Subjects;

namespace ReactiveUI.Extensions.Async;

/// <summary>
/// Provides extension methods for creating and manipulating asynchronous observable sequences.
/// </summary>
/// <remarks>The methods in this class enable advanced operations on asynchronous observables, such as grouping
/// elements by key. These extensions are intended for use with types implementing asynchronous observation patterns,
/// allowing developers to compose and transform streams of data in a reactive manner.</remarks>
public static partial class ObservableAsync
{
    /// <summary>
    /// Groups the elements of an asynchronous observable sequence according to a specified key selector function.
    /// </summary>
    /// <remarks>Each group in the resulting sequence corresponds to a unique key produced by the key
    /// selector. The groups are emitted as soon as their first element is encountered in the source sequence. The
    /// returned grouped observables can be subscribed to independently.</remarks>
    /// <typeparam name="TKey">The type of the key returned by the key selector function. Must be non-nullable.</typeparam>
    /// <typeparam name="TValue">The type of the elements in the source observable sequence.</typeparam>
    /// <param name="source">The asynchronous observable sequence whose elements are to be grouped.</param>
    /// <param name="keySelector">A function to extract the key for each element in the source sequence.</param>
    /// <returns>An asynchronous observable sequence of grouped observables, each containing elements that share a common key.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="keySelector"/> is null.</exception>
    public static ObservableAsync<GroupedAsyncObservable<TKey, TValue>> GroupBy<TKey, TValue>(this ObservableAsync<TValue> source, Func<TValue, TKey> keySelector)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (keySelector == null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }

        return new GroupByAsyncObservable<TKey, TValue>(source, keySelector, static _ => SubjectAsync.Create<TValue>());
    }

    /// <summary>
    /// Groups the elements of an asynchronous observable sequence according to a specified key selector function and
    /// returns an observable sequence of grouped observables.
    /// </summary>
    /// <remarks>Each group in the resulting sequence is represented by a <see
    /// cref="GroupedAsyncObservable{TKey, TValue}"/>, which exposes the group's key and an observable sequence of its
    /// elements. The <paramref name="groupSubjectSelector"/> parameter allows customization of the subject used for
    /// each group, which can affect how elements are buffered or multicast within the group.</remarks>
    /// <typeparam name="TKey">The type of the key returned by the key selector function. Must be non-null.</typeparam>
    /// <typeparam name="TValue">The type of the elements in the source observable sequence.</typeparam>
    /// <param name="source">The asynchronous observable sequence whose elements are to be grouped.</param>
    /// <param name="keySelector">A function to extract the key for each element in the source sequence.</param>
    /// <param name="groupSubjectSelector">A function that provides a subject for each group, given its key. Used to control how elements are published
    /// within each group.</param>
    /// <returns>An asynchronous observable sequence containing grouped observables, each representing a collection of elements
    /// that share a common key.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="keySelector"/> is null.</exception>
    public static ObservableAsync<GroupedAsyncObservable<TKey, TValue>> GroupBy<TKey, TValue>(this ObservableAsync<TValue> source, Func<TValue, TKey> keySelector, Func<TKey, ISubjectAsync<TValue>> groupSubjectSelector)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (keySelector == null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }

        return new GroupByAsyncObservable<TKey, TValue>(source, keySelector, groupSubjectSelector);
    }

    private sealed class GroupByAsyncObservable<TKey, TValue>(
        ObservableAsync<TValue> source,
        Func<TValue, TKey> keySelector,
        Func<TKey, ISubjectAsync<TValue>> groupSubjectSelector) : ObservableAsync<GroupedAsyncObservable<TKey, TValue>>
        where TKey : notnull
    {
        private readonly ObservableAsync<TValue> _source = source;
        private readonly Func<TValue, TKey> _keySelector = keySelector;
        private readonly Func<TKey, ISubjectAsync<TValue>> _groupSubjectSelector = groupSubjectSelector;

        protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(ObserverAsync<GroupedAsyncObservable<TKey, TValue>> observer, CancellationToken cancellationToken)
        {
            var subscrption = new Subscription(this, observer);
            try
            {
                return await subscrption.SubscribeAsync(cancellationToken);
            }
            catch
            {
                await subscrption.DisposeAsync();
                throw;
            }
        }

        private sealed class Subscription(GroupByAsyncObservable<TKey, TValue> parent, ObserverAsync<GroupedAsyncObservable<TKey, TValue>> observer) : ObserverAsync<TValue>
        {
            private readonly CompositeDisposableAsync _disposables = new();
            private Dictionary<TKey, ISubjectAsync<TValue>> _subjectsByKey = new();

            public ValueTask<IAsyncDisposable> SubscribeAsync(CancellationToken cancellationToken) => parent._source.SubscribeAsync(this, cancellationToken);

            protected override async ValueTask OnNextAsyncCore(TValue value, CancellationToken cancellationToken)
            {
                var key = parent._keySelector(value);
                if (!_subjectsByKey.TryGetValue(key, out var subject))
                {
                    subject = parent._groupSubjectSelector(key);
                    _subjectsByKey.Add(key, subject);
                    await observer.OnNextAsync(new Observable(this, key, subject.Values), cancellationToken);
                }

                await subject.OnNextAsync(value, cancellationToken);
            }

            protected override ValueTask OnErrorResumeAsyncCore(Exception error, CancellationToken cancellationToken) => observer.OnErrorResumeAsync(error, cancellationToken);

            protected override async ValueTask OnCompletedAsyncCore(Result result)
            {
                var subjects = _subjectsByKey.Values;
                _subjectsByKey = null!;
                foreach (var subject in subjects)
                {
                    await subject.OnCompletedAsync(result);
                }

                await observer.OnCompletedAsync(result);
            }

            protected override async ValueTask DisposeAsyncCore()
            {
                await base.DisposeAsyncCore();
                await _disposables.DisposeAsync();
            }

            internal class Observable(Subscription parent, TKey key, ObservableAsync<TValue> subjectValues) : GroupedAsyncObservable<TKey, TValue>
            {
                /// <summary>
                /// Gets the key associated with this element.
                /// </summary>
                public override TKey Key => key;

                protected override async ValueTask<IAsyncDisposable> SubscribeAsyncCore(ObserverAsync<TValue> observer, CancellationToken cancellationToken)
                {
                    var subscription = await subjectValues.SubscribeAsync(observer.Wrap(), cancellationToken);
                    await parent._disposables.AddAsync(subscription);
                    return DisposableAsync.Create(async () =>
                    {
                        await parent._disposables.Remove(subscription);
                        await subscription.DisposeAsync();
                    });
                }
            }
        }
    }
}
