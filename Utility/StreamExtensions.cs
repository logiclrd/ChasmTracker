using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.Utility;

public static class StreamExtensions
{
	public static void WriteStructure<T>(this Stream stream, T data)
		where T : notnull
	{
		stream.Write(StructureSerializer.MarshalToBytes(data));
	}

	public static T ReadStructure<T>(this Stream stream)
		where T : notnull
	{
		byte[] buffer = new byte[Marshal.SizeOf<T>()];

		stream.ReadExactly(buffer);

		return StructureSerializer.MarshalFromBytes<T>(buffer);
	}
}
