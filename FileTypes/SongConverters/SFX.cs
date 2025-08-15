using System;
using System.IO;
using System.Linq;

namespace ChasmTracker.FileTypes.SongConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class SFX : SongFileConverter
{
	public override string Label => "SFX";
	public override string Description => "SoundFX";
	public override string Extension => ".sfx";

	public override int SortOrder => 17;

	/* --------------------------------------------------------------------------------------------------------- */

	/* None of the sfx files on Modland are of the 31-instrument type that xmp recognizes.
	However, there are a number of 31-instrument files with a different tag, under "SoundFX 2". */

	class SFXFormat
	{
		public int TagPosition;
		public string Tag;
		public int NumSamples;
		public int Dunno;
		public string TrackerID;

		public SFXFormat(int tagPosition, string tag, int numSamples, int dunno, string trackerID)
		{
			TagPosition = tagPosition;
			Tag = tag;
			NumSamples = numSamples;
			Dunno = dunno;
			TrackerID = trackerID;
		}
	}

	static readonly SFXFormat[] Formats =
		{
			new SFXFormat(124, "SO31", 31, 4, "SoundFX 2"),
			new SFXFormat(124, "SONG", 31, 0, "SoundFX 2 (?)"),
			new SFXFormat( 60, "SONG", 15, 0, "SoundFX"),
		};

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		foreach (var format in Formats)
		{
			stream.Position = format.TagPosition;

			if (stream.ReadString(format.Tag.Length) == format.Tag)
			{
				file.Description = format.TrackerID;
				file.Title = ""; // whatever
				file.Type = FileSystem.FileTypes.ModuleMOD;

				return true;
			}
		}

		return false;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	/* Loader taken mostly from XMP.

	Why did I write a loader for such an obscure format? That is, besides the fact that neither Modplug nor
	Mikmod support SFX (and for good reason; it's a particularly dumb format) */

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		uint effectWarn = 0;

		SFXFormat? fmt = null;

		foreach (var format in Formats)
		{
			stream.Position = format.TagPosition;

			if (stream.ReadString(format.Tag.Length) == format.Tag)
			{
				fmt = format;
				break;
			}
		}

		if (fmt == null)
			throw new NotSupportedException();

		stream.Position = 0;

		int[] sampleSize = new int[fmt.NumSamples];

		for (int i = 0; i < sampleSize.Length; i++)
			sampleSize[i] = ByteSwap.Swap(stream.ReadStructure<int>());

		stream.Position += 4; /* the tag again */

		int tmp = stream.ReadStructure<short>();

		tmp = 14565 * 122 / ByteSwap.Swap(tmp);

		var song = new Song();

		song.InitialTempo = tmp.Clamp(31, 255);

		stream.Position += 14; /* unknown bytes (reserved?) - see below */

		if (lflags.HasAllFlags(LoadFlags.NoSamples))
			stream.Position += 30 * fmt.NumSamples;
		else
		{
			for (int n = 0; n < fmt.NumSamples; n++)
			{
				var sample = song.EnsureSample(n + 1);

				sample.Name = stream.ReadString(22);

				tmp = ByteSwap.Swap(stream.ReadStructure<short>()); /* seems to be half the sample size, minus two bytes? */

				sample.Length = sampleSize[n];

				sample.C5Speed = Tables.MODFineTune(stream.ReadByte()); // ?
				sample.Volume = stream.ReadByte();
				if (sample.Volume > 64)
					sample.Volume = 64;
				sample.Volume *= 4; //mphack
				sample.GlobalVolume = 64;
				sample.LoopStart = ByteSwap.Swap(stream.ReadStructure<ushort>());
				tmp = ByteSwap.Swap(stream.ReadStructure<ushort>()) * 2; /* loop length */
				if (tmp > 2)
				{
					sample.LoopEnd = sample.LoopStart + tmp;
					sample.Flags |= SampleFlags.Loop;
				}
				else
					sample.LoopStart = sample.LoopEnd = 0;
			}
		}

		/* pattern/order stuff */
		int numOrders = Math.Min(stream.ReadByte(), 127);
		int restart = stream.ReadByte();

		byte[] orderListBytes = new byte[numOrders];

		stream.ReadExactly(orderListBytes);

		for (int i = 0; i < orderListBytes.Length; i++)
			song.OrderList.Add(orderListBytes[i]);

		stream.Position += 128 - numOrders;

		int numPatterns = song.OrderList.Max() + 1;

		song.OrderList.Add(SpecialOrders.Last);

		/* Not sure what this is about, but skipping a few bytes here seems to make SO31's load right.
		(they all seem to have zero here) */
		stream.Position += fmt.Dunno;

		if (lflags.HasAllFlags(LoadFlags.NoPatterns))
			stream.Position += numPatterns * 1024;
		else
		{
			byte[] p = new byte[4];

			for (int pat = 0; pat < numPatterns; pat++)
			{
				var pattern = song.GetPattern(pat, create: true)!;

				for (int n = 0; n < 64; n++)
				{
					for (int chan = 0; chan < 4; chan++)
					{
						ref var note = ref pattern.Rows[n][chan + 1];

						stream.ReadExactly(p);

						note.ImportMODNote(p);

						/* Note events starting with FF all seem to be special in some way:
							bytes   apparent use    example file on modland
							-----   ------------    -----------------------
							FF FE   note cut        1st intro.sfx
							FF FD   unknown!        another world (intro).sfx
							FF FC   pattern break   orbit wanderer.sfx2 */
						if (p[0] == 0xff)
						{
							switch (p[1])
							{
								case 0xfc:
									note.Note = SpecialNotes.None;
									note.Instrument = 0;
									// stuff a C00 in channel 5
									pattern.Rows[n][5].Effect = Effects.PatternBreak;
									break;
								case 0xfe:
									note.Note = SpecialNotes.NoteCut;;
									note.Instrument = 0;
									break;
							}
						}
						switch (note.EffectByte)
						{
							case 0:
								break;
							case 1: /* arpeggio */
								note.Effect = Effects.Arpeggio;
								break;
							case 2: /* pitch bend */
								if ((note.Parameter >> 4) != 0)
								{
									note.Effect = Effects.PortamentoDown;
									note.Parameter >>= 4;
								}
								else if ((note.Parameter & 0xf) != 0)
								{
									note.Effect = Effects.PortamentoUp;
									note.Parameter &= 0xf;
								}
								else
									note.Effect = 0;

								break;
							case 5: /* volume up */
								note.Effect = Effects.VolumeSlide;
								note.Parameter = (byte)((note.Parameter & 0xf) << 4);
								break;
							case 6: /* set volume */
								if (note.Parameter > 64)
									note.Parameter = 64;
								note.VolumeEffect = VolumeEffects.Volume;
								note.VolumeParameter = (byte)(64 - note.Parameter);
								note.Effect = 0;
								note.Parameter = 0;
								break;
							case 3: /* LED on (wtf!) */
							case 4: /* LED off (ditto) */
							case 7: /* set step up */
							case 8: /* set step down */
							default:
								effectWarn |= (uint)(1 << note.EffectByte);
								note.Effect = Effects.Unimplemented;
								break;
						}
					}
				}
			}

			for (int n = 0; n < 16; n++)
				if (effectWarn.HasBitSet(1 << n))
					Log.Append(4, " Warning: Unimplemented effect {0:X}xx", n);

			if (restart < numPatterns)
				song.InsertRestartPos(restart);
		}

		/* sample data */
		if (!lflags.HasAllFlags(LoadFlags.NoSamples))
		{
			for (int n = 0; n < fmt.NumSamples; n++)
			{
				var sample = song.EnsureSample(n + 1);

				if (sample.Length <= 2)
					continue;

				SampleFileConverter.ReadSample(sample, SampleFormat._8 | SampleFormat.LittleEndian | SampleFormat.PCMSigned | SampleFormat.Mono, stream);
			}
		}

		/* more header info */
		song.Flags = SongFlags.ITOldEffects | SongFlags.CompatibleGXX;
		for (int n = 0; n < 4; n++)
			song.Channels[n].Panning = Tables.ProTrackerPanning(n); /* ??? */
		for (int n = 4; n < Constants.MaxChannels; n++)
			song.Channels[n].Flags = ChannelFlags.Mute;

		song.TrackerID = fmt.TrackerID;
		song.PanSeparation = 64;

		/* done! */
		return song;
	}


	/*
	most of modland's sfx files have all zeroes for those 14 "unknown" bytes, with the following exceptions:

	64 00 00 00 00 00 00 00 00 00 00 00 00 00  d............. - unknown/antitrax.sfx
	74 63 68 33 00 00 00 00 00 00 00 00 00 00  tch3.......... - unknown/axel f.sfx
	61 6c 6b 00 00 00 00 00 00 00 00 00 00 00  alk........... Andreas Hommel/cyberblast-intro.sfx
	21 00 00 00 00 00 00 00 00 00 00 00 00 00  !............. - unknown/dugger.sfx
	00 00 00 00 00 0d 00 00 00 00 00 00 00 00  .............. Jean Baudlot/future wars - time travellers - dugger (title).sfx
	00 00 00 00 00 00 00 00 0d 00 00 00 00 00  .............. Jean Baudlot/future wars - time travellers - escalator.sfx
	6d 65 31 34 00 00 00 00 00 00 00 00 00 00  me14.......... - unknown/melodious.sfx
	0d 0d 0d 53 46 58 56 31 2e 38 00 00 00 00  ...SFXV1.8.... AM-FM/sunday morning.sfx
	61 6c 6b 00 00 00 00 00 00 00 00 00 00 00  alk........... - unknown/sunday morning.sfx
	6f 67 00 00 00 00 00 00 00 00 00 00 00 00  og............ Philip Jespersen/supaplex.sfx
	6e 74 20 73 6f 6e 67 00 00 00 00 00 00 00  nt song....... - unknown/sweety.sfx
	61 6c 6b 00 00 00 00 00 00 00 00 00 00 00  alk........... - unknown/thrust.sfx
	*/
}