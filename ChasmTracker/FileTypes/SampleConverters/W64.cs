using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.SampleConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class W64 : SampleFileConverter
{
	public override string Label => "W64";
	public override string Description => "Sony Wave64";
	public override string Extension => ".w64";

	public override bool CanSave => false;

	/* Sonic Foundry / Sony Wave64 sample reader and loader.
	* Largely based on the WAVE code, without the xtra and smpl chunks. */

	/* --------------------------------------------------------------------------------------------------------- */
	/* chunk helpers */

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct W64Chunk
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] ID;
		public long Size;
		public long Offset;

		public bool IsEmpty
			=> ((ID == null) || ID.All(b => b == 0)) && (Size == 0) && (Offset == 0);
	}

	/* wave64 is NOT iff, so we have to define our own stupid reading functions. sigh. */
	bool PeekChunk(Stream stream, out W64Chunk chunk)
	{
		chunk = new W64Chunk();

		try
		{
			chunk.ID = new byte[16];

			stream.ReadExactly(chunk.ID);

			chunk.Size = stream.ReadStructure<long>();

			/* Size includes the size of the header, for whatever reason. */
			if (chunk.Size < 24)
				return false;

			chunk.Size -= 24;

			chunk.Offset = stream.Position;

			if (chunk.Offset < 0)
				return false;

			/* w64 sizes are aligned to 64-bit boundaries */
			stream.Position += (chunk.Size + 7) & -7;

			long pos = stream.Position;

			// ?
			if (pos < 0)
				return false;

			return (pos <= stream.Length);
		}
		catch
		{
			return false;
		}
	}

	[ThreadStatic]
	static byte[]? s_buffer = null;

	bool ReceiveChunk(Stream fp, ref W64Chunk chunk, Func<Memory<byte>, bool> callback)
	{
		try
		{
			// Don't want to be trying to process insane things
			if (chunk.Size > 500 * 1048576) // arbitrary: 500 MB
				return false;

			long pos = fp.Position;

			// ?
			if (pos < 0)
				return false;

			if ((s_buffer == null) || (s_buffer.Length < chunk.Size))
				s_buffer = new byte[chunk.Size * 2];

			var slice = s_buffer.AsMemory().Slice(0, (int)chunk.Size);

			try
			{
				fp.Position = chunk.Offset;
				fp.ReadExactly(slice.Span);
				return callback(slice);
			}
			finally
			{
				/* how ? */
				fp.Position = pos;
			}
		}
		catch
		{
			return false;
		}
	}

	int ReadSample(Stream fp, ref W64Chunk chunk, SongSample smp, SampleFormat flags)
	{
		long pos = fp.Position;

		try
		{
			if (pos < 0)
				return 0;

			fp.Position = chunk.Offset;

			return ReadSample(smp, flags, fp);
		}
		finally
		{
			fp.Position = pos;
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

	static readonly byte[] RIFFGuid =
		{
			0x72, 0x69, 0x66, 0x66, 0x2E, 0x91, 0xCF, 0x11,
			0xA5, 0xD6, 0x28, 0xDB, 0x04, 0xC1, 0x00, 0x00,
		};

	static readonly byte[] WAVEGuid =
		{
			0x77, 0x61, 0x76, 0x65, 0xF3, 0xAC, 0xD3, 0x11,
			0x8C, 0xD1, 0x00, 0xC0, 0x4F, 0x8E, 0xDB, 0x8A,
		};

	static readonly byte[] FMTGuid =
		{
			0x66, 0x6D, 0x74, 0x20, 0xF3, 0xAC, 0xD3, 0x11,
			0x8C, 0xD1, 0x00, 0xC0, 0x4F, 0x8E, 0xDB, 0x8A,
		};

	static readonly byte[] DATAGuid =
		{
			0x64, 0x61, 0x74, 0x61, 0xF3, 0xAC, 0xD3, 0x11,
			0x8C, 0xD1, 0x00, 0xC0, 0x4F, 0x8E, 0xDB, 0x8A,
		};

	bool Load(Stream fp, SongSample smp, bool loadSampleData)
	{
		try
		{
			W64Chunk fmtChunk = default, dataChunk = default;

			{
				byte[] guid = new byte[16];

				fp.ReadExactly(guid);
				if (!guid.SequenceEqual(RIFFGuid))
					return false;

				/* skip filesize. */
				fp.Position += 8;

				fp.ReadExactly(guid);
				if (!guid.SequenceEqual(WAVEGuid))
					return false;
			}

			{
				/* go through every chunk in the file. */
				while (PeekChunk(fp, out var c))
				{
					if (c.ID.SequenceEqual(FMTGuid))
					{
						if (!fmtChunk.IsEmpty)
							return false;

						fmtChunk = c;
					}
					else if (c.ID.SequenceEqual(DATAGuid))
					{
						if (!dataChunk.IsEmpty)
							return false;

						dataChunk = c;
					}
				}
			}

			/* this should never happen */
			if (fmtChunk.IsEmpty || dataChunk.IsEmpty)
				return false;

			/* now we have all the chunks we need. */
			WaveFormat? fmt = null;
			SampleFormat flags;

			unsafe bool ReadFmtChunk(Memory<byte> chunkData)
			{
				using (var pin = chunkData.Pin())
				{
					var stream = new UnmanagedMemoryStream((byte *)pin.Pointer, chunkData.Length);

					fmt = WAVFile.ReadFmtChunk(stream);

					return true;
				}
			}

			if (!ReceiveChunk(fp, ref fmtChunk, ReadFmtChunk) || (fmt == null))
				return false;

			// endianness
			flags = SampleFormat.LittleEndian;

			// channels
			flags |= (fmt.Channels == 2) ? SampleFormat.StereoInterleaved : SampleFormat.Mono; // interleaved stereo

			// bit width
			switch (fmt.BitsPerSample)
			{
				case 8:  flags |= SampleFormat._8;  break;
				case 16: flags |= SampleFormat._16; break;
				case 24: flags |= SampleFormat._24; break;
				case 32: flags |= SampleFormat._32; break;
				default: return false; // unsupported
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
				default: return false; // unsupported
			}

			smp.Flags         = 0; // flags are set by csf_read_sample
			smp.Volume        = 64 * 4;
			smp.GlobalVolume  = 64;
			smp.C5Speed       = fmt.FreqHz;
			smp.Length        = (int)(dataChunk.Size / (fmt.Channels * (fmt.BitsPerSample / 8)));

			if (loadSampleData)
				return ReadSample(fp, ref dataChunk, smp, flags) != 0;
			else
			{
				if (fmt.Channels == 2)
					smp.Flags |= SampleFlags.Stereo;

				if (fmt.BitsPerSample > 8)
					smp.Flags |= SampleFlags._16Bit;
			}

			return true;
		}
		catch (IOException)
		{
			return false;
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

	public override SongSample LoadSample(Stream stream)
	{
		SongSample smp = new SongSample();

		if (!Load(stream, smp, loadSampleData: true))
			throw new Exception();

		return smp;
	}

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			SongSample smp = new SongSample();

			if (!Load(stream, smp, loadSampleData: false))
				return false;

			smp.FileName = file.BaseName;

			file.FillFromSample(smp);

			file.Description = Description;
			file.Type = FileTypes.SamplePlain;

			return true;
		}
		catch
		{
			return false;
		}
	}
}
