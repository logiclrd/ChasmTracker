using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.SongConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class ULT : SongFileConverter
{
	public override string Label => "ULT";
	public override string Description => "UltraTracker Module";
	public override string Extension => ".ult";

	public override int SortOrder => 15;

	/* --------------------------------------------------------------------- */

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		if (stream.ReadString(14) != "MAS_UTrack_V00")
			return false;

		stream.Position = 15;

		string title = stream.ReadString(32);

		file.Description = "UltraTracker Module";
		file.Type = FileSystem.FileTypes.ModuleS3M;
		/*file.Extension = ".ult";*/
		file.Title = title;

		return true;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	[Flags]
	enum ULTSampleFlags : byte
	{
		_16bit = 4,
		Loop = 8,
		PingPongLoop = 16,
	};

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct ULTSample
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string Name;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
		public string FileName;
		public int LoopStart;
		public int LoopEnd;
		public int SizeStart;
		public int SizeEnd;
		public byte Volume; // 0-255, apparently prior to 1.4 this was logarithmic?
		public ULTSampleFlags Flags; // above
		public short Speed; // only exists for 1.4+
		public short FineTune;
	}

	bool ReadSample(Stream stream, int ver, out ULTSample smp)
	{
		try
		{
			smp = stream.ReadStructure<ULTSample>();

			if (ver < 4)
			{
				// If these bytes are at the very end of the stream, the ReadStructure will spuriously fail, but
				// I don't think that ever happens.
				smp.FineTune = smp.Speed;
				smp.Speed = 8363;

				stream.Position -= 2;
			}

			return true;
		}
		catch
		{
			smp = new ULTSample();
			return false;
		}
	}


	/* Unhandled effects:
	5x1 - do not loop sample (x is unused)
	5x2 - play sample backwards
	5xC - end loop and finish sample
	9xx - set sample offset to xx * 1024
			with 9yy: set sample offset to xxyy * 4
	E0x - set vibrato strength (2 is normal)
	F00 - reset speed/tempo to 6/125

	Apparently 3xx will CONTINUE to slide until it reaches its destination, or
	until a 300 effect is encountered. I'm not attempting to handle this (yet).

	The logarithmic volume scale used in older format versions here, or pretty
	much anywhere for that matter. I don't even think Ultra Tracker tries to
	convert them. */

	static readonly Effects[] ULTEffectTranslation =
		{
			Effects.Arpeggio,
			Effects.PortamentoUp,
			Effects.PortamentoDown,
			Effects.TonePortamento,
			Effects.Vibrato,
			Effects.None,
			Effects.None,
			Effects.Tremolo,
			Effects.None,
			Effects.Offset,
			Effects.VolumeSlide,
			Effects.Panning,
			Effects.Volume,
			Effects.PatternBreak,
			Effects.None, // extended effects, processed separately
			Effects.Speed,
		};

	enum ULTEffects : byte
	{
		Arpeggio,
		PortamentoUp,
		PortamentoDown,
		TonePortamento,
		Vibrato,
		Unknown1,
		Unknown2,
		Tremolo,
		Unknown3,
		Offset,
		VolumeSlide,
		Panning,
		Volume,
		PatternBreak,
		Extended,
		Speed,
	}

	enum ULTExtendedEffects
	{
		PortamenoUp = 1,
		PortamentoDown = 2,
		SetPanning = 8,
		Retrigger = 9,
		VolumeSlideUp = 10,
		VolumeSlideDown = 11,
		NoteCut = 12,
		NoteDelay = 13,
	}

	void TranslateEffect(ref byte ee, ref byte ep)
	{
		ULTEffects e = (ULTEffects)(ee & 0xf);
		byte p = ep;

		var te = ULTEffectTranslation[(int)e];

		switch (e)
		{
			case ULTEffects.Arpeggio:
				if (p == 0)
					te = Effects.None;
				break;
			case ULTEffects.TonePortamento:
				// 300 apparently stops sliding, which is totally weird
				if (p == 0)
					p = 1; // close enough?
				break;
			case ULTEffects.VolumeSlide:
				// blah, this sucks
				if (p.HasAnyBitSet(0xf0))
					p &= 0xf0;
				break;
			case ULTEffects.Panning:
				// mikmod does this wrong, resulting in values 0-225 instead of 0-255
				p = (byte)((p & 0xf) * 0x11);
				break;
			case ULTEffects.Volume: // volume
				p >>= 2;
				break;
			case ULTEffects.PatternBreak: // pattern break
				p = (byte)(10 * (p >> 4) + (p & 0xf));
				break;
			case ULTEffects.Extended: // special
				switch ((ULTExtendedEffects)(p >> 4))
				{
					case ULTExtendedEffects.PortamenoUp:
						te = Effects.PortamentoUp;
						p = (byte)(0xf0 | (p & 0xf));
						break;
					case ULTExtendedEffects.PortamentoDown:
						te = Effects.PortamentoDown;
						p = (byte)(0xf0 | (p & 0xf));
						break;
					case ULTExtendedEffects.SetPanning:
						te = Effects.Special;
						p = (byte)(0x60 | (p & 0xf));
						break;
					case ULTExtendedEffects.Retrigger:
						te = Effects.Retrigger;
						p &= 0xf;
						break;
					case ULTExtendedEffects.VolumeSlideUp:
						te = Effects.VolumeSlide;
						p = (byte)(((p & 0xf) << 4) | 0xf);
						break;
					case ULTExtendedEffects.VolumeSlideDown:
						te = Effects.VolumeSlide;
						p = (byte)(0xf0 | (p & 0xf));
						break;
					case ULTExtendedEffects.NoteCut:
					case ULTExtendedEffects.NoteDelay:
						te = Effects.Special;
						break;
				}
				break;
			case ULTEffects.Speed:
				if (p > 0x2f)
					te = Effects.Tempo;
				break;
		}

		ee = (byte)te;
		ep = p;
	}

	int ReadEvent(Stream stream, ref SongNote note, ref int lostEffects)
	{
		int repeat = 1;

		int b = stream.ReadByte();

		if (b == 0xfc)
		{
			repeat = stream.ReadByte();
			b = stream.ReadByte();
		}

		note.Note = (b > 0 && b < 61) ? (byte)(b + 36) : SpecialNotes.None;
		note.Instrument = (byte)stream.ReadByte();
		b = stream.ReadByte();
		note.VolumeEffectByte = (byte)(b & 0xf);
		note.EffectByte = (byte)(b >> 4);
		note.VolumeParameter = (byte)stream.ReadByte();
		note.Parameter = (byte)stream.ReadByte();

		TranslateEffect(ref note.VolumeEffectByte, ref note.VolumeParameter);
		TranslateEffect(ref note.EffectByte, ref note.Parameter);

		// sample offset -- this is even more special than digitrakker's
		if (note.VolumeEffectByte == (byte)Effects.Offset && note.Effect == Effects.Offset)
		{
			int off = ((note.VolumeParameter << 8) | note.Parameter) >> 6;
			note.VolumeEffect = VolumeEffects.None;
			note.Parameter = (byte)Math.Min(off, 0xff);
		}
		else if (note.VolumeEffectByte == (byte)Effects.Offset)
		{
			int off = note.VolumeParameter * 4;
			note.VolumeParameter = (byte)Math.Min(off, 0xff);
		}
		else if (note.Effect == Effects.Offset)
		{
			int off = note.Parameter * 4;
			note.Parameter = (byte)Math.Min(off, 0xff);
		}
		else if (note.VolumeEffectByte == note.EffectByte)
		{
			/* don't try to figure out how ultratracker does this, it's quite random */
			note.Effect = Effects.None;
		}

		if (note.Effect == Effects.Volume || (note.Effect == Effects.None && note.VolumeEffectByte != (byte)Effects.Volume))
			note.SwapEffects();

		// Do that dance.
		// Maybe I should quit rewriting this everywhere and make a generic version :P
		int n;

		for (n = 0; n < 4; n++)
		{
			if (XM.ConvertVolumeEffectOf(ref note, n >> 1))
			{
				n = 5;
				break;
			}

			note.SwapEffects();
		}

		if (n < 5)
		{
			if (((Effects)note.VolumeEffectByte).GetWeight() > note.Effect.GetWeight())
				note.SwapEffects();

			lostEffects++;
			//log_appendf(4, "Effect dropped: %c%02X < %c%02X", get_effect_char(note.Voleffect),
			//        note.Volparam, get_effect_char(note.Effect), note.Param);

			note.VolumeEffect = 0;
		}
		if (note.VolumeEffectByte == 0)
			note.VolumeParameter = 0;
		if (note.EffectByte == 0)
			note.Parameter = 0;

		return repeat;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	static readonly string[] VersionString = {"<1.4", "1.4", "1.5", "1.6"};

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		int lostEffects = 0;
		bool gxx = false;

		if (stream.ReadString(14) != "MAS_Utrack_V00")
			throw new NotSupportedException();

		int ver = stream.ReadByte();

		if (ver < '1' || ver > '4')
			throw new FormatException();

		ver -= '0';

		var song = new Song();

		song.Title = stream.ReadString(32);

		song.TrackerID = "Ultra Tracker " + VersionString[ver - 1];

		song.Flags |= SongFlags.CompatibleGXX | SongFlags.ITOldEffects;

		int numMessageLines = stream.ReadByte();

		song.Message = ReadLinedMessage(stream, numMessageLines * 32, 32);

		int numSamples = stream.ReadByte();

		for (int n = 1; n <= numSamples; n++)
		{
			var smp = song.EnsureSample(n);

			if (!ReadSample(stream, ver, out var usmp))
				throw new FormatException();

			smp.Name = usmp.Name;
			smp.FileName = usmp.FileName;

			if (usmp.SizeEnd <= usmp.SizeStart)
				continue;

			smp.Length = usmp.SizeEnd - usmp.SizeStart;
			smp.LoopStart = usmp.LoopStart;
			smp.LoopEnd = Math.Min(usmp.LoopEnd, smp.Length);
			smp.Volume = usmp.Volume; //mphack - should be 0-64 not 0-256
			smp.GlobalVolume = 64;

			/* mikmod does some weird integer math here, but it didn't really work for me */
			smp.C5Speed = usmp.Speed;
			if (usmp.FineTune != 0)
				smp.C5Speed = (int)(smp.C5Speed * Math.Pow(2, (usmp.FineTune / (12.0 * 32768))));

			if (usmp.Flags.HasFlag(ULTSampleFlags.Loop))
				smp.Flags |= SampleFlags.Loop;
			if (usmp.Flags.HasFlag(ULTSampleFlags.PingPongLoop))
				smp.Flags |= SampleFlags.PingPongLoop;
			if (usmp.Flags.HasFlag(ULTSampleFlags._16bit))
			{
				smp.Flags |= SampleFlags._16Bit;
				smp.LoopStart >>= 1;
				smp.LoopEnd >>= 1;
			}
		}

		// ult just so happens to use 255 for its end mark, so there's no need to fiddle with this
		byte[] orderListBytes = new byte[256];

		stream.ReadExactly(orderListBytes);

		foreach (var orderListEntry in orderListBytes)
			song.OrderList.Add(orderListEntry);

		int numChannels = stream.ReadByte() + 1;
		int numPatterns = stream.ReadByte() + 1;

		if (numChannels > 32 || numPatterns > Constants.MaxPatterns)
			throw new FormatException();

		if (ver >= 3)
		{
			for (int n = 0; n < numChannels; n++)
				song.Channels[n].Panning = ((stream.ReadByte() & 0xf) << 2) + 2;
		}
		else
		{
			for (int n = 0; n < numChannels; n++)
				song.Channels[n].Panning = n.HasBitSet(1) ? 48 : 16;
		}

		for (int n = numChannels; n < 64; n++)
		{
			song.Channels[n].Panning = 32;
			song.Channels[n].Flags = ChannelFlags.Mute;
		}
		//mphack - fix the pannings
		for (int n = 0; n < 64; n++)
			song.Channels[n].Panning *= 4;

		if (lflags.HasFlag(LoadFlags.NoSamples | LoadFlags.NoPatterns))
			return song;

		for (int pat = 0; pat < numPatterns; pat++)
			song.GetPattern(pat, create: true);

		for (int chn = 0; chn < numChannels; chn++)
		{
			SongNote evNote = SongNote.Empty;

			for (int pat = 0; pat < numPatterns; pat++)
			{
				int row = 0;

				while (row < 64)
				{
					ref var note = ref song.Patterns[pat].Rows[row][chn + 1];

					int repeat = ReadEvent(stream, ref evNote, ref lostEffects);

					if (evNote.Effect == Effects.TonePortamento
							|| evNote.VolumeEffect == VolumeEffects.TonePortamento)
						gxx |= true;

					if (repeat + row > 64)
						repeat = 64 - row;

					while (repeat-- > 0)
					{
						song.Patterns[pat].Rows[row][chn + 1] = evNote;
						row++;
					}
				}
			}
		}

		if (gxx)
			Log.Append(4, " Warning: Gxx effects may not be suitably imported");
		if (lostEffects > 0)
			Log.Append(4, " Warning: {0} effect{1} dropped", lostEffects, lostEffects == 1 ? "" : "s");

		if (!lflags.HasFlag(LoadFlags.NoSamples))
		{
			for (int n = 1; n <= numSamples; n++)
			{
				var smp = song.EnsureSample(n);

				SampleFileConverter.ReadSample(smp,
					SampleFormat.LittleEndian | SampleFormat.Mono | SampleFormat.PCMSigned | (smp.Flags.HasFlag(SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8),
					stream);
			}
		}

		return song;
	}
}
