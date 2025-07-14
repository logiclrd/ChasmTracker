using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.Converters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

/* --------------------------------------------------------------------------------------------------------- */
/* This loader sucks. Mostly it was implemented based on what Modplug does, which is
kind of counterproductive, but I can't get Farandole to run in Dosbox to test stuff */
public class FAR : SongFileConverter
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	class FARHeader
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
		public string Magic = "";
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
		public string Title = "";
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 3)]
		public string EOF = "";
		public short HeaderLen;
		public byte Version;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] OnOff = Array.Empty<byte>();
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
		public byte[] EditingState = Array.Empty<byte>();
		public byte DefaultSpeed;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] ChannelPanning = Array.Empty<byte>();
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public byte[] PatternState = Array.Empty<byte>();
		public short MessageLength;
	}

	[Flags]
	enum FARSampleType : byte
	{
		_16bit = 1,
	}

	[Flags]
	enum FARLoopFlags : byte
	{
		Enabled = 8,
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	class FARSample
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string Name = "";
		public int Length;
		public byte FineTune;
		public byte Volume;
		public int LoopStart;
		public int LoopEnd;
		public FARSampleType Type;
		public FARLoopFlags Loop;
	}

	bool ReadHeader(Stream fp, out FARHeader hdr)
	{
		try
		{
			hdr = fp.ReadStructure<FARHeader>();

			if (hdr.Magic != "FAR\xFE")
				return false;
			if (hdr.EOF != "\x0D\x0A\x1A")
				return false;

			/* byteswapping is handled in the read functions for now */
			return true;
		}
		catch (IOException)
		{
			hdr = new FARHeader();
			return false;
		}
	}

	bool ReadSample(Stream fp, out FARSample smp)
	{
		try
		{
			smp = fp.ReadStructure<FARSample>();

			return true;
		}
		catch (IOException)
		{
			smp = new FARSample();
			return false;
		}
	}

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		/* The magic for this format is truly weird (which I suppose is good, as the chance of it
		* being "accidentally" correct is pretty low) */

		try
		{
			if (!ReadHeader(stream, out var hdr))
				return false;

			file.Description = "Farandole Module";
			/*file.Extension = "far";*/
			file.Title = hdr.Title.TrimZ();
			file.Type = FileSystem.FileTypes.ModuleS3M;

			return true;
		}
		catch
		{
			return false;
		}
	}

	static readonly Effects[] FAREffects =
		{
			Effects.None,
			Effects.PortamentoUp,
			Effects.PortamentoDown,
			Effects.TonePortamento,
			Effects.Retrigger,
			Effects.Vibrato, // depth
			Effects.Vibrato, // speed
			Effects.VolumeSlide, // up
			Effects.VolumeSlide, // down
			Effects.Vibrato, // sustained (?)
			Effects.None, // actually slide-to-volume
			Effects.Panning,
			Effects.Special, // note offset => note delay?
			Effects.None, // fine tempo down
			Effects.None, // fine tempo up
			Effects.Speed,
		};

	void ImportNote(byte[] data, ref SongNote note)
	{
		if (data[0] > 0 && data[0] < 85)
		{
			note.Note = (byte)(data[0] + 36);
			note.Instrument = (byte)(data[1] + 1);
		}
		if (data[2].HasAnyBitSet(0x0F)) {
			note.VolumeEffect = VolumeEffects.Volume;
			note.VolumeParameter = (byte)((data[2] & 0x0F) << 2); // askjdfjasdkfjasdf
		}
		note.Parameter = (byte)(data[3] & 0xf);
		switch (data[3] >> 4)
		{
			case 3: // porta to note
				note.Parameter <<= 2;
				break;
			case 4: // retrig
				note.Parameter = (byte)(6 / (1 + (note.Parameter & 0xf)) + 1); // ugh?
				break;
			case 6: // vibrato speed
			case 7: // volume slide up
			case 0xb: // panning
				note.Parameter <<= 4;
				break;
			case 0xa: // volume-portamento (what!)
				note.VolumeEffect = VolumeEffects.Volume;
				note.VolumeParameter = (byte)((note.Parameter << 2) + 4);
				break;
			case 0xc: // note offset
				note.Parameter = (byte)(6 / (1 + (note.Parameter & 0xf)) + 1);
				note.Parameter |= 0xd;
				break;
		}
		note.Effect = FAREffects[data[3] >> 4];
	}

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		//uint8_t orderlist[256];
		//uint16_t patternSize[256];
		//uint8_t data[8];

		if (!ReadHeader(stream, out var fhdr))
			throw new NotSupportedException();

		var song = new Song();

		song.Title = fhdr.Title.TrimZ();

		for (int n = 0; n < 16; n++)
		{
			/* WHAT A GREAT WAY TO STORE THIS INFORMATION */
			song.Channels[n].Panning = Tables.ShortPanning(fhdr.ChannelPanning[n] & 0xf);
			song.Channels[n].Panning *= 4; //mphack
			if (fhdr.OnOff[n] == 0)
				song.Channels[n].Flags |= ChannelFlags.Mute;
		}

		for (int n = 16; n < 64; n++)
			song.Channels[n].Flags |= ChannelFlags.Mute;

		song.InitialSpeed = fhdr.DefaultSpeed;
		song.InitialTempo = 80;

		// to my knowledge, no other program is insane enough to save in this format
		song.TrackerID = "Farandole Composer";

		/* Farandole's song message doesn't have line breaks, and the tracker runs in
		* some screwy ultra-wide text mode, so this displays more or less like crap. */
		song.Message = ReadLinedMessage(stream, fhdr.MessageLength, 132);

		if (lflags.HasFlag(LoadFlags.NoSamples | LoadFlags.NoPatterns))
			return song;

		var orderList = new byte[256];

		stream.ReadExactly(orderList);

		stream.ReadByte(); // supposed to be "number of patterns stored in the file"; apparently that's wrong

		int nOrd = stream.ReadByte();
		int restartPos = stream.ReadByte();

		nOrd = Math.Min(nOrd, Constants.MaxOrders);

		song.OrderList.AddRange(orderList.Take(nOrd).Select(n => (int)n));
		song.OrderList.Add(SpecialOrders.Last);

		short[] patternSize = new short[256];

		for (int i = 0; i < 256; i++)
			patternSize[i] = stream.ReadStructure<short>();

		stream.Position = fhdr.HeaderLen - (869 + fhdr.MessageLength);

		for (int pat = 0; pat < 256; pat++)
		{
			int breakPos, rows;

			if (pat >= Constants.MaxPathLength || patternSize[pat] < (2 + 16 * 4))
			{
				stream.Position += patternSize[pat];
				continue;
			}

			breakPos = stream.ReadByte();

			stream.ReadByte(); // apparently, this value is *not* used anymore!!! I will not support it!!

			rows = (patternSize[pat] - 2) / (16 * 4);
			if (rows == 0)
				continue;

			song.Patterns[pat] = new Pattern(rows);
			breakPos = (breakPos > 0) && (breakPos < rows - 2) ? breakPos + 1 : -1;

			byte[] data = new byte[4];

			for (int row = 0; row < rows; row++)
			{
				for (int chn = 0; chn < 16; chn++)
				{
					ref var note = ref song.Patterns[pat].Rows[row][chn];

					stream.ReadExactly(data);
					ImportNote(data, ref note);
				}

				if (row == breakPos)
					song.Patterns[pat].Rows[row][0].Effect = Effects.PatternBreak;
			}
		}

		song.InsertRestartPos(restartPos);

		if (!lflags.HasFlag(LoadFlags.NoSamples))
		{
			long data = stream.ReadStructure<long>();

			Console.WriteLine(data);

			for (int n = 0; n < 64; n++)
			{
				var smp = new SongSample();

				song.Samples[n + 1] = smp;

				if (!data.HasFlag(1 << (n % 8))) /* LOLWHAT */
					continue;

				ReadSample(stream, out var fsmp);

				smp.Name = fsmp.Name.TrimZ();

				smp.Length = fsmp.Length;
				smp.LoopStart = fsmp.LoopStart;
				smp.LoopEnd = fsmp.LoopEnd;
				smp.Volume = fsmp.Volume << 4; // "not supported", but seems to exist anyway

				if (fsmp.Type.HasFlag(FARSampleType._16bit))
				{
					smp.Length >>= 1;
					smp.LoopStart >>= 1;
					smp.LoopEnd >>= 1;
				}

				if (smp.LoopEnd > smp.LoopStart && fsmp.Loop.HasFlag(FARLoopFlags.Enabled))
					smp.Flags |= SampleFlags.Loop;
				smp.C5Speed = 16726;
				smp.GlobalVolume = 64;

				SampleFileConverter.ReadSample(smp, SampleFormat.LittleEndian | SampleFormat.Mono | SampleFormat.PCMSigned | (fsmp.Type.HasFlag(FARSampleType._16bit) ? SampleFormat._16 : SampleFormat._8), stream);
			}
		}

		return song;
	}
}