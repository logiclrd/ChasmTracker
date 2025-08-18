using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
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

	static object s_sync = new object();
	static MethodInfo s_writeStructureMethodDefinition = typeof(StreamExtensions).GetMethod("WriteStructure")!;
	static Dictionary<Type, Delegate> s_writeStructureMethods = new Dictionary<Type, Delegate>();
	static MethodInfo s_readStructureMethodDefinition = typeof(StreamExtensions).GetMethod("ReadStructure")!;
	static Dictionary<Type, Delegate> s_readStructureMethods = new Dictionary<Type, Delegate>();

	public static void WriteStructure<T>(this Stream stream, T data)
		where T : notnull
	{
		stream.Write(StructureSerializer.MarshalToBytes(data, ref s_buffer));
	}

	[ThreadStatic]
	static Dictionary<Type, int>? s_typeSize;

	static readonly MethodInfo s_sizeOfMethodDefinition = typeof(Marshal).GetMethod(nameof(Marshal.SizeOf), Array.Empty<Type>())!;

	public static T ReadStructure<T>(this Stream stream)
		where T : notnull
	{
		s_typeSize ??= new Dictionary<Type, int>();

		var type = typeof(T);

		if (!s_typeSize.TryGetValue(type, out int structureSize))
		{
			if (type.IsEnum)
				type = type.GetEnumUnderlyingType();

			var sizeOf = s_sizeOfMethodDefinition.MakeGenericMethod(type);

			var sizeOfDelegate = sizeOf.CreateDelegate<Func<int>>();

			structureSize = sizeOfDelegate();
		}

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

	public static Stream Slice(this Stream stream, long offset)
		=> Slice(stream, offset, stream.Length - offset);

	public static Stream Slice(this Stream stream, long offset, long length)
		=> new SubStream(stream, offset, length);
}
