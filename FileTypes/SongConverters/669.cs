using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.FileTypes.Converters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class _669 : SongFileConverter
{
	public override string Label => "669";
	public override string Description => "Composer 669 Module";
	public override string Extension => ".669";

	public override int SortOrder => 0;

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Header669
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public byte[] Sig;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)] public byte[] SongMessage;
		public byte Samples;
		public byte Patterns;
		public byte RestartPos;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] Orders;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] TempoList;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] Breaks;

		public string SigString => Sig.ToStringZ();
		public string SongMessageString => SongMessage.ToStringZ();
	}

	Header669 ReadHeader(Stream stream)
		=> stream.ReadStructure<Header669>();

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			var hdr = ReadHeader(stream);

			string sig = hdr.SigString;

			/* Impulse Tracker identifies any 669 file as a "Composer 669 Module",
					regardless of the signature tag. */
			if (sig == "if")
				file.Description = "Composer 669 Module";
			else if (sig == "JN")
				file.Description = "Extended 669 Module";
			else
				return false;

			if (hdr.Samples == 0 || hdr.Patterns == 0
					|| hdr.Samples > 64 || hdr.Patterns > 128
					|| hdr.RestartPos > 127)
				return false;

			for (int i = 0; i < 128; i++)
				if (hdr.Breaks[i] > 0x3f)
					return false;

			file.Title = hdr.SongMessageString;
			file.Type = FileSystem.FileTypes.ModuleS3M;

			return true;
		}
		catch
		{
			return false;
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

	static readonly Effects[] EffectLUT =
		new[]
		{
			Effects.PortamentoUp,   /* slide up (param * 80) hz on every tick */
			Effects.PortamentoDown, /* slide down (param * 80) hz on every tick */
			Effects.TonePortamento, /* slide to note by (param * 40) hz on every tick */
			default,                /* add (param * 80) hz to sample frequency */
			Effects.Vibrato,        /* add (param * 669) hz on every other tick */
			Effects.Speed,          /* set ticks per row */
			Effects.PanningSlide,   /* extended UNIS 669 effect */
			Effects.Retrigger,      /* extended UNIS 669 effect */
		};

	/* <opinion humble="false">This is better than IT's and MPT's 669 loaders</opinion> */
	public override Song LoadSong(Stream stream, LoadFlags flags)
	{
		string tid;

		var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

		switch (reader.ReadInt16())
		{
			case 0x6669: // 'if'
				tid = "Composer 669";
				break;
			case 0x4e4a: // 'JN'
				tid = "UNIS 669";
				break;

			default:
				throw new NotSupportedException();
		}

		var song = new Song();

		/* The message is 108 bytes, split onto 3 lines of 36 bytes each.
		Also copy the first part of the message into the title, because 669 doesn't actually have
		a dedicated title field... */
		song.Message = ReadLinedMessage(stream, 108, 36);

		song.Title = new StringReader(song.Message).ReadLine() ?? "";

		int nSmp = stream.ReadByte();
		int nPat = stream.ReadByte();
		int restartPos = stream.ReadByte();

		if (nSmp > 64 || nPat > 128 || restartPos > 127)
			throw new NotSupportedException();

		song.TrackerID = tid;

		/* orderlist */
		var orderList = reader.ReadBytes(128);

		for (int i = 0; i < orderList.Length; i++)
			song.OrderList.Add(orderList[i]);

		/* stupid crap */
		var patSpeed = reader.ReadBytes(128);
		var breakPos = reader.ReadBytes(128);

		/* samples */
		for (int smp = 1; smp <= nSmp; smp++)
		{
			var sample = new SongSample();

			var name = reader.ReadBytes(13);

			int nameLen = Array.IndexOf(name, 0);

			/* the spec says it's supposed to be ASCIIZ, but some 669's use all 13 chars */
			if (nameLen < 0)
				nameLen = name.Length;

			sample.Name = name.ToStringZ();
			sample.FileName = sample.Name;

			sample.Length = reader.ReadInt32();
			sample.LoopStart = reader.ReadInt32();
			sample.LoopEnd = reader.ReadInt32();

			if (sample.LoopEnd > sample.Length)
				sample.LoopEnd = 0;
			else
				sample.Flags |= SampleFlags.Loop;

			sample.C5Speed = 8363;
			sample.Volume = 60; /* ickypoo */
			sample.Volume *= 4; // mphack
			sample.GlobalVolume = 64; /* ickypoo */
			sample.VibratoType = default;
			sample.VibratoRate = 0;
			sample.VibratoDepth = 0;
			sample.VibratoSpeed = 0;

			song.Samples.Add(sample);
		}

		/* patterns */
		for (int pat = 0; pat < nPat; pat++)
		{
			byte[] effect = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 };

			int rows = breakPos[pat] + 1;

			if (rows > 64)
				throw new NotSupportedException();

			var pattern = new Pattern();

			pattern.Resize(rows.Clamp(32, 64));

			byte[] noteBytes = new byte[3];

			for (int row = 0; row < rows; row++)
			{
				for (int chan = 0; chan < 8; chan++)
				{
					ref var note = ref pattern[row][chan + 1];

					stream.ReadExactly(noteBytes);

					switch (noteBytes[0])
					{
						case 0xfe:     /* no note, only volume */
							note.VolumeEffect = VolumeEffects.Volume;
							note.VolumeParameter = (byte)((noteBytes[1] & 0xF) << 2);
							break;
						case 0xff:     /* no note or volume */
							break;
						default:
							note.Note = (byte)((noteBytes[0] >> 2) + 36 + 1);
							note.Instrument = (byte)(((noteBytes[0] & 3) << 4 | (noteBytes[1] >> 4)) + 1);
							note.VolumeEffect = VolumeEffects.Volume;
							note.VolumeParameter = (byte)((noteBytes[1] & 0xf) << 2);

							effect[chan] = 0xff;
							break;
					}

					/* now handle effects */
					if (noteBytes[2] != 0xff)
						effect[chan] = noteBytes[2];

					/* param value of zero = reset */
					if ((noteBytes[2] & 0x0f) == 0 && noteBytes[2] != 0x30)
						effect[chan] = 0xff;

					if (effect[chan] == 0xff)
						continue;

					note.Parameter = (byte)(effect[chan] & 0x0f);

					int e = effect[chan] >> 4;

					if (e < EffectLUT.Length)
						note.Effect = EffectLUT[e];
					else
					{
						note.Effect = Effects.None;
						continue;
					}

					/* fix some commands */
					switch (e)
					{
						default:
							/* do nothing */
							break;
						case 3: /* D - frequency adjust (??) */
							note.Effect = Effects.PortamentoUp;
							note.Parameter |= 0xf0;
							effect[chan] = 0xff;
							break;
						case 4: /* E - frequency vibrato - almost like an arpeggio, but does not arpeggiate by a given note but by a frequency amount. */
							note.Effect = Effects.Arpeggio;
							note.Parameter |= (byte)(note.Parameter << 4);
							break;
						case 5: /* F - set tempo */
							/* TODO: param 0 is a "super fast tempo" in Unis 669 mode (???) */
							effect[chan] = 0xFF;
							break;
						case 6:
							// G - subcommands (extended)
							switch (note.Parameter)
							{
								case 0:
									// balance fine slide left
									note.Parameter = 0x4F;
									break;
								case 1:
									// balance fine slide right
									note.Parameter = 0xF4;
									break;
								default:
									note.Effect = Effects.None;
									break;
							}

							break;
					}
				}
			}

			if (rows < 64)
			{
				/* skip the rest of the rows beyond the break position */
				stream.Position += 3 * 8 * (64 - rows);
			}

			/* handle the stupid pattern speed */
			for (int chan = 0; chan < 9; chan++)
			{
				ref var note = ref pattern[pat][chan + 1];

				if (note.Effect == Effects.Speed)
					break;
				else if (note.Effect == Effects.None)
				{
					note.Effect = Effects.Speed;
					note.Parameter = patSpeed[pat];
					break;
				}
			}

			/* handle the break position */
			if (rows < 32)
			{
				//Console.WriteLine("adding pattern break for pattern {0}", pat);
				for (int chan = 0; chan < 9; chan++)
				{
					ref var note = ref pattern[rows - 1][chan + 1];
					if (note.Effect == Effects.None)
					{
						note.Effect = Effects.PatternBreak;
						note.Parameter = 0;
						break;
					}
				}
			}

			song.Patterns.Add(pattern);
		}

		song.InsertRestartPos(restartPos);

		/* sample data */

		if (!flags.HasFlag(LoadFlags.NoSamples))
		{
			for (int smp = 1; smp <= nSmp; smp++)
			{
				if (song.Samples[smp]!.Length == 0)
					continue;

				SampleFileConverter.ReadSample(song.Samples[smp]!, SampleFormat.LittleEndian | SampleFormat.Mono | SampleFormat.PCMUnsigned | SampleFormat._8, stream);
			}
		}

		/* set the rest of the stuff */
		song.InitialSpeed = 4;
		song.InitialTempo = 78;
		song.Flags = SongFlags.ITOldEffects | SongFlags.LinearSlides;

		song.PanSeparation = 64;

		for (int n = 0; n < 8; n++)
			song.Channels[n].Panning = ((n & 1) != 0) ? 256 : 0; //mphack
		for (int n = 8; n < 64; n++)
			song.Channels[n].Flags = ChannelFlags.Mute;

		/* done! */
		return song;
	}
}
