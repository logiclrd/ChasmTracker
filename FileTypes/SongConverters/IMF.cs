using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.Converters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class IMF : SongFileConverter
{
	public override string Label => "IMF";
	public override string Description => "Imago Orpheus";
	public override string Extension => ".imf";

	public override int SortOrder => 16;

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			byte[] magic = new byte[4];

			stream.Position += 60;
			stream.ReadExactly(magic);

			if (magic.ToStringZ() != "IM10")
				return false;

			stream.Position -= 64;

			byte[] title = new byte[32];

			stream.ReadExactly(title);

			file.Description = Description;
			/*file.Extension = "imf";*/
			file.Title = title.ToStringZ();
			file.Type = FileSystem.FileTypes.ModuleIT;

			return true;
		}
		catch
		{
			return false;
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct IMFChannel
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
		public byte[] Name;         /* Channelname (ASCIIZ-String, max 11 chars) */
		public byte Chorus;         /* Default chorus */
		public byte Reverb;         /* Default reverb */
		public byte Panning;        /* Pan positions 00-FF */
		public byte Status;         /* Channel status: 0 = enabled, 1 = mute, 2 = disabled (ignore effects!) */
	}

	enum IMFModuleFlags : short
	{
		LinearSlides = 1,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct IMFHeader
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] Title;          /* Songname (ASCIIZ-String, max. 31 chars) */
		public short OrdNum;          /* Number of orders saved */
		public short PatNum;          /* Number of patterns saved */
		public short InsNum;          /* Number of instruments saved */
		public IMFModuleFlags Flags;           /* Module flags (&1 => linear) */
		long _unused1;
		public byte Tempo;            /* Default tempo (Axx, 1..255) */
		public byte BPM;              /* Default beats per minute (BPM) (Txx, 32..255) */
		public byte Master;           /* Default mastervolume (Vxx, 0..64) */
		public byte Amp;              /* Amplification factor (mixing volume, 4..127) */
		long _unused2;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public byte[] IM10;           /* 'IM10' */
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public IMFChannel[] Channels; /* Channel settings */
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public byte[] OrderList;      /* Order list (0xff = +++; blank out anything beyond ordnum) */
	};

	static IMFHeader ReadHeader(Stream fp)
	{
		var hdr = fp.ReadStructure<IMFHeader>();

		if (hdr.IM10.ToStringZ() != "IM10")
			throw new FormatException();

		return hdr;
	}

	enum EnvelopeType
	{
		Volume = 0,
		Panning = 1,
		Filter = 2,
	}

	[Flags]
	enum IMFEnvelopeFlags : byte
	{
		Enable = 1,
		SustainLoop = 2,
		Loop = 4,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct IMFEnvelope
	{
		public byte Points;         /* Number of envelope points */
		public byte Sustain;        /* Envelope sustain point */
		public byte LoopStart;      /* Envelope loop start point */
		public byte LoopEnd;        /* Envelope loop end point */
		public IMFEnvelopeFlags Flags;          /* Envelope flags */
		byte _unused1, _unused2, _unused3;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct IMFEnvelopeNode
	{
		public ushort Tick;
		public ushort Value;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct IMFInstrument
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] Name;          /* Inst. name (ASCIIZ-String, max. 31 chars) */
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 120)]
		public byte[] Map;           /* Multisample settings */
		long _unused;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public IMFEnvelopeNode[] VolumeEnvelopeNodes;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public IMFEnvelopeNode[] PanningEnvelopeNodes;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public IMFEnvelopeNode[] FilterEnvelopeNodes;
		public IMFEnvelope VolumeEnvelope;
		public IMFEnvelope PanningEnvelope;
		public IMFEnvelope FilterEnvelope;
		public short FadeOut;        /* Fadeout rate (0...0FFFH) */
		public short SampleCount;    /* Number of samples in instrument */
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public byte[] II10;          /* 'II10' */

		public IMFEnvelope GetEnvelope(EnvelopeType type, out IMFEnvelopeNode[] nodes)
		{
			switch (type)
			{
				case EnvelopeType.Volume:
					nodes = VolumeEnvelopeNodes;
					return VolumeEnvelope;
				case EnvelopeType.Panning:
					nodes = PanningEnvelopeNodes;
					return PanningEnvelope;
				case EnvelopeType.Filter:
					nodes = FilterEnvelopeNodes;
					return FilterEnvelope;
				default: throw new Exception();
			}
		}
	}

	static IMFInstrument ReadInstrument(Stream fp)
	{
		var inst = fp.ReadStructure<IMFInstrument>();

		if (inst.II10.ToStringZ() != "II10")
			throw new FormatException();

		return inst;
	}

	enum IMFSampleFlags : byte
	{
		Loop = 1,
		PingPongLoop = 2,
		_16Bit = 4,
		Panning = 8,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct IMFSample
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
		public byte[] Name;          /* Sample filename (12345678.ABC) */
		byte _unused1, _unused2, _unused3;
		public int Length;           /* Length */
		public int LoopStart;        /* Loop start */
		public int LoopEnd;          /* Loop end */
		public int C5Speed;          /* Samplerate */
		public byte Volume;          /* Default volume (0..64) */
		public byte Panning;         /* Default pan (00h = Left / 80h = Middle) */
		long _unused4;
		int _unused5;
		short _unused6;
		public IMFSampleFlags Flags; /* Sample flags */
		int _unused7;
		byte _unused8;
		public ushort EMS;           /* Reserved for internal usage */
		public uint DRAM;            /* Reserved for internal usage */
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public byte[] IS10;          /* 'IS10' or 'IW10' */
	};

	static IMFSample ReadSample(Stream fp)
	{
		var smpl = fp.ReadStructure<IMFSample>();

		if (smpl.IS10.ToStringZ() != "IS10")
			throw new FormatException();

		return smpl;
	}

	static Effects[] IMFEffectTranslation =
		new[]
		{
			Effects.None,
			Effects.Speed, // 0x01 1xx Set Tempo
			Effects.Tempo, // 0x02 2xx Set BPM
			Effects.TonePortamento, // 0x03 3xx Tone Portamento                  (*)
			Effects.TonePortamentoVolume, // 0x04 4xy Tone Portamento + Volume Slide   (*)
			Effects.Vibrato, // 0x05 5xy Vibrato                          (*)
			Effects.VibratoVolume, // 0x06 6xy Vibrato + Volume Slide           (*)
			Effects.FineVibrato, // 0x07 7xy Fine Vibrato                     (*)
			Effects.Tremolo, // 0x08 8xy Tremolo                          (*)
			Effects.Arpeggio, // 0x09 9xy Arpeggio                         (*)
			Effects.Panning, // 0x0A Axx Set Pan Position
			Effects.PanningSlide, // 0x0B Bxy Pan Slide                        (*)
			Effects.Volume, // 0x0C Cxx Set Volume
			Effects.VolumeSlide, // 0x0D Dxy Volume Slide                     (*)
			Effects.VolumeSlide, // 0x0E Exy Fine Volume Slide                (*)
			Effects.Special, // 0x0F Fxx Set Finetune
			Effects.NoteSlideUp, // 0x10 Gxy Note Slide Up                    (*)
			Effects.NoteSlideDown, // 0x11 Hxy Note Slide Down                  (*)
			Effects.PortamentoUp, // 0x12 Ixx Slide Up                         (*)
			Effects.PortamentoDown, // 0x13 Jxx Slide Down                       (*)
			Effects.PortamentoUp, // 0x14 Kxx Fine Slide Up                    (*)
			Effects.PortamentoDown, // 0x15 Lxx Fine Slide Down                  (*)
			Effects.MIDI, // 0x16 Mxx Set Filter Cutoff - XXX
			Effects.None, // 0x17 Nxy Filter Slide + Resonance - XXX
			Effects.Offset, // 0x18 Oxx Set Sample Offset                (*)
			Effects.None, // 0x19 Pxx Set Fine Sample Offset - XXX
			Effects.KeyOff, // 0x1A Qxx Key Off
			Effects.Retrigger, // 0x1B Rxy Retrig                           (*)
			Effects.Tremor, // 0x1C Sxy Tremor                           (*)
			Effects.PositionJump, // 0x1D Txx Position Jump
			Effects.PatternBreak, // 0x1E Uxx Pattern Break
			Effects.GlobalVolume, // 0x1F Vxx Set Mastervolume
			Effects.GlobalVolumeSlide, // 0x20 Wxy Mastervolume Slide               (*)
			Effects.Special, // 0x21 Xxx Extended Effect
			//      X1x Set Filter
			//      X3x Glissando
			//      X5x Vibrato Waveform
			//      X8x Tremolo Waveform
			//      XAx Pattern Loop
			//      XBx Pattern Delay
			//      XCx Note Cut
			//      XDx Note Delay
			//      XEx Ignore Envelope
			//      XFx Invert Loop
			Effects.None, // 0x22 Yxx Chorus - XXX
			Effects.None, // 0x23 Zxx Reverb - XXX
		};

	static void ImportIMFEffect(ref SongNote note)
	{
		// fix some of them
		switch (note.EffectByte)
		{
			case 0xe: // fine volslide
								// hackaround to get almost-right behavior for fine slides (i think!)
				if (note.Parameter == 0)
					/* nothing */ { }
				else if (note.Parameter == 0xf0)
					note.Parameter = 0xef;
				else if (note.Parameter == 0x0f)
					note.Parameter = 0xfe;
				else if (note.Parameter.HasAnyBitSet(0xf0))
					note.Parameter |= 0xf;
				else
					note.Parameter |= 0xf0;
				break;
			case 0xf: // set finetune
				// we don't implement this, but let's at least import the value
				note.Parameter = (byte)(0x20 | Math.Min(note.Parameter >> 4, 0xf));
				break;
			case 0x14: // fine slide up
			case 0x15: // fine slide down
				// this is about as close as we can do...
				if (note.Parameter >> 4 != 0)
					note.Parameter = (byte)(0xf0 | Math.Min(note.Parameter >> 4, 0xf));
				else
					note.Parameter |= 0xe0;
				break;
			case 0x16: // filter
				note.Parameter = (byte)((255 - note.Parameter) / 2); // TODO: cutoff range in IMF is 125...8000 Hz
				break;
			case 0x1f: // set global volume
				note.Parameter = (byte)Math.Min(note.Parameter << 1, 0xff);
				break;
			case 0x21:
			{
				int n = 0;

				switch (note.Parameter >> 4)
				{
					case 0:
						/* undefined, but since S0x does nothing in IT anyway, we won't care.
						this is here to allow S00 to pick up the previous value (assuming IMF
						even does that -- I haven't actually tried it) */
						break;
					default: // undefined
					case 0x1: // set filter
					case 0xf: // invert loop
						note.Effect = Effects.None;
						break;
					case 0x3: // glissando
						n = 0x20;
						break;
					case 0x5: // vibrato waveform
						n = 0x30;
						break;
					case 0x8: // tremolo waveform
						n = 0x40;
						break;
					case 0xa: // pattern loop
						n = 0xb0;
						break;
					case 0xb: // pattern delay
						n = 0xe0;
						break;
					case 0xc: // note cut
					case 0xd: // note delay
										// no change
						break;
					case 0xe: // ignore envelope
						switch (note.Parameter & 0x0F)
						{
							/* predicament: we can only disable one envelope at a time.
							volume is probably most noticeable, so let's go with that. */
							case 0: note.Parameter = 0x77; break;
							// Volume
							case 1: note.Parameter = 0x77; break;
							// Panning
							case 2: note.Parameter = 0x79; break;
							// Filter
							case 3: note.Parameter = 0x7B; break;
						}
						break;
					case 0x18: // sample offset
						// O00 doesn't pick up the previous value
						if (note.Parameter == 0)
							note.Effect = Effects.None;
						break;
				}
				if (n != 0)
					note.Parameter = (byte)(n | (note.Parameter & 0xf));
				break;
			}
		}

		note.Effect = (note.EffectByte < 0x24) ? IMFEffectTranslation[note.EffectByte] : Effects.None;
		if (note.Effect == Effects.Volume && note.VolumeEffect == VolumeEffects.None) {
			note.VolumeEffect = VolumeEffects.Volume;
			note.VolumeParameter = note.Parameter;
			note.Effect = Effects.None;
			note.Parameter = 0;
		}
	}

	/* return: number of lost effects */
	static int LoadPattern(Song song, int pat, uint ignoreChannels, Stream fp)
	{
		int lostEffects = 0;

		//long startPos = fp.Position;

		int length = fp.ReadStructure<short>();
		int numRows = fp.ReadStructure<short>();

		song.Patterns[pat] = new Pattern(numRows);

		int row = 0;

		PatternRow junkRow = new PatternRow();

		while (row < numRows)
		{
			int mask = fp.ReadByte();

			if (mask < 0)
				throw new EndOfStreamException();

			if (mask == 0)
			{
				row++;
				continue;
			}

			int channel = mask & 0x1f;

			PatternRow rowData;

			if (ignoreChannels.HasBitSet(1 << channel))
			{
				/* should do this better, i.e. not go through the whole process of deciding
				what to do with the effects since they're just being thrown out */
				//Console.WriteLine("disabled channel {0} contains data", channel + 1);
				rowData = junkRow;
			}
			else
			{
				rowData = song.Patterns[pat].Rows[row];
			}

			ref var note = ref rowData[channel];

			if (mask.HasBitSet(0x20))
			{
				/* read note/instrument */
				byte noteByte = (byte)fp.ReadByte();
				note.Note = noteByte;
				note.Instrument = (byte)fp.ReadByte();
				if (note.Note == 160)
					note.Note = SpecialNotes.NoteOff; /* ??? */
				else if (note.Note == 255)
					note.Note = SpecialNotes.None; /* ??? */
				else
				{
					note.Note = (byte)((note.Note >> 4) * 12 + (note.Note & 0xf) + 12 + 1);
					if (!SongNote.IsNote(note.Note))
					{
						//Console.WriteLine("{0}.{1}.{2}: funny note 0x{3:X2}\n",
						//      pat, row, channel, noteByte);
						note.Note = SpecialNotes.None;;
					}
				}
			}
			if (mask.HasBitSet(0xc0))
			{
				/* read both effects and figure out what to do with them */
				byte e1c = (byte)fp.ReadByte();
				byte e1d = (byte)fp.ReadByte();
				byte e2c = (byte)fp.ReadByte();
				byte e2d = (byte)fp.ReadByte();

				if (e1c == 0xc)
				{
					note.VolumeParameter = Math.Min(e1d, (byte)0x40);
					note.VolumeEffect = VolumeEffects.Volume;
					note.EffectByte = e2c;
					note.Parameter = e2d;
				}
				else if (e2c == 0xc)
				{
					note.VolumeParameter = Math.Min(e2d, (byte)0x40);
					note.VolumeEffect = VolumeEffects.Volume;
					note.EffectByte = e1c;
					note.Parameter = e1d;
				}
				else if (e1c == 0xa)
				{
					note.VolumeParameter = (byte)(e1d * 64 / 255);
					note.VolumeEffect = VolumeEffects.Panning;
					note.EffectByte = e2c;
					note.Parameter = e2d;
				}
				else if (e2c == 0xa)
				{
					note.VolumeParameter = (byte)(e2d * 64 / 255);
					note.VolumeEffect = VolumeEffects.Panning;
					note.EffectByte = e1c;
					note.Parameter = e1d;
				}
				else
				{
					/* check if one of the effects is a 'global' effect
					-- if so, put it in some unused channel instead.
					otherwise pick the most important effect. */
					lostEffects++;
					note.EffectByte = e2c;
					note.Parameter = e2d;
				}
			}
			else if (mask.HasBitSet(0xc0))
			{
				/* there's one effect, just stick it in the effect column */
				note.EffectByte = (byte)fp.ReadByte();
				note.Parameter = (byte)fp.ReadByte();
			}

			if (note.EffectByte != 0)
				ImportIMFEffect(ref note);
		}

		return lostEffects;
	}

	class EnvelopeFlagValues
	{
		public InstrumentFlags Enable;
		public InstrumentFlags SustainLoop;
		public InstrumentFlags Loop;

		public EnvelopeFlagValues(InstrumentFlags enable, InstrumentFlags sustainLoop, InstrumentFlags loop)
		{
			Enable = enable;
			SustainLoop = sustainLoop;
			Loop = loop;
		}
	}

	static readonly Dictionary<EnvelopeType, EnvelopeFlagValues> EnvelopeFlags =
		new Dictionary<EnvelopeType, EnvelopeFlagValues>()
		{
			{ EnvelopeType.Volume, new EnvelopeFlagValues(InstrumentFlags.VolumeEnvelope, InstrumentFlags.VolumeEnvelopeSustain, InstrumentFlags.VolumeEnvelopeLoop) },
			{ EnvelopeType.Panning, new EnvelopeFlagValues(InstrumentFlags.PanningEnvelope, InstrumentFlags.PanningEnvelopeSustain, InstrumentFlags.PanningEnvelopeLoop) },
			{ EnvelopeType.Filter, new EnvelopeFlagValues(InstrumentFlags.PitchEnvelope | InstrumentFlags.Filter, InstrumentFlags.PitchEnvelopeSustain, InstrumentFlags.PitchEnvelopeLoop) },
		};

	static Envelope LoadEnvelope(SongInstrument ins, ref IMFInstrument imfIns, EnvelopeType e)
	{
		int min = 0; // minimum tick value for next node
		int shift = (e == EnvelopeType.Volume ? 0 : 2);
		int mirror = (e == EnvelopeType.Filter) ? 0xff : 0x00;

		var imfEnv = imfIns.GetEnvelope(e, out var nodes);

		int nodeCount = imfEnv.Points.Clamp(2, 25);

		var env = new Envelope();

		env.LoopStart = imfEnv.LoopStart;
		env.LoopEnd = imfEnv.LoopEnd;
		env.SustainStart = env.SustainEnd = imfEnv.Sustain;

		for (int n = 0; n < nodeCount; n++)
		{
			int t = nodes[n].Tick;
			int v = ((nodes[n].Value & 0xff) ^ mirror) >> shift;

			env.Nodes.Add(new EnvelopeNode(
				Math.Max(min, t),
				Math.Min(v, 64)));

			min = t + 1;
		}

		// this would be less retarded if the envelopes all had their own flags...
		var envFlags = EnvelopeFlags[e];

		if (imfEnv.Flags.HasFlag(IMFEnvelopeFlags.Enable))
			ins.Flags |= envFlags.Enable;
		if (imfEnv.Flags.HasFlag(IMFEnvelopeFlags.SustainLoop))
			ins.Flags |= envFlags.SustainLoop;
		if (imfEnv.Flags.HasFlag(IMFEnvelopeFlags.Loop))
			ins.Flags |= envFlags.Loop;

		return env;
	}

	public override Song LoadSong(Stream stream, LoadFlags flags)
	{
		int firstSample = 1; // first sample for the current instrument
		uint ignoreChannels = 0; /* bit set for each channel that's completely disabled */
		int lostEffects = 0;

		var hdr = ReadHeader(stream);

		if (hdr.OrdNum > Constants.MaxOrders || hdr.PatNum > Constants.MaxPathLength || hdr.InsNum > Constants.MaxInstruments)
			throw new FormatException();

		var song = new Song();

		song.Title = hdr.Title.ToStringZ();
		song.TrackerID = Description;

		if (hdr.Flags.HasFlag(IMFModuleFlags.LinearSlides))
			song.Flags |= SongFlags.LinearSlides;
		song.Flags |= SongFlags.InstrumentMode;
		song.InitialSpeed = hdr.Tempo;
		song.InitialTempo = hdr.BPM;
		song.InitialGlobalVolume = 2 * hdr.Master;
		song.MixingVolume = hdr.Amp;

		for (int n = 0; n < 32; n++)
		{
			song.Channels[n] = new SongChannel();
			song.Channels[n].Panning = hdr.Channels[n].Panning * 64 / 255;
			song.Channels[n].Panning *= 4; //mphack
			/* TODO: reverb/chorus??? */
			switch (hdr.Channels[n].Status)
			{
				case 0: /* enabled; don't worry about it */
					break;
				case 1: /* mute */
					song.Channels[n].Flags |= ChannelFlags.Mute;
					break;
				case 2: /* disabled */
					song.Channels[n].Flags |= ChannelFlags.Mute;
					ignoreChannels |= (1u << n);
					break;
				default: /* uhhhh.... freak out */
					//Console.Error.WriteLine("imf: channel {0} has unknown status {1}", n, hdr.Channels[n].Status);
					throw new FormatException();
			}
		}

		for (int n = 32; n < Constants.MaxChannels; n++)
			song.Channels[n].Flags |= ChannelFlags.Mute;

		/* From mikmod: work around an Orpheus bug */
		if (hdr.Channels[0].Status == 0)
		{
			int n;

			for (n = 1; n < 16; n++)
				if (hdr.Channels[n].Status != 1)
					break;

			if (n == 16)
				for (n = 1; n < 16; n++)
					song.Channels[n].Flags &= ~ChannelFlags.Mute;
		}

		for (int n = 0; n < hdr.OrdNum; n++)
			song.OrderList.Add((hdr.OrderList[n] == 0xff) ? SpecialOrders.Skip : hdr.OrderList[n]);

		for (int n = 0; n < hdr.PatNum; n++)
			lostEffects += LoadPattern(song, n, ignoreChannels, stream);

		if (lostEffects > 0)
			Log.Append(4, " Warning: {0} effect{1} dropped", lostEffects, lostEffects == 1 ? "" : "s");

		for (int n = 0; n < hdr.InsNum; n++)
		{
			// read the ins header
			var imfIns = ReadInstrument(stream);

			var ins = song.Instruments[n + 1] = new SongInstrument(song);

			ins.Name = imfIns.Name.ToStringZ();

			if (imfIns.SampleCount > 0)
			{
				for (int s = 12; s < 120; s++) {
					ins.NoteMap[s] = (byte)(s + 1);
					ins.SampleMap[s] = (byte)(firstSample + imfIns.Map[s - 12]);
				}
			}

			/* Fadeout:
			IT1 - 64
			IT2 - 256
			FT2 - 4095
			IMF - 4095
			MPT - god knows what, all the loaders are inconsistent
			Schism - 256 presented; 8192? internal

			IMF and XM have the same range and modplug's XM loader doesn't do any bit shifting with it,
			so I'll do the same here for now. I suppose I should get this nonsense straightened
			out at some point, though. */
			ins.FadeOut = imfIns.FadeOut;
			ins.GlobalVolume = 128;

			ins.VolumeEnvelope = LoadEnvelope(ins, ref imfIns, EnvelopeType.Volume);
			ins.PanningEnvelope = LoadEnvelope(ins, ref imfIns, EnvelopeType.Panning);
			ins.PitchEnvelope = LoadEnvelope(ins, ref imfIns, EnvelopeType.Filter);

			/* I'm not sure if XM's envelope hacks apply here or not, but Orpheus *does* at least stop
			samples upon note-off when no envelope is active. Whether or not this depends on the fadeout
			value, I don't know, and since the fadeout doesn't even seem to be implemented in the gui,
			I might never find out. :P */
			if (!ins.Flags.HasFlag(InstrumentFlags.VolumeEnvelope))
			{
				ins.VolumeEnvelope.Nodes.Clear();
				ins.VolumeEnvelope.Nodes.Add((0, 64));
				ins.VolumeEnvelope.Nodes.Add((1, 0));
				ins.VolumeEnvelope.SustainStart = ins.VolumeEnvelope.SustainEnd = 0;
				ins.Flags |= InstrumentFlags.VolumeEnvelope | InstrumentFlags.VolumeEnvelopeSustain;
			}

			for (int s = 0; s < imfIns.SampleCount; s++)
			{
				var imfSmp = ReadSample(stream);

				SampleFormat sFlags = SampleFormat.LittleEndian | SampleFormat.Mono | SampleFormat.PCMSigned;

				var sample = song.EnsureSample(s + 1);

				sample.FileName = imfSmp.Name.ToStringZ();
				sample.Name = sample.FileName;
				sample.Length = imfSmp.Length;
				sample.LoopStart = imfSmp.LoopStart;
				sample.LoopEnd = imfSmp.LoopEnd;
				sample.C5Speed = imfSmp.C5Speed;
				sample.Volume = imfSmp.Volume * 4; //mphack
				sample.Panning = imfSmp.Panning; //mphack (IT uses 0-64, IMF uses the full 0-255)
				if (imfSmp.Flags.HasFlag(IMFSampleFlags.Loop))
					sample.Flags |= SampleFlags.Loop;
				if (imfSmp.Flags.HasFlag(IMFSampleFlags.PingPongLoop))
					sample.Flags |= SampleFlags.PingPongLoop;
				if (imfSmp.Flags.HasFlag(IMFSampleFlags._16Bit))
				{
					sFlags |= SampleFormat._16;
					sample.Length >>= 1;
					sample.LoopStart >>= 1;
					sample.LoopEnd >>= 1;
				}
				else
					sFlags |= SampleFormat._8;

				if (imfSmp.Flags.HasFlag(IMFSampleFlags.Panning))
					sample.Flags |= SampleFlags.Panning;

				if (!flags.HasFlag(LoadFlags.NoSamples))
					SampleFileConverter.ReadSample(sample, sFlags, stream);
				else
					stream.Position += imfSmp.Length * (sFlags.HasFlag(SampleFormat._16) ? 2 : 1);
			}

			firstSample += imfIns.SampleCount;
		}

		// Fix the MIDI settings, because IMF files might include Zxx effects
		song.MIDIConfig.SFx[0] = "F0F000z";
		song.Flags |= SongFlags.EmbedMIDIConfig;

		return song;
	}
}
