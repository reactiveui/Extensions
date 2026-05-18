// Copyright (c) 2019-2026 ReactiveUI Association Incorporated. All rights reserved.
// ReactiveUI Association Incorporated licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using ReactiveUI.Extensions.Internal;
#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
using System.Buffers.Binary;
#endif

namespace ReactiveUI.Extensions.Operators;

/// <summary>
/// Operator that randomly shuffles arrays emitted by the source.
/// Replaces the closure-based implementation in ReactiveExtensions.Shuffle.
/// </summary>
/// <typeparam name="T">The array element type.</typeparam>
/// <param name="source">The source observable emitting arrays.</param>
internal sealed class ShuffleObservable<T>(IObservable<T[]> source) : IObservable<T[]>
{
    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T[]> observer)
    {
        InvalidOperationExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observer);
        return source.Subscribe(new ShuffleObserver(observer));
    }

    /// <summary>
    /// Observer that shuffles arrays.
    /// </summary>
    /// <param name="downstream">The downstream observer receiving shuffled arrays.</param>
    private sealed class ShuffleObserver(IObserver<T[]> downstream) : IObserver<T[]>
    {
        /// <summary>Reusable random number generator.</summary>
        private readonly RandomNumberGenerator _random = RandomNumberGenerator.Create();

        /// <inheritdoc/>
        public void OnNext(T[] value)
        {
            if (value is null)
            {
                downstream.OnNext(value!);
                return;
            }

#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
#else
            var buffer = new byte[sizeof(uint)];
#endif
            var n = value.Length;
            while (n > 1)
            {
                n--;
                var maxExclusive = (uint)(n + 1);
                uint val;
                var limit = uint.MaxValue - (uint.MaxValue % maxExclusive);
                do
                {
                    _random.GetBytes(buffer);
#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                    val = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
#else
                    val = BitConverter.ToUInt32(buffer, 0);
#endif
                }
                while (val >= limit);

                var k = (int)(val % maxExclusive);
                (value[n], value[k]) = (value[k], value[n]);
            }

            downstream.OnNext(value);
        }

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            _random.Dispose();
            downstream.OnError(error);
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
            _random.Dispose();
            downstream.OnCompleted();
        }
    }
}
