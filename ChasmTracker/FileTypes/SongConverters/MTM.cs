using System;
using System.Collections.Generic;
using System.IO;

namespace ChasmTracker.FileTypes.SongConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class MTM : SongFileConverter
{
	public override string Label => "MTM";
	public override string Description => "MultiTracker Module";
	public override string Extension => ".mtm";

	public override int SortOrder => 6;

	/* --------------------------------------------------------------------- */

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		if (stream.ReadString(3) != "MTM")
			return false;

		stream.Position++;

		string title = stream.ReadString(20);

		file.Description = "MultiTracker Module";
		/*file.Extension = str_dup("mtm");*/
		file.Title = title;
		file.Type = FileSystem.FileTypes.ModuleMOD;

		return true;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	void UnpackTrack(Span<byte> b, List<SongNote[]> trackList, int rows)
	{
		var track = new SongNote[rows];

		for (int n = 0; n < rows; n++, b = b.Slice(3))
		{
			ref var note = ref track[n];

			note.Note = ((b[0] & 0xfc) != 0) ? (byte)((b[0] >> 2) + 36 + 1) : SpecialNotes.None;
			note.Instrument = (byte)(((b[0] & 0x3) << 4) | (b[1] >> 4));
			note.VolumeEffect = VolumeEffects.None;
			note.VolumeParameter = 0;
			note.EffectByte = (byte)(b[1] & 0xf);
			note.Parameter = b[2];

			/* From mikmod: volume slide up always overrides slide down */
			if (note.EffectByte == 0xa && note.Parameter.HasAnyBitSet(0xf0))
				note.Parameter &= 0xf0;
			else if (note.EffectByte == 0x8)
				note.EffectByte = note.Parameter = 0;
			else if (note.EffectByte == 0xe)
			{
				switch (note.Parameter >> 4)
				{
					case 0x0:
					case 0x3:
					case 0x4:
					case 0x6:
					case 0x7:
					case 0xF:
						note.EffectByte = note.Parameter = 0;
						break;
					default:
						break;
				}
			}

			if ((note.EffectByte != 0) || (note.Parameter != 0))
				note.ImportMODEffect(note.EffectByte, note.Parameter, fromXM: false);
		}

		trackList.Add(track);
	}

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		if (stream.ReadString(3) != "MTM")
			throw new NotSupportedException();

		var song = new Song();

		int v = stream.ReadByte();

		song.TrackerID = $"MultiTracker {v >> 4}.{v & 0xF}";
		song.Title = stream.ReadString(20);

		int nTrk = stream.ReadStructure<short>();
		int nPat = stream.ReadByte();
		int nOrd = stream.ReadByte() + 1;

		int commentLen = stream.ReadStructure<short>();

		int nSmp = stream.ReadByte();

		stream.ReadByte(); /* attribute byte (unused) */

		int rows = stream.ReadByte(); /* beats per track (translation: number of rows in every pattern) */

		int todo = 0;

		if (rows != 64)
			todo |= 64;

		rows = Math.Min(rows, 64);

		int nChan = stream.ReadByte();

		if (stream.Position >= stream.Length)
			throw new FormatException();

		for (int n = 0; n < 32; n++)
		{
			int pan = stream.ReadByte() & 0xf;
			pan = Tables.ShortPanning(pan);
			pan *= 4; //mphack
			song.Channels[n].Panning = pan;
		}

		for (int n = nChan; n < Constants.MaxChannels; n++)
			song.Channels[n].Flags = ChannelFlags.Mute;

		/* samples */
		if (nSmp > Constants.MaxSamples)
			Log.Append(4, " Warning: Too many samples");

		for (int n = 1; n <= nSmp; n++)
		{
			var sample = song.EnsureSample(n);

			/*
			if (n > Constants.MaxSamples)
			{
				stream.Position += 37;
				continue;
			}
			*/

			/* IT truncates .mtm sample names at the first \0 rather than the normal behavior
			of presenting them as spaces (k-achaet.mtm has some "junk" in the sample text) */
			sample.Name = stream.ReadString(22);
			sample.Length = stream.ReadStructure<int>();
			sample.LoopStart = stream.ReadStructure<int>();
			sample.LoopEnd = stream.ReadStructure<int>();

			if ((sample.LoopEnd - sample.LoopStart) > 2)
				sample.Flags |= SampleFlags.Loop;
			else
			{
				/* Both Impulse Tracker and Modplug do this */
				sample.LoopStart = 0;
				sample.LoopEnd = 0;
			}

			// This does what OpenMPT does; it treats the finetune as Fasttracker
			// units, multiplied by 16 (which I believe makes them the same as MOD
			// but with a higher range)
			int finetune = stream.ReadByte();

			sample.C5Speed = SongNote.TransposeToFrequency(0, finetune * 16);
			sample.Volume = stream.ReadByte();
			sample.Volume *= 4; //mphack
			if (stream.ReadByte().HasBitSet(1))
			{
				sample.Flags |= SampleFlags._16Bit;
				sample.Length /= 2;
				sample.LoopStart /= 2;
				sample.LoopEnd /= 2;
			}

			sample.VibratoType = 0;
			sample.VibratoRate = 0;
			sample.VibratoDepth = 0;
			sample.VibratoSpeed = 0;
		}

		/* orderlist */
		byte[] orderListBytes = new byte[128];

		stream.ReadExactly(orderListBytes);

		for (int i = 0; i < nOrd; i++)
			song.OrderList.Add(orderListBytes[i]);
		song.OrderList.Add(SpecialOrders.Last);

		/* tracks */
		var trackDefinitions = new List<SongNote[]>();

		byte[] buffer = new byte[3 * rows];

		for (int n = 0; n < nTrk; n++)
		{
			stream.ReadExactly(buffer);

			UnpackTrack(buffer, trackDefinitions, rows);
		}

		/* patterns */
		if (nPat >= Constants.MaxPatterns)
			Log.Append(4, " Warning: Too many patterns");

		for (int pat = 0; pat <= nPat; pat++)
		{
			var pattern = song.GetPattern(pat, create: true, rowsInNewPattern: Math.Max(rows, 32))!;

			for (int chan = 1; chan <= 32; chan++)
			{
				int trk = stream.ReadStructure<short>();

				if (trk == 0)
					continue;
				else if (trk > nTrk)
					throw new FormatException();

				for (int n = 0; n < rows; n++)
					pattern.Rows[n][chan] = trackDefinitions[trk][n];
			}

			if (rows < 32)
			{
				/* stick a pattern break on the first channel with an empty effect column
				* (XXX don't do this if there's already one in another column) */
				for (int channel = 1; channel <= Constants.MaxChannels; channel++)
				{
					ref var note = ref pattern.Rows[rows - 1][channel];

					if ((note.EffectByte == 0) && (note.Parameter == 0))
					{
						note.Effect = Effects.PatternBreak;
						break;
					}
				}
			}
		}

		song.Message = ReadLinedMessage(stream, commentLen, 40);

		/* sample data */
		if (!lflags.HasAllFlags(LoadFlags.NoSamples))
		{
			for (int smp = 1; smp <= nSmp && smp <= Constants.MaxSamples; smp++)
			{
				var sample = song.EnsureSample(smp);

				if (sample.Length == 0)
					continue;

				SampleFileConverter.ReadSample(sample,
					SampleFormat.LittleEndian | SampleFormat.PCMUnsigned | SampleFormat.Mono
					| (sample.Flags.HasAllFlags(SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8),
					stream);
			}
		}

		/* set the rest of the stuff */
		song.Flags = SongFlags.ITOldEffects | SongFlags.CompatibleGXX;

		if (todo.HasBitSet(64))
			Log.Append(2, " TODO: test this file with other players (beats per track != 64)");

		/* done! */
		return song;
	}
}
