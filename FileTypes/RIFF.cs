using System.IO;

namespace ChasmTracker.FileTypes;

public class RIFF
{
	public static IFFChunk? PeekChunk(Stream stream)
	{
		return IFF.PeekChunkEx(stream, ChunkFlags.Aligned | ChunkFlags.SizeLittleEndian);
	}
}
