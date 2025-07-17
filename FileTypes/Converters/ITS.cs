using System;
using System.IO;
using System.Runtime.InteropServices;
using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using Microsoft.Extensions.FileSystemGlobbing;

namespace ChasmTracker.FileTypes.Converters;

public class ITS : SampleFileConverter
{
	[Flags]
	enum ITSampleFlags : byte
	{
		HasData = 1,
		_16Bit = 2,
		Stereo = 4,
		Compressed = 8,
		Loop = 16,
		SustainLoop = 32,
		PingPongLoop = 64,
		PingPongSustain = 128,
	}

	// IT Sample Format
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct ITSample
	{
		public int ID;            // 0x53504D49
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
		public byte[] FileName;
		public byte Zero;
		public byte GlobalVolume;
		public ITSampleFlags Flags;
		public byte Volume;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 26)]
		public byte[] Name;
		public byte Convert;
		public byte DefaultPanning;
		public int Length;
		public int LoopBegin;
		public int LoopEnd;
		public int C5Speed;
		public int SustainLoopBegin;
		public int SustainLoopEnd;
		public int SamplePointer;
		public byte VibratoSpeed;
		public byte VibratoDepth;
		public byte VibratoRate;
		public byte VibratoWaveform;
	}

	/* --------------------------------------------------------------------- */

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			var its = stream.ReadStructure<ITSample>();

			if (its.ID != 0x53504D49) // IMPS
				return false;

			file.SampleLength = its.Length;
			file.SampleFlags = 0;

			if (its.Flags.HasFlag(ITSampleFlags._16Bit))
				file.SampleFlags |= SampleFlags._16Bit;

			if (its.Flags.HasFlag(ITSampleFlags.Loop))
			{
				file.SampleFlags |= SampleFlags.Loop;
				if (its.Flags.HasFlag(ITSampleFlags.PingPongLoop))
					file.SampleFlags |= SampleFlags.PingPongLoop;
			}

			if (its.Flags.HasFlag(ITSampleFlags.SustainLoop)) {
				file.SampleFlags |= SampleFlags.SustainLoop;
				if (its.Flags.HasFlag(ITSampleFlags.PingPongSustain))
					file.SampleFlags |= SampleFlags.PingPongSustain;
			}

			if (its.DefaultPanning.HasBitSet(128)) file.SampleFlags |= SampleFlags.Panning;
			if (its.Flags.HasFlag(ITSampleFlags.Stereo)) file.SampleFlags |= SampleFlags.Stereo;

			file.SampleDefaultVolume = its.Volume;
			file.SampleGlobalVolume = its.GlobalVolume;
			file.SampleVibratoSpeed = its.VibratoSpeed;
			file.SampleVibratoDepth = its.VibratoDepth & 0x7f;
			file.SampleVibratoRate = its.VibratoRate;

			file.SampleLoopStart = its.LoopBegin;
			file.SampleLoopEnd = its.LoopEnd;
			file.SampleSpeed = its.C5Speed;
			file.SampleSustainStart = its.SustainLoopBegin;
			file.SampleSustainEnd = its.SustainLoopEnd;

			file.SampleFileName = its.FileName.ToStringZ();
			file.Description = "Impulse Tracker Sample";
			file.Type = FileSystem.FileTypes.SampleExtended;

			file.Title = its.Name.ToStringZ();

			return true;
		}
		catch
		{
			return false;
		}
	}

	// cwtv should be 0x214 when loading from its or iti
	public bool TryLoadSample(Stream fp, ushort createdWithTrackerVersion, out SongSample smp)
	{
		smp = new SongSample();

		var its = fp.ReadStructure<ITSample>();

		if (its.ID != 0x53504D49)
			return false;

		/* alright, let's get started */
		smp.Length = its.Length;
		if (its.Flags.HasFlag(ITSampleFlags.HasData))
			return false; // sample associated with header

		smp.GlobalVolume = its.GlobalVolume;
		if (its.Flags.HasFlag(ITSampleFlags.Loop))
		{
			smp.Flags |= SampleFlags.Loop;
			if (its.Flags.HasFlag(ITSampleFlags.PingPongLoop))
				smp.Flags |= SampleFlags.PingPongLoop;
		}
		if (its.Flags.HasFlag(ITSampleFlags.SustainLoop))
		{
			smp.Flags |= SampleFlags.SustainLoop;
			if (its.Flags.HasFlag(ITSampleFlags.PingPongSustain))
				smp.Flags |= SampleFlags.PingPongSustain;
		}

		/* IT sometimes didn't clear the flag after loading a stereo sample. This appears to have
		* been fixed sometime before IT 2.14, which is fortunate because that's what a lot of other
		* programs annoyingly identify themselves as. */
		if (createdWithTrackerVersion < 0x0214)
			its.Flags &= ~ITSampleFlags.Stereo;

		smp.Name = its.Name.ToStringZ();
		smp.FileName = its.FileName.ToStringZ();

		smp.Volume = its.Volume * 4;
		smp.Panning = (its.DefaultPanning & 127) * 4;
		if (its.DefaultPanning.HasBitSet(128))
			smp.Flags |= SampleFlags.Panning;
		smp.LoopStart = its.LoopBegin;
		smp.LoopEnd = its.LoopEnd;
		smp.C5Speed = its.C5Speed;
		smp.SustainStart = its.SustainLoopBegin;
		smp.SustainEnd = its.SustainLoopEnd;

		switch (its.VibratoWaveform)
		{
			case 0: smp.VibratoType = VibratoType.Sine; break;
			case 1: smp.VibratoType = VibratoType.RampDown; break;
			case 2: smp.VibratoType = VibratoType.Square; break;
			case 3: smp.VibratoType = VibratoType.Random; break;
		}

		// sanity checks purged, SongSample.AdjustLoop already does them  -paper

		long pos = fp.Position;
		if (pos < 0)
			return false;

		bool r;

		if (its.Flags.HasFlag(ITSampleFlags.HasData) && its.Convert == 64 && its.Length == 12)
		{
			// OPL instruments in OpenMPT MPTM files (which are essentially extended IT files)
			fp.Position = its.SamplePointer;

			smp.AdlibBytes = new byte[12];

			fp.ReadExactly(smp.AdlibBytes);

			smp.Flags |= SampleFlags.Adlib;
			// dumb hackaround that ought to some day be fixed:
			smp.Length = 1;
			smp.AllocateData();

			r = true;
		}
		else if (its.Flags.HasFlag(ITSampleFlags.HasData))
		{
			fp.Position = its.SamplePointer;

			// endianness (always zero)
			SampleFormat flags = SampleFormat.LittleEndian;
			// channels
			flags |= its.Flags.HasFlag(ITSampleFlags.Stereo) ? SampleFormat.StereoSplit : SampleFormat.Mono;
			if (its.Flags.HasFlag(ITSampleFlags.Compressed))
				flags |= its.Convert.HasBitSet(4) ? SampleFormat.IT215Compressed : SampleFormat.IT214Compressed; // compression algorithm
			else
			{
				// signedness (or delta?)

				// XXX for some reason I had a note in pm/fmt/it.c saying that I had found some
				// .it files with the signed flag set incorrectly and to assume unsigned when
				// hdr.cwtv < 0x0202. Why, and for what files?
				// Do any other players use the header for deciding sample data signedness?
				flags |= its.Convert.HasBitSet(4) ? SampleFormat.PCMDeltaEncoded : (its.Convert.HasBitSet(1) ? SampleFormat.PCMSigned : SampleFormat.PCMUnsigned);
			}

			// bit width
			flags |= its.Flags.HasFlag(ITSampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8;

			r = (ReadSample(smp, flags, fp) != 0);
		}
		else
		{
			smp.Length = 0;
			r = false;
		}

		fp.Position = pos;

		return r;
	}

	public override SongSample LoadSample(Stream stream)
	{
		if (TryLoadSample(stream, 0x0214, out var smp))
			return smp;
		else
			throw new FormatException();
	}

	public void SaveHeader(SongSample smp, Stream fp)
	{
		var its = new ITSample();

		its.ID = 0x53504D49; // IMPS
		its.FileName = smp.FileName.ToBytes(12);
		its.GlobalVolume = (byte)smp.GlobalVolume;
		if (smp.HasData && (smp.Length != 0))
			its.Flags |= ITSampleFlags.HasData;
		if (smp.Flags.HasFlag(SampleFlags._16Bit))
			its.Flags |= ITSampleFlags._16Bit;
		if (smp.Flags.HasFlag(SampleFlags.Stereo))
			its.Flags |= ITSampleFlags.Stereo;
		if (smp.Flags.HasFlag(SampleFlags.Loop))
			its.Flags |= ITSampleFlags.Loop;
		if (smp.Flags.HasFlag(SampleFlags.SustainLoop))
			its.Flags |= ITSampleFlags.SustainLoop;
		if (smp.Flags.HasFlag(SampleFlags.PingPongLoop))
			its.Flags |= ITSampleFlags.PingPongLoop;
		if (smp.Flags.HasFlag(SampleFlags.PingPongSustain))
			its.Flags |= ITSampleFlags.PingPongSustain;

		its.Volume = (byte)(smp.Volume / 4);
		its.Name = smp.Name.ToBytes(26);
		its.Name[25] = 0;
		its.Convert = 1; // signed samples
		its.DefaultPanning = (byte)(smp.Panning / 4);
		if (smp.Flags.HasFlag(SampleFlags.Panning))
			its.DefaultPanning |= 0x80;
		its.Length = smp.Length;
		its.LoopBegin = smp.LoopStart;
		its.LoopEnd = smp.LoopEnd;
		its.C5Speed = smp.C5Speed;
		its.SustainLoopBegin = smp.SustainStart;
		its.SustainLoopEnd = smp.SustainEnd;
		//its.SamplePointer = 42; - this will be filled in later
		its.VibratoSpeed = (byte)smp.VibratoSpeed;
		its.VibratoRate = (byte)smp.VibratoRate;
		its.VibratoDepth = (byte)smp.VibratoDepth;
		switch (smp.VibratoType)
		{
			case VibratoType.Random:   its.VibratoWaveform = 3; break;
			case VibratoType.Square:   its.VibratoWaveform = 2; break;
			case VibratoType.RampDown: its.VibratoWaveform = 1; break;
			default:
			case VibratoType.Sine:     its.VibratoWaveform = 0; break;
		}

		fp.WriteStructure(its);
	}

	public override SaveResult SaveSample(Stream stream, SongSample smp)
	{
		if (smp.Flags.HasFlag(SampleFlags.Adlib))
			return SaveResult.Unsupported;

		SaveHeader(smp, stream);

		long dataOffset = stream.Position;

		WriteSample(stream, smp, SampleFormat.LittleEndian | SampleFormat.PCMSigned
			| (smp.Flags.HasFlag(SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8)
			| (smp.Flags.HasFlag(SampleFlags.Stereo) ? SampleFormat.StereoSplit : SampleFormat.Mono),
			uint.MaxValue);

		/* Write the sample pointer. In an ITS file, the sample data is right after the header,
		* so its position in the file will be the same as the size of the header. */
		stream.Position = 48;
		stream.WriteStructure((int)dataOffset);

		return SaveResult.Success;
	}
}