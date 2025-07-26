using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.InstrumentConverters;

using System;
using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class PAT : InstrumentFileConverter, IFileInfoReader
{
	public override string Label => "PAT";
	public override string Description => "Gravis Patch File";
	public override string Extension => ".pat";

	/* --------------------------------------------------------------------- */

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct GF1PatchHeader
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
		public string Signature; // "GF1PATCH"
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
		public string Version; // "100" or "110"
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
		public string ID; // "ID#000002\0"
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 60)]
		public string Discription; // Discription (in ASCII) [sic]
		public byte InstrumentNumber; // To some patch makers, 0 means 1 [what?]
		public byte VoiceNumber; // Voices (Always 14?)
		public byte ChannelNumber; // Channels
		public short Waveforms;
		public short MasterVolume; // 0-127 [then why is it 16-bit? ugh]
		public int DataSize;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
		public byte[] Reserved1;
		public short InstrumentID; // Instrument ID [0..0xFFFF] [?]
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
		public string InstrumentName; // Instrument name (in ASCII)
		public int InstrumentSize; // Instrument size
		public byte Layers;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
		public byte[] Reserved2;
		public byte LayerDuplicate;
		public byte Layer;
		public int LayerSize;
		public byte SampleCount;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
		public byte[] Reserved3;
	}

	bool ReadHeader(Stream stream, out GF1PatchHeader hdr)
	{
		try
		{
			hdr = stream.ReadStructure<GF1PatchHeader>();

			if ((hdr.Signature != "GF1PATCH")
			 || ((hdr.Version != "110") && (hdr.Version != "100"))
			 || (hdr.ID != "ID#000002"))
				return false;

			return true;
		}
		catch
		{
			hdr = default;

			return false;
		}
	}

	[Flags]
	enum GF1SampleMode : byte
	{
		_16Bit = 1,
		Unsigned = 2,
		Loop = 4,
		PingPongLoop = 8,
		Reverse = 16,
		Sustain = 32,
		Envelope = 64,
		ClampedRelease = 128,
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct GF1PatchSampleHeader
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 7)]
		public string WaveName; // Wave name (in ASCII)
		public byte Fractions; // bits 0-3 loop start frac / 4-7 loop end frac
		public int SampleSize; // Sample data size (s)
		public int LoopStart;
		public int LoopEnd;
		public ushort SampleRate;
		public uint LowFrequency;
		public uint HighFrequency;
		public int RootFrequency;
		public ushort Tune; // Tune (Always 1, not used anymore)
		public byte Panning; // Panning (L=0 -> R=15)
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
		public byte[] Envelopes;
		public byte TremoloSpeed, TremoloRate, TremoloDepth;
		public byte VibratoSpeed, VibratoRate, VibratoDepth;
		public GF1SampleMode SampleMode; // bit mask: 16, unsigned, loop, pingpong, reverse, sustain, envelope, clamped release
		public ushort ScaleFrequency;
		public ushort ScaleFactor; // Scale factor [0..2048] (1024 is normal)
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
		public byte[] Reserved;
	}

	bool ReadSampleHeader(Stream stream, out GF1PatchSampleHeader hdr)
	{
		try
		{
			hdr = stream.ReadStructure<GF1PatchSampleHeader>();

			return true;
		}
		catch
		{
			hdr = default;

			return false;
		}
	}

	/* --------------------------------------------------------------------- */

	public bool FillExtendedData(Stream stream, FileReference file)
	{
		if (!ReadHeader(stream, out var hdr))
			return false;

		file.Description = "Gravis Patch File";
		file.Title = hdr.InstrumentName;
		file.Type = FileSystem.FileTypes.InstrumentOther;

		return true;
	}

	public override bool LoadInstrument(Stream file, int slot)
	{
		if (slot == 0)
			return false;

		if (!ReadHeader(file, out var header))
			return false;

		var ii = new InstrumentLoader(Song.CurrentSong, slot);

		var g = ii.Instrument;

		g.Name = header.InstrumentName;

		int numSamples = header.SampleCount.Clamp(1, 16);

		for (int i = 0; i < 120; i++)
		{
			g.SampleMap[i] = 0;
			g.NoteMap[i] = (byte)(i + 1);
		}

		for (int i = 0; i < numSamples; i++)
		{
			if (!ReadSampleHeader(file, out var gfsamp))
				return false;

			int n = ii.GetNewSampleNumber(i + 1);

			var smp = Song.CurrentSong.EnsureSample(n);

			int lo = Tables.GUSFrequency(gfsamp.LowFrequency).Clamp(0, 95);
			int hi = Tables.GUSFrequency(gfsamp.HighFrequency).Clamp(0, 95);

			if (lo > hi)
				(lo, hi) = (hi, lo);

			for (; lo < hi; lo++)
				g.SampleMap[lo + 12] = (byte)n;

			if (gfsamp.SampleMode.HasFlag(GF1SampleMode._16Bit))
			{
				gfsamp.SampleSize >>= 1;
				gfsamp.LoopStart >>= 1;
				gfsamp.LoopEnd >>= 1;
			}

			smp.Length = gfsamp.SampleSize;
			smp.LoopStart = smp.SustainStart = gfsamp.LoopStart;
			smp.LoopEnd = smp.SustainEnd = gfsamp.LoopEnd;
			smp.C5Speed = gfsamp.SampleRate;

			smp.Flags = 0;

			var rs = SampleFormat.Mono | SampleFormat.LittleEndian; // channels; endianness
			rs |= gfsamp.SampleMode.HasFlag(GF1SampleMode._16Bit) ? SampleFormat._16 : SampleFormat._8; // bit width
			rs |= gfsamp.SampleMode.HasFlag(GF1SampleMode.Unsigned) ? SampleFormat.PCMUnsigned : SampleFormat.PCMSigned; // encoding

			if (gfsamp.SampleMode.HasFlag(GF1SampleMode.Sustain))
			{
				if (gfsamp.SampleMode.HasFlag(GF1SampleMode.Loop))
					smp.Flags |= SampleFlags.SustainLoop;
				if (gfsamp.SampleMode.HasFlag(GF1SampleMode.PingPongLoop))
					smp.Flags |= SampleFlags.PingPongSustain;
			}
			else
			{
				if (gfsamp.SampleMode.HasFlag(GF1SampleMode.Loop))
					smp.Flags |= SampleFlags.Loop;
				if (gfsamp.SampleMode.HasFlag(GF1SampleMode.PingPongLoop))
					smp.Flags |= SampleFlags.PingPongLoop;
			}

			smp.FileName = gfsamp.WaveName;
			smp.Name = smp.FileName;

			smp.VibratoSpeed = gfsamp.VibratoSpeed;
			smp.VibratoRate = gfsamp.VibratoRate;
			smp.VibratoDepth = gfsamp.VibratoDepth;

			SampleFileConverter.ReadSample(smp, rs, file);
		}

		return true;
	}
}
