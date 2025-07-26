using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.InstrumentConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class SF2 : InstrumentFileConverter, IFileInfoReader
{
	public override string Label => "SF2";
	public override string Description => "SoundFont2";
	public override string Extension => ".sf2";

	/* --------------------------------------------------------------------- */

	/* We simply treat an SF2 as a packed format containing a crap
	 * ton of samples, rather than a crap ton of *separate instruments*.
	 * This makes it a little bit easier, since we don't have to do
	 * anything past making an instrument loader (and letting the
	 * sample libraries do the rest of the work) */

	enum SF2ChunkType
	{
		/* these three are the only ones we care about outside of sf2_read */
		INAM,
		shdr, /* in pdta */
		smpl, /* contains raw sample data */
	};

	bool Read(Stream stream, Dictionary<SF2ChunkType, IFFChunk> cs)
	{
		if (stream.ReadString(4) != "RIFF")
			return false;

		stream.Position += 4;

		if (stream.ReadString(4) != "sfbk")
			return false;

		IFFChunk? cINFO = null;
		IFFChunk? csdta = null;
		IFFChunk? cpdta = null;

		while (RIFF.PeekChunk(stream) is IFFChunk c)
		{
			/* we only support LIST chunks
			 * actually, why the hell did they put
			 * everything in LIST chunks???
			 * it makes everything so damn complicated */
			if (c.ID != 0x4C495354)
				continue;

			int id = IFF.ReadStructure<int>(stream, c);

			id = ByteSwap.Swap(id);

			switch (id)
			{
				case 0x494E464F: /* INFO */
					cINFO = c;
					break;
				case 0x73647461: /* sdta */
					csdta = c;
					break;
				case 0x70647461: /* pdta */
					cpdta = c;
					break;
			}
		}

		if ((cINFO == null) || (csdta == null) || (cpdta == null))
			return false; /* definitely not sf2 */

		stream.Position = cINFO.Offset + 4;

		while ((RIFF.PeekChunk(stream) is IFFChunk c) && (stream.Position <= cINFO.Offset + cINFO.Size))
		{
			switch (c.ID)
			{
				case 0x494E414D: /* INAM */
					cs[SF2ChunkType.INAM] = c;
					break;
				case 0x6966696C: { /* ifil */
					if (c.Size != 4)
						return false;

					int ver = IFF.ReadStructure<int>(stream, c);

					/* low word = major,
					* high word = minor */

					switch (ver & 0xFFFF)
					{
						case 2:
							break;
						case 1:
						case 3:
							/* v1 and v3 are not tested; they'll probably
							* send back garbage. */
						default:
							return false;
					}

					break;
				}
				default:
					/* don't care */
					break;
			}
		}

		stream.Position = cpdta.Offset + 4;

		while ((RIFF.PeekChunk(stream) is IFFChunk c) && (stream.Position <= cpdta.Offset + cpdta.Size))
		{
			switch (c.ID)
			{
				case 0x73686472: /* shdr */
					cs[SF2ChunkType.shdr] = c;
					break;
			}
		}

		/* we always assume this chunk is 46 bytes large.
		* maybe different major versions have different structures for this
		* format. for now I'm not going to care :) */
		if ((cs[SF2ChunkType.shdr].Size % 46) != 0)
			return false;

		stream.Position = csdta.Offset + 4;

		while ((RIFF.PeekChunk(stream) is IFFChunk c) && (stream.Position <= csdta.Offset + csdta.Size))
		{
			switch (c.ID)
			{
				case 0x736D706C: /* smpl */
					cs[SF2ChunkType.smpl] = c;
					break;
				/* NOTE: there is also a `sm24` chunk that contains
				* raw 8-bit sample data, that can be added onto the
				* 16-bit sample data to create 24-bit data.
				* We don't even support 24-bit though, so I don't
				* care. :) */
			}
		}

		if (!cs.ContainsKey(SF2ChunkType.smpl))
			return false;

		return true;
	}

	public bool FillExtendedData(Stream stream, FileReference file)
	{
		var cs = new Dictionary<SF2ChunkType, IFFChunk>();

		if (!Read(stream, cs))
			return false;

		file.Description = Description;
		file.Type = FileSystem.FileTypes.InstrumentOther;

		if (cs.TryGetValue(SF2ChunkType.INAM, out var inam))
			file.Title = IFF.ReadString(stream, inam);

		return true;
	}

	enum SF2SampleType : short
	{
		Mono = 1,
		RightStereo = 2,
		LeftStereo = 4,
		LinkedSample = 8,
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct SF2SampleHeader
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
		public string Name;

		public int SampleOffset;
		public int SampleEnd;
		public int LoopStart;
		public int LoopEnd;
		public int Rate;
		public byte Note;
		public byte Cents;
		public short Link;
		public SF2SampleType Type;
	}

	public override bool LoadInstrument(Stream file, int slot)
	{
		var cs = new Dictionary<SF2ChunkType, IFFChunk>();

		if (slot == 0)
			return false;

		if (!Read(file, cs))
			return false;

		int numSamples = cs[SF2ChunkType.shdr].Size / 46;
		/* numSamples = MIN(numSamples, MAX_SAMPLES); -- some samples may be ignored */

		var instrumentLoader = new InstrumentLoader(Song.CurrentSong, slot);

		var g = instrumentLoader.Instrument;

		g.Name = IFF.ReadString(file, cs[SF2ChunkType.INAM]);

		for (int i = 0; i < 120; i++)
		{
			g.SampleMap[i] = 0;
			g.NoteMap[i] = (byte)(i + 1);
		}

		file.Position = cs[SF2ChunkType.shdr].Offset;

		for (int i = 0; i < numSamples; i++)
		{
			// char name[20];

			var header = file.ReadStructure<SF2SampleHeader>();

			switch (header.Type)
			{
				case SF2SampleType.Mono: /* mono */
				case SF2SampleType.RightStereo: /* right stereo (FIXME: which one contains the useful info?) */
					break;
				case SF2SampleType.LeftStereo: /* left stereo */
				case SF2SampleType.LinkedSample: /* linked sample (will never support this) */
				default: /* ??? */
					continue; /* unsupported */
			}

			/* invalid ?? */
			if (header.SampleEnd > (cs[SF2ChunkType.smpl].Size / 2))
				continue;

			/* NOW, allocate a sample number. */
			int n = instrumentLoader.GetNewSampleNumber(i + 1);

			if (n == 0)
				break;

			var smp = Song.CurrentSong.EnsureSample(n);

			smp.Name = header.Name;

			smp.Length = header.SampleEnd - header.SampleOffset;

			if ((header.LoopStart | header.LoopEnd) != 0)
			{
				smp.Flags |= SampleFlags.Loop;
				smp.LoopStart = header.LoopStart;
				smp.LoopEnd = header.LoopEnd;
			}

			/* now, transpose the frequency; also account for the cents as well.
			* hopefully at this point the sample is an actual middle C. */
			smp.C5Speed = (int)(header.Rate * Math.Pow(2, ((60 - header.Note) - (header.Cents / 100.0)) / 12.0));

			switch (header.Type)
			{
				case SF2SampleType.Mono: /* mono */
					IFF.ReadSample(file, cs[SF2ChunkType.smpl], header.SampleOffset << 1, smp,
						SampleFormat._16 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCMSigned);
					break;
				case SF2SampleType.RightStereo: /* stereo */
				{
					/* I HATE SF2 :))) */
					long rOffset = header.SampleOffset;
					long rEnd = header.SampleEnd;

					/* seek to the offsets */
					file.Position = cs[SF2ChunkType.shdr].Offset + (header.Link * 46) + 20;

					long lOffset = file.ReadStructure<uint>();
					long lEnd = file.ReadStructure<uint>();

					/* meh */
					rOffset = (rOffset * 2) + cs[SF2ChunkType.smpl].Offset;
					rEnd = (rEnd * 2) + cs[SF2ChunkType.smpl].Offset;
					lOffset = (lOffset * 2) + cs[SF2ChunkType.smpl].Offset;
					lEnd = (lEnd * 2) + cs[SF2ChunkType.smpl].Offset;

					/* so far I haven't found any files where lEnd == rOffset,
					* so I'm not going to add a special case for it ;) */

					var sf2SampleStream = new SF2SampleStream(
						file,
						lOffset, lEnd - lOffset,
						rOffset, rEnd - rOffset);

					SampleFileConverter.ReadSample(smp, SampleFormat._16 | SampleFormat.StereoSplit | SampleFormat.LittleEndian | SampleFormat.PCMSigned, sf2SampleStream);

					/* seek back to the next sample */
					file.Position = cs[SF2ChunkType.shdr].Offset + ((i + 1) * 46);

					break;
				}
				default:
					/* ??? */
					break;
			}
		}

		return true;
	}
}