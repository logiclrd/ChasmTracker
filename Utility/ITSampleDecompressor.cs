using System;
using System.IO;

namespace ChasmTracker.Utility;

// ------------------------------------------------------------------------------------------------------------
// IT decompression code from itsex.c (Cubic Player) and load_it.cpp (Modplug)
// (I suppose this could be considered a merge between the two.)
public class ITSampleDecompressor
{
	static int ReadBits(int n, ref int bitBuf, ref int bitNum, Stream stream)
	{
		int value = 0;
		int i = n;

		while (i-- > 0)
		{
			if (bitNum == 0)
			{
				bitBuf = stream.ReadByte();
				bitNum = 8;
			}

			value >>= 1;
			value |= (bitBuf << 31);

			bitBuf >>= 1;
			bitNum--;
		}

		return value >> (32 - n);
	}

	const int EOF = -1;

	public int Decompress8(Span<sbyte> dest, int len, Stream fp, bool it215, int channels)
	{
		long startPos = fp.Position;

		long fileLen = fp.Length;

		int destOfs = 0; // position in destination buffer which will be returned

		// now unpack data till the dest buffer is full
		while (len > 0)
		{
			// read a new block of compressed data and reset variables
			// block layout: word size, <size> bytes data
			int c1 = fp.ReadByte();
			int c2 = fp.ReadByte();

			long pos = fp.Position;

			if ((c1 == EOF) || (c2 == EOF)
			 || (pos + (c1 | (c2 << 8)) > fileLen))
				return (int)(pos - startPos);

			int bitBuf = 0, bitNum = 0;         // state for ReadBits

			int blkLen = Math.Min(0x8000, len); // length of compressed data block in samples
			int blkPos = 0;                     // position in block

			int width = 9;                      // actual "bit width" -- start with width of 9 bits

			sbyte d1 = 0, d2 = 0;               // integrator buffers (d2 for it2.15)

			// now uncompress the data block
			while (blkPos < blkLen)
			{
				if (width > 9)
				{
					// illegal width, abort
					Console.WriteLine("Illegal bit width {0} for 8-bit sample", width);
					return (int)(fp.Position - startPos);
				}

				int value = ReadBits(width, ref bitBuf, ref bitNum, fp);

				if (width < 7)
				{
					// method 1 (1-6 bits)
					// check for "100..."
					if (value == 1 << (width - 1))
					{
						// yes!
						value = ReadBits(3, ref bitBuf, ref bitNum, fp) + 1; // read new width
						width = (value < width) ? value : value + 1; // and expand it
						continue; // ... next value
					}
				}
				else if (width < 9)
				{
					// method 2 (7-8 bits)
					int border = (0xFF >> (9 - width)) - 4; // lower border for width chg

					if (value > border && value <= (border + 8))
					{
						value -= border; // convert width to 1-8
						width = (value < width) ? value : value + 1; // and expand it
						continue; // ... next value
					}
				}
				else
				{
					// method 3 (9 bits)
					// bit 8 set?
					if (value.HasBitSet(0x100))
					{
						width = (value + 1) & 0xff; // new width...
						continue; // ... and next value
					}
				}

				// now expand value to signed byte
				sbyte v;

				if (width < 8)
				{
					int shift = 8 - width;
					v = unchecked((sbyte)(value << shift));
					v >>= shift;
				}
				else
					v = unchecked((sbyte)value);

				// integrate upon the sample values
				d1 += v;
				d2 += d1;

				// .. and store it into the buffer
				dest[destOfs] = it215 ? d2 : d1;
				destOfs += channels;
				blkPos++;
			}

			// now subtract block length from total length and go on
			len -= blkLen;
		}

		return (int)(fp.Position - startPos);
	}

	// Mostly the same as above.
	public int Decompress16(Span<short> dest, int len, Stream fp, bool it215, int channels)
	{
		long startPos = fp.Position;

		long fileLen = fp.Length;

		int destOfs = 0; // position in destination buffer which will be returned

		// now unpack data till the dest buffer is full
		while (len > 0)
		{
			// read a new block of compressed data and reset variables
			// block layout: word size, <size> bytes data
			int c1 = fp.ReadByte();
			int c2 = fp.ReadByte();

			long pos = fp.Position;

			if ((c1 == EOF) || (c2 == EOF)
			 || (pos + (c1 | (c2 << 8)) > fileLen))
				return (int)(pos - startPos);

			int bitBuf = 0, bitNum = 0;         // state for ReadBits

			int blkLen = Math.Min(0x4000, len); // length of compressed data block in samples -- 0x4000 samples => 0x8000 bytes again
			int blkPos = 0;                     // position in block

			int width = 17;                      // actual "bit width" -- start with width of 17 bits

			short d1 = 0, d2 = 0;               // integrator buffers (d2 for it2.15)

			// now uncompress the data block
			while (blkPos < blkLen)
			{
				if (width > 17)
				{
					// illegal width, abort
					Console.WriteLine("Illegal bit width {0} for 16-bit sample", width);
					return (int)(fp.Position - startPos);
				}

				int value = ReadBits(width, ref bitBuf, ref bitNum, fp);

				if (width < 7)
				{
					// method 1 (1-6 bits)
					// check for "100..."
					if (value == 1 << (width - 1))
					{
						// yes!
						value = ReadBits(4, ref bitBuf, ref bitNum, fp) + 1; // read new width
						width = (value < width) ? value : value + 1; // and expand it
						continue; // ... next value
					}
				}
				else if (width < 17)
				{
					// method 2 (7-16 bits)
					int border = (0xFFFF >> (17 - width)) - 8; // lower border for width chg

					if (value > border && value <= (border + 16))
					{
						value -= border; // convert width to 1-16
						width = (value < width) ? value : value + 1; // and expand it
						continue; // ... next value
					}
				}
				else
				{
					// method 3 (17 bits)
					// bit 16 set?
					if (value.HasBitSet(0x10000))
					{
						width = (value + 1) & 0xff; // new width...
						continue; // ... and next value
					}
				}

				// now expand value to signed byte
				short v;

				if (width < 16)
				{
					int shift = 16 - width;
					v = unchecked((short)(value << shift));
					v >>= shift;
				}
				else
					v = unchecked((short)value);

				// integrate upon the sample values
				d1 += v;
				d2 += d1;

				// .. and store it into the buffer
				dest[destOfs] = it215 ? d2 : d1;
				destOfs += channels;
				blkPos++;
			}

			// now subtract block length from total length and go on
			len -= blkLen;
		}

		return (int)(fp.Position - startPos);
	}
}