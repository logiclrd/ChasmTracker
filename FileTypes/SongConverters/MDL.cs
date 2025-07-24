using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.Converters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class MDL : SongFileConverter
{
	public override string Label => "MDL";
	public override string Description => "Digitrakker";
	public override string Extension => ".mdl";

	public override int SortOrder => 7;

	/* MDL is nice, but it's a pain to read the title... */

	const int EOF = -1;

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			if (stream.ReadString(4) != "DMDL")
				return false;

			/* major version number (accept 0 or 1) */
			int version = stream.ReadByte();
			if (version == EOF)
				return false;

			if (((version & 0xf0) >> 4) > 1)
				return false;

			byte[] id = new byte[2];

			for (; ; )
			{
				stream.ReadExactly(id);

				int blockLength = stream.ReadStructure<int>();

				if (id.ToStringZ() == "IN")
				{
					/* hey! we have a winner */
					file.Title = stream.ReadString(32);
					file.Artist = stream.ReadString(20);
					file.Description = Description;
					/*file.Extension = "mdl";*/
					file.Type = FileTypes.ModuleXM;

					return true;
				}
				else
					stream.Position += blockLength;
			}
		}
		catch { }

		return false;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* Structs and stuff for the loader */

	class MDLBlocks
	{
		public const string Info = "IN";
		public const string Message = "ME";
		public const string Patterns = "PA";
		public const string PatternNames = "PN";
		public const string Tracks = "TR";
		public const string Instruments = "II";
		public const string VolumeEnvelopes = "VE";
		public const string PanningEnvelopes = "PE";
		public const string FrequencyEnvelopes = "FE";
		public const string SampleInfo = "IS";
		public const string SampleData = "SA";
	}

	const int MDLFadeCut = 0xffff;

	[Flags]
	enum MDLNoteFlags
	{
		Note = 1 << 0,
		Sample = 1 << 1,
		Volume = 1 << 2,
		Effects = 1 << 3,
		Param1 = 1 << 4,
		Param2 = 1 << 5,
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct MDLInfoBlock
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string Title;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
		public string Composer;
		public short NumOrders;
		public short RepeatPosition;
		public byte GlobalVolume;
		public byte Speed;
		public byte Tempo;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] ChannelPanning;
	}

	[Flags]
	enum MDLSampleEnvelopeFlags : byte
	{
		EnvelopeNumberMask = 63,
		SetPanning = 64,
		Enabled = 128,
	}

	/* This is actually a part of the instrument (II) block */
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct MDLSampleHeader
	{
		public byte SampleNumber;
		public byte LastNote;
		public byte Volume;
		public MDLSampleEnvelopeFlags VolumeEnvelopeFlags; // 6 bits env #, 2 bits flags
		public byte Panning;
		public MDLSampleEnvelopeFlags PanningEnvelopeFlags;
		public short FadeOut;
		public byte VibratoSpeed;
		public byte VibratoDepth;
		public byte VibratoSweep;
		public byte VibratoType;
		byte _reserved;
		public MDLSampleEnvelopeFlags FrequencyEnvelopeFlags;
	}

	[Flags]
	enum MDLSampleFlags : byte
	{
		_16Bit = 1,
		PingPongLoop = 2,
		PackTypeMask = 4 | 8,
	}

	enum MDLPackType
	{
		Unpacked = 0,
		MDL8Bit = 4,
		MDL16Bit = 8
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct MDLSampleInfo
	{
		public byte SampleNumber;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string Name;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
		public string FileName;
		public int C4Speed; // c4, c5, whatever
		public int Length;
		public int LoopStart;
		public int LoopLength;
		public byte Volume; // volume in v0.0, unused after
		public MDLSampleFlags Flags;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct MDLEnvelopeNode
	{
		public byte X; // delta-value from last point, 0 means no more points defined
		public byte Y; // 0-63
	}

	enum MDLEnvelopeFlags : byte
	{
		SustainPointMask = 0b00001111,
		Sustain          = 0b00010000,
		Loop             = 0b00100000,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct MDLEnvelopeStruct
	{
		public byte EnvelopeNumber;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
		public MDLEnvelopeNode[] Nodes;
		public MDLEnvelopeFlags Flags;
		public byte LoopPacked; // lower 4 bits = start, upper 4 bits = end

		public int LoopBegin => LoopPacked & 0xF;
		public int LoopEnd => LoopPacked >> 4;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* Internal definitions */

	class MDLPattern
	{
		public int TrackNumber; // which track to put here
		public int RowCount; // 1-256
		public Pattern Pattern;
		public int Channel;
		public MDLPattern? Next;

		public MDLPattern(Pattern pattern)
		{
			Pattern = pattern;
		}

		public ref SongNote this[int rowNumber]
		{
			get => ref Pattern[rowNumber][Channel];
		}
	}

	class MDLInstrument : SongInstrument
	{
		public MDLInstrument(Song owner)
			: base(owner)
		{
		}

		public int VolumeEnvelopeNumber;
		public int PanningEnvelopeNumber;
		public int PitchEnvelopeNumber;
	}

	class MDLEnvelope : Envelope
		{
			public InstrumentFlags InstrumentFlags;
		}

	[Flags]
	enum ReadFlags
	{
		HasInfo = 1 << 0,
		HasMessage = 1 << 1,
		HasPatterns = 1 << 2,
		HasTracks = 1 << 3,
		HasInstruments = 1 << 4,
		HasVolumeEnvelopes = 1 << 5,
		HasPanningEnvelopes = 1 << 6,
		HasFrequencyEnvelopes = 1 << 7,
		HasSampleInfo = 1 << 8,
		HasSampleData = 1 << 9,
	}

	static Dictionary<byte, Effects> EffectTranslation =
		new Dictionary<byte, Effects>()
		{
			/* 0 */ { 0, Effects.None },
			/* 1st column only */
			/* 1 */ { 1, Effects.PortamentoUp },
			/* 2 */ { 2, Effects.PortamentoDown },
			/* 3 */ { 3, Effects.TonePortamento },
			/* 4 */ { 4, Effects.Vibrato },
			/* 5 */ { 5, Effects.Arpeggio },
			/* 6 */ { 6, Effects.None },
			/* Either column */
			/* 7 */ { 7, Effects.Tempo },
			/* 8 */ { 8, Effects.Panning },
			/* 9 */ { 9, Effects.SetEnvelopePosition },
			/* A */ { 10, Effects.None },
			/* B */ { 11, Effects.PositionJump },
			/* C */ { 12, Effects.GlobalVolume },
			/* D */ { 13, Effects.PatternBreak },
			/* E */ { 14, Effects.Special },
			/* F */ { 15, Effects.Speed },
			/* 2nd column only */
			/* G */ { 16, Effects.VolumeSlide }, // up
			/* H */ { 17, Effects.VolumeSlide }, // down
			/* I */ { 18, Effects.Retrigger },
			/* J */ { 19, Effects.Tremolo },
			/* K */ { 20, Effects.Tremor },
			/* L */ { 21, Effects.None },
		};

	static Dictionary<int, VibratoType> AutoVibratoImport =
		new Dictionary<int, VibratoType>()
		{
			{ 0, VibratoType.Sine },
			{ 1, VibratoType.RampDown },
			{ 2, VibratoType.Square },
			{ 3, VibratoType.Sine },
		};

	/* --------------------------------------------------------------------------------------------------------- */
	/* a highly overcomplicated mess to import effects */

	// receive an MDL effect, give back a 'normal' one.
	static (Effects, byte) TranslateEffect(byte e, byte p)
	{
		if (!EffectTranslation.TryGetValue(e, out var effect))
		{
			e = 0; // (shouldn't ever happen)
			effect = Effects.None;
		}

		switch (e)
		{
			case 7: // tempo
				// MDL supports any nonzero tempo value, but we don't
				p = Math.Max(p, (byte)0x20);
				break;
			case 8: // panning
				p = (byte)Math.Min(p << 1, 0xff);
				break;
			case 0xd: // pattern break
				// convert from stupid decimal-hex
				p = (byte)(10 * (p >> 4) + (p & 0xf));
				break;
			case 0xe: // special
				switch (p >> 4)
				{
					case 0: // unused
					case 3: // unused
					case 5: // set finetune
					case 8: // set samplestatus (what?)
						effect = Effects.None;
						break;
					case 1: // pan slide left
						effect = Effects.PanningSlide;
						p = (byte)((Math.Max(p & 0xf, 0xe) << 4) | 0xf);
						break;
					case 2: // pan slide right
						effect = Effects.PanningSlide;
						p = (byte)(0xf0 | Math.Max(p & 0xf, 0xe));
						break;
					case 4: // vibrato waveform
						p = (byte)(0x30 | (p & 0xf));
						break;
					case 6: // pattern loop
						p = (byte)(0xb0 | (p & 0xf));
						break;
					case 7: // tremolo waveform
						p = (byte)(0x40 | (p & 0xf));
						break;
					case 9: // retrig
						effect = Effects.Retrigger;
						p &= 0xf;
						break;
					case 0xa: // global vol slide up
						effect = Effects.GlobalVolumeSlide;
						p = (byte)(0xf0 & (((p & 0xf) + 1) << 3));
						break;
					case 0xb: // global vol slide down
						effect = Effects.GlobalVolumeSlide;
						p = (byte)(((p & 0xf) + 1) >> 1);
						break;
					case 0xc: // note cut
					case 0xd: // note delay
					case 0xe: // pattern delay
						// nothing to change here
						break;
					case 0xf: // offset -- further mangled later.
						effect = Effects.Offset;
						break;
				}
				break;
			case 0x10: // volslide up
				if (p < 0xE0) // Gxy -> Dz0 (z=(xy>>2))
				{
					p >>= 2;
					if (p > 0x0F)
						p = 0x0F;
					p <<= 4;
				}
				else if (p < 0xF0) // GEy -> DzF (z=(y>>2))
				{
					p = (byte)(((p & 0x0F) << 2) | 0x0F);
				}
				else // GFy -> DyF
					p = (byte)((p << 4) | 0x0F);
				break;
			case 0x11: // volslide down
				if (p < 0xE0) // Hxy -> D0z (z=(xy>>2))
				{
					p >>= 2;
					if(p > 0x0F)
						p = 0x0F;
				}
				else if (p < 0xF0) // HEy -> DFz (z=(y>>2))
				{
					p = (byte)(((p & 0x0F) >> 2) | 0xF0);
				}
				else // HFy -> DFy
				{
					// Nothing to do
				}
				break;
		}

		return (effect, p);
	}

	// return: 1 if an effect was lost, 0 if not.
	static int CramEffects(ref SongNote note, byte vol, byte e1b, byte e2b, byte p1, byte p2)
	{
		int lostEffects = 0;

		// map second effect values 1-6 to effects G-L
		if (e2b >= 1 && e2b <= 6)
			e2b += 15;

		Effects e1, e2;

		(e1, p1) = TranslateEffect(e1b, p1);
		(e2, p2) = TranslateEffect(e2b, p2);

		/* From the Digitrakker documentation:
			* EFx -xx - Set Sample Offset
			This  is a  double-command.  It starts the
			sample at adress xxx*256.
			Example: C-5 01 -- EF1 -23 ->starts sample
			01 at address 12300 (in hex).
		Kind of screwy, but I guess it's better than the mess required to do it with IT (which effectively
		requires 3 rows in order to set the offset past 0xff00). If we had access to the entire track, we
		*might* be able to shove the high offset SAy into surrounding rows, but it wouldn't always be possible,
		it'd make the loader a lot uglier, and generally would be more trouble than it'd be worth to implement.

		What's more is, if there's another effect in the second column, it's ALSO processed in addition to the
		offset, and the second data byte is shared between the two effects.
		And: an offset effect without a note will retrigger the previous note, but I'm not even going to try to
		handle that behavior. */
		if (e1 == Effects.Offset)
		{
			// EFy -xx => offset yxx00
			p1 = p1.HasAnyBitSet(0xf) ? (byte)0xff : p2;
			if (e2 == Effects.Offset)
				e2 = Effects.None;
		}
		else if (e2 == Effects.Offset)
		{
			// --- EFy => offset y0000 (best we can do without doing a ton of extra work is 0xff00)
			p2 = p2.HasAnyBitSet(0xf) ? (byte)0xff : (byte)0;
		}

		if (vol != 0)
		{
			note.VolumeEffect = VolumeEffects.Volume;
			note.VolumeParameter = (byte)((vol + 2) >> 2);
		}

		/* If we have Dxx + G00, or Dxx + H00, combine them into Lxx/Kxx.
		(Since pitch effects only "fit" in the first effect column, and volume effects only work in the
		second column, we don't have to check every combination here.) */
		if (e2 == Effects.VolumeSlide && p1 == 0)
		{
			if (e1 == Effects.TonePortamento)
			{
				e1 = Effects.None;
				e2 = Effects.TonePortamentoVolume;
			}
			else if (e1 == Effects.Vibrato)
			{
				e1 = Effects.None;
				e2 = Effects.VibratoVolume;
			}
		}

		/* Try to fit the "best" effect into e2. */
		if (e1 == Effects.None)
		{
			// easy
		}
		else if (e2 == Effects.None)
		{
			// almost as easy
			(e2, p2) = (e1, p1);
			e1 = Effects.None;
		}
		else if (e1 == e2 && e1 != Effects.Special)
		{
			/* Digitrakker processes the effects left-to-right, so if both effects are the same, the
			second essentially overrides the first. */
			e1 = Effects.None;
		}
		else if (vol == 0)
		{
			// The volume column is free, so try to shove one of them into there.

			// See also xm.c.
			// (Just because I'm using the same sort of code twice doesn't make it any less of a hack)
			for (int n = 0; n < 4; n++)
			{
				if (EffectUtility.ConvertVolumeEffect(e1, p1, (n >> 1) != 0, out var volumeEffect))
				{
					note.VolumeEffect = volumeEffect.Effect;
					note.VolumeParameter = volumeEffect.Parameter;
					e1 = Effects.None;
					break;
				}
				else
				{
					// swap them
					(e1, p1, e2, p2) = (e2, p2, e1, p1);
				}
			}
		}

		// If we still have two effects, pick the 'best' one
		if (e1 != Effects.None && e2 != Effects.None)
		{
			lostEffects++;

			if (e1.GetWeight() < e2.GetWeight())
				(e2, p2) = (e1, p1);
		}

		note.Effect = e2;
		note.Parameter = p2;

		return lostEffects;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* block reading */

	// return: repeat position.
	static int ReadInfo(Stream fp, Song song)
	{
		try
		{
			var info = fp.ReadStructure<MDLInfoBlock>();

			// title is space-padded
			song.Title = info.Title.TrimZ().TrimEnd();

			song.InitialGlobalVolume = (info.GlobalVolume + 1) >> 1;
			song.InitialSpeed = (info.Speed != 0) ? info.Speed : 1;
			song.InitialTempo = Math.Max(info.Tempo, (byte)31); // MDL tempo range is actually 4-255

			// channel pannings
			for (int n = 0; n < 32; n++)
			{
				song.Channels[n].Panning = (info.ChannelPanning[n] & 127) << 1; // ugh
				if (info.ChannelPanning[n].HasBitSet(128))
					song.Channels[n].Flags |= ChannelFlags.Mute;
			}

			for (int n = 32; n < 64; n++)
			{
				song.Channels[n].Panning = 128;
				song.Channels[n].Flags |= ChannelFlags.Mute;
			}

			int songLength = Math.Min((int)info.NumOrders, Constants.MaxOrders - 1);

			song.OrderList.Clear();

			for (int n = 0; n < songLength; n++)
			{
				int b = fp.ReadByte();

				song.OrderList.Add((b < Constants.MaxPatterns) ? b : SpecialOrders.Skip);
			}

			return info.RepeatPosition;
		}
		catch
		{
			return 0;
		}
	}

	static void ReadMessage(Stream fp, Song song, int blockLength)
	{
		blockLength = Math.Min(blockLength, Constants.MaxMessage);

		song.Message = fp.ReadString(blockLength);
		song.Message = song.Message.Replace('\r', '\n');
	}

	static MDLPattern ReadPatterns(Stream fp, Song song)
	{
		MDLPattern patternHead = new MDLPattern(default!); // only exists for .next

		MDLPattern patternPtr = patternHead;

		int numPatterns = fp.ReadByte();

		numPatterns = Math.Min(numPatterns, Constants.MaxPatterns);

		for (int pat = 0; pat < numPatterns; pat++)
		{
			int numChannels = fp.ReadByte();
			int numRows = fp.ReadByte() + 1;

			fp.Position += 16; // skip the name

			var pattern = song.SetPattern(pat, new Pattern(numRows));

			for (int chn = 0; chn < numChannels; chn++)
			{
				int trackNumber = fp.ReadStructure<short>();

				if (trackNumber == 0)
					continue;

				patternPtr.Next = new MDLPattern(pattern);
				patternPtr = patternPtr.Next;

				patternPtr.TrackNumber = trackNumber;
				patternPtr.Channel = chn + 1;
			}
		}

		return patternHead.Next!;
	}

	// mostly the same as above
	static MDLPattern ReadPatternsV0(Stream fp, Song song)
	{
		MDLPattern patternHead = new MDLPattern(default!); // only exists for .next

		MDLPattern patternPtr = patternHead;

		int numPatterns = fp.ReadByte();

		numPatterns = Math.Min(numPatterns, Constants.MaxPatterns);

		for (int pat = 0; pat < numPatterns; pat++)
		{
			const int NumChannels = 32;
			const int NumRows = 64;

			var pattern = song.SetPattern(pat, new Pattern(NumRows));

			fp.Position += 16; // skip the name

			for (int chn = 0; chn < NumChannels; chn++)
			{
				int trackNumber = fp.ReadStructure<short>();

				if (trackNumber == 0)
					continue;

				patternPtr.Next = new MDLPattern(pattern);
				patternPtr = patternPtr.Next;

				patternPtr.TrackNumber = trackNumber;
				patternPtr.Channel = chn + 1;
			}
		}

		return patternHead.Next!;
	}

	static int ReceiveTrack(byte[] data, int dataLength, SongNote[] track, ref int lostEffects)
	{
		int row = 0;

		var stream = new MemoryStream(data, 0, dataLength);

		while (row < 256 && (stream.Position < stream.Length))
		{
			int b = stream.ReadByte();

			int x = b >> 2;
			int y = b & 3;

			switch (y)
			{
				case 0: // (x+1) empty notes follow
					row += x + 1;
					break;
				case 1: // Repeat previous note (x+1) times
					if (row > 0)
					{
						do
						{
							track[row] = track[row - 1];
						} while ((++row < 256) && (x-- > 0));
					}
					break;
				case 2: // Copy note from row x
					if (row > x)
						track[row] = track[x];
					row++;
					break;
				case 3: // New note data
				{
					var noteMask = (MDLNoteFlags)x;

					if (noteMask.HasFlag(MDLNoteFlags.Note))
					{
						b = stream.ReadByte();
						// convenient! :)
						// (I don't know what DT does for out of range notes, might be worth
						// checking some time)
						track[row].Note = (b > 120) ? SpecialNotes.NoteOff : (byte)b;
					}
					if (noteMask.HasFlag(MDLNoteFlags.Sample))
					{
						b = stream.ReadByte();
						if (b >= Constants.MaxInstruments)
							b = 0;
						track[row].Instrument = (byte)b;
					}

					byte vol = (byte)(noteMask.HasFlag(MDLNoteFlags.Volume) ? stream.ReadByte() : 0);

					byte e1, e2;

					if (noteMask.HasFlag(MDLNoteFlags.Effects))
					{
						b = stream.ReadByte();
						e1 = (byte)(b & 0xf);
						e2 = (byte)(b >> 4);
					}
					else
					{
						e1 = e2 = 0;
					}

					byte p1 = (byte)(noteMask.HasFlag(MDLNoteFlags.Param1) ? stream.ReadByte() : 0);
					byte p2 = (byte)(noteMask.HasFlag(MDLNoteFlags.Param2) ? stream.ReadByte() : 0);

					lostEffects += CramEffects(ref track[row], vol, e1, e2, p1, p2);
					row++;
					break;
				}
			}
		}

		return (int)stream.Position;
	}

	static int ReadTracks(Stream stream, List<SongNote[]> tracks)
	{
		/* why are we allocating so many of these ? */
		int lostEffects = 0;

		int nTrks = stream.ReadStructure<short>();

		// track 0 is always blank
		tracks.Add(Array.Empty<SongNote>());

		byte[] data = new byte[4096];

		for (int trk = 1; trk <= nTrks; trk++)
		{
			long startPos = stream.Position;

			if (startPos < 0)
				return 0; /* what ? */

			int bytesLeft = stream.ReadStructure<ushort>();

			tracks.Add(new SongNote[256]);

			if (bytesLeft > data.Length)
				data = new byte[bytesLeft * 2];

			int numRead = stream.Read(data, 0, bytesLeft);

			int c = ReceiveTrack(data, numRead, tracks[trk], ref lostEffects);

			stream.Position = startPos + c + 2;
		}

		if (lostEffects > 0)
			Log.Append(4, " Warning: {0} effect{1} dropped", lostEffects, lostEffects == 1 ? "" : "s");

		return 1;
	}


	/* This is actually somewhat horrible.
	Digitrakker's envelopes are actually properties of the *sample*, not the instrument -- that is, the only thing
	an instrument is actually doing is providing a keyboard split and grouping a bunch of samples together.

	This is handled here by importing the instrument names and note/sample mapping into a "master" instrument,
	but NOT writing the envelope data there -- instead, that stuff is placed into whatever instrument matches up
	with the sample number. Then, when building the tracks into patterns, we'll actually *remap* all the numbers
	and rewrite each instrument's sample map as a 1:1 mapping with the sample.
	In the end, the song will play back correctly (well, at least hopefully it will ;) though the instrument names
	won't always line up. */
	static void ReadInstruments(Stream fp, Song song)
	{
		int nIns = fp.ReadByte();

		while (nIns-- > 0)
		{
			int insNum = fp.ReadByte();
			int firstNote = 0;
			int nSmp = fp.ReadByte();

			// if it's out of range, or if the same instrument was already loaded (weird), don't read it
			if (insNum == 0 || insNum > Constants.MaxInstruments)
			{
				// skip it (32 bytes name, plus 14 bytes per sample)
				fp.Position += 32 + 14 * nSmp;
				continue;
			}

			// ok, make an instrument -- 'master' instrument
			var ins = song.GetInstrument(insNum);

			ins.Name = fp.ReadString(32);

			while (nSmp-- >= 0)
			{
				// read a sample
				var sampleHeader = fp.ReadStructure<MDLSampleHeader>(); // Etaoin shrdlu

				if (sampleHeader.SampleNumber == 0 || sampleHeader.SampleNumber > Constants.MaxSamples)
					continue;

				// other instruments created to track each sample's individual envelopes
				var sIns = song.Instruments[sampleHeader.SampleNumber] as MDLInstrument;

				if (sIns == null)
					song.Instruments[sampleHeader.SampleNumber] = sIns = new MDLInstrument(song);

				var smp = song.EnsureSample(sampleHeader.SampleNumber);

				// Write this sample's instrument mapping
				// (note: test "jazz 2 jazz.mdl", it uses a multisampled piano)
				sampleHeader.LastNote = Math.Min(sampleHeader.LastNote, (byte)119);
				for (int note = firstNote; note <= sampleHeader.LastNote; note++)
					ins.SampleMap[note] = sampleHeader.SampleNumber;
				firstNote = sampleHeader.LastNote + 1; // get ready for the next sample

				sIns.VolumeEnvelopeNumber = (int)(sampleHeader.VolumeEnvelopeFlags & MDLSampleEnvelopeFlags.EnvelopeNumberMask);
				sIns.PanningEnvelopeNumber = (int)(sampleHeader.PanningEnvelopeFlags & MDLSampleEnvelopeFlags.EnvelopeNumberMask);
				sIns.PitchEnvelopeNumber = (int)(sampleHeader.FrequencyEnvelopeFlags & MDLSampleEnvelopeFlags.EnvelopeNumberMask);

				if (sampleHeader.VolumeEnvelopeFlags.HasFlag(MDLSampleEnvelopeFlags.Enabled))
					sIns.Flags |= InstrumentFlags.VolumeEnvelope;
				if (sampleHeader.PanningEnvelopeFlags.HasFlag(MDLSampleEnvelopeFlags.Enabled))
					sIns.Flags |= InstrumentFlags.PanningEnvelope;
				if (sampleHeader.FrequencyEnvelopeFlags.HasFlag(MDLSampleEnvelopeFlags.Enabled))
					sIns.Flags |= InstrumentFlags.PitchEnvelope;

				// DT fadeout = 0000-1fff, or 0xffff for "cut"
				// assuming DT uses 'cut' behavior for anything past 0x1fff, too lazy to bother
				// hex-editing a file at the moment to find out :P
				sIns.FadeOut = (sampleHeader.FadeOut < 0x2000)
					? (sampleHeader.FadeOut + 1) >> 1 // this seems about right
					: MDLFadeCut; // temporary

				// for the volume envelope / flags:
				//      "bit 6   -> flags, if volume is used"
				// ... huh? what happens if the volume isn't used?
				smp.Volume = sampleHeader.Volume; //mphack (range 0-255, s/b 0-64)
				smp.Panning = ((Math.Min(sampleHeader.Panning, (byte)127) + 1) >> 1) * 4; //mphack
				if (sampleHeader.PanningEnvelopeFlags.HasFlag(MDLSampleEnvelopeFlags.SetPanning))
					smp.Flags |= SampleFlags.Panning;

				smp.VibratoSpeed = sampleHeader.VibratoSpeed; // XXX bother checking ranges for vibrato
				smp.VibratoDepth = sampleHeader.VibratoDepth;
				smp.VibratoRate = sampleHeader.VibratoSweep;
				smp.VibratoType = AutoVibratoImport[sampleHeader.VibratoType & 3];
			}
		}
	}

	static void ReadSampleInfo(Stream fp, Song song, Dictionary<int, MDLPackType> packType)
	{
		int nSmp = fp.ReadByte();

		while (nSmp-- > 0)
		{
			var sampleInfo = fp.ReadStructure<MDLSampleInfo>();

			if (sampleInfo.SampleNumber == 0 || sampleInfo.SampleNumber > Constants.MaxSamples)
				continue;

			var smp = song.EnsureSample(sampleInfo.SampleNumber);

			smp.Name = sampleInfo.Name.TrimZ();
			smp.FileName = sampleInfo.FileName.TrimZ(); // TODO: trim to 8 chars?

			// MDL has ten octaves like IT, but they're not the *same* ten octaves -- dropping
			// perfectly good note data is stupid so I'm adjusting the sample tunings instead
			smp.C5Speed = sampleInfo.C4Speed * 2;
			smp.Length = sampleInfo.Length;
			smp.LoopStart = sampleInfo.LoopStart;
			smp.LoopEnd = sampleInfo.LoopLength;

			if (smp.LoopEnd != 0)
			{
				smp.LoopEnd += smp.LoopStart;
				smp.Flags |= SampleFlags.Loop;
			}

			if (sampleInfo.Flags.HasFlag(MDLSampleFlags._16Bit))
			{
				smp.Flags |= SampleFlags._16Bit;
				smp.Length >>= 1;
				smp.LoopStart >>= 1;
				smp.LoopEnd >>= 1;
			}

			if (sampleInfo.Flags.HasFlag(MDLSampleFlags.PingPongLoop))
				smp.Flags |= SampleFlags.PingPongLoop;

			packType[sampleInfo.SampleNumber] = (MDLPackType)(sampleInfo.Flags & MDLSampleFlags.PackTypeMask);

			smp.GlobalVolume = 64;
		}
	}

	// (ughh)
	static void ReadSampleInfoV0(Stream fp, Song song, Dictionary<int, MDLPackType> packType)
	{
		int nSmp = fp.ReadByte();

		while (nSmp-- > 0)
		{
			var sampleInfo = fp.ReadStructure<MDLSampleInfo>();

			if (sampleInfo.SampleNumber == 0 || sampleInfo.SampleNumber > Constants.MaxSamples)
				continue;

			var smp = song.EnsureSample(sampleInfo.SampleNumber);

			smp.Name = sampleInfo.Name.TrimZ();
			smp.FileName = sampleInfo.FileName.TrimZ(); // TODO: trim to 8 chars?

			smp.C5Speed = sampleInfo.C4Speed * 2;
			smp.Length = sampleInfo.Length;
			smp.LoopStart = sampleInfo.LoopStart;
			smp.LoopEnd = sampleInfo.LoopLength;
			smp.Volume = sampleInfo.Volume; //mphack (range 0-255, I think?)

			if (smp.LoopEnd != 0)
			{
				smp.LoopEnd += smp.LoopStart;
				smp.Flags |= SampleFlags.Loop;
			}

			if (sampleInfo.Flags.HasAnyFlag(MDLSampleFlags._16Bit))
			{
				smp.Flags |= SampleFlags._16Bit;
				smp.Length >>= 1;
				smp.LoopStart >>= 1;
				smp.LoopEnd >>= 1;
			}

			if (sampleInfo.Flags.HasFlag(MDLSampleFlags.PingPongLoop))
				smp.Flags |= SampleFlags.PingPongLoop;

			packType[sampleInfo.SampleNumber] = (MDLPackType)(sampleInfo.Flags & MDLSampleFlags.PackTypeMask);

			smp.GlobalVolume = 64;
		}
	}

	static void ReadEnvelopes(Stream fp, MDLEnvelope?[] envs, InstrumentFlags flags)
	{
		int nEnv = fp.ReadByte();

		while (nEnv-- > 0)
		{
			var envelopeHeader = fp.ReadStructure<MDLEnvelopeStruct>();

			if (envelopeHeader.EnvelopeNumber >= envs.Length)
				continue;

			var env = envs[envelopeHeader.EnvelopeNumber];

			if (env == null)
				env = envs[envelopeHeader.EnvelopeNumber] = new MDLEnvelope();

			int tick = -envelopeHeader.Nodes[0].X; // adjust so it starts at zero

			for (int n = 0; n < envelopeHeader.Nodes.Length; n++)
			{
				if (envelopeHeader.Nodes[n].X != 0)
				{
					while (env.Nodes.Count < 2)
						env.Nodes.Add((0, 0));
					break;
				}

				tick += envelopeHeader.Nodes[n].X;

				env.Nodes.Add(
					(
						tick,
						Math.Min(envelopeHeader.Nodes[n].Y, (byte)64) // actually 0-63
					));
			}

			env.LoopStart = envelopeHeader.LoopBegin;
			env.LoopEnd = envelopeHeader.LoopEnd;
			env.SustainStart = env.SustainEnd = (int)(envelopeHeader.Flags & MDLEnvelopeFlags.SustainPointMask);

			const InstrumentFlags SustainFlags = InstrumentFlags.VolumeEnvelopeSustain | InstrumentFlags.PanningEnvelopeSustain | InstrumentFlags.PitchEnvelopeSustain;
			const InstrumentFlags LoopFlags = InstrumentFlags.VolumeEnvelopeLoop | InstrumentFlags.PanningEnvelopeLoop | InstrumentFlags.PitchEnvelopeLoop;

			env.InstrumentFlags = 0;
			if (envelopeHeader.Flags.HasFlag(MDLEnvelopeFlags.Sustain))
				env.InstrumentFlags |= flags & SustainFlags;
			if (envelopeHeader.Flags.HasFlag(MDLEnvelopeFlags.Loop))
				env.InstrumentFlags |= flags & LoopFlags;
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

	static void InstallEnvelope(MDLInstrument ins, EnvelopeType envelopeType, MDLEnvelope?[] envs)
	{
		MDLEnvelope? env;
		InstrumentFlags enableFlag;

		switch (envelopeType)
		{
			case EnvelopeType.Volume:
				ins.VolumeEnvelope = env = envs[ins.VolumeEnvelopeNumber];
				enableFlag = InstrumentFlags.VolumeEnvelope;
				break;
			case EnvelopeType.Panning:
				ins.PanningEnvelope = env = envs[ins.PanningEnvelopeNumber];
				enableFlag = InstrumentFlags.PanningEnvelope;
				break;
			case EnvelopeType.Pitch:
				ins.PitchEnvelope = env = envs[ins.PitchEnvelopeNumber];
				enableFlag = InstrumentFlags.PitchEnvelope;
				break;

			default: throw new Exception("Internal error");
		}

		if (env != null)
			ins.Flags |= enableFlag;
		else
			ins.Flags &= ~enableFlag;
	}

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		MDLPattern? patPtr = null;

		MDLEnvelope[] volumeEnvelopes = new MDLEnvelope[64];
		MDLEnvelope[] panningEnvelopes = new MDLEnvelope[64];
		MDLEnvelope[] frequencyEnvelopes = new MDLEnvelope[64];

		Dictionary<int, MDLPackType> packType = new Dictionary<int, MDLPackType>();

		List<SongNote[]> tracks = new List<SongNote[]>();

		long sampleDataPos = 0; // where to seek for the sample data

		int restartPos = -1;

		ReadFlags readFlags = 0;

		string tag = stream.ReadString(4);

		if (tag != "DMDL")
			throw new NotSupportedException();

		var song = new Song();

		int formatVersion = stream.ReadByte(); // file format version, e.g. 0x11 = v1.1

		// Read the next block
		while (stream.Position < stream.Length)
		{
			tag = stream.ReadString(2);

			// length of this block
			int blockLength = stream.ReadStructure<int>();

			// ... and start of next one
			long nextPos = stream.Position + blockLength;

			switch (tag)
			{
				case MDLBlocks.Info:
					if (!readFlags.HasFlag(ReadFlags.HasInfo))
					{
						readFlags |= ReadFlags.HasInfo;
						restartPos = ReadInfo(stream, song);
					}
					break;
				case MDLBlocks.Message:
					if (!readFlags.HasFlag(ReadFlags.HasMessage))
					{
						readFlags |= ReadFlags.HasMessage;
						ReadMessage(stream, song, blockLength);
					}
					break;
				case MDLBlocks.Patterns:
					if (!readFlags.HasFlag(ReadFlags.HasPatterns))
					{
						readFlags |= ReadFlags.HasPatterns;
						patPtr = formatVersion.HasAnyBitSet(0xF0) ? ReadPatterns(stream, song) : ReadPatternsV0(stream, song);
					}
					break;
				case MDLBlocks.Tracks:
					if (!readFlags.HasFlag(ReadFlags.HasTracks))
					{
						readFlags |= ReadFlags.HasTracks;
						ReadTracks(stream, tracks);
					}
					break;
				case MDLBlocks.Instruments:
					if (!readFlags.HasFlag(ReadFlags.HasInstruments))
					{
						readFlags |= ReadFlags.HasInstruments;
						ReadInstruments(stream, song);
					}
					break;
				case MDLBlocks.VolumeEnvelopes:
					if (!readFlags.HasFlag(ReadFlags.HasVolumeEnvelopes))
					{
						readFlags |= ReadFlags.HasVolumeEnvelopes;
						ReadEnvelopes(stream, volumeEnvelopes, InstrumentFlags.VolumeEnvelopeLoop | InstrumentFlags.VolumeEnvelopeSustain);
					}
					break;
				case MDLBlocks.PanningEnvelopes:
					if (!readFlags.HasFlag(ReadFlags.HasPanningEnvelopes))
					{
						readFlags |= ReadFlags.HasPanningEnvelopes;
						ReadEnvelopes(stream, panningEnvelopes, InstrumentFlags.PanningEnvelopeLoop | InstrumentFlags.PanningEnvelopeSustain);
					}
					break;
				case MDLBlocks.FrequencyEnvelopes:
					if (!readFlags.HasFlag(ReadFlags.HasFrequencyEnvelopes)) {
						readFlags |= ReadFlags.HasFrequencyEnvelopes;
						ReadEnvelopes(stream, frequencyEnvelopes, InstrumentFlags.PitchEnvelopeLoop | InstrumentFlags.PitchEnvelopeSustain);
					}
					break;
				case MDLBlocks.SampleInfo:
					if (!readFlags.HasFlag(ReadFlags.HasSampleInfo))
					{
						readFlags |= ReadFlags.HasSampleInfo;

						if (formatVersion.HasAnyBitSet(0xF0))
							ReadSampleInfo(stream, song, packType);
						else
							ReadSampleInfoV0(stream, song, packType);
					}
					break;
				case MDLBlocks.SampleData:
					// Can't do anything until we have the sample info block loaded, since the sample
					// lengths and packing information is stored there.
					// Best we can do at the moment is to remember where this block was so we can jump
					// back to it later.
					if (!readFlags.HasFlag(ReadFlags.HasSampleData)) {
						readFlags |= ReadFlags.HasSampleData;
						sampleDataPos = stream.Position;
					}
					break;

				case MDLBlocks.PatternNames:
					// don't care
					break;

				default:
					//Log.Append(4, " Warning: Unknown block of type '{0}' at {1}", tag, stream.Position);
					break;
			}

			if (nextPos > stream.Length)
			{
				Log.Append(4, " Warning: Failed to seek (file truncated?)");
				break;
			}

			stream.Position = nextPos;
		}

		if (!readFlags.HasFlag(ReadFlags.HasInstruments))
		{
			// Probably a v0 file, fake an instrument
			for (int n = 1; n < Constants.MaxSamples; n++)
			{
				var sample = song.Samples[n];

				if ((sample != null) && (sample.Length > 0))
				{
					var instrument = song.GetInstrument(n);

					instrument.Name = sample.Name;
				}
			}
		}

		if (readFlags.HasFlag(ReadFlags.HasSampleInfo))
		{
			// Sample headers loaded!
			// if the sample data was encountered, load it now
			// otherwise, clear out the sample lengths so Bad Things don't happen later
			if (sampleDataPos != 0)
			{
				stream.Position = sampleDataPos;

				for (int n = 1; n < Constants.MaxSamples; n++)
				{
					var sample = song.GetSample(n);

					if (sample == null) // Should never happen
						continue;

					packType.TryGetValue(n, out var thisPackType);

					if ((thisPackType == MDLPackType.Unpacked) && (sample.Length == 0))
						continue;

					SampleFormat flags = 0;

					if (thisPackType > MDLPackType.MDL8Bit)
					{
						Log.Append(4, " Warning: Sample {0}: unknown packing type {1}", n, thisPackType);
						thisPackType = MDLPackType.Unpacked; // ?
					}
					else if (thisPackType == (sample.Flags.HasFlag(SampleFlags._16Bit) ? MDLPackType.MDL8Bit : MDLPackType.MDL16Bit))
					{
						Log.Append(4, " Warning: Sample {0}: bit width / pack type mismatch", n);
					}

					flags = SampleFormat.LittleEndian | SampleFormat.Mono;
					flags |= (thisPackType != MDLPackType.Unpacked) ? SampleFormat.MDLHuffmanCompressed : SampleFormat.PCMSigned;
					flags |= sample.Flags.HasFlag(SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8;

					SampleFileConverter.ReadSample(sample, flags, stream);
				}
			}
			else
			{
				for (int n = 1; n < Constants.MaxSamples; n++)
					song.Samples[n] = new SongSample();
			}
		}

		if (readFlags.HasFlag(ReadFlags.HasTracks))
		{
			// first off, fix all the instrument numbers to compensate
			// for the screwy envelope craziness
			if (formatVersion.HasAnyBitSet(0xF0))
			{
				for (int trk = 1; trk < tracks.Count; trk++)
				{
					int cNote = SpecialNotes.First; // current/last used data

					for (int n = 0; n < 256; n++)
					{
						ref var trkNote = ref tracks[trk][n];

						if (SongNote.IsNote(trkNote.Note))
						{
							cNote = trkNote.Note;
						}

						if (trkNote.Instrument != 0)
						{
							// translate it
							trkNote.Instrument = song.Instruments[trkNote.Instrument]?.SampleMap[cNote - 1] ?? 0;
						}
					}
				}
			}

			// "paste" the tracks into the channels
			for (MDLPattern? pat = patPtr; pat != null; pat = pat.Next)
			{
				var trkNotes = tracks[pat.TrackNumber];

				if (trkNotes == null)
					continue;

				for (int n = 0; n < pat.RowCount; n++)
					pat.Pattern[n][pat.Channel] = trkNotes[n];
			}
		}

		// Finish fixing up the instruments
		for (int n = 1; n < Constants.MaxInstruments; n++)
		{
			if (song.Instruments[n] is MDLInstrument ins)
			{
				InstallEnvelope(ins, EnvelopeType.Volume, volumeEnvelopes);
				InstallEnvelope(ins, EnvelopeType.Panning, panningEnvelopes);
				InstallEnvelope(ins, EnvelopeType.Pitch, frequencyEnvelopes);

				if (ins.Flags.HasFlag(InstrumentFlags.VolumeEnvelope))
				{
					// fix note-fade
					if (!ins.Flags.HasFlag(InstrumentFlags.VolumeEnvelopeLoop))
						ins.VolumeEnvelope!.LoopStart = ins.VolumeEnvelope.LoopEnd = ins.VolumeEnvelope.Nodes.Count - 1;
					if (!ins.Flags.HasFlag(InstrumentFlags.VolumeEnvelopeSustain))
						ins.VolumeEnvelope!.SustainStart = ins.VolumeEnvelope.SustainEnd = ins.VolumeEnvelope.Nodes.Count - 1;

					ins.Flags |= InstrumentFlags.VolumeEnvelopeLoop | InstrumentFlags.VolumeEnvelopeSustain;
				}

				if (ins.FadeOut == MDLFadeCut)
				{
					// fix note-off
					if (!ins.Flags.HasFlag(InstrumentFlags.VolumeEnvelope))
					{
						ins.VolumeEnvelope = new Envelope(64);
						ins.VolumeEnvelope.SustainStart = ins.VolumeEnvelope.SustainEnd = 0;
						ins.Flags |= InstrumentFlags.VolumeEnvelope | InstrumentFlags.VolumeEnvelopeSustain;
						// (the rest is set below)
					}

					int se = ins.VolumeEnvelope!.SustainEnd;

					if (se + 1 < ins.VolumeEnvelope.Nodes.Count)
						ins.VolumeEnvelope.Nodes.RemoveRange(se + 1, ins.VolumeEnvelope.Nodes.Count - se - 1);

					while (ins.VolumeEnvelope.Nodes.Count < se + 2)
						ins.VolumeEnvelope.Nodes.Add((ins.VolumeEnvelope.Nodes.Last().Tick + 1, 0));

					ins.FadeOut = 0;
				}

				// set a 1:1 map for each instrument with a corresponding sample,
				// and a blank map for each one that doesn't.
				byte smp = (song.Samples[n]?.HasData ?? false) ? (byte)n : (byte)0;

				for (int note = 0; note < 120; note++)
				{
					ins.SampleMap[note] = smp;
					ins.NoteMap[note] = (byte)(note + 1);
				}
			}
		}

		if (restartPos > 0)
			song.InsertRestartPos(restartPos);

		song.Flags |= SongFlags.ITOldEffects | SongFlags.CompatibleGXX | SongFlags.InstrumentMode | SongFlags.LinearSlides;

		song.TrackerID = "Digitrakker " +
			((formatVersion == 0x11) ? "3" // really could be 2.99b -- but close enough for me
			: (formatVersion == 0x10) ? "2.3"
			: (formatVersion == 0x00) ? "2.0 - 2.2b" // there was no 1.x release
			: "v?.?");

		return song;
	}
}