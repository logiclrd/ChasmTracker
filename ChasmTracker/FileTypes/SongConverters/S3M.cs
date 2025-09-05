using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.SongConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.FileTypes.SampleConverters;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class S3M : SongFileConverter
{
	public override string Label => "S3M";
	public override string Description => "Scream Tracker 3";
	public override string Extension => ".s3m";

	public override bool CanSave => true;

	public override int SortOrder => 2;
	public override int SaveOrder => 2;

	/* --------------------------------------------------------------------- */

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		long startOffset = stream.Position;

		try
		{
			stream.Position = startOffset + 44;

			if (stream.ReadString(4) != "SCRM")
				return false;

			stream.Position = startOffset;

			string title = stream.ReadString(27);

			file.Description = "Scream Tracker 3";
			/*file->extension = str_dup("s3m");*/
			file.Title = title;
			file.Type = FileSystem.FileTypes.ModuleS3M;

			return true;
		}
		catch
		{
			return false;
		}
		finally
		{
			try
			{
				stream.Position = startOffset;
			}
			catch {}
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

	enum S3MInstrumentType
	{
		None = 0,
		PCM = 1,
		Admel = 2,

		Control = 0xFF, // only internally used for saving
	}

	/* misc flags for loader (internal) */
	[Flags]
	enum LoaderFlags
	{
		Unsigned = 1,
		ChanPan = 2, // the FC byte
	}

	[Flags]
	enum S3MNoteMask : byte
	{
		EndOfRow = 0,

		ChannelNumberMask = 0b0000011111,
		NoteAndInstrument = 0b0000100000,
		Volume            = 0b0001000000,
		Effect            = 0b0010000000,
	}

	bool ImportEditTime(Song song, ushort trkVers, uint reserved32)
	{
		if (song.History.Any())
			return false; // ?

		var runtime = DateTimeConversions.DecodeEditTimer(trkVers, reserved32);

		song.History.Add(new SongHistory() { Runtime = DateTimeConversions.DOSTimeToTimeSpan(runtime) });

		return true;
	}

	const int EOF = -1;

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		long startOffset = stream.Position;

		var misc = LoaderFlags.Unsigned | LoaderFlags.ChanPan; // temporary flags, these are both generally true

		/* check the tag */
		stream.Position = startOffset + 44;
		if (stream.ReadString(4) != "SCRM")
			throw new NotSupportedException();

		var song = new Song();

		/* read the title */
		stream.Position = startOffset;

		song.Title = stream.ReadString(28);

		/* skip the supposed-to-be-0x1a byte,
		the tracker ID, and the two useless reserved bytes */
		stream.Position += 4;

		int nOrd = stream.ReadStructure<short>();
		int nSmp = stream.ReadStructure<short>();
		int nPat = stream.ReadStructure<short>();

		if (nOrd > Constants.MaxOrders || nSmp > Constants.MaxSamples || nPat > Constants.MaxPatterns)
			throw new FormatException();

		song.Flags = SongFlags.ITOldEffects;

		var flags = stream.ReadStructure<short>(); /* flags (don't really care) */

		var trackerVersion = stream.ReadStructure<ushort>();

		var fileFormatInfo = stream.ReadStructure<short>();

		if (fileFormatInfo == 1)
			misc &= ~LoaderFlags.Unsigned;     /* signed samples (ancient s3m) */

		stream.Position += 4; /* skip the tag */

		song.InitialGlobalVolume = stream.ReadByte() << 1;

		// In the case of invalid data, ST3 uses the speed/tempo value that's set in the player prior to
		// loading the song, but that's just crazy.
		song.InitialSpeed = stream.ReadByte();
		if (song.InitialSpeed == 0)
			song.InitialSpeed = 6;

		song.InitialTempo = stream.ReadByte();
		if (song.InitialTempo <= 32)
		{
			// (Yes, 32 is ignored by Scream Tracker.)
			song.InitialTempo = 125;
		}

		song.MixingVolume = stream.ReadByte();

		int mixVolume = song.MixingVolume; /* detect very old modplug tracker */

		if (song.MixingVolume.HasBitSet(0x80))
			song.MixingVolume ^= 0x80;
		else
			song.Flags |= SongFlags.NoStereo;

		int ultraClickRemoval = stream.ReadByte(); /* ultraclick removal (useless) */

		if (stream.ReadByte() != 0xfc)
			misc &= ~LoaderFlags.ChanPan;     /* stored pan values */

		var reserved16low = stream.ReadStructure<ushort>(); // low 16 bits of version info -- schism & openmpt version info
		var reserved32 = stream.ReadStructure<uint>(); // Impulse Tracker edit time
		var reserved16high = stream.ReadStructure<ushort>(); // high 16 bits of version info -- schism version info

		stream.Position -= 8;

		string reserved = stream.ReadString(8);

		int special = stream.ReadStructure<short>(); // field not used by st3

		/* channel settings */
		byte[] channelTypes = new byte[32];

		stream.ReadExactly(channelTypes);

		uint adlib = 0; // bitset

		for (int n = 0; n < 32; n++)
		{
			/* Channel 'type': 0xFF is a disabled channel, which shows up as (--) in ST3.
			Any channel with the high bit set is muted.
			00-07 are L1-L8, 08-0F are R1-R8, 10-18 are adlib channels A1-A9.
			Hacking at a file with a hex editor shows some perhaps partially-implemented stuff:
			types 19-1D show up in ST3 as AB, AS, AT, AC, and AH; 20-2D are the same as 10-1D
			except with 'B' insted of 'A'. None of these appear to produce any sound output,
			apart from 19 which plays adlib instruments briefly before cutting them. (Weird!)
			Also, 1E/1F and 2E/2F display as "??"; and pressing 'A' on a disabled (--) channel
			will change its type to 1F.
			Values past 2F seem to display bits of the UI like the copyright and help, strange!
			These out-of-range channel types will almost certainly hang or crash ST3 or
			produce other strange behavior. Simply put, don't do it. :) */
			int c = channelTypes[n];
			if (c.HasBitSet(0x80))
			{
				song.Channels[n].Flags |= ChannelFlags.Mute;
				// ST3 doesn't even play effects in muted channels -- throw them out?
				c &= ~0x80;
			}

			if (c < 0x08)
			{
				// L1-L8 (panned to 3 in ST3)
				song.Channels[n].Panning = 14;
			}
			else if (c < 0x10)
			{
				// R1-R8 (panned to C in ST3)
				song.Channels[n].Panning = 50;
			}
			else if (c < 0x19)
			{
				// A1-A9
				song.Channels[n].Panning = 32;
				adlib |= 1u << n;
			}
			else
			{
				// Disabled 0xff/0x7f, or broken
				song.Channels[n].Panning = 32;
				song.Channels[n].Flags |= ChannelFlags.Mute;
			}
			song.Channels[n].Volume = 64;
		}

		for (int n = 32; n < song.Channels.Length; n++)
		{
			song.Channels[n].Panning = 32;
			song.Channels[n].Volume = 64;
			song.Channels[n].Flags = ChannelFlags.Mute;
		}

		// Schism Tracker before 2018-11-12 played AdLib instruments louder than ST3. Compensate by lowering the sample mixing volume.
		if ((adlib != 0) && trackerVersion >= 0x4000 && trackerVersion < 0x4D33)
			song.MixingVolume = song.MixingVolume * 2274 / 4096;

		/* orderlist */
		byte[] orderList = new byte[nOrd];

		stream.ReadExactly(orderList);

		foreach (int patternNumber in orderList)
			song.OrderList.Add(patternNumber);

		song.OrderList.Add(SpecialOrders.Last);

		/* load the parapointers */
		ushort[] paraSamples = new ushort[nSmp];
		long[] paraSampleData = new long[nSmp];
		SampleFormat[] sampleFormats = new SampleFormat[nSmp];
		ushort[] paraPatterns = new ushort[nPat];

		for (int i = 0; i < nSmp; i++)
			paraSamples[i] = stream.ReadStructure<ushort>();
		for (int i = 0; i < nPat; i++)
			paraPatterns[i] = stream.ReadStructure<ushort>();

		/* default pannings */
		if (misc.HasAllFlags(LoaderFlags.ChanPan))
		{
			for (int n = 0; n < 32; n++)
			{
				int pan = stream.ReadByte();

				if (pan.HasBitSet(0x20) && (!adlib.HasBitSet(1 << n) || trackerVersion > 0x1320))
					song.Channels[n].Panning = ((pan & 0xf) * 64) / 15;
			}
		}

		//mphack - fix the pannings
		for (int n = 0; n < Constants.MaxChannels; n++)
			song.Channels[n].Panning *= 4;

		/* samples */
		bool anySamples = false;
		ushort gusAddresses = 0;

		for (int n = 0; n < nSmp; n++)
		{
			var sample = song.EnsureSample(n + 1);

			stream.Position = startOffset + (paraSamples[n] << 4);

			var type = (S3IType)stream.ReadByte();

			sample.FileName = stream.ReadString(12);

			byte[] dataPointerBytes = new byte[4];

			stream.ReadExactly(dataPointerBytes.Slice(0, 3)); // data pointer for pcm, irrelevant otherwise

			// wat
			int dataPointer = dataPointerBytes[1] | (dataPointerBytes[2] << 8) | (dataPointerBytes[0] << 16);

			switch (type)
			{
				case S3IType.PCM:
					paraSampleData[n] = dataPointer;

					sample.Length = stream.ReadStructure<int>();
					sample.LoopStart = stream.ReadStructure<int>();
					sample.LoopEnd = stream.ReadStructure<int>();
					sample.Volume = stream.ReadByte() * 4; //mphack
					stream.ReadByte();      /* unused byte */
					stream.ReadByte();      /* packing info (never used) */
					S3IFormatFlags c = stream.ReadStructure<S3IFormatFlags>();  /* flags */
					if (c.HasAllFlags(S3IFormatFlags.Loop))
						sample.Flags |= SampleFlags.Loop;
					sampleFormats[n] = SampleFormat.LittleEndian
						| (misc.HasAllFlags(LoaderFlags.Unsigned) ? SampleFormat.PCMUnsigned : SampleFormat.PCMSigned)
						| (c.HasAllFlags(S3IFormatFlags._16Bit) ? SampleFormat._16 : SampleFormat._8)
						| (c.HasAllFlags(S3IFormatFlags.Stereo) ? SampleFormat.StereoSplit : SampleFormat.Mono);
					if (sample.Length != 0)
						anySamples = true;
					break;

				default:
				//Console.WriteLine("s3m: mystery-meat sample type {0}\n", type);
				//goto case S3IType.None;
				case S3IType.None:
					stream.Position += 12;
					sample.Volume = stream.ReadByte() * 4; //mphack
					stream.Position += 3;
					break;

				case S3IType.AdMel:
					sample.AdLibBytes = new byte[12];

					stream.ReadExactly(sample.AdLibBytes);

					sample.Volume = stream.ReadByte() * 4; //mphack

					// next byte is "dsk", what is that?
					stream.Position += 3;
					sample.Flags |= SampleFlags.AdLib;
					// dumb hackaround that ought to some day be fixed:
					sample.Length = 1;
					sample.AllocateData();
					break;
			}

			sample.C5Speed = stream.ReadStructure<int>();

			if (type == S3IType.AdMel)
			{
				if (sample.C5Speed < 1000 || sample.C5Speed > 0xFFFF)
					sample.C5Speed = 8363;
			}

			stream.Position += 4;         /* unused space */

			var gusAddress = stream.ReadStructure<ushort>();

			gusAddresses |= gusAddress;

			stream.Position += 6;

			sample.Name = stream.ReadString(25);

			sample.VibratoType = 0;
			sample.VibratoRate = 0;
			sample.VibratoDepth = 0;
			sample.VibratoSpeed = 0;
			sample.GlobalVolume = 64;
		}

		/* sample data */
		if (!lflags.HasAllFlags(LoadFlags.NoSamples))
		{
			for (int n = 0; n < nSmp; n++)
			{
				var sample = song.EnsureSample(n + 1);

				if ((sample.Length == 0) || sample.Flags.HasAllFlags(SampleFlags.AdLib))
					continue;

				stream.Position = startOffset + (paraSampleData[n] << 4);

				SampleFileConverter.ReadSample(sample, sampleFormats[n], stream);
			}
		}

		// Mixing volume is not used with the GUS driver; relevant for PCM + OPL tracks
		if (gusAddresses > 1)
			song.MixingVolume = 48;

		if (!lflags.HasAllFlags(LoadFlags.NoPatterns))
		{
			for (int n = 0; n < nPat; n++)
			{
				if (paraPatterns[n] == 0)
					continue;

				stream.Position = startOffset + (paraPatterns[n] << 4);

				long end = stream.Position + stream.ReadStructure<ushort>() + 2;

				var pattern = song.GetPattern(n, create: true, rowsInNewPattern: 64)!;

				int row = 0;

				while (row < 64 && stream.Position < end)
				{
					int maskByte = stream.ReadByte();

					if (maskByte == EOF)
					{
						Log.Append(4, " Warning: Pattern {0}: file truncated", n);
						break;
					}

					var mask = (S3MNoteMask)maskByte;

					if (mask == S3MNoteMask.EndOfRow)
					{
						/* done with the row */
						row++;
						continue;
					}

					int chn = (int)(mask & S3MNoteMask.ChannelNumberMask);

					ref var note = ref pattern.Rows[row][chn + 1];

					if (mask.HasAllFlags(S3MNoteMask.NoteAndInstrument))
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
								note.Note = (byte)((note.Note >> 4) * 12 + (note.Note & 0xf) + 13);
								break;
							case 255:
								note.Note = SpecialNotes.None;
								break;
							case 254:
								note.Note = adlib.HasBitSet(1 << chn) ? SpecialNotes.NoteOff : SpecialNotes.NoteCut;
								break;
						}
					}

					if (mask.HasAllFlags(S3MNoteMask.Volume))
					{
						/* volume */
						note.VolumeEffect = VolumeEffects.Volume;
						note.VolumeParameter = (byte)stream.ReadByte();

						if (note.VolumeParameter == 255)
						{
							note.VolumeEffect = VolumeEffects.None;
							note.VolumeParameter = 0;
						}
						else if (note.VolumeParameter >= 128 && note.VolumeParameter <= 192)
						{
							// ModPlug (or was there any earlier tracker using this command?)
							note.VolumeEffect = VolumeEffects.Panning;
							note.VolumeParameter -= 128;
						}
						else if (note.VolumeParameter > 64)
						{
							// some weirdly saved s3m?
							note.VolumeParameter = 64;
						}
					}

					if (mask.HasAllFlags(S3MNoteMask.Effect))
					{
						note.EffectByte = (byte)stream.ReadByte();
						note.Parameter = (byte)stream.ReadByte();

						EffectUtility.ImportS3MEffect(ref note, fromIT: false);

						if (note.Effect == Effects.Special)
						{
							// mimic ST3's SD0/SC0 behavior
							if (note.Parameter == 0xd0)
							{
								note.Note = SpecialNotes.None;
								note.Instrument = 0;
								note.VolumeEffect = VolumeEffects.None;
								note.VolumeParameter = 0;
								note.Effect = Effects.None;
								note.Parameter = 0;
							}
							else if (note.Parameter == 0xc0)
							{
								note.Effect = Effects.None;
								note.Parameter = 0;
							}
							else if ((note.Parameter & 0xf0) == 0xa0)
							{
								// Convert the old messy SoundBlaster stereo control command (or an approximation of it, anyway)
								int ctype = channelTypes[chn] & 0x7f;

								if (gusAddresses > 1 || ctype >= 0x10)
									note.Effect = Effects.None;
								else if (note.Parameter == 0xa0 || note.Parameter == 0xa2)  // Normal panning
									note.Parameter = ctype.HasBitSet(8) ? (byte)0x8c : (byte)0x83;
								else if (note.Parameter == 0xa1 || note.Parameter == 0xa3)  // Swap left / right channel
									note.Parameter = ctype.HasBitSet(8) ? (byte)0x83 : (byte)0x8c;
								else if (note.Parameter <= 0xa7)  // Center
									note.Parameter = 0x88;
								else
									note.Effect = Effects.None;
							}
						}
					}
					/* ... next note, same row */
				}
			}
		}

		/* MPT identifies as ST3.20 in the trackerVersion field, but it puts zeroes for the 'special' field, only ever
		* sets flags 0x10 and 0x40, writes multiples of 16 orders, always saves channel pannings, and writes
		* zero into the ultraclick removal field. (ST3.2x always puts either 16, 24, or 32 there, older versions put 0).
		* Velvet Studio also pretends to be ST3, but writes zeroes for 'special'. ultraclick, and flags, and
		* does NOT save channel pannings. Also, it writes a fairly recognizable LRRL pattern for the channels,
		* but I'm not checking that. (yet?) */

		string? trackerID = null;

		if (trackerVersion == 0x1320)
		{
			if (reserved == "SCLUB2.0")
				trackerID = "Sound Club 2";
			else if (special == 0 && ultraClickRemoval == 0 && !flags.HasAnyBitSet(~0x50)
					&& (misc == (LoaderFlags.Unsigned | LoaderFlags.ChanPan)) && (nOrd % 16) == 0)
			{
				/* from OpenMPT:
				* MPT 1.0 alpha5 doesn't set the stereo flag, but MPT 1.0 alpha6 does. */

				trackerID = ((mixVolume & 0x80) != 0)
					? "ModPlug Tracker / OpenMPT 1.17"
					: "ModPlug Tracker 1.0 alpha";
			}
			else if (special == 0 && ultraClickRemoval == 0 && flags == 0 && misc == LoaderFlags.Unsigned)
			{
				if (song.InitialGlobalVolume == 128 && mixVolume == 48)
					trackerID = "PlayerPRO";
				else  // Always stereo
					trackerID = "Velvet Studio";
			}
			else if (special == 0 && ultraClickRemoval == 0 && flags == 8 && misc == LoaderFlags.Unsigned)
				trackerID = "Impulse Tracker < 1.03";  // Not sure if 1.02 saves like this as I don't have it
			else if (ultraClickRemoval != 16 && ultraClickRemoval != 24 && ultraClickRemoval != 32)
				trackerID = "Unknown tracker"; // sure isn't scream tracker

			if (trackerID == null)
			{
				switch (trackerVersion >> 12) {
					case 0:
						if (trackerVersion == 0x0208)
							trackerID = "Akord";
						break;
					case 1:
						if (gusAddresses > 1)
							trackerID = "Scream Tracker {0}.{1:x2} (GUS)";
						else if (gusAddresses == 1 || !anySamples || trackerVersion == 0x1300)
							trackerID = "Scream Tracker {0}.{1:x2} (SB)"; // could also be a GUS file with a single sample
						else
						{
							trackerID = "Unknown tracker";

							if (trackerVersion == 0x1301 && ultraClickRemoval == 0)
							{
								if (!flags.HasAnyBitSet(~0x50) && mixVolume.HasBitSet(0x80) && misc.HasAllFlags(LoaderFlags.ChanPan))
									trackerID = "UNMO3";
								else if ((flags == 0) && song.InitialGlobalVolume == 96 && mixVolume == 176 && song.InitialTempo == 150 && !misc.HasAllFlags(LoaderFlags.ChanPan))
									trackerID = "deMODifier";  // SoundSmith to S3M converter
								else if ((flags == 0) && song.InitialGlobalVolume == 128 && song.InitialSpeed == 6 && song.InitialTempo == 125 && !misc.HasAllFlags(LoaderFlags.ChanPan))
									trackerID = "Kosmic To-S3M";  // MTM to S3M converter by Zab/Kosmic
							}
						}
						break;
					case 2:
						if (trackerVersion == 0x2013) // PlayerPRO on Intel forgets to byte-swap the tracker ID bytes
							trackerID = "PlayerPRO";
						else
							trackerID = "Imago Orpheus {0}.{1:x2}";
						break;
					case 3:
						if (trackerVersion <= 0x3214)
							trackerID = "Impulse Tracker {0}.{1:x2}";
						else if (trackerVersion == 0x3320)
							trackerID = "Impulse Tracker 1.03";  // Could also be 1.02, maybe? I don't have that one
						else if (trackerVersion >= 0x3215 && trackerVersion <= 0x3217)
						{
							switch (trackerVersion - 0x3215)
							{
								case 0:
									trackerID = "Impulse Tracker 2.14p1-2";
									break;
								case 1:
									trackerID = "Impulse Tracker 2.14p3";
									break;
								case 2:
									trackerID = "Impulse Tracker 2.14p4-5";
									break;
							}
						}

						if (trackerVersion >= 0x3207 && trackerVersion <= 0x3217 && (reserved32 != 0))
							 ImportEditTime(song, trackerVersion, reserved32);

						break;
					case 4:
						if (trackerVersion == 0x4100)
							trackerID = "BeRoTracker";
						else
						{
							uint fullVersion = (((uint)reserved16high) << 16) | (reserved16low);

							trackerID = "Schism Tracker " + Version.DecodeCreatedWithTrackerVersion(trackerVersion, fullVersion);

							if (trackerVersion == 0x4fff && fullVersion >= Version.MKTime(2024, 11, 24))
								ImportEditTime(song, 0x0000, reserved32);
						}
						break;
					case 5:
						/* from OpenMPT src:
						*
						* Liquid Tracker's ID clashes with OpenMPT's.
						* OpenMPT started writing full version information with OpenMPT 1.29 and later changed the ultraClicks value from 8 to 16.
						* Liquid Tracker writes an ultraClicks value of 16.
						* So we assume that a file was saved with Liquid Tracker if the reserved fields are 0 and ultraClicks is 16. */
						if ((trackerVersion >> 8) == 0x57)
							trackerID = "NESMusa {0}.{X}"; /* tool by Bisquit */
						else if ((reserved16low == 0) && ultraClickRemoval == 16 && channelTypes[1] != 1)
							trackerID = "Liquid Tracker {0}.{X}";
						else if (trackerVersion == 0x5447)
							trackerID = "Graoumf Tracker";
						else if (trackerVersion >= 0x5129 && (reserved16low != 0))
						{
							/* e.x. 1.29.01.12 <-> 0x1290112 */
							uint ver = (((trackerVersion & 0xfffu) << 16) | reserved16low);

							trackerID = string.Format("OpenMPT {0}.{1:X2}.{2:X2}.{3:X2}", ver >> 24, (ver >> 16) & 0xFF, (ver >> 8) & 0xFF, ver & 0xFF);

							if (ver >= 0x01320031u)
								ImportEditTime(song, 0x0000, reserved32);
						}
						else
							trackerID = "OpenMPT {0}.{X2}";
						break;
					case 6:
						trackerID = "BeRoTracker";
						break;
					case 7:
						trackerID = "CreamTracker";
						break;
					case 12:
						if (trackerVersion == 0xCA00)
							trackerID = "Camoto";
						break;
					default:
						break;
				}
			}
		}

		if (trackerID != null)
			song.TrackerID = string.Format(trackerID, (trackerVersion & 0xF00) >> 8, trackerVersion & 0xFF);

		//      if (ferror(stream)) {
		//              return LOAD_FILE_ERROR;
		//      }
		/* done! */

		return song;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	/* IT displays some of these slightly differently
	most notably "Only 100 patterns supported" which doesn't follow the general pattern,
	and the channel limits (IT entirely refuses to save data in channels > 16 at all).
	Also, the AdLib and sample count warnings of course do not exist in IT at all. */

	[Flags]
	enum Warnings
	{
		None = 0,

		[Description("Over 100 patterns")]
		MaxPatterns = 1,
		[Description("Channel volumes")]
		ChannelVolume = 2,
		[Description("Linear slides")]
		LinearSlides = 4,
		[Description("Sample volumes")]
		SampleVolume = 8,
		[Description("Sustain and Ping Pong loops")]
		Loops = 16,
		[Description("Sample vibrato")]
		SampleVibrato = 32,
		[Description("Instrument functions")]
		Instruments = 64,
		[Description("Pattern lengths other than 64 rows")]
		PatternLength = 128,
		[Description("Data outside 32 channels")]
		MaxChannels = 256,
		[Description("Over 16 PCM channels")]
		MaxPCM = 512,
		[Description("Over 9 AdLib channels")]
		MaxAdLib = 1024,
		[Description("AdLib and PCM in the same channel")]
		PCMAdLibMix = 2048,
		[Description("Data in muted channels")]
		Muted = 4096,
		[Description("Notes outside the range C-1 to B-8")]
		NoteRange = 8192,
		[Description("Extended volume column effects")]
		VolumeEffects = 16384,
		[Description("Over 99 samples")]
		MaxSamples = 32768,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	struct S3MHeader
	{
		public S3MHeader() { }

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 28)]
		public string Title = "";
		public byte EOF; // 0x1a
		public byte Type; // 16
		public short Reserved0;
		public short OrdNum, SmpNum, PatNum; // ordnum should be even
		public ushort Flags; // 0
		public ushort CreatedWithTrackerVersion; // 0x4nnn
		public ushort SampleFormat; // 2 for unsigned
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
		public string SCRM = "SCRM";
		public byte GlobalVolume; // half range of IT
		public byte InitialSpeed;
		public byte InitialTempo;
		public byte MasterVolume;
		public byte UltraClick; // should be 8/12/16
		public byte DP = 252;
		public ushort Reserved1; // extended version information is stored here
		public uint Reserved2; // Impulse Tracker hides its edit timer here
		public ushort Reserved3; // high bits of extended version information
		public ushort Reserved4;
	}

	const S3IType S3ITypeControl = unchecked((S3IType)(-1));

	static Warnings WritePattern(Song song, int patternNumber, S3IType[] chanTypes, ushort[] paraPatterns, Stream stream)
	{
		var warn = Warnings.None;

		var pat = song.GetPattern(patternNumber, false);

		if ((pat == null) || pat.IsEmpty)
		{
			// easy!
			paraPatterns[patternNumber] = 0;
			return Warnings.None;
		}

		if (pat.Rows.Count != 64)
			warn |= Warnings.PatternLength;

		int rows = Math.Min(64, pat.Rows.Count);

		stream.WriteAlign(16);

		long start = stream.Position;

		paraPatterns[patternNumber] = (ushort)(start >> 4);

		// write a bogus length for now...
		stream.WriteByte(0);
		stream.WriteByte(0);

		for (int row = 0; row < rows; row++)
		{
			for (int chan = 0; chan < 32; chan++)
			{
				SongNote @out = pat.Rows[row][chan + 1];

				S3MNoteMask b = 0;

				if (song.Channels[chan].Flags.HasAllFlags(ChannelFlags.Mute))
				{
					if ((@out.Instrument != 0) || (@out.EffectByte != 0))
					{
						/* most players do in fact play data on muted channels, but that's
						wrong since ST3 doesn't. to eschew the problem, we'll just drop the
						data when writing (and complain) */
						warn |= Warnings.Muted;
						continue;
					}
				}
				else if (song.IsInstrumentMode
						&& (@out.Instrument != 0) && SongNote.IsNote(@out.Note))
				{
					var ins = song.Instruments[@out.Instrument];

					if (ins != null)
					{
						@out.Instrument = ins.SampleMap[@out.Note - 1];
						@out.Note = ins.NoteMap[@out.Note - 1];
					}
				}

				/* Translate notes */
				if ((@out.Note > 0 && @out.Note <= 12) || (@out.Note >= 109 && @out.Note <= 120))
				{
					// Octave 0/9 (or higher?)
					warn |= Warnings.NoteRange;
					@out.Note = 255;
				}
				else if (@out.Note > 12 && @out.Note < 109)
				{
					// C-1 through B-8
					@out.Note -= 13;
					@out.Note = (byte)((@out.Note % 12) + ((@out.Note / 12) << 4));
					b |= S3MNoteMask.NoteAndInstrument;
				}
				else if (@out.Note == SpecialNotes.NoteCut || @out.Note == SpecialNotes.NoteOff)
				{
					// IT translates === to ^^^ when writing S3M files
					// (and more importantly, we load ^^^ as === in adlib-channels)
					@out.Note = 254;
					b |= S3MNoteMask.NoteAndInstrument;
				}
				else
				{
					// Nothing (or garbage values)
					@out.Note = 255;
				}

				if (@out.Instrument != 0)
				{
					var sample = song.GetSample(@out.Instrument);

					if (sample != null)
					{
						S3IType type;

						if (sample.Flags.HasAllFlags(SampleFlags.AdLib))
							type = S3IType.AdMel;
						else if (sample.HasData)
							type = S3IType.PCM;
						else
							type = S3IType.None;

						if (type != S3IType.None)
						{
							if (chanTypes[chan] == S3IType.None
									|| chanTypes[chan] == S3ITypeControl)
							{
								chanTypes[chan] = type;
							}
							else if (chanTypes[chan] != type)
							{
								warn |= Warnings.PCMAdLibMix;
							}
						}

						b |= S3MNoteMask.NoteAndInstrument;
					}
				}

				switch (@out.VolumeEffect)
				{
					case VolumeEffects.None:
						break;
					case VolumeEffects.Volume:
						b |= S3MNoteMask.Volume;
						break;
					default:
						warn |= Warnings.VolumeEffects;
						break;
				}

				EffectUtility.ExportS3MEffect(ref @out.EffectByte, ref @out.Parameter, toIT: false);

				if ((@out.EffectByte != 0) || (@out.Parameter != 0))
				{
					b |= S3MNoteMask.Effect;
				}

				// If there's an effect, don't allow the channel to be muted in the S3M file.
				// S3I_TYPE_CONTROL is an internal value indicating that the channel should get a
				// "junk" value (such as B1) that doesn't actually play.
				if (chanTypes[chan] == S3IType.None && (@out.EffectByte != 0))
					chanTypes[chan] = S3ITypeControl;

				if (b == 0)
					continue;

				b |= (S3MNoteMask)chan;

				// write it!
				stream.WriteByte((byte)b);

				if (b.HasAllFlags(S3MNoteMask.NoteAndInstrument))
				{
					stream.WriteByte(@out.Note);
					stream.WriteByte(@out.Instrument);
				}

				if (b.HasAllFlags(S3MNoteMask.Volume))
					stream.WriteByte(@out.VolumeParameter);

				if (b.HasAllFlags(S3MNoteMask.Effect))
				{
					stream.WriteByte(@out.EffectByte);
					stream.WriteByte(@out.Parameter);
				}
			}

			if (!warn.HasAllFlags(Warnings.MaxChannels))
			{
				/* if the flag is already set, there's no point in continuing to search for stuff */
				for (int chan = 32; chan < Constants.MaxChannels; chan++)
					if (!pat.Rows[row][chan + 1].IsBlank)
					{
						warn |= Warnings.MaxChannels;
						break;
					}
			}

			stream.WriteByte(0); /* end of row */
		}

		/* if the pattern was < 64 rows, pad it */
		for (int row = rows; row < 64; row++)
			stream.WriteByte(0);

		/* hop back and write the real length */
		long end = stream.Position;

		stream.Position = start;
		stream.WriteStructure((ushort)(end - start));
		stream.Position = end;

		return warn;
	}

	Warnings FixUpChanTypes(Span<SongChannel> channels, Span<S3IType> chanTypes)
	{
		var warn = Warnings.None;

		int nPcm = 0, nAdMel = 0, nCtrl = 0;

		int pcm = 0, adMel = 0x10, junk = 0x20;

		/*
		Value   Label           Value   Label           (20-2F => 10-1F with B instead of A)
		00      L1              10      A1
		01      L2              11      A2
		02      L3              12      A3
		03      L4              13      A4
		04      L5              14      A5
		05      L6              15      A6
		06      L7              16      A7
		07      L8              17      A8
		08      R1              18      A9
		09      R2              19      AB
		0A      R3              1A      AS
		0B      R4              1B      AT
		0C      R5              1C      AC
		0D      R6              1D      AH
		0E      R7              1E      ??
		0F      R8              1F      ??

		For the L1 R1 L2 R2 pattern: ((n << 3) | (n >> 1)) & 0xf

		PCM  * 16 = 00-0F
		AdLib * 9 = 10-18
		Remaining = 20-2F (nothing will be played, but effects are still processed)

		Try to make as many of the "control" channels PCM as possible.
		*/

		for (int n = 0; n < 32; n++)
		{
			switch (chanTypes[n])
			{
				case S3IType.PCM:
					nPcm++;
					break;
				case S3IType.AdMel:
					nAdMel++;
					break;
				case S3ITypeControl:
					nCtrl++;
					break;
			}
		}

		if (nPcm > 16)
		{
			nPcm = 16;
			warn |= Warnings.MaxPCM;
		}

		if (nAdMel > 9)
		{
			nAdMel = 9;
			warn |= Warnings.MaxAdLib;
		}

		for (int n = 0; n < 32; n++)
		{
			// wtf is going on here
			switch (chanTypes[n])
			{
				case S3IType.PCM:
					if (pcm <= 0x0f)
						chanTypes[n] = (S3IType)pcm++;
					else
						chanTypes[n] = (S3IType)junk++;
					break;
				case S3IType.AdMel:
					if (adMel <= 0x18)
						chanTypes[n] = (S3IType)adMel++;
					else
						chanTypes[n] = (S3IType)junk++;
					break;
				case S3IType.None:
					if (channels[n].Flags.HasAllFlags(ChannelFlags.Mute))
					{
						chanTypes[n] = (S3IType)255; // (--)
						break;
					}
					// else fall through - attempt to honor unmuted channels.
					goto default;
				default:
					if (nPcm < 16)
					{
						chanTypes[n] = (S3IType)(((pcm << 3) | (pcm >> 1)) & 0xf);
						pcm++;
						nPcm++;
					}
					else if (nAdMel < 9)
					{
						chanTypes[n] = (S3IType)adMel++;
						nAdMel++;
					}
					else if (chanTypes[n] == S3IType.None)
						chanTypes[n] = (S3IType)255; // (--)
					else
						chanTypes[n] = (S3IType)junk++; // give up
					break;
			}

			if (junk > 0x2f)
				junk = 0x19; // "overflow" to the adlib drums
		}

		return warn;
	}

	public override SaveResult SaveSong(Song song, Stream stream)
	{
		long startOffset = stream.Position;

		var warn = Warnings.None;

		if (song.Flags.HasAllFlags(SongFlags.InstrumentMode))
			warn |= Warnings.Instruments;
		if (song.Flags.HasAllFlags(SongFlags.LinearSlides))
			warn |= Warnings.LinearSlides;

		int nOrd = song.OrderList.Count + 1;

		// TECH.DOC says orders should be even. In practice it doesn't appear to matter (in fact IT doesn't
		// make the number even), but if the spec says...
		if (nOrd.HasBitSet(1))
			nOrd++;

		// see note in IT writer -- shouldn't clamp here, but can't save more than we're willing to load
		//nOrd = nOrd.Clamp(2, Constants.MaxOrders);

		int nSmp = song.GetSampleCount(); // ST3 always saves one sample
		if (nSmp == 0)
			nSmp = 1;

		if (nSmp > 99)
		{
			nSmp = 99;
			warn |= Warnings.MaxSamples;
		}

		int nPat = song.GetPatternCount(); // ST3 always saves one pattern
		if (nPat == 0)
			nPat = 1;

		if (nPat > 100)
		{
			nPat = 100;
			warn |= Warnings.MaxPatterns;
		}

		Log.Append(5, " {0} orders, {0} samples, {0} patterns", nOrd, nSmp, nPat);

		/* this is used to identify what kinds of samples (pcm or adlib)
		are used on which channels, since it actually matters to st3 */
		var chanTypes = new S3IType[32];

		var hdr = new S3MHeader();

		hdr.Title = song.Title;
		if (hdr.Title.Length > 25)
			hdr.Title = hdr.Title.Substring(0, 25);

		hdr.EOF = 0x1a;
		hdr.Type = 16; // ST3 module (what else is there?!)
		hdr.OrdNum = (short)nOrd;
		hdr.SmpNum = (short)nSmp;
		hdr.PatNum = (short)nPat;
		hdr.Flags = 0;
		hdr.CreatedWithTrackerVersion = (ushort)(0x4000 | Version.CreatedWithTrackerVersion);
		hdr.SampleFormat = 2; // format version; 1 = signed samples, 2 = unsigned
		hdr.GlobalVolume = (byte)(song.InitialGlobalVolume / 2);
		hdr.InitialSpeed = (byte)song.InitialSpeed;
		hdr.InitialTempo = (byte)song.InitialTempo;

		/* .S3M "MasterVolume" only supports 0x10 .. 0x7f,
		* if we save 0x80, the schism max volume, it becomes zero in S3M.
		* I didn't test to see what ScreamTracker does if a value below 0x10
		* is loaded into it, but its UI prevents setting below 0x10.
		* Just enforce both bounds here.
		*/
		hdr.MasterVolume = (byte)song.MixingVolume.Clamp(0x10, 0x7F);
		if (!song.Flags.HasAllFlags(SongFlags.NoStereo))
			hdr.MasterVolume |= 128;

		hdr.UltraClick = 16; // ultraclick (the "Waste GUS channels" option)
		hdr.DP = 252;
		hdr.Reserved1 = (ushort)Version.Reserved;
		hdr.Reserved3 = (ushort)(Version.Reserved >> 16);

		/* Save the edit time in the reserved header, where
		* Impulse Tracker also conveniently stores it */
		hdr.Reserved2 = 0;

		for (int j = 0; j < song.History.Count; j++)
			hdr.Reserved2 += DateTimeConversions.TimeSpanToDOSTime(song.History[j].Runtime);

		// 32-bit DOS tick count (tick = 1/18.2 second; 54945 * 18.2 = 999999 which is Close Enough)
		hdr.Reserved2 += DateTimeConversions.TimeSpanToDOSTime(song.EditTimeElapsed);

		/* The sample data parapointers are 24+4 bits, whereas pattern data and sample headers are only 16+4
		bits -- so while the sample data can be written up to 268 MB within the file (starting at 0xffffff0),
		the pattern data and sample headers are restricted to the first 1 MB (starting at 0xffff0). In effect,
		this practically requires the sample data to be written last in the file, as it is entirely possible
		(and quite easy, even) to write more than 1 MB of sample data in a file.
		The "practical standard order" listed in TECH.DOC is sample headers, patterns, then sample data.
		Thus:
				File header
				Channel settings
				Orderlist
				Sample header pointers
				Pattern pointers
				Default pannings
				Sample headers
				Pattern data
				Sample data
		*/

		stream.WriteStructure(hdr); // header

		stream.Position += 32; // channel settings (skipped for now)

		byte[] orderList = new byte[nOrd];

		for (int i = 0; i < orderList.Length; i++)
			orderList[i] = (byte)song.OrderList[i];

		stream.Write(orderList);

		/* sample header pointers
		because the sample headers are fixed-size, it's possible to determine where they will be written
		right now: the first sample will be at the start of the next 16-byte block after all the header
		stuff, and each subsequent sample starts 0x50 bytes after the previous one. */
		int sampleHeaderPos = (0x60 + nOrd + 2 * (nSmp + nPat) + 32 + 15) & ~15;

		int pos = sampleHeaderPos;

		for (int n = 0; n < nSmp; n++)
		{
			int w = pos >> 4;
			stream.WriteStructure((short)w);
			pos += 0x50;
		}

		/* pattern pointers
		can't figure these out ahead of time since the patterns are variable length,
		but do make a note of where to seek later in order to write the values... */
		long patternPointerPosition = stream.Position;

		stream.Position += 2 * nPat;

		/* channel pannings ... also not yet! */
		stream.Position += 32;

		/* skip ahead past the sample headers as well (what a pain) */
		stream.Position += 0x50 * nSmp;

		/* patterns -- finally omg we can write some data */
		ushort[] paraPatterns = new ushort[nPat];

		for (int n = 0; n < nPat; n++)
			warn |= WritePattern(song, n, chanTypes, paraPatterns, stream);

		/* sample data */
		long[] paraSampleData = new long[nPat];

		for (int n = 0; n < nSmp; n++)
		{
			var smp = song.EnsureSample(n + 1);

			if (smp.Flags.HasAllFlags(SampleFlags.AdLib) || !smp.HasData)
			{
				paraSampleData[n] = 0;
				continue;
			}

			stream.WriteAlign(16);
			paraSampleData[n] = stream.Position - startOffset;

			SampleFileConverter.WriteSample(stream, smp, SampleFormat.LittleEndian | SampleFormat.PCMUnsigned
				| (smp.Flags.HasAllFlags(SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8)
				| (smp.Flags.HasAllFlags(SampleFlags.Stereo) ? SampleFormat.StereoSplit : SampleFormat.Mono),
				uint.MaxValue);
		}

		/* now that we're done adding stuff to the end of the file,
		go back and rewrite everything we skipped earlier.... */

		// channel types
		warn |= FixUpChanTypes(song.Channels, chanTypes);

		stream.Position = startOffset + 0x40;
		stream.WriteStructure(chanTypes);

		// pattern pointers
		stream.Position = patternPointerPosition;
		stream.WriteStructure(paraPatterns);

		/* channel panning settings come after the pattern pointers...
		This produces somewhat left-biased panning values, but this is what IT does, and more importantly
		it's stable across repeated load/saves. (Hopefully.)
		Technically it is possible to squeeze out two "extra" values for hard-left and hard-right panning by
		writing a "disabled" pan value (omit the 0x20 bit, so it's presented as a dot in ST3) -- but some
		trackers, including MPT and older Schism Tracker versions, load such values as 16/48 rather than 0/64,
		so this would result in potentially inconsistent behavior and is therefore undesirable. */
		for (int n = 0; n < 32; n++)
		{
			var ch = song.Channels[n];

			if (ch.Volume != 64)
				warn |= Warnings.ChannelVolume;

			//mphack: channel panning range
			byte b = (byte)((((int)chanTypes[n] & 0x7f) < 0x20)
				? (ch.Panning * 15 / 64)
				: 0);

			stream.WriteByte(b);
		}

		/* sample headers */
		stream.Position = startOffset + sampleHeaderPos;

		var s3i = new S3I();

		for (int n = 0; n < nSmp; n++)
		{
			var smp = song.EnsureSample(n + 1);

			if (smp.GlobalVolume != 64)
				warn |= Warnings.SampleVolume;

			if (smp.Flags.HasAllFlags(SampleFlags.Loop | SampleFlags.PingPongLoop)
					|| smp.Flags.HasAllFlags(SampleFlags.SustainLoop))
				warn |= Warnings.Loops;

			if (smp.VibratoDepth != 0)
				warn |= Warnings.SampleVibrato;

			s3i.WriteHeader(smp, (uint)paraSampleData[n], stream);
		}

		/* announce all the things we broke */
		foreach (var warningType in Enum.GetValues<Warnings>())
		{
			if ((warningType != Warnings.None) && warn.HasAllFlags(warningType))
				Log.Append(4, " Warning: {0} unsupported in S3M format", warningType.GetDescription());
		}

		return SaveResult.Success;
	}

}
