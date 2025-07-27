using System;
using System.IO;

namespace ChasmTracker.FileTypes.SongConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class STX : STM
{
	public override string Label => "STX";
	public override string Description => "ST Music Interface Kit";
	public override string Extension => ".stx";

	public override int SortOrder => 18;

	/* --------------------------------------------------------------------- */

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			stream.Position = 60;

			if (stream.ReadString(4) != "SCRM")
				return false;

			stream.Position = 20;

			byte[] id = new byte[8];

			stream.ReadExactly(id);

			for (int i = 0; i < 8; i++)
				if (id[i] < 0x20 || id[i] > 0x7E)
					return false;

			string title = stream.ReadString(20);

			file.Description = Description;
			/*file.Extension = str_dup("stx");*/
			file.Title = title;
			file.Type = FileTypes.ModuleMOD;

			return true;
		}
		catch
		{
			return false;
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

	enum S3ISampleFlags
	{
		None = 0,
		PCM = 1,
	}

	const int EOF = -1;

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{

		/* check the tag */
		stream.Position = 60;

		if (stream.ReadString(4) != "SCRM")
			throw new NotSupportedException();

		stream.Position = 20;

		byte[] id = new byte[8];

		stream.ReadExactly(id);

		for (int i = 0; i < 8; i++)
			if (id[i] < 0x20 || id[i] > 0x7E)
				throw new NotSupportedException();

		var song = new Song();

		/* read the title */
		stream.Position = 0;

		song.Title = stream.ReadString(20);

		stream.Position += 8;

		int firstPatternSize = stream.ReadStructure<short>();

		stream.Position += 2;

		int patternParapointersParapointer = stream.ReadStructure<short>();
		int sampleParapointersParapointer = stream.ReadStructure<short>();
		int channelsParapointer = stream.ReadStructure<short>();

		stream.Position += 4;

		song.InitialGlobalVolume = stream.ReadByte() << 1;

		int tempo = stream.ReadByte();

		song.InitialSpeed = ((tempo >> 4) != 0) ? (tempo >> 4) : 6;
		song.InitialTempo = ConvertSTMTempoToBPM(tempo);

		stream.Position += 4;

		int numPatterns = stream.ReadStructure<short>();
		int numSamples = stream.ReadStructure<short>();
		int numOrders = stream.ReadStructure<short>();

		// STX 1.0 modules sometimes have bugged sample counts...
		if (numSamples > 31)
			numSamples = 31;

		if (numOrders > Constants.MaxOrders || numSamples > Constants.MaxSamples || numPatterns > Constants.MaxPatterns)
			throw new FormatException();

		song.Flags = SongFlags.ITOldEffects | SongFlags.NoStereo;

		stream.Position = (channelsParapointer << 4) + 32;

		/* orderlist */
		for (int n = 0; n < numOrders; n++)
		{
			song.OrderList[n] = stream.ReadByte();
			stream.Position += 4;
		}

		/* load the parapointers */
		int[] sampleParapointers = new int[numSamples];

		stream.Position = sampleParapointersParapointer << 4;
		for (int i = 0; i < sampleParapointers.Length; i++)
			sampleParapointers[i] = stream.ReadStructure<short>();

		short[] patternParapointers = new short[numPatterns];

		stream.Position = patternParapointersParapointer << 4;
		for (int i = 0; i < patternParapointers.Length; i++)
			patternParapointers[i] = stream.ReadStructure<short>();

		/* samples */
		for (int n = 1; n <= numSamples; n++)
		{
			var sample = song.EnsureSample(n);

			stream.Position = sampleParapointers[n - 1] << 4;

			S3ISampleFlags type = (S3ISampleFlags)stream.ReadByte();

			sample.FileName = stream.ReadString(12).Replace('\xFF', ' ');

			byte[] b = new byte[3];

			stream.ReadExactly(b); // data pointer for pcm, irrelevant otherwise

			switch (type)
			{
				case S3ISampleFlags.PCM:
					sampleParapointers[n] = b[1] | (b[2] << 8) | (b[0] << 16);

					sample.Length = stream.ReadStructure<int>();
					sample.LoopStart = stream.ReadStructure<int>();
					sample.LoopEnd = stream.ReadStructure<int>();
					sample.Volume = stream.ReadByte() * 4; //mphack
					stream.Position += 2;
					int c = stream.ReadByte();  /* flags */
					if (c.HasBitSet(1))
						sample.Flags |= SampleFlags.Loop;
					break;

				default:
				case S3ISampleFlags.None:
					stream.Position += 12;
					sample.Volume = stream.ReadByte() * 4; //mphack
					stream.Position += 3;
					break;
			}

			sample.C5Speed = stream.ReadStructure<int>();
			stream.Position += 12;        /* unused space */
			sample.Name = stream.ReadString(25).Replace('\xFF', ' ');

			sample.VibratoType = 0;
			sample.VibratoRate = 0;
			sample.VibratoDepth = 0;
			sample.VibratoSpeed = 0;
			sample.GlobalVolume = 64;
		}

		int patternSize;
		int subversion = 1;

		if (firstPatternSize != 0x1A)
		{
			stream.Position = patternParapointers[0] << 4;

			// 1.0 files have pattern size before pattern data
			// which should match the header's specified size.

			patternSize = stream.ReadStructure<short>();

			// Amusingly, Purple Motion's "Future Brain" actually
			// specifies pattern size in the song header even though
			// the patterns themselves don't specify their size.
			if (patternSize == firstPatternSize)
				subversion = 0;
		}

		if (!lflags.HasFlag(LoadFlags.NoPatterns))
		{
			for (int n = 0; n < numPatterns; n++)
			{
				if (patternParapointers[n] == 0)
					continue;

				stream.Position = patternParapointers[n] << 4;

				if (subversion == 0)
					stream.Position += 2;

				var pattern = song.Patterns[n] = new Pattern(64);

				int row = 0;

				while (row < 64)
				{
					int mask = stream.ReadByte();
					int chn = (mask & 31);

					if (mask == EOF)
					{
						Log.Append(4, " Warning: Pattern {0}: file truncated", n);
						break;
					}

					if (mask == 0)
					{
						/* done with the row */
						row++;
						continue;
					}

					ref var note = ref pattern[row][chn];

					if (mask.HasBitSet(32))
					{
						/* note/instrument */
						note.Note = (byte)stream.ReadByte();
						note.Instrument = (byte)stream.ReadByte();
						//if (note.Instrument > 99)
						//      note.Instrument = 0;
						switch (note.Note)
						{
							default:
								// Note; hi=oct, lo=note
								note.Note = (byte)(((note.Note >> 4) + 2) * 12 + (note.Note & 0xf) + 13);
								break;
							case 255:
								note.Note = SpecialNotes.None;
								break;
							case 254:
								note.Note = SpecialNotes.NoteCut;
								break;
						}
					}

					if (mask.HasBitSet(64))
					{
						/* volume */
						note.VolumeEffect = VolumeEffects.Volume;
						note.VolumeParameter = (byte)stream.ReadByte();
						if (note.VolumeParameter == 255)
						{
							note.VolumeEffect = VolumeEffects.None;
							note.VolumeParameter = 0;
						}
						else if (note.VolumeParameter > 64)
						{
							note.VolumeParameter = 64;
						}
					}

					if (mask.HasBitSet(128))
					{
						note.Effect = STMEffects[stream.ReadByte() & 0xf];
						note.Parameter = (byte)stream.ReadByte();

						ImportSTMEffectParameter(ref note);
					}

					for (chn = 0; chn < 32; chn++)
					{
						ref var chanNote = ref pattern[row][chn + 1];

						if (chanNote.Effect == Effects.Speed)
						{
							int param = chanNote.Parameter;
							chanNote.Parameter >>= 4;
							HandleSTMTempoPattern(pattern, row, param);
						}
					}
					/* ... next note, same row */
				}
			}
		}

		/* sample data */
		if (!lflags.HasFlag(LoadFlags.NoSamples))
		{
			for (int n = 1; n <= numSamples; n++)
			{
				var sample = song.EnsureSample(n);

				if (sample.Length < 3)
					continue;

				stream.Position = sampleParapointers[n - 1] << 4;

				SampleFileConverter.ReadSample(sample, SampleFormat.LittleEndian | SampleFormat.PCMSigned | SampleFormat._8 | SampleFormat.Mono, stream);
			}
		}

		for (int n = 0; n < 4; n++)
			song.Channels[n].Panning = (n.HasBitSet(1) ? 64 : 0) * 4; //mphack
		for (int n = 4; n < 64; n++)
			song.Channels[n].Flags |= ChannelFlags.Mute;
		song.PanSeparation = 64;

		song.TrackerID = $"ST Music Interface Kit (1.{subversion})";

		/* done! */
		return song;
	}
}