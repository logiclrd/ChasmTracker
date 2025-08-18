using System;
using System.Collections.Generic;
using System.IO;

namespace ChasmTracker.FileTypes.SongConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class PSM : SongFileConverter
{
	public override string Label => "PSM";
	public override string Description => "Epic MegaGames MASI (New Version)";
	public override string Extension => ".psm";

	public override int SortOrder => 11;

	/* There are four(!) different variations of the PSM format, all
	* incompatible with one another. In order, from oldest to newest(-ish):
	*   1. PS16: unused. I do not know of any files that actually use this
	*      format. Documentation for it *does* exist on modland though.
	*   2. PSM16: used in Silverball, and is closer in relation to Scream
	*      Tracker modules than later versions[citation needed]. Documentation
	*      for this format also exists on modland.
	*   3. "New" PSM: used in Jazz Jackrabbit, Epic Pinball, Extreme Pinball,
	*      and One Must Fall. This one is kind of a mess, probably to get
	*      everything to play as intended.
	*   4. Sinaria: a variation of the previous one, only used in the game
	*      Sinaria. There are a couple subtle differences that make it easy
	*      to detect, e.g. in pattern indexes. This format handles effects
	*      slightly different than the "New" PSM format, which is adjusted
	*      accordingly in the below code. */

	/* ------------------------------------------------------------------------ */
	/* "New" PSM support starts here */

	const uint ID_DSMP = 0x44534D50;
	const uint ID_INST = 0x494E5354;
	const uint ID_OPLH = 0x4F504C48;
	const uint ID_PATT = 0x50415454;
	const uint ID_PBOD = 0x50424F44;
	const uint ID_PPAN = 0x5050414E;
	const uint ID_SONG = 0x534F4E47;
	const uint ID_TITL = 0x5449544C;

	[Flags]
	enum PSMPatternFlags : byte
	{
		Note = 0x80,
		Instrument = 0x40,
		Volume = 0x20,
		Effect = 0x10,
	}

	[Flags]
	enum PSMSampleFlags : byte
	{
		Loop = 0x80,
	}

	protected const int EOF = -1;

	/* this ends the file pointer on the start of all the chunks */
	bool VerifyHeader(Stream fp)
	{
		if (fp.ReadString(4) != "PSM ")
			return false;

		fp.Position += 4;

		if (fp.ReadString(4) != "FILE")
			return false;

		return true;
	}

	public override bool FillExtendedData(Stream fp, FileReference file)
	{
		if (!VerifyHeader(fp))
			return false;

		while (IFF.PeekChunkEx(fp, ChunkFlags.SizeLittleEndian) is IFFChunk c)
		{
			if (c.ID == ID_TITL)
			{
				/* we only need the title, really */
				file.Title = IFF.ReadString(fp, c, length: 256);

				break;
			}
		}

		/* I had it all, and I looked at it and said:
		* 'This is a bigger jail than I just got out of.' */
		file.Description = "Epic MegaGames MASI";
		/*file->extension = str_dup("psm");*/
		file.Type = FileSystem.FileTypes.ModuleS3M;

		return true;
	}

	/* eh */
	byte ConvertPortamento(byte param, bool sinaria)
	{
		if (sinaria)
			return param;

		return (byte)((param < 4)
			? (param | 0xF0)
			: (param >> 2));
	}

	bool ImportEffect(ref SongNote note, Stream fp, bool sinaria)
	{
		/* e[0] == effect
		 * e[1] == param */

		try
		{
			byte[] e = new byte[2];

			fp.ReadExactly(e);

			switch (e[0])
			{
				/* the logic to handle these was gracefully stolen from openmpt. */
				case 0x01: /* Fine volume slide up */
					note.Effect = Effects.VolumeSlide;

					note.Parameter = (byte)(sinaria
						? (e[1] << 4)
						: ((e[1] & 0x1E) << 3));

					note.Parameter |= 0x0F;
					break;
				case 0x02: /* Volume slide up */
					note.Effect = Effects.VolumeSlide;

					note.Parameter = (byte)(sinaria
						? (e[1] << 4)
						: (e[1] << 3));

					note.Parameter &= 0x0F;

					break;
				case 0x03: /* Fine volume slide down */
					note.Effect = Effects.VolumeSlide;

					note.Parameter = sinaria
						? (e[1])
						: (byte)(e[1] >> 1);

					note.Parameter |= 0xF0;

					break;
				case 0x04: /* Volume slide down */
					note.Effect = Effects.VolumeSlide;

					note.Parameter = (byte)(sinaria
						? (e[1] & 0x0F)
						: (e[1] < 2)
							? (e[1] | 0xF0)
							: ((e[1] >> 1) & 0x0F));
					break;

				/* Portamento! */
				case 0x0B: /* Fine portamento up */
					note.Effect = Effects.PortamentoUp;
					note.Parameter = (byte)(0xF0 | ConvertPortamento(e[1], sinaria));
					break;
				case 0x0C: /* Portamento up */
					note.Effect = Effects.PortamentoUp;
					note.Parameter = ConvertPortamento(e[1], sinaria);
					break;
				case 0x0D: /* Fine portamento down */
					note.Effect = Effects.PortamentoDown;
					note.Parameter = (byte)(0xF0 | ConvertPortamento(e[1], sinaria));
					break;
				case 0x0E: /* Portamento down */
					note.Effect = Effects.PortamentoDown;
					note.Parameter = ConvertPortamento(e[1], sinaria);
					break;

				case 0x0F: /* Tone portamento */
					note.Effect = Effects.TonePortamento;

					note.Parameter = sinaria
						? (e[1])
						: (byte)(e[1] >> 2);
					break;
				case 0x10: /* Tone portamento + volume slide up */
					note.Effect = Effects.TonePortamentoVolume;
					note.Parameter = (byte)(e[1] & 0xF0);
					break;
				case 0x12: /* Tone portamento + volume slide down */
					note.Effect = Effects.TonePortamentoVolume;
					note.Parameter = (byte)((e[1] >> 4) & 0x0F);
					break;

				case 0x11: /* Glissando control */
					note.Effect = Effects.Special;
					note.Parameter = (byte)(0x10 | (e[1] & 0x01));
					break;

				case 0x13: /* Scream Tracker Sxx -- "actually hangs/crashes MASI" */
					note.Effect = Effects.Special;
					note.Parameter = e[1];
					break;

				/* Vibrato */
				case 0x15: /* Vibrato */
					note.Effect = Effects.Vibrato;
					note.Parameter = e[1];
					break;
				case 0x16: /* Vibrato waveform */
					note.Effect = Effects.Special;
					note.Parameter = (byte)(0x30 | (e[1] & 0x0F));
					break;
				case 0x17: /* Vibrato + volume slide up */
					note.Effect = Effects.VibratoVolume;
					note.Parameter = (byte)(e[1] & 0xF0);
					break;
				case 0x18: /* Vibrato + volume slide down */
					note.Effect = Effects.VibratoVolume;
					note.Parameter = e[1]; /* & 0x0F ?? */
					break;

				/* Tremolo */
				case 0x1F: /* Tremolo */
					note.Effect = Effects.Tremolo;
					note.Parameter = e[1];
					break;
				case 0x20: /* Tremolo waveform */
					note.Effect = Effects.Special;
					note.Parameter = (byte)(0x40 | (e[1] & 0x0F));
					break;

				/* Sample commands */
				case 0x29: /* Offset */
					/* Oxx - offset (xx corresponds to the 2nd byte) */
					fp.ReadExactly(e);

					note.Effect = Effects.Offset;
					note.Parameter = e[0];

					break;
				case 0x2A: /* Retrigger */
					note.Effect = Effects.Retrigger;
					note.Parameter = e[1];
					break;
				case 0x2B: /* Note Cut */
					note.Effect = Effects.Special;
					note.Parameter = (byte)(0xC0 | e[1]);
					break;
				case 0x2C: /* Note Delay */
					note.Effect = Effects.Special;
					note.Parameter = (byte)(0xD0 | e[1]);
					break;

				/* Position change */
				case 0x33: /* Position jump -- ignored by PLAY.EXE */
					/* just copied what OpenMPT does here... */
					note.Effect = Effects.PositionJump;
					note.Parameter = (byte)(e[1] >> 1);

					fp.Position++;

					break;
				case 0x34: /* Break to row */
					note.Effect = Effects.PatternBreak;

					/* PLAY.EXE entirely ignores the parameter (it always breaks to the first
					* row), so it is maybe best to do this in your own player as well. */
					note.Parameter = 0;
					break;
				case 0x35: /* Pattern loop -- are you sure you want to know? :-) */
					note.Effect = Effects.Special;
					note.Parameter = (byte)(0xB0 | (e[1] & 0x0F));
					break;
				case 0x36: /* Pattern delay */
					note.Effect = Effects.Special;
					note.Parameter = (byte)(0xE0 | (e[1] & 0x0F));
					break;

				/* Speed change */
				case 0x3D: /* Set speed */
					note.Effect = Effects.Speed;
					note.Parameter = e[1];
					break;
				case 0x3E: /* Set tempo */
					note.Effect = Effects.Tempo;
					note.Parameter = e[1];
					break;

				/* Misc. commands */
				case 0x47: /* arpeggio */
					note.Effect = Effects.Arpeggio;
					note.Parameter = e[1];
					break;
				case 0x48: /* Set finetune */
					note.Effect = Effects.Special;
					note.Parameter = (byte)(0x20 | (e[1] & 0x0F));
					break;
				case 0x49: /* Set balance */
					note.Effect = Effects.Special;
					note.Parameter = (byte)(0x80 | (e[1] & 0x0F));
					break;

				default:
					note.Effect = Effects.None;
					note.Parameter = e[1]; /* eh */
					break;
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	int ReadPatternIndex(Stream fp, ref bool sinaria)
	{
		try
		{
			/* "Pxxx" */
			string offsetString = fp.ReadString(4);
			int offset = 1;

			if (offsetString == "PATT")
			{
				/* Sinaria: "PATTxxxx" */
				offsetString = fp.ReadString(4);

				sinaria = true;
				offset = 0;
			}

			if (!int.TryParse(offsetString.AsSpan().Slice(offset), out var r))
				return -1;

			return r;
		}
		catch
		{
			return -1;
		}
	}

	bool ReadPattern(Stream fp, IFFChunk c, Song song, ref bool sinaria, ref int numChannels)
	{
		try
		{
			numChannels = 0;

			fp.Position = c.Offset;

			int length = fp.ReadStructure<int>();

			if (length != c.Size || length < 8)
				return false;

			var index = ReadPatternIndex(fp, ref sinaria);

			if (index < 0 || index >= Constants.MaxPatterns)
				return false;

			int rows = fp.ReadStructure<short>();

			var pattern = song.GetPattern(index, create: true, rowsInNewPattern: rows)!;

			for (int i = 0; i < rows; i++)
			{
				var row = pattern.Rows[i];

				int rowSize = fp.ReadStructure<short>();

				if (rowSize <= 2)
					continue;

				long start = fp.Position;

				while (fp.Position + 3 - 2 < start + rowSize - 2)
				{
					var flags = (PSMPatternFlags)fp.ReadByte();
					int channel = fp.ReadByte();

					if (channel < 0)
						return false;
					if (channel >= Constants.MaxChannels)
						continue;

					numChannels = Math.Max(numChannels, channel);

					ref var note = ref row[channel + 1];

					if (flags.HasAllFlags(PSMPatternFlags.Note))
					{
						/* note */
						int n = fp.ReadByte();

						if (n == EOF)
							return false;

						if (sinaria)
						{
							note.Note = (byte)((n < 85)
								? (n + 36)
								: n);
						}
						else
						{
							note.Note = (byte)((n == 0xFF)
								? SpecialNotes.NoteCut
								: (n < 129)
									? (n & 0x0F) + (12 * (n >> 4) + 13)
									: SpecialNotes.None);
						}
					}

					if (flags.HasAllFlags(PSMPatternFlags.Instrument))
					{
						int s = fp.ReadByte();
						if (s == EOF)
							return false;

						note.Instrument = (byte)(s + 1);
					}

					if (flags.HasAllFlags(PSMPatternFlags.Volume))
					{
						int v = fp.ReadByte();
						if (v == EOF)
							return false;

						note.VolumeEffect = VolumeEffects.Volume;
						note.VolumeParameter = (byte)((Math.Min(v, 127) + 1) / 2);
					}

					if (flags.HasAllFlags(PSMPatternFlags.Effect) && !ImportEffect(ref note, fp, sinaria))
						return false;
				}
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		var songChunks = new List<IFFChunk>();
		var titlChunk = default(IFFChunk);
		var dsmpChunks = new List<IFFChunk>();
		var pbodChunks = new List<IFFChunk>();

		/* "Now, grovel at the feet of the Archdemon Satanichia!" */
		bool sinaria = false;

		if (!VerifyHeader(stream))
			throw new NotSupportedException();

		while (IFF.PeekChunkEx(stream, ChunkFlags.SizeLittleEndian) is IFFChunk c)
		{
			switch (c.ID)
			{
				case ID_SONG: /* order list, channel & module settings */
					if (songChunks.Count >= Constants.MaxOrders)
						break; /* don't care */

					songChunks.Add(c);
					break;
				case ID_DSMP:
					if (dsmpChunks.Count >= Constants.MaxSamples)
						break;

					dsmpChunks.Add(c);
					break;
				case ID_PBOD:
					if (pbodChunks.Count >= Constants.MaxPatterns)
						break;

					pbodChunks.Add(c);
					break;
				case ID_TITL:
					titlChunk = c;
					break;
			}
		}

		var song = new Song();

		if (titlChunk != null)
		{
			/* UNTESTED -- I don't have any PSM files with this chunk. :) */
			song.Title = IFF.ReadString(stream, titlChunk);
		}

		/* need to load patterns; otherwise we won't know
		* whether we're Sinaria or not! */
		int numChannels = 0;

		for (int i = 0; i < pbodChunks.Count; i++)
			ReadPattern(stream, pbodChunks[i], song, ref sinaria, ref numChannels);

		if (!lflags.HasAllFlags(LoadFlags.NoSamples))
		{
			for (int i = 0; i < dsmpChunks.Count; i++)
			{
				stream.Position = dsmpChunks[i].Offset;

				var flags = stream.ReadStructure<PSMSampleFlags>();

				string fileName = stream.ReadString(8); /* Filename of the original module (without extension) */

				/* skip sample ID */
				stream.Position += sinaria ? 8 : 4;

				string name = stream.ReadString(33); /* ??? */

				/* skip unknown bytes */
				stream.Position += 6;

				/* this is the actual sample number */
				short w = stream.ReadStructure<short>();

				if (w > Constants.MaxSamples)
					continue; /* hm */

				var smp = song.EnsureSample(w + 1);

				/* now, put everything we just read into the sample slot */
				smp.Flags = 0;
				if (flags.HasAllFlags(PSMSampleFlags.Loop))
					smp.Flags |= SampleFlags.Loop;

				smp.Name = name;
				smp.FileName = fileName;
				smp.Length = stream.ReadStructure<int>();
				smp.LoopStart = stream.ReadStructure<int>();
				smp.LoopEnd = stream.ReadStructure<int>();

				if (!sinaria && (smp.LoopEnd != 0))
				{
					/* blurb from OpenMPT:
					* Note that we shouldn't add + 1 for MTM conversions here (e.g. the OMF 2097 music),
					* but I think there is no way to figure out the original format, and in the case of the OMF 2097 soundtrack
					* it doesn't make a huge audible difference anyway (no chip samples are used).
					* On the other hand, sample 8 of MUSIC_A.PSM from Extreme Pinball will sound detuned if we don't adjust the loop end here. */
					smp.LoopEnd++;
				}

				/* skip unknown bytes */
				stream.Position += sinaria ? 2 : 1;

				/* skip finetune */
				stream.Position++;

				smp.Volume = (stream.ReadStructure<byte>() + 1) * 2; /* this is what OpenMPT does */

				/* skip unknown bytes */
				stream.Position += 4;

				if (sinaria)
					smp.C5Speed = stream.ReadStructure<short>();
				else
					smp.C5Speed = stream.ReadStructure<int>();

				/* skip padding */
				stream.Position += sinaria ? 16 : 19;

				SampleFileConverter.ReadSample(smp, SampleFormat.LittleEndian | SampleFormat.Mono | SampleFormat._8 | SampleFormat.PCMDeltaEncoded, stream);
			}
		}

		/* Now that we've loaded samples and patterns, let's load all
		 * the stupid subsong information. */
		int numOrders = 0;

		for (int i = 0; i < songChunks.Count; i++)
		{
			stream.Position = songChunks[i].Offset;

			/* skip song type (9 characters), and compression (1 8-bit int) */
			stream.Position += 10;

			int subsongChannels = stream.ReadByte();
			if (subsongChannels == EOF)
				break; /* ??? */

			if (numOrders > 0)
			{
				/* can't really load anything else? */
				if (numOrders + 1 > Constants.MaxOrders)
					break;

				/* anything past the first song are "hidden patterns" */
				song.OrderList.Add(SpecialOrders.Last);
			}

			/* sub-chunks */
			IFFChunk? oplhChunk = null;
			IFFChunk? ppanChunk = null;

			while ((IFF.PeekChunkEx(stream, ChunkFlags.SizeLittleEndian) is IFFChunk c) && (stream.Position <= songChunks[i].Offset + songChunks[i].Size))
			{
				switch (c.ID)
				{
					/* should we handle DATE? */
					case ID_OPLH: /* Order list, channel, and module settings */
						oplhChunk = c;
						break;
					case ID_PPAN: /* Channel pan table */
						ppanChunk = c;
						break;
				}
			}

			if ((oplhChunk != null) && (oplhChunk.Size >= 9))
			{
				int chunkCount = 0, firstOrderChunk = int.MaxValue;

				stream.Position = oplhChunk.Offset;

				/* skip chunk amount (uint16_t) */
				stream.Position += 6;

				while (stream.Position <= oplhChunk.Offset + oplhChunk.Size)
				{
					/* FIXME: Pretty much all of these treat the subsong it's working
					* on as if it were the *whole song*, which is wrong. */
					int opcode = stream.ReadByte();
					if ((opcode == EOF) || (opcode == 0))
						break; /* last chunk reached? */

					/* this stuff was all stolen from openmpt. */
					switch (opcode)
					{
						case 0x01:
						{
							/* Play order list item */
							int index;

							index = ReadPatternIndex(stream, ref sinaria);
							if (index < 0)
								break; /* wut */

							if (numOrders + 1 > Constants.MaxOrders)
								break;

							if (index == 0xFF)
								index = SpecialOrders.Last;
							else if (index == 0xFE)
								index = SpecialOrders.Skip;

							song.OrderList.Add(index);

							// Decide whether this is the first order chunk or not (for finding out the correct restart position)
							if (firstOrderChunk == int.MaxValue)
								firstOrderChunk = chunkCount;
							break;
						}

						case 0x02:
							/* Play Range (xx from line yy to line zz).
							* Three 16-bit parameters but it seems like the next opcode
							* is parsed at the same position as the third parameter. */
							stream.Position += 4;
							break;

						case 0x03:
						/* Jump Loop (like Jump Line, but with a third, unknown byte
						* following -- nope, it does not appear to be a loop count) */
						case 0x04:
						{
							/* Jump Line (Restart position) */

							short restartChunk = stream.ReadStructure<short>();

							if (restartChunk >= firstOrderChunk)
								song.InsertRestartPos(restartChunk - firstOrderChunk);

							if (opcode == 0x03)
								stream.Position++;

							break;
						}

						case 0x06: // Transpose (appears to be a no-op in MASI)
							stream.Position++;
							break;

						case 0x05:
						{
							/* Channel Flip (changes channel type without changing pan position) */
							byte channelNumber = stream.ReadStructure<byte>();
							byte newPanning = stream.ReadStructure<byte>();

							if (channelNumber >= Constants.MaxChannels)
								break;

							song.Channels[channelNumber].Panning = newPanning;

							break;
						}

						case 0x07: /* Default Speed */
						{
							int speed = stream.ReadByte();
							if (speed != EOF)
								song.InitialSpeed = speed;
							break;
						}

						case 0x08: /* Default Tempo */
						{
							int tempo = stream.ReadByte();
							if (tempo != EOF)
								song.InitialTempo = tempo;
							break;
						}

						case 0x0C: /* "Sample map table" */
						{
							byte[] table = new byte[6];

							stream.ReadExactly(table);

							if ((table[0] != 0) || (table[1] != 0xFF) || (table[2] != 0) || (table[3] != 0) || (table[4] != 1) || (table[5] != 0))
								throw new NotSupportedException();

							break;
						}

						case 0x0D:
						{
							/* Channel panning table - can be set using CONVERT.EXE /E */
							int channelNumber = stream.ReadByte();
							int initialPanning = stream.ReadByte();
							int panningType = stream.ReadByte();

							if (panningType == EOF)
								break;

							if (channelNumber >= Constants.MaxChannels)
								break;

							song.Channels[channelNumber].Panning = initialPanning;

							/* FIXME: need to handle the pan TYPE as well
							*
							* this format is so fucking bad */

							break;
						}

						case 0x0E:
						{
							/* Channel volume table (0...255) - can be set using CONVERT.EXE /E, is
							* 255 in all "official" PSMs except for some OMF 2097 tracks */
							int channelNumber = stream.ReadByte();
							int initialVolume = stream.ReadByte();

							if (initialVolume == EOF)
								break;

							if (channelNumber >= Constants.MaxChannels)
								break;

							song.Channels[channelNumber].Volume = (initialVolume / 4) + 1;

							break;
						}

						default:
							Log.Append(4, " PSM/OPLH: unknown opcode: {0:x} at {1}", opcode, chunkCount);
							throw new NotSupportedException();
					}

					chunkCount++;
				}
			}

			if ((ppanChunk != null) && (songChunks.Count > 0) /* TODO handle other subsongs */)
			{
				Assert.IsTrue(ppanChunk.Size > subsongChannels * 2, "ppanChunk.Size > subsongChannels * 2", "PSM: PPAN chunk is too small");

				stream.Position = ppanChunk.Offset;

				byte[] xx = new byte[Constants.MaxChannels * 2];

				stream.ReadExactly(xx);

				for (int j = 0; j < numChannels; j++)
				{
					byte panningType = xx[j * 2];
					byte panningValue = xx[j * 2 + 1];

					switch (panningType)
					{
						case 0: /* normal panning */
							song.Channels[j].Panning = panningValue;
							break;
						case 1: /* surround */
							song.Channels[j].Panning = 128;
							song.Channels[j].Flags = ChannelFlags.Surround;
							break;
						case 2:
							song.Channels[j].Panning = 128;
							break;
					}
				}
			}
		}

		song.PanSeparation = 128;
		song.Flags = SongFlags.ITOldEffects | SongFlags.CompatibleGXX;

		if (sinaria)
			song.TrackerID = "Epic MegaGames MASI (New Version / Sinaria)";
		else
			song.TrackerID = "Epic MegaGames MASI (New Version)";

		return song;
	}
}