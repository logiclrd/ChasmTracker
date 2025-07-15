using System.Runtime.InteropServices;

namespace libFLAC;

/** FLAC STREAMINFO structure.  (c.f. <A HREF="https://xiph.org/flac/format.html#metadata_block_streaminfo">format specification</A>)
 */
public struct StreamMetadata_StreamInfo
{
	public int MinBlocksize, maxBlocksize;
	public int MinFramesize, maxFramesize;
	public int SampleRate;
	public int Channels;
	public int BitsPerSample;
	public ulong TotalSamples;
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
	public byte[] MD5Sum;
}