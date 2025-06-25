using System;
using System.IO;

namespace ChasmTracker.FileTypes;

public class SampleCompression
{
	public static int ITDecompress8(Span<sbyte> dest, int offset, int len, Stream fp, bool it215, int channels)
	{
		throw new NotImplementedException();
	}

	public static int ITDecompress16(Span<short> dest, int offset, int len, Stream fp, bool it215, int channels)
	{
		throw new NotImplementedException();
	}

	public static int MDLDecompress8(Span<sbyte> dest, int len, Stream fp)
	{
		throw new NotImplementedException();
	}

	public static int MDLDecompress16(Span<short> dest, int len, Stream fp)
	{
		throw new NotImplementedException();
	}
}
