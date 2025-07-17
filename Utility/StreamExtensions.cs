using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.Utility;

public static class StreamExtensions
{
	[ThreadStatic]
	static byte[]? s_buffer;

	public static void WriteStructure<T>(this Stream stream, T data)
		where T : notnull
	{
		stream.Write(StructureSerializer.MarshalToBytes(data, ref s_buffer));
	}

	public static T ReadStructure<T>(this Stream stream)
		where T : notnull
	{
		int structureSize = Marshal.SizeOf<T>();

		if ((s_buffer == null) || (s_buffer.Length < structureSize))
			s_buffer = new byte[structureSize * 2];

		var slice = s_buffer.AsMemory(0, structureSize);

		stream.ReadExactly(slice.Span);

		return StructureSerializer.MarshalFromBytes<T>(slice);
	}

	public static string ReadString(this Stream stream, int length, Encoding? encoding = null, bool nullTerminated = true)
	{
		encoding ??= Encoding.ASCII;

		if ((s_buffer == null) || (s_buffer.Length < length))
			s_buffer = new byte[length * 2];

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
}
