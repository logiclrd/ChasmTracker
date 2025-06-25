using System;
using System.IO;
using System.Text;

namespace ChasmTracker.FileTypes;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public abstract class FileConverter
{
	public abstract bool FillExtendedData(Stream stream, FileReference fileReference);
	public abstract Song LoadSong(Stream stream, LoadFlags flags);
	// public abstract void SaveSong(Song song);

	protected string ReadLinedMessage(Stream fp, int len, int lineLen, Encoding? encoding = null)
	{
		encoding ??= Encoding.ASCII;

		byte[] line = new byte[lineLen];

		var msg = new StringBuilder();

		while (len > 0)
		{
			int lineSize = Math.Min(len, lineLen);

			fp.ReadExactly(line, 0, lineSize);

			len -= lineSize;

			msg.AppendLine(encoding.GetString(line, 0, lineSize).Replace('\0', ' ').TrimEnd());
		}

		return msg.ToString();
	}

	static readonly short[] ByteSwap;

	static FileConverter()
	{
		ByteSwap = new short[65536];

		for (int i = 0; i < 256; i++)
			for (int j = 0; j < 256; j++)
				ByteSwap[i + j << 8] = unchecked((short)(i << 8 + j));
	}

	public static int ReadSample(SongSample sample, SampleFormat flags, Stream fp)
	{
		int memSize = (int)(fp.Length - fp.Position);

		if (sample.Flags.HasFlag(SampleFlags.Adlib))
			return 0; // no sample data

		if ((sample.Length < 1) || !fp.CanRead)
			return 0;

		// validate the read flags before anything else
		if (!Enum.IsDefined(flags & SampleFormat.BitMask) || flags.HasFlag(SampleFormat.BitMask))
		{
			Log.Append(4, "ReadSample: internal error: unsupported bit width {0}", flags & SampleFormat.BitMask);
			return 0;
		}

		if (!Enum.IsDefined(flags & SampleFormat.ChannelMask) || flags.HasFlag(SampleFormat.ChannelMask))
		{
			Log.Append(4, "ReadSample: internal error: unspported channel mask {0}", flags & SampleFormat.ChannelMask);
			return 0;
		}

		if (!Enum.IsDefined(flags & SampleFormat.EndiannessMask) || flags.HasFlag(SampleFormat.EndiannessMask))
		{
			Log.Append(4, "ReadSample: internal error: unsupported endianness {0}", flags & SampleFormat.EndiannessMask);
			return 0;
		}

		if (!Enum.IsDefined(flags & SampleFormat.EncodingMask) || flags.HasFlag(SampleFormat.EncodingMask))
		{
			Log.Append(4, "ReadSample: internal error: unsupported encoding {0}", flags & SampleFormat.EncodingMask);
			return 0;
		}

		var extraFlags = flags & (SampleFormat.BitMask | SampleFormat.ChannelMask | SampleFormat.EndiannessMask | SampleFormat.EncodingMask);

		if (extraFlags != default)
		{
			Log.Append(4, "ReadSample: internal error: unsupported extra flag {0}", extraFlags);
			return 0;
		}

		// cap the sample length
		if (sample.Length > Constants.MaxSampleLength)
			sample.Length = Constants.MaxSampleLength;

		int mem = sample.Length;

		// fix the sample flags
		sample.Flags &= ~(SampleFlags._16Bit | SampleFlags.Stereo);
		switch (flags & SampleFormat.BitMask)
		{
			case SampleFormat._16:
			case SampleFormat._24:
			case SampleFormat._32:
			case SampleFormat._64:
				// these are all stuffed into 16 bits.
				mem *= 2;
				sample.Flags |= SampleFlags._16Bit;
				break;
		}

		switch (flags & SampleFormat.ChannelMask)
		{
			case SampleFormat.StereoInterleaved:
			case SampleFormat.StereoSplit:
				mem *= 2;
				sample.Flags |= SampleFlags.Stereo;
				break;
		}

		sample.AllocateData();

		int sampleBytes = -1;

		switch (flags)
		{
			// 7-bit (data shifted one bit left)
			case SampleFormat._7 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.PCMSigned:
			case SampleFormat._7 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMSigned:
			{
				sample.Flags &= ~(SampleFlags._16Bit | SampleFlags.Stereo);

				sample.Length = Math.Min(sample.Length, memSize);

				var buffer = new byte[sample.Length];

				fp.ReadExactly(buffer);

				Buffer.BlockCopy(buffer, 0, sample.RawData8!, sample.Data8.Offset, sample.Length);

				for (int j = 0; j < sample.Length; j++)
					sample.Data8[j] = unchecked((sbyte)(sample.Data8[j] * 2).Clamp(-128, 127));

				sampleBytes = sample.Length;

				break;
			}

			// 8-bit mono PCM
			default:
				Console.WriteLine("DEFAULT: {0}", flags);
				flags = SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMSigned;
				goto case SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMSigned;

			case SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMSigned:
			case SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMDeltaEncoded:
			case SampleFormat._8 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.PCMSigned:
			case SampleFormat._8 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._8 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.PCMDeltaEncoded:
			{
				byte iAdd = (flags & SampleFormat.EncodingMask) == SampleFormat.PCMUnsigned ? (byte)0x80 : (byte)0;

				if (sample.Length > memSize)
					sample.Length = memSize;

				// read
				var buffer = new byte[sample.Length];

				fp.ReadExactly(buffer);

				// process
				bool delta = (flags & SampleFormat.EncodingMask) == SampleFormat.PCMDeltaEncoded;

				for (int j = 0; j < buffer.Length; j++)
				{
					buffer[j] = unchecked((byte)(buffer[j] + iAdd));

					if (delta)
						iAdd = buffer[j];
				}

				Buffer.BlockCopy(buffer, 0, sample.RawData8!, sample.Data8.Offset, sample.Length);

				sampleBytes = sample.Length;

				break;
			}

			// 8-bit stereo samples
			case SampleFormat._8 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.PCMSigned:
			case SampleFormat._8 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._8 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.PCMDeltaEncoded:
			case SampleFormat._8 | SampleFormat.StereoSplit | SampleFormat.BigEndian | SampleFormat.PCMSigned:
			case SampleFormat._8 | SampleFormat.StereoSplit | SampleFormat.BigEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._8 | SampleFormat.StereoSplit | SampleFormat.BigEndian | SampleFormat.PCMDeltaEncoded:
			{
				int len = sample.Length * 2;

				if (len > memSize)
					len = memSize >> 1;

				var buffer = new byte[len];

				fp.ReadExactly(buffer);

				bool delta = (flags & SampleFormat.EncodingMask) == SampleFormat.PCMDeltaEncoded;

				for (int c = 0; c < 2; c++)
				{
					int d = c * sample.Length;

					byte iAdd = (flags & SampleFormat.EncodingMask) == SampleFormat.PCMUnsigned ? (byte)0x80 : (byte)0;

					for (int j = 0; j < sample.Length; j++)
					{
						sample.Data8[j + j + c] = unchecked((sbyte)(buffer[j + d] + iAdd));
						if (delta)
							iAdd = unchecked((byte)sample.Data8[j + j + c]);
					}
				}

				sampleBytes = sample.Length * 2;

				break;
			}

			// 8-bit interleaved stereo samples
			case SampleFormat._8 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.PCMSigned:
			case SampleFormat._8 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._8 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.PCMDeltaEncoded:
			case SampleFormat._8 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.PCMSigned:
			case SampleFormat._8 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._8 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.PCMDeltaEncoded:
			{
				int len = sample.Length * 2;

				if (len > memSize)
					len = memSize >> 1;

				var buffer = new byte[len];

				fp.ReadExactly(buffer);

				bool delta = (flags & SampleFormat.EncodingMask) == SampleFormat.PCMDeltaEncoded;

				for (int c = 0; c < 2; c++)
				{
					byte iAdd = (flags & SampleFormat.EncodingMask) == SampleFormat.PCMUnsigned ? (byte)0x80 : (byte)0;

					for (int j = 0; j < len; j += 2)
					{
						sample.Data8[j + c] = unchecked((sbyte)(buffer[j + c] + iAdd));
						if (delta)
							iAdd = unchecked((byte)sample.Data8[j + c]);
					}
				}

				sampleBytes = sample.Length * 2;

				break;
			}

			// 16-bit mono PCM samples
			case SampleFormat._16 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMDeltaEncoded:
			case SampleFormat._16 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMSigned:
			case SampleFormat._16 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._16 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.PCMDeltaEncoded:
			case SampleFormat._16 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.PCMSigned:
			case SampleFormat._16 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.PCMUnsigned:
			{
				short iAdd = ((flags & SampleFormat.EncodingMask) == SampleFormat.PCMUnsigned)
					? (short)-0x8000
					: (short)0;

				int len = sample.Length;

				if (len * 2 > memSize)
					break;

				// read
				var rawData = new byte[len * 2];

				fp.ReadExactly(rawData);

				// process
				Buffer.BlockCopy(rawData, 0, sample.RawData16!, sample.Data16.Offset, rawData.Length);

				var data = sample.Data16;

				if ((flags & SampleFormat.EndiannessMask) == SampleFormat.BigEndian)
				{
					for (int i = 0; i < data.Count; i++)
						data[i] = ByteSwap[unchecked((ushort)data[i])];
				}

				bool delta = (flags & SampleFormat.EncodingMask) == SampleFormat.PCMDeltaEncoded;

				for (int j = 0; j < len; j++)
				{
					data[j] = unchecked((short)(data[j] + iAdd));

					if (delta)
						iAdd = data[j];
				}

				sampleBytes = len * 2;

				break;
			}

			// 16-bit stereo PCM samples
			case SampleFormat._16 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.PCMDeltaEncoded:
			case SampleFormat._16 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.PCMSigned:
			case SampleFormat._16 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._16 | SampleFormat.StereoSplit | SampleFormat.BigEndian | SampleFormat.PCMDeltaEncoded:
			case SampleFormat._16 | SampleFormat.StereoSplit | SampleFormat.BigEndian | SampleFormat.PCMSigned:
			case SampleFormat._16 | SampleFormat.StereoSplit | SampleFormat.BigEndian | SampleFormat.PCMUnsigned:
			// 16-bit interleaved stereo samples
			case SampleFormat._16 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.PCMDeltaEncoded:
			case SampleFormat._16 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.PCMSigned:
			case SampleFormat._16 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._16 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.PCMDeltaEncoded:
			case SampleFormat._16 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.PCMSigned:
			case SampleFormat._16 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.PCMUnsigned:
			{
				int len = sample.Length * 2;

				if (len * 2 > memSize)
					break;

				// read
				var rawData = new byte[len * 2];

				fp.ReadExactly(rawData);

				// process
				var data = sample.Data16;

				bool bswap = (flags & SampleFormat.EndiannessMask) == SampleFormat.BigEndian;
				bool alreadyInterleaved = (flags & SampleFormat.ChannelMask) == SampleFormat.StereoInterleaved;

				if (alreadyInterleaved)
					Buffer.BlockCopy(rawData, 0, data.Array!, 2 * data.Offset, rawData.Length);
				else
				{
					for (int i = 0; i < sample.Length; i++)
						for (int c = 0; c < 2; c++)
						{
							int o = c * sample.Length + i;
							int p = i * 2 + c;

							Buffer.BlockCopy(rawData, o + o, data.Array!, 2 * data.Offset + p, 2);

							if (bswap)
								data[p] = ByteSwap[unchecked((ushort)data[p])];
						}
				}

				bool delta = (flags & SampleFormat.EncodingMask) == SampleFormat.PCMDeltaEncoded;

				for (int c = 0; c < 2; c++)
				{
					short iAdd = ((flags & SampleFormat.EncodingMask) == SampleFormat.PCMUnsigned)
						? (short)-0x8000
						: (short)0;

					for (int j = c; j < len; j += 2)
					{
						data[j] = unchecked((short)(data[j] + iAdd));

						if (delta)
							iAdd = data[j];
					}
				}

				sampleBytes = len * 2;

				break;
			}

			// PCM 24-bit -> load sample, and normalize it to 16-bit
			case SampleFormat._24 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMSigned:
			case SampleFormat._24 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._24 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.PCMSigned:
			case SampleFormat._24 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._24 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.PCMSigned:
			case SampleFormat._24 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._24 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.PCMSigned:
			case SampleFormat._24 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.PCMUnsigned:
			{
				int samplesPerTick =
					((flags & SampleFormat.ChannelMask) == SampleFormat.Mono)
					? 1
					: 2;

				int sampleSize = 3 * samplesPerTick;

				int sampleLength = sample.Length * samplesPerTick;

				int byteLength = sample.Length * sampleSize;

				if (byteLength > memSize)
					break;

				byte[] rawData = new byte[byteLength];

				fp.ReadExactly(rawData);

				int[] intermediate = new int[sampleLength];

				bool bswap = (flags & SampleFormat.EndiannessMask) == SampleFormat.BigEndian;
				int sign = (flags & SampleFormat.EncodingMask) == SampleFormat.PCMUnsigned
					? 0
					: -0x80000000;
				int max = 0xFF;

				for (int i = 0, j = 0; i < byteLength; i += 3, j++)
				{
					byte b1 = rawData[i];
					byte b2 = rawData[i + 1];
					byte b3 = rawData[i + 2];

					int l = bswap
						? (b1 << 16) | (b2 << 8) | b3
						: (b3 << 16) | (b2 << 8) | b1;

					l = (l << 8) ^ sign;

					intermediate[j] = l;

					// int.MinValue can't be negated
					if (l == int.MinValue)
						max = int.MaxValue;
					else
					{
						if (l > max) max = l;
						if (-l > max) max = -l;
					}
				}

				// TODO: ability to turn this off? just divide by 65536 and be done with it?
				int divisor = max >> 16 + 1;

				var dest = sample.Data16;

				for (int i = 0; i < sampleLength; i++)
					dest[i] = unchecked((short)(intermediate[i] / divisor));

				sampleBytes = byteLength;

				break;
			}
			// PCM 32-bit -> load sample, and normalize it to 16-bit
			case SampleFormat._32 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMSigned:
			case SampleFormat._32 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._32 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.PCMSigned:
			case SampleFormat._32 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._32 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.PCMSigned:
			case SampleFormat._32 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.PCMUnsigned:
			case SampleFormat._32 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.PCMSigned:
			case SampleFormat._32 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.PCMUnsigned:
			{
				int samplesPerTick =
					((flags & SampleFormat.ChannelMask) == SampleFormat.Mono)
					? 1
					: 2;

				int sampleSize = 4 * samplesPerTick;

				int sampleLength = sample.Length * samplesPerTick;

				int byteLength = sample.Length * sampleSize;

				if (byteLength > memSize)
					break;

				byte[] rawData = new byte[byteLength];

				fp.ReadExactly(rawData);

				int[] intermediate = new int[sampleLength];

				bool bswap = (flags & SampleFormat.EndiannessMask) == SampleFormat.BigEndian;
				int sign = (flags & SampleFormat.EncodingMask) == SampleFormat.PCMUnsigned
					? 0
					: -0x80000000;
				int max = 0xFF;

				for (int i = 0, j = 0; i < byteLength; i += 4, j++)
				{
					byte b1 = rawData[i];
					byte b2 = rawData[i + 1];
					byte b3 = rawData[i + 2];
					byte b4 = rawData[i + 3];

					int l = bswap
						? (b1 << 24) | (b2 << 16) | (b3 << 8) | b4
						: (b4 << 24) | (b3 << 16) | (b2 << 8) | b1;

					l = l ^ sign;

					intermediate[j] = l;

					// int.MinValue can't be negated
					if (l == int.MinValue)
						max = int.MaxValue;
					else
					{
						if (l > max) max = l;
						if (-l > max) max = -l;
					}
				}

				// TODO: ability to turn this off? just divide by 65536 and be done with it?
				int divisor = max >> 16 + 1;

				var dest = sample.Data16;

				for (int i = 0; i < sampleLength; i++)
					dest[i] = unchecked((short)(intermediate[i] / divisor));

				sampleBytes = byteLength;

				break;
			}
			// 32-bit IEEE floating point
			case SampleFormat._32 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.IEEEFloatingPoint:
			case SampleFormat._32 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.IEEEFloatingPoint:
			case SampleFormat._32 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.IEEEFloatingPoint:
			case SampleFormat._32 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.IEEEFloatingPoint:
			case SampleFormat._32 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.IEEEFloatingPoint:
			case SampleFormat._32 | SampleFormat.StereoSplit | SampleFormat.BigEndian | SampleFormat.IEEEFloatingPoint:
			{
				int len = sample.Length, mc = 1;

				if ((flags & SampleFormat.ChannelMask) != SampleFormat.Mono)
				{
					len *= 2;
					mc = 2;
				}

				var data = sample.Data16;

				byte[] bytes = new byte[4];

				bool bswap = ((flags & SampleFormat.EndiannessMask) == SampleFormat.BigEndian);

				bool interleaved = ((flags & SampleFormat.ChannelMask) == SampleFormat.StereoInterleaved);

				int li = interleaved ? 1 : sample.Length;
				int dl = interleaved ? 2 : 1;

				for (int c = 0; c < mc; c++)
				{
					for (int k = 0, l = li; k < len; k++, l += dl)
					{
						fp.ReadExactly(bytes);

						if (bswap)
							(bytes[0], bytes[1], bytes[2], bytes[3]) = (bytes[3], bytes[2], bytes[1], bytes[0]);

						double num = BitConverter.ToSingle(bytes);

						num *= short.MaxValue;

						data[l] = (short)double.Clamp(num, short.MinValue, short.MaxValue);
					}
				}

				sampleBytes = len * 2;

				break;
			}
			// 64-bit IEEE floating point
			case SampleFormat._64 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.IEEEFloatingPoint:
			case SampleFormat._64 | SampleFormat.Mono | SampleFormat.BigEndian | SampleFormat.IEEEFloatingPoint:
			case SampleFormat._64 | SampleFormat.StereoInterleaved | SampleFormat.LittleEndian | SampleFormat.IEEEFloatingPoint:
			case SampleFormat._64 | SampleFormat.StereoInterleaved | SampleFormat.BigEndian | SampleFormat.IEEEFloatingPoint:
			case SampleFormat._64 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.IEEEFloatingPoint:
			case SampleFormat._64 | SampleFormat.StereoSplit | SampleFormat.BigEndian | SampleFormat.IEEEFloatingPoint:
			{
				int len = sample.Length, mc = 1;

				if ((flags & SampleFormat.ChannelMask) != SampleFormat.Mono)
				{
					len *= 2;
					mc = 2;
				}

				var data = sample.Data16;

				byte[] bytes = new byte[8];

				bool bswap = ((flags & SampleFormat.EndiannessMask) == SampleFormat.BigEndian);

				bool interleaved = ((flags & SampleFormat.ChannelMask) == SampleFormat.StereoInterleaved);

				int li = interleaved ? 1 : sample.Length;
				int dl = interleaved ? 2 : 1;

				for (int c = 0; c < mc; c++)
				{
					for (int k = 0, l = li; k < len; k++, l += dl)
					{
						fp.ReadExactly(bytes);

						if (bswap)
							(bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7]) = (bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0]);

						double num = BitConverter.ToDouble(bytes);

						num *= short.MaxValue;

						data[l] = (short)double.Clamp(num, short.MinValue, short.MaxValue);
					}
				}

				sampleBytes = len * 2;

				break;
			}
			// IT 2.14 compressed samples
			case SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.IT214Compressed:
			case SampleFormat._16 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.IT214Compressed:
			case SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.IT215Compressed:
			case SampleFormat._16 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.IT215Compressed:
			{
				int len = memSize;

				if (len < 2)
					break;

				bool it215 = ((flags & SampleFormat.EncodingMask) == SampleFormat.IT215Compressed);

				if ((flags & SampleFormat.BitMask) == SampleFormat._8)
				{
					SampleCompression.ITDecompress8(sample.Data8, 0, sample.Length, fp, it215, 1);
					sampleBytes = sample.Length;
				}
				else
				{
					SampleCompression.ITDecompress16(sample.Data16, 0, sample.Length, fp, it215, 1);
					sampleBytes = sample.Length * 2;
				}

				break;
			}
			case SampleFormat._8 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.IT214Compressed:
			case SampleFormat._16 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.IT214Compressed:
			case SampleFormat._8 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.IT215Compressed:
			case SampleFormat._16 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.IT215Compressed:
			{
				int len = memSize;

				if (len < 4)
					break;

				bool it215 = ((flags & SampleFormat.EncodingMask) == SampleFormat.IT215Compressed);

				if ((flags & SampleFormat.BitMask) == SampleFormat._8)
				{
					SampleCompression.ITDecompress8(sample.Data8, 0, sample.Length, fp, it215, 2);
					SampleCompression.ITDecompress8(sample.Data8, 1, sample.Length, fp, it215, 2);
					sampleBytes = sample.Length * 2;
				}
				else
				{
					SampleCompression.ITDecompress16(sample.Data16, 0, sample.Length, fp, it215, 2);
					SampleCompression.ITDecompress16(sample.Data16, 1, sample.Length, fp, it215, 2);
					sampleBytes = sample.Length * 4;
				}

				break;
			}
			// PTM 8bit delta to 16-bit sample
			case SampleFormat._16 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PTMDeltaEncoded:
			{
				int len = sample.Length * 2;

				if (len > memSize)
					break;

				sbyte[] data = new sbyte[len];

				sbyte delta8 = 0;

				for (int j = 0; j < len; j++)
				{
					delta8 = unchecked((sbyte)(delta8 + fp.ReadByte()));
					data[j] = delta8;
				}

				bool bswap = (flags & SampleFormat.EndiannessMask) == SampleFormat.BigEndian;

				if (bswap)
				{
					for (int j = 0; j + 1 < len; j += 2)
						(data[j], data[j + 1]) = (data[j + 1], data[j]);
				}

				Buffer.BlockCopy(data, 0, sample.RawData16!, sample.Data16.Offset, data.Length);

				sampleBytes = sample.Data16.Count * 2;

				break;
			}

			// Huffman MDL compressed samples
			case SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.MDLHuffmanCompressed:
			case SampleFormat._16 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.MDLHuffmanCompressed:
			{
				if (memSize < 8)
					break;

				if ((flags & SampleFormat.BitMask) == SampleFormat._8)
				{
					SampleCompression.MDLDecompress8(sample.Data8, sample.Length, fp);
					sampleBytes = sample.Length;
				}
				else
				{
					SampleCompression.MDLDecompress16(sample.Data16, sample.Length, fp);
					sampleBytes = sample.Length * 2;
				}

				break;
			}

			// 8-bit ADPCM data w/ 16-byte table (MOD ADPCM)
			case SampleFormat.PCM16bitTableDeltaEncoded | SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian:
			{
				int len = (sample.Length + 1) / 2 + 16;

				if (len > memSize)
					break;

				byte[] table = new byte[16];

				fp.ReadExactly(table);

				var data = sample.Data8;
				sbyte smpVal = 0;

				for (int j = 16, o = 0; j < len; j++)
				{
					int c = fp.ReadByte();

					smpVal = unchecked((sbyte)(smpVal + table[c & 0xF]));
					data[o++] = smpVal;

					smpVal = unchecked((sbyte)(smpVal + table[(c >> 4) & 0xF]));
					data[o++] = smpVal;
				}

				sampleBytes = data.Count;

				break;
			}
		}

		if (sampleBytes < 0)
			throw new NotSupportedException();

		sample.AdjustLoop();

		return sampleBytes;
	}
}
