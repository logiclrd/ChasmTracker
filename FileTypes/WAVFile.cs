using System;
using System.IO;
using System.Text;

namespace ChasmTracker.FileTypes;

using ChasmTracker.Songs;
using ChasmTracker.Utility;

public static class WAVFile
{
	public static int WriteHeader(Stream fp, int bits, int channels, int rate, int length, out long dataSizeOffset)
	{
		int bps = ((bits + 7) / 8) * channels;

		var writer = new BinaryWriter(fp, Encoding.ASCII, leaveOpen: true);

		writer.WritePlain("RIFF");

		/* write a very large size for now */
		writer.Write((byte)255);
		writer.Write((byte)255);
		writer.Write((byte)255);
		writer.Write((byte)255);

		writer.WritePlain("WAVEfmt ");

		writer.Write(16); // fmt chunk size
		writer.Write((short)1); // linear pcm
		writer.Write((short)channels); // number of channels
		writer.Write(rate); // sample rate
		writer.Write(bps * rate); // "byte rate" (why?! I have no idea)
		writer.Write((short)bps); // (oh, come on! the format already stores everything needed to calculate this!)
		writer.Write((short)bits); // bits per sample

		writer.WritePlain("data");

		writer.Flush();

		dataSizeOffset = fp.Position;

		writer.Write(bps * length);

		writer.Flush();

		return bps;
	}

	static void WriteINFOChunk(BinaryWriter writer, string chunk, string text)
	{
		writer.WritePlain(chunk);
		writer.Write(text.Length + (text.Length & 1));
		writer.WritePlain(text);

		/* word align; I'm not sure if this is the "correct" way
		* to do this, but eh */
		if ((text.Length & 1) != 0) writer.Write((byte)32);
	}

	public static void WriteLISTChunk(Stream fp, string? title = null)
	{
		/* this is used to "fix" the */
		long start = fp.Position;

		var writer = new BinaryWriter(fp, Encoding.ASCII, leaveOpen: true);

		writer.WritePlain("LIST");

		writer.Write(0);

		writer.WritePlain("INFO");

		{
			/* ISFT (Software) chunk */
			string ver = "Chasm Tracker " + typeof(Program).Assembly.GetName().Version;

			WriteINFOChunk(writer, "ISFT", ver);
		}

		if (!string.IsNullOrEmpty(title))
		{
			/* INAM (title/name) chunk */
			WriteINFOChunk(writer, "INAM", title);
		}

		writer.Flush();

		long end = fp.Position;

		/* now we can fill in the length */
		fp.Position = start + 4;

		writer.Write((int)(end - start - 8));
		writer.Flush();

		/* back to the end */
		fp.Position = end;
	}

	// Standard IFF chunks IDs
	const uint IFFID_FORM = 0x464f524du;
	const uint IFFID_RIFF = 0x52494646u;
	const uint IFFID_WAVE = 0x57415645u;
	const uint IFFID_LIST = 0x4C495354u;
	const uint IFFID_INFO = 0x494E464Fu;

	// Wave IFF chunks IDs
	const uint IFFID_wave = 0x77617665u;
	const uint IFFID_fmt =  0x666D7420u;
	const uint IFFID_wsmp = 0x77736D70u;
	const uint IFFID_pcm =  0x70636d20u;
	const uint IFFID_data = 0x64617461u;
	const uint IFFID_smpl = 0x736D706Cu;
	const uint IFFID_xtra = 0x78747261u;

	static readonly byte[] SubformatBaseCheck =
		{
			0x00, 0x00, 0x10, 0x00,
			0x80, 0x00, 0x00, 0xAA,
			0x00, 0x38, 0x9B, 0x71,
		};

	public static WaveFormat ReadFmtChunk(Stream data)
	{
		var reader = new BinaryReader(data, Encoding.ASCII, leaveOpen: true);

		var fmt = new WaveFormat();

		fmt.Format = (WaveFormatTypes)reader.ReadInt16();
		fmt.Channels = reader.ReadInt16();
		fmt.FreqHz = reader.ReadInt32();
		fmt.BytesSec = reader.ReadInt32();
		fmt.SampleSize = reader.ReadInt16();
		fmt.BitsPerSample = reader.ReadInt16();

		/* BUT I'M NOT DONE YET */
		if (fmt.Format == WaveFormatTypes.Extensible)
		{
			short extSize = reader.ReadInt16();

			if (extSize < 22)
				throw new FormatException();

			// Skip 6 bytes
			reader.ReadInt32();
			reader.ReadInt16();

			int subformat = reader.ReadInt32();
			byte[] subformatBase = reader.ReadBytes(12);

			for (int i = 0; i < 12; i++)
				if (subformatBase[i] != SubformatBaseCheck[i])
					throw new FormatException();

			fmt.Format = (WaveFormatTypes)subformat;
		}

		return fmt;
	}

	public static SongSample? Load(Stream stream)
	{
		long baseOffset = stream.Position;

		var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

		{
			if (reader.ReadPlainString(4) != "RIFF")
				return null;

			/* skip filesize. */
			reader.ReadInt32();

			if (reader.ReadPlainString(4) != "WAVE")
				return null;
		}

		IFFChunk? fmtChunk, dataChunk, smplChunk, xtraChunk;

		fmtChunk = dataChunk = smplChunk = xtraChunk = null;

		{
			while (RIFF.PeekChunk(stream) is IFFChunk c)
			{
				switch (c.ID)
				{
					case IFFID_fmt:
						if (fmtChunk != null)
							return null;

						fmtChunk = c;
						break;
					case IFFID_data:
						if (dataChunk != null)
							return null;

						dataChunk = c;
						break;
					case IFFID_xtra:
						xtraChunk = c;
						break;
					case IFFID_smpl:
						smplChunk = c;
						break;
					default:
						break;
				}
			}
		}

		/* this should never happen */
		if ((fmtChunk == null) || (dataChunk == null))
			return null;

		/* now we have all the chunks we need. */
		WaveFormat fmt = default!;

		IFF.Receive(
			stream,
			fmtChunk,
			(data, length) =>
			{
				// TODO: wav_chunk_fmt_read
				fmt = ReadFmtChunk(new MemoryStream(data, 0, length));
				return 0;
			});

		// endianness
		var flags = SampleFormat.LittleEndian;

		// channels
		flags |= (fmt.Channels == 2) ? SampleFormat.StereoInterleaved : SampleFormat.Mono; // interleaved stereo

		// bit width
		switch (fmt.BitsPerSample)
		{
			case 8: flags |= SampleFormat._8; break;
			case 16: flags |= SampleFormat._16; break;
			case 24: flags |= SampleFormat._24; break;
			case 32: flags |= SampleFormat._32; break;
			default: return null; // unsupported
		}

		// encoding (8-bit wav is unsigned, everything else is signed -- yeah, it's stupid)
		switch (fmt.Format)
		{
			case WaveFormatTypes.PCM:
				flags |= (fmt.BitsPerSample == 8) ? SampleFormat.PCMUnsigned : SampleFormat.PCMSigned;
				break;
			case WaveFormatTypes.IEEEFloatingPoint:
				flags |= SampleFormat.IEEEFloatingPoint;
				break;
			default: return null; // unsupported
		}

		var smp = new SongSample();

		smp.Flags = 0; // flags are set by csf_read_sample
		smp.Volume = 64 * 4;
		smp.GlobalVolume = 64;
		smp.C5Speed = fmt.FreqHz;
		smp.Length = dataChunk.Size / ((fmt.BitsPerSample / 8) * fmt.Channels);

		if (fmt.Channels == 2)
			smp.Flags |= SampleFlags.Stereo;

		if (fmt.BitsPerSample > 8)
			smp.Flags |= SampleFlags._16Bit;

		/* if we have XTRA or SMPL chunks, fill them in as well. */
		if (xtraChunk != null)
		{
			stream.Position = xtraChunk.Offset;
			IFF.ReadXtraChunk(stream, smp);
		}

		if (smplChunk != null)
		{
			stream.Position = smplChunk.Offset;
			IFF.ReadSmplChunk(stream, smp);
		}

		// TODO: loadsample? bool flag

		if (IFF.ReadSample(stream, dataChunk, (int)baseOffset, smp, flags) > 0)
			return smp;

		return null;
	}

	public static SaveResult Save(Stream stream, SongSample smp)
	{
		long startPosition = stream.Position;

		SampleFormat flags = SampleFormat.LittleEndian;

		if (smp.Flags.HasFlag(SampleFlags.AdLib))
			return SaveResult.Unsupported;

		flags |= smp.Flags.HasFlag(SampleFlags._16Bit) ? (SampleFormat._16 | SampleFormat.PCMSigned) : (SampleFormat._8 | SampleFormat.PCMUnsigned);
		flags |= smp.Flags.HasFlag(SampleFlags.Stereo) ? SampleFormat.StereoInterleaved : SampleFormat.Mono;

		int bps = WriteHeader(stream, smp.Flags.HasFlag(SampleFlags._16Bit) ? 16 : 8, smp.Flags.HasFlag(SampleFlags.Stereo) ? 2 : 1,
			smp.C5Speed, smp.Length, out var _);

		if (SampleFileConverter.WriteSample(stream, smp, flags, uint.MaxValue) != smp.Length * bps)
		{
			Log.Append(4, "WAV: unexpected data size written");
			return SaveResult.InternalError;
		}

		var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

		{
			IFF.FillXtraChunk(smp, writer);
			IFF.FillSmplChunk(smp, writer);
		}

		writer.Flush();

		WriteLISTChunk(stream, smp.Name);

		/* fix the length in the file header */
		long endPosition = stream.Position;

		int chunkContentLength = (int)(endPosition - startPosition - 8);

		stream.Position = startPosition + 4;

		writer.Write(chunkContentLength);
		writer.Flush();

		return SaveResult.Success;
	}
}
