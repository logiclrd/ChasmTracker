using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ChasmTracker.FileTypes.SongConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public abstract class MODBase : SongFileConverter
{
	public override string Label => "MOD";
	public override string Description => "Amiga ProTracker";
	public override string Extension => ".mod";

	/* --------------------------------------------------------------------- */

	/* TODO: WOW files */

	/* Ugh. */
	static List<(string Tag, string Description)> s_validTags =
		new List<(string, string)>()
		{
			/* M.K. must be the first tag! (to test for WOW files) */
			/* the first 5 descriptions are a bit weird */
			("M.K.", "Amiga-NewTracker"),
			("M!K!", "Amiga-ProTracker"),
			("M&K!", "Amiga-NoiseTracker"),
			("N.T.", "Amiga-NoiseTracker"),
			("FEST", "Amiga-NoiseTracker"), /* jobbig.mod */
			/* Atari Octalyzer */
			("CD61", "6 Channel Falcon"),
			("CD81", "8 Channel Falcon"),
			/* Startrekker (quite rare...) */
			("FLT4", "4 Channel Startrekker"),
			("EXO4", "4 Channel Startrekker"),
			("FLT8", "8 Channel Startrekker"),
			("EXO8", "8 Channel Startrekker"),
			/* Oktalyzer */
			("OCTA", "8 Channel MOD"),
			("OKTA", "8 Channel MOD"),
			("TDZ1", "1 Channel MOD"),
			("TDZ2", "2 Channel MOD"),
			("TDZ3", "3 Channel MOD"),
			/* xCHN = generic */
			("1CHN", "1 Channel MOD"),
			("2CHN", "2 Channel MOD"),
			("3CHN", "3 Channel MOD"),
			("4CHN", "4 Channel MOD"),
			("5CHN", "5 Channel MOD"),
			("6CHN", "6 Channel MOD"),
			("7CHN", "7 Channel MOD"),
			("8CHN", "8 Channel MOD"),
			("9CHN", "9 Channel MOD"),
			/* xxCN/xxCH = generic */
			("10CN", "10 Channel MOD"),
			("11CN", "11 Channel MOD"),
			("12CN", "12 Channel MOD"),
			("13CN", "13 Channel MOD"),
			("14CN", "14 Channel MOD"),
			("15CN", "15 Channel MOD"),
			("16CN", "16 Channel MOD"),
			("17CN", "17 Channel MOD"),
			("18CN", "18 Channel MOD"),
			("19CN", "19 Channel MOD"),
			("20CN", "20 Channel MOD"),
			("21CN", "21 Channel MOD"),
			("22CN", "22 Channel MOD"),
			("23CN", "23 Channel MOD"),
			("24CN", "24 Channel MOD"),
			("25CN", "25 Channel MOD"),
			("26CN", "26 Channel MOD"),
			("27CN", "27 Channel MOD"),
			("28CN", "28 Channel MOD"),
			("29CN", "29 Channel MOD"),
			("30CN", "30 Channel MOD"),
			("31CN", "31 Channel MOD"),
			("32CN", "32 Channel MOD"),
		};

	[Flags]
	enum Warnings
	{
		[Description("Linear slides")]
		LinearSlides,
		[Description("Sample volumes")]
		SampleVolume,
		[Description("Sustain and Ping Pong loops")]
		Loops,
		[Description("Sample vibrato")]
		SampleVibrato,
		[Description("Instrument functions")]
		Instruments,
		[Description("Pattern lengths other than 64 rows")]
		PatternLength,
		[Description("Notes outside the range C-4 to B-6")]
		NoteRange,
		[Description("Extended volume column effects")]
		VolumeEffects,
		[Description("Over 31 samples")]
		MaxSamples,
		[Description("Odd sample length or greater than 131070")]
		LongSamples,
		[Description("Patterns outside order list")]
		UnusedPatterns,
	}

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		if (stream.Length < 1085)
			return false;

		try
		{
			string title = stream.ReadString(20);

			stream.Position = 1080;

			string tag = stream.ReadString(4);

			foreach (var validTag in s_validTags)
			{
				if (tag == validTag.Tag)
				{
					/* if (i == 0) {
						Might be a .wow; need to calculate some crap to find out for sure.
						For now, since I have no wow's, I'm not going to care.
					} */

					file.Description = validTag.Description;
					file.Title = title;
					file.Type = FileSystem.FileTypes.ModuleMOD;

					return true;
				}
			}

			/* check if it could be a SoundTracker MOD */
			stream.Position = 0;

			int errors = 0;
			for (int i = 0; i < 20; i++)
			{
				int b = stream.ReadByte();

				if (b > 0 && b < 32)
				{
					errors++;

					if (errors > 5)
						return false;
				}
			}

			int allVolumes = 0, allLengths = 0;

			for (int i = 0; i < 15; i++)
			{
				stream.Position = 20 + i * 30 + 22;

				int lengthHigh = stream.ReadByte();
				int lengthLow = stream.ReadByte();
				int length = lengthHigh * 0x100 + lengthLow;
				int finetune = stream.ReadByte();
				int volume = stream.ReadByte();

				if (finetune != 0)
					return false; /* invalid finetune */

				if (volume > 64)
					return false; /* invalid volume */

				if (length > 32768)
					return false; /* invalid sample length */

				allVolumes |= volume;
				allLengths |= lengthHigh | lengthLow;
			}

			if ((allLengths == 0) || (allVolumes == 0))
				return false;

			file.Description = "SoundTracker";
			/*file->extension = str_dup("mod");*/
			file.Title = title;
			file.Type = FileSystem.FileTypes.ModuleMOD;

			return true;
		}
		catch
		{
			return false;
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

	/* "TakeTrackered with version 0.9E!!!!!" XOR with 0xDF. */
	static readonly byte[] TakeTracker =
		{
			0x8B, 0xBE, 0xB4, 0xBA, 0x8B, 0xAD, 0xBE, 0xBC,
			0xB4, 0xBA, 0xAD, 0xBA, 0xBB, 0xFF, 0xA8, 0xB6,
			0xAB, 0xB7, 0xFF, 0xA9, 0xBA, 0xAD, 0xAC, 0xB6,
			0xB0, 0xB1, 0xFF, 0xEF, 0xF1, 0xE6, 0xBA, 0xFE,
			0xFE, 0xFE, 0xFE, 0xFE,
		};

	/* This is actually nine bytes, but the final three vary between
	* Tetramed versions.
	* Possibly they could be used to fingerprint versions?? */
	static readonly byte[] Tetramed = {0x00, 0x11, 0x55, 0x33, 0x22, 0x11};

	/* force determines whether the loader will force-read untagged files as
		15-sample mods */
	protected Song LoadSongImplementation(Stream stream, LoadFlags lflags, bool forceUntaggedAs15Sample)
	{
		int nSamples = 31; /* default; tagless mods have 15 */

		/* check the tag (and set the number of channels) -- this is ugly, so don't look */
		stream.Position = 1080;

		string tag = stream.ReadString(4);

		bool startrekker = false;
		bool testWOW = false;
		bool mk = false;
		bool maybeST3 = false;
		bool maybeFT2 = false;
		bool hisMastersNoise = false;
		string? tid = null;
		int nChannels = 31; /* default; tagless mods have 15 */

		if (tag == "M.K.")
		{
			/* M.K. = Protracker etc., or Mod's Grave (*.wow) */
			nChannels = 4;
			testWOW = true;
			mk = true;
			maybeFT2 = true;
			tid = "Amiga-NewTracker";
		}
		else if (tag == "M!K!")
		{
			nChannels = 4;
			tid = "Amiga-ProTracker";
		}
		else if ((tag == "M&K!") || (tag == "N.T.") || (tag == "FEST"))
		{
			nChannels = 4;
			tid = "Amiga-NoiseTracker";
			if ((tag == "M&K!") || (tag == "FEST"))
			{
				// Alternative finetuning
				hisMastersNoise = true;
			}
		}
		else if ((tag.StartsWith("FLT") || tag.StartsWith("EXO")) && (tag.Length > 3) && (tag[3] == '4' || tag[3] == '8'))
		{
			// Hopefully EXO8 is stored the same way as FLT8
			nChannels = tag[3] - '0';
			startrekker = (nChannels == 8);
			tid = "{0} Channel Startrekker";
			//log_appendf(4, " Warning: Startrekker AM synth is not supported");
		}
		else if ((tag == "OCTA") || (tag == "OKTA"))
		{
			nChannels = 8;
			tid = "Amiga Oktalyzer"; // IT just identifies this as "8 Channel MOD"
		}
		else if ((tag == "CD61") || (tag == "CD81"))
		{
			nChannels = 8;
			tid = "8 Channel Falcon"; // Atari Oktalyser
		}
		else if ((tag.Length == 4) && (tag[0] > '0') && (tag[0] <= '9') && tag.EndsWith("CHN"))
		{
			/* nCHN = Fast Tracker (if n is even) or TakeTracker (if n = 5, 7, or 9) */
			nChannels = tag[0] - '0';
			if (nChannels == 5 || nChannels == 7 || nChannels == 9)
				tid = "{0} Channel TakeTracker";
			else
			{
				if (!nChannels.HasBitSet(1))
					maybeFT2 = true;
				tid = "{0} Channel MOD"; // generic
			}
			maybeST3 = true;
		}
		else if ((tag.Length == 4) && tag[0] > '0' && tag[0] <= '9' && tag[1] >= '0' && tag[1] <= '9'
				&& tag[2] == 'C' && (tag[3] == 'H' || tag[3] == 'N'))
		{
			/* nnCH = Fast Tracker (if n is even and <= 32) or TakeTracker (if n = 11, 13, 15)
			* Not sure what the nnCN variant is. */
			nChannels = 10 * (tag[0] - '0') + (tag[1] - '0');
			if (nChannels == 11 || nChannels == 13 || nChannels == 15)
				tid = "{0} Channel TakeTracker";
			else
			{
				if ((nChannels & 1) == 0 && nChannels <= 32 && tag[3] == 'H')
					maybeFT2 = true;
				tid = "{0} Channel MOD"; // generic
			}
			if (tag[3] == 'H')
				maybeST3 = true;
		}
		else if ((tag.Length == 4) && tag.StartsWith("TDZ") && tag[3] > '0' && tag[3] <= '9')
		{
			/* TDZ[1-3] = TakeTracker */
			nChannels = tag[3] - '0';
			if (nChannels < 4)
				tid = "{0} Channel TakeTracker";
			else
				tid = "{0} Channel MOD";
		}
		else if (forceUntaggedAs15Sample)
		{
			/* some old modules don't have tags, so try loading anyway */
			nChannels = 4;
			nSamples = 15;
			tid = "{0} Channel MOD";
		}
		else
			throw new NotSupportedException();

		/* suppose the tag is 90CH :) */
		if (nChannels > Constants.MaxChannels)
		{
			//Console.Error.WriteLine("{0}: Too many channels!", filename);
			throw new FormatException();
		}

		var song = new Song();

		/* read the title */
		stream.Position = 0;

		song.Title = stream.ReadString(20);

		int sampleSize = 0;

		/* sample headers */
		for (int n = 1; n <= nSamples; n++)
		{
			var sample = song.EnsureSample(n);

			sample.Name = stream.ReadString(22);
			sample.Length = ByteSwap.Swap(stream.ReadStructure<short>()) * 2;

			/* this is only necessary for the wow test... */
			sampleSize += sample.Length;

			if (hisMastersNoise)
				sample.C5Speed = SongNote.TransposeToFrequency(0, -unchecked((sbyte)(stream.ReadByte() << 3)));
			else
				sample.C5Speed = Tables.FineTuneTable[stream.ReadByte()];

			sample.Volume = stream.ReadByte();
			if (sample.Volume > 64)
				sample.Volume = 64;
			if ((sample.Length == 0) && (sample.Volume != 0))
				maybeFT2 = false;
			sample.Volume *= 4; //mphack
			sample.GlobalVolume = 64;

			sample.LoopStart = ByteSwap.Swap(stream.ReadStructure<short>()) * 2;

			int tmp = ByteSwap.Swap(stream.ReadStructure<short>()) * 2;

			if (tmp > 2)
				sample.Flags |= SampleFlags.Loop;
			else if (tmp == 0)
				maybeST3 = false;
			else if (sample.Length == 0)
				maybeFT2 = false;

			sample.LoopEnd = sample.LoopStart + tmp;
			sample.VibratoType = 0;
			sample.VibratoRate = 0;
			sample.VibratoDepth = 0;
			sample.VibratoSpeed = 0;
		}

		/* pattern/order stuff */
		int nOrd = stream.ReadByte();
		int restart = stream.ReadByte();

		byte[] orderListBytes = new byte[128];

		stream.ReadExactly(orderListBytes);

		for (int i = 0; i < 128; i++)
			song.OrderList.Add(orderListBytes[i]);

		int nPat = 0;

		if (startrekker)
		{
			/* from mikmod: if the file says FLT8, but the orderlist
			has odd numbers, it's probably really an FLT4 */
			for (int n = 0; n < 128; n++)
			{
				if (song.OrderList[n].HasBitSet(1))
				{
					startrekker = false;
					nChannels = 4;
					break;
				}
			}
		}

		if (startrekker)
		{
			for (int n = 0; n < 128; n++)
				song.OrderList[n] >>= 1;
		}

		for (int n = 0; n < 128; n++)
		{
			if (song.OrderList[n] >= Constants.MaxPatterns)
				song.OrderList[n] = SpecialOrders.Skip;
			else if (song.OrderList[n] > nPat)
				nPat = song.OrderList[n];
		}

		/* set all the extra orders to the end-of-song marker */
		song.OrderList.RemoveRange(nOrd, song.OrderList.Count - nOrd);
		song.OrderList.Add(SpecialOrders.Last);

		if (restart == 0x7f && maybeST3)
			tid = "Scream Tracker 3?";
		else if (restart == 0x7f && mk)
			tid = "{0} Channel ProTracker";
		else if (restart <= nPat && maybeFT2)
			tid = "{0} Channel FastTracker";
		else if (restart == nPat && mk)
			tid = "{0} Channel Soundtracker";

		/* hey, is this a wow file? */
		if (testWOW)
		{
			if (stream.Length == 2048 * nPat + sampleSize + 3132)
			{
				nChannels = 8;
				tid = "Mod's Grave WOW";
			}
		}

		/* 15-sample mods don't have a 4-byte tag... or the other 16 samples */
		stream.Position = (nSamples == 15) ? 600 : 1084;

		/* pattern data */
		byte[] noteBuffer = new byte[4];

		if (startrekker)
		{
			// 8 channel data that is formatted as one 4 channel pattern appended to another 4 channel pattern
			for (int pat = 0; pat <= nPat; pat++)
			{
				var pattern = song.GetPattern(pat, create: true)!;

				for (int n = 0; n < 64; n++)
				{
					for (int chan = 1; chan <= 4; chan++)
					{
						ref var note = ref pattern.Rows[n][chan];

						stream.ReadExactly(noteBuffer);

						note.ImportMODNote(noteBuffer);
					}
				}

				for (int n = 0; n < 64; n++)
				{
					for (int chan = 1; chan <= 4; chan++)
					{
						ref var note = ref pattern.Rows[n][chan + 4];

						stream.ReadExactly(noteBuffer);

						note.ImportMODNote(noteBuffer);
					}
				}
			}
		}
		else
		{
			for (int pat = 0; pat <= nPat; pat++)
			{
				var pattern = song.Patterns[pat] = new Pattern();

				for (int n = 0; n < 64; n++)
				{
					for (int chan = 1; chan <= nChannels; chan++)
					{
						ref var note = ref pattern.Rows[n][chan];

						stream.ReadExactly(noteBuffer);

						note.ImportMODNote(noteBuffer);
					}
				}
			}
		}

		if (restart < nPat)
			song.InsertRestartPos(restart);

		{
			byte[] magicEOF = new byte[Math.Max(TakeTracker.Length, Tetramed.Length + 3)];

			for (int n = 1; n < nSamples + 1; n++)
			{
				var sample = song.EnsureSample(n);

				if (sample.Length == 0)
					continue;

				/* check for ADPCM compression */
				SampleFormat flags = SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian;

				string sstart = stream.ReadString(5);

				if (sstart == "ADPCM")
					flags |= SampleFormat.PCM16bitTableDeltaEncoded;
				else
				{
					flags |= SampleFormat.PCMSigned;
					stream.Position -= 5;
				}

				if (lflags.HasAllFlags(LoadFlags.NoSamples))
				{
					/* just skip the data, I guess */
					stream.Position += sample.Length;
				}
				else
					SampleFileConverter.ReadSample(sample, flags, stream);
			}

			int len = stream.Read(magicEOF, 0, magicEOF.Length);

			/* Some trackers dump extra data at the end of the file. */
			if (nChannels <= 16 && len >= TakeTracker.Length && magicEOF.Take(TakeTracker.Length).SequenceEqual(TakeTracker))
				tid = "{0} Channel TakeTracker";
			else if (mk && len >= (Tetramed.Length + 3) && magicEOF.Take(Tetramed.Length).SequenceEqual(Tetramed))
				tid = "{0} Channel Tetramed";
#if false
			else if (len >= 0)
				for (int z = 0; z < len; z++)
					Console.Write("{0:x2} ", magicEOF[z]);
#endif
		}

		/* set some other header info that's always the same for .mod files */
		song.Flags = SongFlags.ITOldEffects | SongFlags.CompatibleGXX;
		for (int n = 0; n < nChannels; n++)
			song.Channels[n].Panning = Tables.ProTrackerPanning(n);

		for (int n = nChannels; n < song.Channels.Length; n++)
			song.Channels[n].Flags |= ChannelFlags.Mute;

		song.PanSeparation = 64;

		song.TrackerID = string.Format(tid ?? "{0} Channel MOD", nChannels);

		/* done! */
		return song;
	}

	/* .MOD saving routines -- exposed via class MOD31 */
	public override SaveResult SaveSong(Song song, Stream stream)
	{
		Warnings warn = 0;

		if (song.IsInstrumentMode)
			warn |= Warnings.Instruments;
		if (song.Flags.HasAllFlags(SongFlags.LinearSlides))
			warn |= Warnings.LinearSlides;

		int nSmp = song.GetSampleCount();

		if (nSmp > 31)
		{
			Console.WriteLine(nSmp);
			nSmp = 31;
			warn |= Warnings.MaxSamples;
		}

		int nChannels = song.GetHighestUsedChannel() + 1;

		stream.WriteString(song.Title, 20);

		// Now writing sample headers
		for (int n = 1; n <= 31; ++n)
		{
			var sample = song.GetSample(n);

			if (sample == null)
			{
				stream.Position += 30;
				continue;
			}

			if (sample.GlobalVolume != 64)
				warn |= Warnings.SampleVolume;
			if (sample.Flags.HasAllFlags(SampleFlags.Loop | SampleFlags.PingPongLoop) || sample.Flags.HasAllFlags(SampleFlags.SustainLoop))
				warn |= Warnings.Loops;
			if (sample.VibratoDepth != 0)
				warn |= Warnings.SampleVibrato;
			/* these should be separate warnings. */
			if (sample.Length.HasBitSet(1) || (sample.Length > 0x1FFFE))
				warn |= Warnings.LongSamples;

			stream.WriteString(sample.Name, 22);

			/* sample length. */
			ushort w = Math.Min((ushort)(sample.Length >> 1), (ushort)0xFFFF);

			w = ByteSwap.Swap(w);

			stream.WriteStructure(w);

			/* ...this seems rather stupid. why aren't we just finding the
			* value with the least difference? */
			int finetuneValue;

			for (finetuneValue = 15; (finetuneValue != 0) && (Tables.FineTuneTable[finetuneValue] > sample.C5Speed); --finetuneValue)
				if ((sample.C5Speed > 10000) && (finetuneValue == 8))
					break; /* determine from finetune_table entry */

			byte b = (byte)((finetuneValue ^ 8) & 0x0F);

			stream.WriteByte(b);

			stream.WriteByte((byte)((sample.Volume + 1) / 4)); /* volume, 0..64 */

			if (sample.Flags.HasAllFlags(SampleFlags.Loop))
			{
				w = Math.Min((ushort)(sample.LoopStart >> 1), (ushort)0xFFFF);
				w = ByteSwap.Swap(w);

				stream.WriteStructure(w);

				w = Math.Min((ushort)((sample.LoopEnd - sample.LoopStart) >> 1), (ushort)0xFFFF);
				w = ByteSwap.Swap(w);

				stream.WriteStructure(w);
			}
			else
				stream.WriteStructure(ByteSwap.Swap(1)); // 00 00 00 01
		}

		int nOrd = song.GetOrderCount();

		byte[] tmp = new byte[128];

		tmp[0] = (byte)nOrd;
		tmp[1] = 0x7f;

		stream.Write(tmp.Slice(0, 2));

		byte[] modOrders = new byte[128];
		int maxPat = 0;

		for (int i = 0; (i < nOrd) && (i < 128); ++i)
		{
			modOrders[i] = (byte)song.OrderList[i];
			maxPat = Math.Max(maxPat, modOrders[i]);
		}

		if (maxPat + 1 < song.GetPatternCount())
			warn |= Warnings.UnusedPatterns;

		stream.Write(modOrders);

		if (nChannels == 4)
			stream.WriteString((maxPat < 64) ? "M.K." : "M!K!", 4);
		else
		{
			string tag;

			if (nChannels >= 10)
				tag = nChannels + "CN";
			else
				tag = nChannels + "CHN";

			/* guten tag */
			stream.WriteString(tag, 4);
		}

		byte[] modPattern = new byte[nChannels * 4 * 64];

		Pattern? blankPattern = null;

		for (int n = 0; n <= maxPat; ++n)
		{
			Array.Clear(modPattern);

			var pattern = song.Patterns[n];

			if (pattern == null)
				pattern = blankPattern ??= new Pattern();

			int jmax = pattern.Rows.Count;

			if (jmax != 64)
			{
				jmax = Math.Min(jmax, 64);
				warn |= Warnings.PatternLength;
			}

			jmax *= Constants.MaxChannels;

			int j, joutpos;

			for (j = joutpos = 0; j < jmax; ++j)
			{
				int row = j / Constants.MaxChannels;
				int channel = 1 + j % Constants.MaxChannels;

				ref var m = ref pattern.Rows[row][channel];

				if (channel < nChannels)
				{
					int period = Tables.AmigaPeriodTable[(m.Note) & 0xff];

					if (m.Note.HasAnyBitSet(0xff) && ((period < 113) || (period > 856)))
						warn |= Warnings.NoteRange;

					modPattern[joutpos] = (byte)(((m.Instrument) & 0x10) | (period >> 8));
					modPattern[joutpos + 1] = (byte)(period & 0xff);
					modPattern[joutpos + 2] = (byte)((m.Instrument & 0xf) << 4);

					byte modEffect = 0;
					byte modEffectParameter = m.Parameter;

					if (m.VolumeEffect == VolumeEffects.Volume)
					{
						modEffect = 0x0c;
						modEffectParameter = m.VolumeParameter;
					}
					else if (m.VolumeEffect == VolumeEffects.None)
					{
						switch (m.Effect)
						{
							case Effects.None: modEffectParameter = 0; break;
							case Effects.Arpeggio: modEffect = 0; break;
							case Effects.PortamentoUp:
								modEffect = 1;
								if ((modEffectParameter & 0xf0) == 0xe0)
								{
									modEffect = 0x0e;
									modEffectParameter = (byte)(0x10 | ((modEffectParameter & 0xf) >> 2));
								}
								else if ((modEffectParameter & 0xf0) == 0xf0)
								{
									modEffect = 0x0e;
									modEffectParameter = (byte)(0x10 | (modEffectParameter & 0xf));
								}
								break;
							case Effects.PortamentoDown:
								modEffect = 2;
								if ((modEffectParameter & 0xf0) == 0xe0)
								{
									modEffect = 0x0e;
									modEffectParameter = (byte)(0x20 | ((modEffectParameter & 0xf) >> 2));
								}
								else if ((modEffectParameter & 0xf0) == 0xf0)
								{
									modEffect = 0x0e;
									modEffectParameter = (byte)(0x20 | (modEffectParameter & 0xf));
								}
								break;
							case Effects.TonePortamento: modEffect = 3; break;
							case Effects.Vibrato: modEffect = 4; break;
							case Effects.TonePortamentoVolume: modEffect = 5; break;
							case Effects.VibratoVolume: modEffect = 6; break;
							case Effects.Tremolo: modEffect = 7; break;
							case Effects.Panning: modEffect = 8; break;
							case Effects.Offset: modEffect = 9; break;
							case Effects.VolumeSlide:
								modEffect = 0x0a;
								if (modEffectParameter.HasAnyBitSet(0xf0) && modEffectParameter.HasAnyBitSet(0x0f))
								{
									if ((modEffectParameter & 0xf0) == 0xf0)
									{ // fine volslide down!
										modEffect = 0x0e;
										modEffectParameter &= 0xbf;
									}
									else if ((modEffectParameter & 0x0f) == 0x0f)
									{ // fine volslide up!
										modEffect = 0x0e;
										modEffectParameter = (byte)(0xa0 | (modEffectParameter >> 4));
									}
								}
								break;
							case Effects.PositionJump: modEffect = 0x0b; break;
							case Effects.Volume: modEffect = 0x0c; break;
							case Effects.PatternBreak: modEffect = 0x0d; modEffectParameter = (byte)(((modEffectParameter / 10) << 4) | (modEffectParameter % 10)); break;
							case Effects.Speed: modEffect = 0x0f; break;
							case Effects.Tempo: modEffect = 0x0f; break;
							case Effects.Special:
								modEffect = 0x0e;
								switch (modEffectParameter & 0xf0)
								{
									case 0x10: modEffectParameter = (byte)((modEffectParameter & 0x0f) | 0x30); break;
									case 0x20: modEffectParameter = (byte)((modEffectParameter & 0x0f) | 0x50); break; // there is an error in Protracker 2.1 docs!
									case 0x30: modEffectParameter = (byte)((modEffectParameter & 0x0f) | 0x40); break;
									case 0x40: modEffectParameter = (byte)((modEffectParameter & 0x0f) | 0x70); break;
									case 0xb0: modEffectParameter = (byte)((modEffectParameter & 0x0f) | 0x60); break;
									default: break; // handling silently E0x,E6x,E8x,ECx,EDx,EEx,(?EFx)
								}
								break;
							case Effects.Retrigger: modEffect = 0x0e; modEffectParameter = (byte)(0x90 | (modEffectParameter & 0x0f)); break;
							default:
								warn |= Warnings.VolumeEffects;
								break;
						}
					}
					else
					{
						/* TODO: try harder */
						warn |= Warnings.VolumeEffects;
					}

					modPattern[joutpos + 2] |= (byte)(modEffect & 0x0f);
					modPattern[joutpos + 3] = modEffectParameter;
					joutpos += 4;
				}
			}

			stream.Write(modPattern);
		}

		// Now writing sample data
		for (int n = 0; (n < nSmp) && (n < 31); ++n)
		{
			var smp = song.GetSample(n + 1);

			if ((smp != null) && smp.HasData)
			{
				if (smp.Flags.HasAllFlags(SampleFlags.Loop) && (smp.LoopStart < smp.LoopEnd) && (smp.LoopEnd <= Math.Min(smp.Length, 0x1FFFE)))
					SampleFileConverter.WriteSample(stream, smp, SampleFormat.PCMSigned | SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian, 0x1FFFE);
				else if (smp.Length >= 1)
				{
					// Math.Floor(smp.Length / 2) MUST be positive!
					long tmpPos = stream.Position;

					SampleFileConverter.WriteSample(stream, smp, SampleFormat.PCMSigned | SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian, 0x1FFFE);

					stream.Position = tmpPos;
					stream.WriteStructure((short)0);
					stream.Position = stream.Length;
				}
			}
		}

		/* announce all the things we broke - ripped from s3m.c */

		foreach (var warning in Enum.GetValues<Warnings>())
			if (warn.HasAllFlags(warning))
				Log.Append(4, " Warning: {0} unsupported in MOD format", warning.GetDescription());

		return SaveResult.Success;
	}
}