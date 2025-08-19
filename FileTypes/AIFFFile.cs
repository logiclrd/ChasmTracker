using System.IO;
using System.Text;

namespace ChasmTracker.FileTypes;

using ChasmTracker.Utility;

public static class AIFFFile
{
	public static int WriteAIFFHeader(Stream fp, int bits, int channels, int rate,
			string? name, int length, AIFFWriteData? awd /* out */)
	{
		int bps = ((bits + 7) / 8);

		/* note: channel multiply is done below -- need single-channel value for the COMM chunk */
		var writer = new BinaryWriter(fp, Encoding.ASCII, leaveOpen: true);

		writer.WritePlain("FORM");

		/* write a very large size for now */
		writer.Write(uint.MaxValue);

		writer.WritePlain("AIFF");

		if (!string.IsNullOrEmpty(name))
		{
			writer.WritePlain("NAME");

			int tlen = name.Length;

			int ul = (tlen + 1) & ~1; /* must be even */

			ul = ByteSwap.Swap(ul);

			writer.Write(ul);

			writer.WritePlain(name);

			if ((tlen & 1) != 0)
				writer.Write(default(byte));
		}

		/* Common Chunk
			The Common Chunk describes fundamental parameters of the sampled sound.
		typedef struct {
			ID              ckID;           // 'COMM'
			long            ckSize;         // 18
			short           numChannels;
			unsigned long   numSampleFrames;
			short           sampleSize;
			extended        sampleRate;
		} CommonChunk; */
		writer.WritePlain("COMM");
		writer.Write(ByteSwap.Swap(18)); /* chunk size -- won't change */
		writer.Write(ByteSwap.Swap((short)channels));

		if (awd != null)
		{
			writer.Flush();
			awd.COMMFramesOffset = fp.Position;
		}

		writer.Write(ByteSwap.Swap(length)); /* num sample frames */
		writer.Write(ByteSwap.Swap((short)bits));
		writer.Write(Float80.ToIEEE80Bytes(rate));

		/* NOW do this (sample size in AIFF is indicated per channel, not per frame) */
		int bpf = bps * channels; /* == number of bytes per (stereo) sample */

		/* Sound Data Chunk
			The Sound Data Chunk contains the actual sample frames.
		typedef struct {
			ID              ckID;           // 'SSND'
			long            ckSize;         // data size in bytes, *PLUS EIGHT* (for offset and blockSize)
			unsigned long   offset;         // just set this to 0...
			unsigned long   blockSize;      // likewise
			unsigned char   soundData[];
		} SoundDataChunk; */
		writer.WritePlain("SSND");

		if (awd != null)
		{
			writer.Flush();
			awd.SSNDSizeOffset = fp.Position;
			awd.BytesPerSample = bps;
			awd.BytesPerFrame = bpf;
		}

		writer.Write(ByteSwap.Swap(length * bps + 8));
		writer.Write(0);
		writer.Write(0);

		return bps;
	}
}
