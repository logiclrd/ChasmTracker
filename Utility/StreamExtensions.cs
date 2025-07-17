using System;
using System.IO;
using System.Runtime.InteropServices;

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
}
