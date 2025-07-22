using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.FileTypes.Converters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class AIFF : SampleFileConverter
{
	public override string Label => "AIFF";
	public override string Description => "Audio IFF";
	public override string Extension => ".aiff";

	public override int SortOrder => 3;

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct AIFFChunkVHDR
	{
		public uint smp_highoct_1shot;
		public uint smp_highoct_repeat;
		public uint smp_cycle_highoct;
		public ushort smp_per_sec;
		public byte num_octaves;
		public byte compression; // 0 = none, 1 = fibonacci-delta
		public uint volume; // fixed point, 65536 = 1.0
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct AIFFChunkCOMM
	{
		public ushort num_channels;
		public uint num_frames;
		public ushort sample_size;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
		public byte[]? sample_rate; // IEEE-extended
	}

	// other chunks that might exist: "NAME", "AUTH", "ANNO", "(c) "

	// Wish I could do this:
	//#define ID(x) ((0[#x] << 24) | (1[#x] << 16) | (2[#x] << 8) | (3[#x]))
	// It works great in C, but gcc doesn't think it's a constant, so it can't be used in a switch.
	// (although with -O, it definitely does optimize it into a constant...)
	// It is completely impossible in C#, which doesn't have a preprocessor.

	const uint ID_FORM = 0x464F524Du;
	const uint ID_8SVX = 0x38535658u;
	const uint ID_16SV = 0x31365356u;
	const uint ID_VHDR = 0x56484452u;
	const uint ID_BODY = 0x424F4459u;
	const uint ID_NAME = 0x4E414D45u;
	const uint ID_AUTH = 0x41555448u;
	const uint ID_ANNO = 0x414E4E4Fu;
	const uint ID__C__ = 0x28632920u; /* "(c) " */
	const uint ID_AIFF = 0x41494646u;
	const uint ID_COMM = 0x434F4D4Du;
	const uint ID_SSND = 0x53534E44u;

	/* --------------------------------------------------------------------- */

	SongSample? ReadIFF(Stream stream, FileReference? file, bool readSampleData)
	{
		IFFChunk? vhdr = null;
		IFFChunk? body = null;
		IFFChunk? name = null;
		IFFChunk? auth = null;
		IFFChunk? anno = null;
		IFFChunk? ssnd = null;
		IFFChunk? comm = null;

		var chunk = IFF.PeekChunk(stream);

		if (chunk == null)
			return null;

		if (chunk.ID != ID_FORM)
			return null;

		byte[] filetypeData = new byte[4];

		if (IFF.Read(stream, chunk, filetypeData) != filetypeData.Length)
			return null;

		uint filetype = Convert.ToUInt32(filetypeData);

		// jump "into" the FORM chunk
		stream.Position = chunk.Offset + filetypeData.Length;

		filetype = ByteSwap.Swap(filetype);

		switch (filetype)
		{
			case ID_16SV: /* 16-bit */
			case ID_8SVX:
			{
				while (IFF.PeekChunk(stream) is IFFChunk innerChunk)
				{
					switch (innerChunk.ID)
					{
						case ID_VHDR: vhdr = innerChunk; break;
						case ID_BODY: body = innerChunk; break;
						case ID_NAME: name = innerChunk; break;
						case ID_AUTH: auth = innerChunk; break;
						case ID_ANNO: anno = innerChunk; break;
					}
				}

				if ((vhdr == null) || (body == null))
					return null;

				AIFFChunkVHDR chunkVHDR = default;

				if (0 == IFF.Receive(
					stream,
					vhdr,
					(buf, bufLen) =>
					{
						chunkVHDR = StructureSerializer.MarshalFromBytes<AIFFChunkVHDR>(buf, bufLen);
						return 1;
					}))
					return null;

				if (chunkVHDR.compression != 0)
				{
					Log.Append(4, "error: compressed IFF files are unsupported");
					return null;
				}

				if (chunkVHDR.num_octaves != 1)
					Log.Append(4, "warning: IFF file contains {0} octaves", chunkVHDR.num_octaves);

				if (file != null)
				{
					file.SampleSpeed = chunkVHDR.smp_per_sec;
					file.SampleLength = body.Size;

					file.Description = (filetype == ID_16SV)
						? "16SV sample"
						: "8SVX sample";
					file.Type = FileTypes.SamplePlain;
				}

				if (name == null)
					name = auth;
				if (name == null)
					name = anno;

				string? sampleName = null;

				if (name != null)
				{
					byte[] nameBytes = new byte[name.Size];

					IFF.Read(stream, name, nameBytes);

					int nameLength = Array.IndexOf(nameBytes, 0);

					if (nameLength < 0)
						nameLength = nameBytes.Length;

					string nameValue = nameBytes.ToStringZ();

					if (file != null)
						file.Title = nameValue;

					if (readSampleData)
						sampleName = nameValue;
				}

				if (readSampleData)
				{
					var smp = new SongSample();

					if (sampleName != null)
						smp.Name = sampleName;

					SampleFormat flags = SampleFormat.BigEndian | SampleFormat.PCMSigned | SampleFormat.Mono;

					smp.C5Speed = ByteSwap.Swap(chunkVHDR.smp_per_sec);
					smp.Length = body.Size;

					/* volume (should this be global volume, or just smp volume?) */
					chunkVHDR.volume = ByteSwap.Swap(chunkVHDR.volume);
					chunkVHDR.volume = Math.Min(chunkVHDR.volume, 0x10000);

					/* round to 0..64 -- 1024 == (0x10000 / 64) */
					chunkVHDR.volume = (chunkVHDR.volume + 512) / 1024;

					smp.Volume = (int)chunkVHDR.volume * 4; //mphack
					smp.GlobalVolume = 64;

					// this is done kinda weird
					smp.LoopEnd = (int)ByteSwap.Swap(chunkVHDR.smp_highoct_repeat);
					if (smp.LoopEnd != 0)
					{
						smp.LoopStart = (int)ByteSwap.Swap(chunkVHDR.smp_highoct_1shot);
						smp.LoopEnd += smp.LoopStart;
						if (smp.LoopStart > smp.Length)
							smp.LoopStart = 0;
						if (smp.LoopEnd > smp.Length)
							smp.LoopEnd = smp.Length;
						if (smp.LoopStart + 2 < smp.LoopEnd)
							smp.Flags |= SampleFlags.Loop;
					}

					if (filetype == ID_16SV)
					{
						flags |= SampleFormat._16;

						smp.Length >>= 1;
						smp.LoopStart >>= 1;
						smp.LoopEnd >>= 1;
					}
					else
						flags |= SampleFormat._8;

					IFF.ReadSample(stream, body, 0, smp, flags);

					return smp;
				}

				break;
			}
			case ID_AIFF:
			{
				AIFFChunkCOMM chunkCOMM = default;

				while (IFF.PeekChunk(stream) is IFFChunk innerChunk)
				{
					switch (innerChunk.ID)
					{
						case ID_COMM: comm = chunk; break;
						case ID_SSND: ssnd = chunk; break;
						case ID_NAME: name = chunk; break;
						default: break;
					}
				}

				if (!(comm != null && ssnd != null))
					return null;

				if (0 == IFF.Receive(
					stream,
					comm,
					(buf, bufLen) =>
					{
						chunkCOMM = StructureSerializer.MarshalFromBytes<AIFFChunkCOMM>(buf, bufLen);
						return 1;
					}))
					return null;

				if (file != null)
				{
					if (chunkCOMM.sample_rate != null)
						file.SampleSpeed = (int)Float80.FromIEEE80Bytes(chunkCOMM.sample_rate);
					file.SampleLength = (int)ByteSwap.Swap(chunkCOMM.num_frames);

					file.Description = "Audio IFF sample";
					file.Type = FileTypes.SamplePlain;
				}

				string? sampleName = null;

				if (name != null)
				{
					byte[] nameBytes = new byte[name.Size];

					IFF.Read(stream, name, nameBytes);

					int nameLength = Array.IndexOf(nameBytes, 0);

					if (nameLength < 0)
						nameLength = nameBytes.Length;

					string nameValue = nameBytes.ToStringZ();

					if (file != null)
						file.Title = nameValue;

					if (readSampleData)
						sampleName = nameValue;
				}

				/* TODO loop points */

				if (readSampleData)
				{
					var smp = new SongSample();

					if (sampleName != null)
						smp.Name = sampleName;

					SampleFormat flags = SampleFormat.BigEndian | SampleFormat.PCMSigned;

					switch (ByteSwap.Swap(chunkCOMM.num_channels))
					{
						default:
							Log.Append(4, "warning: multichannel AIFF is unsupported");
							goto case 1;
						case 1:
							flags |= SampleFormat.Mono;
							break;
						case 2:
							flags |= SampleFormat.StereoInterleaved;
							break;
					}

					switch ((ByteSwap.Swap(chunkCOMM.sample_size) + 7) & ~7)
					{
						default:
							Log.Append(4, "warning: AIFF has unsupported bit-width");
							goto case 8;
						case 8:
							flags |= SampleFormat._8;
							break;
						case 16:
							flags |= SampleFormat._16;
							break;
						case 24:
							flags |= SampleFormat._24;
							break;
						case 32:
							flags |= SampleFormat._32;
							break;
					}

					// TODO: data checking; make sure sample count and byte size agree
					// (and if not, cut to shorter of the two)

					if (chunkCOMM.sample_rate != null)
						smp.C5Speed = (int)Float80.FromIEEE80Bytes(chunkCOMM.sample_rate);
					smp.Length = (int)ByteSwap.Swap(chunkCOMM.num_frames);
					smp.Volume = 64 * 4;
					smp.GlobalVolume = 64;

					// the audio data starts 8 bytes into the chunk
					// (don't care about the block alignment stuff)
					IFF.ReadSample(stream, ssnd, 8, smp, flags);

					return smp;
				}

				break;
			}
		}

		return null;
	}

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			ReadIFF(stream, file, false);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public override SongSample LoadSample(Stream stream)
	{
		return ReadIFF(stream, null, true) ?? throw new NotSupportedException();
	}

	public override SaveResult SaveSample(SongSample smp, Stream stream)
	{
		long startPosition = stream.Position;

		if (smp.Flags.HasFlag(SampleFlags.AdLib))
			return SaveResult.Unsupported;

		SampleFormat flags = SampleFormat.BigEndian | SampleFormat.PCMSigned;

		flags |= smp.Flags.HasFlag(SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8;
		flags |= smp.Flags.HasFlag(SampleFlags.Stereo) ? SampleFormat.StereoInterleaved : SampleFormat.Mono;

		int bps = AIFFFile.WriteAIFFHeader(stream, smp.Flags.HasFlag(SampleFlags._16Bit) ? 16 : 8, smp.Flags.HasFlag(SampleFlags.Stereo) ? 2 : 1,
			smp.C5Speed, smp.Name, smp.Length, null);

		if (WriteSample(stream, smp, flags, uint.MaxValue) != smp.Length * bps)
		{
			Log.Append(4, "AIFF: unexpected data size written");
			return SaveResult.InternalError;
		}

		/* TODO: loop data */

		/* fix the length in the file header */
		long endPosition = stream.Position;

		int chunkContentLength = (int)(endPosition - startPosition - 8);

		chunkContentLength = ByteSwap.Swap(chunkContentLength);

		stream.Position = startPosition + 4;

		var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

		writer.Write(chunkContentLength);
		writer.Flush();

		return SaveResult.Success;
	}
}
