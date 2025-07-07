using System.IO;

namespace ChasmTracker.Utility;

public static class StreamExtensions
{
	public static void WriteStructure<T>(this Stream stream, T data)
		where T : notnull
	{
		stream.Write(StructureSerializer.MarshalToBytes(data));
	}
}
