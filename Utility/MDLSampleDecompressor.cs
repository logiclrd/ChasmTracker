using System;
using System.ComponentModel;
using System.IO;

namespace ChasmTracker.Utility;

// ------------------------------------------------------------------------------------------------------------
// MDL sample decompression
public class MDLSampleDecompressor
{
	static int ReadBits(ref int bitBuf, ref int bitNum, Stream stream, int n)
	{
		int v = (bitBuf & ((1 << n) - 1));
		bitBuf >>= n;
		bitNum -= n;

		if (bitNum <= 24)
		{
			bitBuf |= (stream.ReadByte() << bitNum);
			bitNum += 8;
		}

		return v;
	}

	public int Decompress8(Span<sbyte> dest, int len, Stream fp)
	{
		long startPos = fp.Position;

		long fileLen = fp.Length;

		int bitNum = 32;

		sbyte dlt = 0;

		// first 4 bytes indicate packed length
		int v = fp.ReadStructure<int>();

		v = (int)Math.Min(v, fileLen - startPos) + 4;

		int bitBuf = fp.ReadStructure<int>();

		for (int j=0; j<len; j++)
		{
			int sign = ReadBits(ref bitBuf, ref bitNum, fp, 1);

			int hiByte;
			if (ReadBits(ref bitBuf, ref bitNum, fp, 1) != 0)
				hiByte = ReadBits(ref bitBuf, ref bitNum, fp, 3);
			else {
				hiByte = 8;
				while (ReadBits(ref bitBuf, ref bitNum, fp, 1) == 0) hiByte += 0x10;
				hiByte += ReadBits(ref bitBuf, ref bitNum, fp, 4);
			}

			if (sign != 0)
				hiByte = ~hiByte;

			dlt += unchecked((sbyte)hiByte);

			dest[j] = dlt;
		}

		fp.Position = startPos + v;

		return v;
	}

	int Decompress16(Span<short> dest, int len, Stream fp)
	{
		long startPos = fp.Position;

		long fileLen = fp.Length;

		int bitNum = 32;

		short dlt = 0;

		// first 4 bytes indicate packed length
		int v = fp.ReadStructure<int>();

		v = (int)Math.Min(v, fileLen - startPos) + 4;

		int bitBuf = fp.ReadStructure<int>();

		for (int j = 0; j < len; j++)
		{
			int lowByte = ReadBits(ref bitBuf, ref bitNum, fp, 8);

			int sign = ReadBits(ref bitBuf, ref bitNum, fp, 1);

			int hiByte;
			if (ReadBits(ref bitBuf, ref bitNum, fp, 1) != 0)
				hiByte = ReadBits(ref bitBuf, ref bitNum, fp, 3);
			else {
				hiByte = 8;
				while (ReadBits(ref bitBuf, ref bitNum, fp, 1) == 0) hiByte += 0x10;
				hiByte += ReadBits(ref bitBuf, ref bitNum, fp, 4);
			}

			if (sign != 0)
				hiByte = ~hiByte;

			dlt += unchecked((sbyte)hiByte);

			dest[j << 1] = unchecked((sbyte)lowByte);
			dest[(j << 1) + 1] = dlt;
		}

		fp.Position = startPos + v;

		return v;
	}
}
