using System;
using System.ComponentModel;
using System.IO;
using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using Microsoft.Maui.Authentication;

namespace ChasmTracker.FileTypes.SongConverters;

public class PSM16 : PSM
{
	public override string Label => "PSM16";
	public override string Description => "Epic MegaGames MASI (Old Version)";
	public override int SortOrder => 12;

	/* ------------------------------------------------------------------------ */
	/* here comes PSM16...
	*
	* ...you know, I wrote this loader hoping that there would actually be some
	* value in the songs that I'd actually be able to load, but the only one
	* I really like is "snooker champ". also there's that one that uses samples
	* from "beyond music", I guess. besides that there's not a whole lot of
	* value to this format. :p */

	bool VerifyHeader(Stream stream)
	{
		/* This is probably way more validation than necessary, but oh well :) */
		if (stream.ReadString(4) != "PSM\xFE")
			return false;

		stream.Position += 59;

		/* "The final byte, 60, MUST have a ^Z in it." */
		if (stream.ReadByte() != 0x1A)
			return false;

		int b = stream.ReadByte();
		if (b == EOF || b.HasAnyBitSet(0x03))
			return false;

		b = stream.ReadByte();
		if (b != 0x10 && b != 0x01 /* from OpenMPT: this is sometimes 0x01 !!! */)
			return false;

		b = stream.ReadByte();
		if (b != 0 /* "255ch" format not documented, and I don't care */)
			return false;

		/* skip speed */
		stream.Position++;

		/* skip """BPM""", actually tempo, and yes, they are very different */
		b = stream.ReadByte();
		if (!(b >= 32 && b <= 255))
			return false;

		/* skip master volume */
		stream.Position++;

		for (int i = 0; i < 4; i++)
		{
			/* next four values are all 16-bit values,
			* and all have the exact same limits */
			short w = stream.ReadStructure<short>();

			if (!(w >= 1 && w <= 255))
				return false;
		}

		/* "Number of Channels to Play" and "Number of Channels to Process"...
		* docs say that this varies from 1 to 32, so I suppose the 255
		* channel format is just completely meaningless. */
		for (int i = 0; i < 2; i++)
		{
			short w = stream.ReadStructure<short>();

			if (!(w >= 1 && w <= 32))
				return false;
		}

		return true;
	}

	public override bool FillExtendedData(Stream fp, FileReference file)
	{
		if (!VerifyHeader(fp))
			return false;

		fp.Position = 4;

		byte[] titleBytes = new byte[59];

		fp.ReadExactly(titleBytes);

		/* discover never-before-seen secrets of the damned */
		for (int i = 0; i < 59; i++)
			if (titleBytes[i] == 0)
				titleBytes[i] = 32;

		/* trim it up! */
		file.Title = titleBytes.ToStringZ().TrimEnd();
		file.Description = "Epic MegaGames MASI";
		/*file.Extension = ".psm";*/
		file.Type = FileSystem.FileTypes.ModuleS3M;

		return true;
	}

	/* if this function succeeds, the file position is at the offset.
	* otherwise, it is unknown, and needs manual readjustment :) */
	bool CheckParapointer(Stream fp, int paraptr, string magic)
	{
		try
		{
			if (paraptr < 4)
				return false;

			fp.Position = paraptr - 4;

			if (fp.ReadString(4) != magic)
				return false;

			return true;
		}
		catch
		{
			return false;
		}
	}

	[Flags]
	enum PSM16SampleFlags : byte
	{
		/* sample data flags */
		Synthesized = 0x01, /* unused, AFAIK */
		_16Bit = 0x04,
		Unsigned = 0x08,
		Delta = 0x10,
		BidiLoop = 0x20,

		/* loop flags */
		Loop = 0x80,

		Sample = 0xFF & ~Loop,
	}

	[Flags]
	enum PSM16PatternCommand : byte
	{
		ChannelMask = 0x1F,
		Effect = 0x20,
		Volume = 0x40,
		NoteAndInstrument = 0x80,
	}

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		if (!VerifyHeader(stream))
			throw new NotSupportedException();

		stream.Position = 4;

		var song = new Song();

		/* something fun I discovered: by doing it this way, we actually
		* uncover mysterious hidden bits of the title (one of the songs
		* in silverball literally is titled "\0eyond_music", lol) */

		song.Title = stream.ReadString(60, nullTerminated: false).Replace('\0', ' ');

		/* skip stuff that has already been verified */
		stream.Position += 3;

		int speed = stream.ReadByte();
		if (speed == EOF)
			throw new NotSupportedException();

		int tempo = stream.ReadByte();
		if (tempo == EOF)
			throw new NotSupportedException();

		int masterVolume = stream.ReadByte();
		if (masterVolume == EOF)
			throw new NotSupportedException();

		song.InitialSpeed = speed;
		song.InitialTempo = tempo;

		/* from openmpt:
		* Most of the time, the master volume value makes sense...
		* ...Just not when it's 255. */
		song.MixingVolume = (masterVolume == 255) ? 48 : masterVolume;

		/* skip "song length" ??? */
		stream.Position += 2;

		int numOrders = stream.ReadStructure<short>();
		int numPatterns = stream.ReadStructure<short>();
		int numSamples = stream.ReadStructure<short>();
		int numChannelsPlay = stream.ReadStructure<short>();
		int numChannelsProcess = stream.ReadStructure<short>();

		int numChannels = numChannelsProcess.Clamp(numChannelsPlay, Constants.MaxChannels);

		numSamples = Math.Min(numSamples, Constants.MaxSamples);

		int offsetOrders = stream.ReadStructure<int>();
		int offsetChannelPans = stream.ReadStructure<int>();
		int offsetPatterns = stream.ReadStructure<int>();
		int offsetSamples = stream.ReadStructure<int>();
		int offsetMessage = stream.ReadStructure<int>();

		/* mcmeat deluxe special now available at mickey d's */

		if (CheckParapointer(stream, offsetOrders, "PORD"))
		{
			byte[] orderListBytes = new byte[Math.Min(numOrders, Constants.MaxOrders - 1)];

			stream.ReadExactly(orderListBytes);

			foreach (var order in orderListBytes)
				song.OrderList.Add(order);

			/* insert last order ... */
			song.OrderList.Add(SpecialOrders.Last);
		}

		if (CheckParapointer(stream, offsetChannelPans, "PPAN"))
		{
			byte[] channelPans = new byte[numChannels];

			stream.ReadExactly(channelPans);

			for (int n = 0; n < numChannels; n++)
				/* panning is reversed in PSM16 (15 is full left, 0 is full right) */
				song.Channels[n].Panning = ((15 - (channelPans[n] & 15)) * 256 + 8) / 15;
		}

		/* mute any extra channels */
		for (int n = numChannels; n < Constants.MaxChannels; n++)
			song.Channels[n].Flags |= ChannelFlags.Mute;

		if (CheckParapointer(stream, offsetSamples, "PSAH"))
		{
			for (int i = 0; i < numSamples; i++)
			{
				stream.Position = offsetSamples + i * 64;

				string fileName = stream.ReadString(13);
				string description = stream.ReadString(24);
				int offsetSampleData = stream.ReadStructure<int>();

				stream.Position += 4; /* "Memory Location" */

				int sampleNumber = stream.ReadStructure<short>();

				if (sampleNumber >= Constants.MaxSamples)
					continue; /* uhh? */

				/* finally... */
				var smp = song.EnsureSample(sampleNumber);

				smp.FileName = fileName;
				smp.Name = description;

				int b = stream.ReadByte();
				if (b == EOF)
					continue;

				var psmFlags = (PSM16SampleFlags)b;

				/* convert bit flags to internal bit flags */
				var flags = SampleFormat.Mono | SampleFormat.LittleEndian | (psmFlags.HasAllFlags(PSM16SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8);

				if (psmFlags.HasAllFlags(PSM16SampleFlags.Unsigned))
					flags |= SampleFormat.PCMUnsigned;
				else if (psmFlags.HasAllFlags(PSM16SampleFlags.Delta) || !psmFlags.HasAllFlags(PSM16SampleFlags.Sample))
					flags |= SampleFormat.PCMDeltaEncoded;
				else
					flags |= SampleFormat.PCMSigned;

				if (psmFlags.HasAllFlags(PSM16SampleFlags.BidiLoop))
					smp.Flags |= SampleFlags.PingPongLoop;

				if (psmFlags.HasAllFlags(PSM16SampleFlags.Loop))
					smp.Flags |= SampleFlags.Loop;

				smp.Length = stream.ReadStructure<int>();

				if (psmFlags.HasAllFlags(PSM16SampleFlags._16Bit))
					smp.Length >>= 1;

				smp.LoopStart = stream.ReadStructure<int>();
				smp.LoopEnd = stream.ReadStructure<int>();

				int finetune = stream.ReadByte();
				if (finetune == EOF)
					continue;

				int volume = stream.ReadByte();
				if (volume == EOF)
					continue;

				smp.Volume = volume * 4; //mphack

				short c2Freq = stream.ReadStructure<short>();

				/* voodoo magic effortlessly copied from openmpt  :) */
				smp.C5Speed = (int)Math.Round(c2Freq * Math.Pow(2.0, ((finetune ^ 0x08) - 0x78) / (12.0 * 16.0)));

				if (!lflags.HasAllFlags(LoadFlags.NoSamples))
				{
					stream.Position = offsetSampleData;
					SampleFileConverter.ReadSample(smp, flags, stream);
				}
			}
		}

		if (!lflags.HasAllFlags(LoadFlags.NoPatterns) && CheckParapointer(stream, offsetPatterns, "PPAT"))
		{
			/* ok, let's do this */
			for (int i = 0; i < numPatterns; i++)
			{
				long start = stream.Position;

				int row = 0;

				short size = stream.ReadStructure<short>();

				if (size < 4)
					continue; /* WTF */

				int numRows = stream.ReadByte();
				int numChans = stream.ReadByte();

				var pattern = song.GetPattern(i, create: true, rowsInNewPattern: numRows)!;

				while (stream.Position <= start + size && row < numRows)
				{
					int commandByte = stream.ReadByte();
					int chan;

					if (commandByte == 0 || commandByte == EOF)
					{
						row++;
						continue;
					}

					var command = (PSM16PatternCommand)commandByte;

					chan = Math.Min((int)(command & PSM16PatternCommand.ChannelMask), numChans);

					ref var n = ref pattern.Rows[row][chan];

					if (command.HasAllFlags(PSM16PatternCommand.NoteAndInstrument))
					{
						int note = stream.ReadByte();
						if (note == EOF) break;

						int instr = stream.ReadByte();
						if (instr == EOF) break;

						n.Note = (byte)(note + (3 * 12)); /* 3 octaves up to adjust for c2freq */
						n.Instrument = (byte)instr;
					}

					if (command.HasAllFlags(PSM16PatternCommand.Volume))
					{
						int volume = stream.ReadByte();
						if (volume == EOF) break;

						n.VolumeEffect = VolumeEffects.Volume;
						n.VolumeParameter = (byte)Math.Min(volume, 64);
					}

					if (command.HasAllFlags(PSM16PatternCommand.Effect))
					{
						int effect = stream.ReadByte();
						if (effect == EOF) break;

						int param = stream.ReadByte();
						if (param == EOF) break;

						switch (effect)
						{
							/* Volume Commands */
							case 0x01: /* Fine Volume Slide Up */
								n.Effect = Effects.VolumeSlide;
								n.Parameter = (byte)((param << 4) | 0x0F);
								break;
							case 0x02: /* Volume Slide Up */
								n.Effect = Effects.VolumeSlide;
								n.Parameter = (byte)((param << 4) & 0xF0);
								break;
							case 0x03: /* Fine Volume Slide Down */
								n.Effect = Effects.VolumeSlide;
								n.Parameter = (byte)(param | 0xF0);
								break;
							case 0x04: /* Volume Slide Down */
								n.Effect = Effects.VolumeSlide;
								n.Parameter = (byte)(param & 0x0F);
								break;

							/* Portamento Commands */
							case 0x0A: /* Fine Portamento Up */
								n.Effect = Effects.PortamentoUp;
								n.Parameter = (byte)(param | 0xF0);
								break;
							case 0x0B: /* Portamento Down */
								n.Effect = Effects.PortamentoUp;
								n.Parameter = (byte)param;
								break;
							case 0x0C: /* Fine Portamento Down */
								n.Effect = Effects.PortamentoDown;
								n.Parameter = (byte)(param | 0xF0);
								break;
							case 0x0D: /* Portamento Down */
								n.Effect = Effects.PortamentoDown;
								n.Parameter = (byte)param;
								break;
							case 0x0E: /* Tone Portamento */
								n.Effect = Effects.TonePortamento;
								n.Parameter = (byte)param;
								break;
							case 0x0F: /* Set Glissando Control */
								n.Effect = Effects.Special;
								n.Parameter = (byte)(0x10 | (param & 0x0F));
								break;
							case 0x10: /* Tone Port+Vol Slide Up */
								n.Effect = Effects.TonePortamentoVolume;
								n.Parameter = (byte)(param << 4);
								break;
							case 0x11: /* Tone Port+Vol Slide Down */
								n.Effect = Effects.TonePortamentoVolume;
								n.Parameter = (byte)(param & 0x0F);
								break;

							/* Vibrato Commands */
							case 0x14: /* Vibrato */
								n.Effect = Effects.Vibrato;
								n.Parameter = (byte)param;
								break;
							case 0x15: /* Set Vibrato Waveform */
								n.Effect = Effects.Special;
								n.Parameter = (byte)(0x30 | (param & 0x0F));
								break;
							case 0x16: /* Vibrato+Vol Slide Up */
								n.Effect = Effects.VibratoVolume;
								n.Parameter = (byte)(param << 4);
								break;
							case 0x17: /* Vibrato+Vol Slide Down */
								n.Effect = Effects.VibratoVolume;
								n.Parameter = (byte)(param & 0x0F);
								break;

							/* Tremolo Commands */
							case 0x1E: /* Tremolo */
								n.Effect = Effects.Tremolo;
								n.Parameter = (byte)param;
								break;
							case 0x1F: /* Set Tremolo Control */
								n.Effect = Effects.Special;
								n.Parameter = (byte)(0x40 | (param & 0x0F));
								break;

							/* Sample Commands */
							case 0x28: /* Sample Offset */
								/* 3-byte offset, but we can only import the middle one */
								n.Effect = Effects.Offset;
								n.Parameter = (byte)stream.ReadByte();
								stream.Position++;
								break;
							case 0x29: /* Retrig Note */
								n.Effect = Effects.Retrigger;
								n.Parameter = (byte)(param & 0x0F);
								break;
							case 0x2A: /* Note Cut */
								n.Effect = Effects.Special;
								n.Parameter = (byte)0xC0;
								break;
							case 0x2B: /* Note Delay */
								n.Effect = Effects.Special;
								n.Parameter = (byte)(0xD0 | (param & 0x0F));
								break;

							/* Pos. Change Commands */
							case 0x32: /* Position Jump */
								n.Effect = Effects.PositionJump;
								n.Parameter = (byte)param;
								break;
							case 0x33: /* Pattern Break */
								n.Effect = Effects.PatternBreak;
								n.Parameter = (byte)param;
								break;
							case 0x34: /* Jump Loop */
								n.Effect = Effects.Special;
								n.Parameter = (byte)(0xB0 | (param & 0x0F));
								break;
							case 0x35: /* Pattern Delay */
								n.Effect = Effects.Special;
								n.Parameter = (byte)(0xE0 | (param & 0x0F));
								break;

							/* Speed Change Cmds */
							case 0x3C: /* Set Regular Speed */
								n.Effect = Effects.Speed;
								n.Parameter = (byte)param;
								break;
							case 0x3D: /* Set BPM (Tempo) */
								n.Effect = Effects.Tempo;
								n.Parameter = (byte)param;
								break;

							/* Misc. Commands */
							case 0x46: /* Arpeggio */
								n.Effect = Effects.Arpeggio;
								n.Parameter = (byte)param;
								break;
							case 0x47: /* Set Finetune */
								n.Effect = Effects.Special;
								n.Parameter = (byte)((0x20 | (param & 0x0F)));
								break;
							case 0x48: /* Set Balance */
								/* openmpt handles this as panning, so that's what
								* I'm doing here as well. */
								n.Effect = Effects.Special;
								n.Parameter = (byte)((0x80 | (param & 0x0F)));
								break;

							default:
								n.Effect = Effects.None;
								n.Parameter = (byte)param;
								break;
						}
					}
				}

				/* readjust file pointer, as the patterns are 16 bytes aligned */
				stream.Position = start + ((size + 15) & ~15);
			}
		}

#if false
		/* 100% untested, dunno if it even works :P */
		if (CheckParapointer(stream, offsetMessage, "TEXT"))
		{
			int size = stream.ReadStructure<int>();

			song.Message = stream.ReadString(size);
		}
#endif

		song.PanSeparation = 128;
		/* FIXME: should compat Gxx be here? */
		song.Flags = SongFlags.ITOldEffects | SongFlags.CompatibleGXX;

		song.TrackerID = "Epic MegaGames MASI (Old Version)";

		return song;
	}
}
