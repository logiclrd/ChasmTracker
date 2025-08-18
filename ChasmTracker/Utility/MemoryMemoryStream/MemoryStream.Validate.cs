// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Runtime.CompilerServices;

namespace ChasmTracker.Utility.MemoryMemoryStream
{
	/// <summary>
	/// A factory class to produce <see cref="MemoryStream{TSource}"/> instances.
	/// </summary>
	internal static partial class MemoryStreamFactory
	{
		/// <summary>
		/// Validates the <see cref="Stream.Position"/> argument (it needs to be in the [0, length]) range.
		/// </summary>
		/// <param name="position">The new <see cref="Stream.Position"/> value being set.</param>
		/// <param name="length">The maximum length of the target <see cref="Stream"/>.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ValidatePosition(long position, int length)
		{
			if ((ulong)position > (ulong)length)
			{
				throw new ArgumentOutOfRangeException(nameof(position));
			}
		}

		/// <summary>
		/// Validates the <see cref="Stream.Read(byte[],int,int)"/> or <see cref="Stream.Write(byte[],int,int)"/> arguments.
		/// </summary>
		/// <param name="buffer">The target array.</param>
		/// <param name="offset">The offset within <paramref name="buffer"/>.</param>
		/// <param name="count">The number of elements to process within <paramref name="buffer"/>.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ValidateBuffer(byte[]? buffer, int offset, int count)
		{
			if (buffer is null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(offset));
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			if (offset + count > buffer!.Length)
			{
				throw new ArgumentException("The specified range extends past the end of the buffer.");
			}
		}

		/// <summary>
		/// Validates the <see cref="MemoryStream{TSource}.CanWrite"/> property.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ValidateCanWrite(bool canWrite)
		{
			if (!canWrite)
			{
				throw new NotSupportedException();
			}
		}

		/// <summary>
		/// Validates that a given <see cref="Stream"/> instance hasn't been disposed.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ValidateDisposed(bool disposed)
		{
			if (disposed)
			{
				throw new ObjectDisposedException(nameof(MemoryStreamFactory));
			}
		}
	}
}