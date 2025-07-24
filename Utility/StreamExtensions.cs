using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.Utility;

public static class StreamExtensions
{
	[ThreadStatic]
	static byte[]? s_buffer;

	[MemberNotNull(nameof(s_buffer))]
	static void EnsureBuffer(int size)
	{
		if ((s_buffer == null) || (s_buffer.Length < size))
			s_buffer = new byte[size * 2];
	}

	public static void WriteStructure<T>(this Stream stream, T data)
		where T : notnull
	{
		stream.Write(StructureSerializer.MarshalToBytes(data, ref s_buffer));
	}

	public static T ReadStructure<T>(this Stream stream)
		where T : notnull
	{
		int structureSize = Marshal.SizeOf<T>();

		EnsureBuffer(structureSize);

		var slice = s_buffer.AsMemory(0, structureSize);

		stream.ReadExactly(slice.Span);

		return StructureSerializer.MarshalFromBytes<T>(slice);
	}

	public static string ReadString(this Stream stream, int length, Encoding? encoding = null, bool nullTerminated = true)
	{
		encoding ??= Encoding.ASCII;

		EnsureBuffer(length);

		var slice = s_buffer.Slice(0, length);

		stream.ReadExactly(slice);

		if (nullTerminated)
		{
			int terminator = slice.IndexOf((byte)0);

			if (terminator >= 0)
				slice = slice.Slice(0, terminator);
		}

		return encoding.GetString(slice);
	}

	public static void WriteString(this Stream stream, string str, int length, Encoding? encoding = null)
	{
		encoding ??= Encoding.ASCII;

		EnsureBuffer(length);

		var slice = s_buffer.Slice(0, length);

		encoding.GetBytes(str, slice);

		stream.Write(slice);
	}

	public static void WriteAlign(this Stream stream, int alignment)
	{
		long offset = stream.Position % alignment;

		if (offset != 0)
		{
			int paddingBytes = (int)(alignment - offset);

			EnsureBuffer(paddingBytes);

			Array.Clear(s_buffer, 0, paddingBytes);

			stream.Write(s_buffer, 0, paddingBytes);
		}
	}
}
