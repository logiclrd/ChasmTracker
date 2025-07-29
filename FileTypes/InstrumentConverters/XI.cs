using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.FileTypes.InstrumentConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class XI : InstrumentFileConverter
{
	// TODO
	public override string Label => "XI";
	public override string Description => "Fasttracker II";
	public override string Extension => ".xi";

	/* --------------------------------------------------------------------- */

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct XMSampleHeader
	{
		public int SampleLength;
		public int LoopStart;
		public int LoopLength;
		public byte Volume;
		public sbyte FineTune;
		public XMSampleType Type;
		public byte Panning;
		public sbyte RelativeNote;
		public byte Res;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 22)]
		public string Name;
	}

	struct XIEnvelopeNode
	{
		public ushort Ticks;        // Time in tracker ticks
		public ushort Value;        // Value from 0x00 to 0x40.
	}

	[Flags]
	enum XIEnvelopeFlags : byte
	{
		Enabled = 1,
		Sustain = 2,
		Loop = 4,
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct XISampleHeader
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)]
		public byte[] SampleNumber;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
		public XIEnvelopeNode[] VolumeEnvelope;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
		public XIEnvelopeNode[] PanningEnvelope;
		public byte VolumeEnvelopeNodeCount;
		public byte PanningEnvelopeNodeCount;
		public byte VolumeSustain;
		public byte VolumeLoopStart;
		public byte VolumeLoopEnd;
		public byte PanningSustain;
		public byte PanningLoopStart;
		public byte PanningLoopEnd;
		public XIEnvelopeFlags VolumeType;
		public XIEnvelopeFlags PanningType;
		public byte VibratoType;
		public byte VibratoSweep;
		public byte VibratoDepth;
		public byte VibratoRate;
		public ushort VolumeFade;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x16)]
		public byte[] Reserved1;
		public ushort NumSamples;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct XIFileHeader
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x15)]
		public string Header;    // "Extended Instrument: "
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x16)]
		public string Name;      // Name of instrument
		public byte Magic;          // 0x1a, DOS EOF char so you can 'type file.xi'
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x14)]
		public string Tracker;   // Name of tracker
		public ushort Version;       // big-endian 0x0102

		// sample header
		public XISampleHeader SampleHeader;
	}

	bool ReadXMSampleHeader(Stream stream, out XMSampleHeader shdr)
	{
		try
		{
			shdr = stream.ReadStructure<XMSampleHeader>();
			return true;
		}
		catch
		{
			shdr = default;
			return false;
		}
	}

	static readonly byte[] s_reservedBytes = new byte[0x16];

	static bool ReadXIFileHeader(Stream stream, out XIFileHeader hdr)
	{
		try
		{
			hdr = stream.ReadStructure<XIFileHeader>();

			if (hdr.Header != "Extended Instrument: ")
				return false;

			if (hdr.Magic != 0x1A)
				return false;

			if (hdr.Version != 0x0102)
				return false;

			return true;
		}
		catch
		{
			hdr = default;
			return false;
		}
	}

	/* --------------------------------------------------------------------- */

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		if (!ReadXIFileHeader(stream, out var xi))
			return false;

		file.Description = "Fasttracker II Instrument";
		file.Title = xi.Name;
		file.Type = FileTypes.InstrumentXI;

		return true;
	}

	public override bool LoadInstrument(Stream file, int slot)
	{
		if (!ReadXIFileHeader(file, out var xi))
			return false;

		if (slot == 0)
			return false;

		//Song.CurrentSong.DeleteInstrument(slot, false);

		var ii = new InstrumentLoader(Song.CurrentSong, slot);

		var g = ii.Instrument;

		g.Name = xi.Name;

		for (int k = 0; k < 96; k++)
		{
			if (xi.SampleHeader.SampleNumber[k] > 15)
				xi.SampleHeader.SampleNumber[k] = 15;
			xi.SampleHeader.SampleNumber[k] = (byte)ii.GetNewSampleNumber(xi.SampleHeader.SampleNumber[k] + 1);
			g.NoteMap[k + 12] = (byte)(k + 1 + 12);
			if (xi.SampleHeader.SampleNumber[k] != 0)
				g.SampleMap[k + 12] = xi.SampleHeader.SampleNumber[k];
		}

		for (int k = 0; k < 12; k++)
		{
			g.NoteMap[k] = 0;
			g.SampleMap[k] = 0;
			g.NoteMap[k + 108] = 0;
			g.SampleMap[k + 108] = 0;
		}

		// Set up envelope types in instrument
		if (xi.SampleHeader.VolumeType.HasFlag(XIEnvelopeFlags.Enabled))  g.Flags |= InstrumentFlags.VolumeEnvelope;
		if (xi.SampleHeader.VolumeType.HasFlag(XIEnvelopeFlags.Sustain))  g.Flags |= InstrumentFlags.VolumeEnvelopeSustain;
		if (xi.SampleHeader.VolumeType.HasFlag(XIEnvelopeFlags.Loop))     g.Flags |= InstrumentFlags.VolumeEnvelopeLoop;
		if (xi.SampleHeader.PanningType.HasFlag(XIEnvelopeFlags.Enabled)) g.Flags |= InstrumentFlags.PanningEnvelope;
		if (xi.SampleHeader.PanningType.HasFlag(XIEnvelopeFlags.Sustain)) g.Flags |= InstrumentFlags.PanningEnvelopeSustain;
		if (xi.SampleHeader.PanningType.HasFlag(XIEnvelopeFlags.Loop))    g.Flags |= InstrumentFlags.PanningEnvelopeLoop;

		int prevTick = -1;

		// Copy envelopes into instrument
		g.VolumeEnvelope = new Envelope();

		for (int k = 0; k < xi.SampleHeader.VolumeEnvelopeNodeCount; k++)
		{
			if (xi.SampleHeader.VolumeEnvelope[k].Ticks < prevTick)
				prevTick++;
			else
				prevTick = xi.SampleHeader.VolumeEnvelope[k].Ticks;
			g.VolumeEnvelope.Nodes.Add((prevTick, xi.SampleHeader.VolumeEnvelope[k].Value));
		}

		prevTick = -1;

		g.PanningEnvelope = new Envelope();

		for (int k = 0; k < xi.SampleHeader.PanningEnvelopeNodeCount; k++)
		{
			if (xi.SampleHeader.PanningEnvelope[k].Ticks < prevTick)
				prevTick++;
			else
				prevTick = xi.SampleHeader.PanningEnvelope[k].Ticks;
			g.PanningEnvelope.Nodes.Add((prevTick, xi.SampleHeader.PanningEnvelope[k].Value));
		}

		g.VolumeEnvelope.LoopStart = xi.SampleHeader.VolumeLoopStart;
		g.VolumeEnvelope.LoopEnd = xi.SampleHeader.VolumeLoopEnd;
		g.VolumeEnvelope.SustainStart = xi.SampleHeader.VolumeSustain;

		g.PanningEnvelope.LoopStart = xi.SampleHeader.PanningLoopStart;
		g.PanningEnvelope.LoopEnd = xi.SampleHeader.PanningLoopEnd;
		g.PanningEnvelope.SustainStart = xi.SampleHeader.PanningSustain;

		// Determine where the sample data starts.
		int sampleDataOffset = Marshal.SizeOf(xi) + (Marshal.SizeOf<XMSampleHeader>() * xi.SampleHeader.NumSamples);

		for (int k = 0; k < xi.SampleHeader.NumSamples; k++)
		{
			if (!ReadXMSampleHeader(file, out var xmss))
				break;

			SampleFormat rs;

			rs = SampleFormat.LittleEndian | SampleFormat.PCMDeltaEncoded; // endianness; encoding
			rs |= xmss.Type.HasFlag(XMSampleType.Stereo) ? SampleFormat.StereoSplit : SampleFormat.Mono; // channels
			rs |= xmss.Type.HasFlag(XMSampleType._16Bit) ? SampleFormat._16 : SampleFormat._8; // bits

			if (xmss.Type.HasFlag(XMSampleType._16Bit))
			{
				xmss.LoopLength >>= 1;
				xmss.LoopStart >>= 1;
				xmss.SampleLength >>= 1;
			}

			if (xmss.Type.HasFlag(XMSampleType.Stereo))
			{
				xmss.LoopLength >>= 1;
				xmss.LoopStart >>= 1;
				xmss.SampleLength >>= 1;
			}

			if (xmss.LoopStart >= xmss.SampleLength)
				xmss.Type &= ~XMSampleType.LoopMask;
			xmss.LoopLength += xmss.LoopStart;
			if (xmss.LoopLength > xmss.SampleLength)
				xmss.LoopLength = xmss.SampleLength;
			if (xmss.LoopLength == 0)
				xmss.Type &= ~XMSampleType.LoopMask;

			int n = ii.GetNewSampleNumber(k + 1);

			var smp = Song.CurrentSong.EnsureSample(n);

			smp.Flags = 0;
			smp.Name = xmss.Name;

			smp.Length = xmss.SampleLength;
			smp.LoopStart = xmss.LoopStart;
			smp.LoopEnd = xmss.LoopLength;
			if (smp.LoopEnd < smp.LoopStart)
				smp.LoopEnd = smp.Length;
			if (smp.LoopStart >= smp.LoopEnd)
				smp.LoopStart = smp.LoopEnd = 0;
			if (xmss.Type.HasAnyFlag(XMSampleType.Loop | XMSampleType.PingPongLoop))
			{
				smp.Flags |= SampleFlags.Loop;

				if (xmss.Type.HasFlag(XMSampleType.PingPongLoop))
					smp.Flags |= SampleFlags.PingPongLoop;
			}
			smp.Volume = xmss.Volume << 2;
			if (smp.Volume > 256)
				smp.Volume = 256;
			smp.GlobalVolume = 64;
			smp.Panning = xmss.Panning;
			smp.Flags |= SampleFlags.Panning;
			smp.VibratoType = (VibratoType)xi.SampleHeader.VibratoType;
			smp.VibratoSpeed = Math.Min(xi.SampleHeader.VibratoDepth, (byte)64);
			smp.VibratoDepth = Math.Min(xi.SampleHeader.VibratoDepth, (byte)32);
			if ((xi.SampleHeader.VibratoRate | xi.SampleHeader.VibratoDepth) != 0)
			{
				if (xi.SampleHeader.VibratoSweep != 0)
				{
					int s = smp.VibratoDepth * 256 / xi.SampleHeader.VibratoSweep;
					smp.VibratoRate = s.Clamp(0, 255);
				}
				else
					smp.VibratoRate = 255;
			}

			smp.C5Speed = SongNote.TransposeToFrequency(xmss.RelativeNote, xmss.FineTune);

			if (smp.Length != 0)
			{
				// Save our spot, read the sample data, then jump back.
				long sampleHeaderOffset = file.Position;

				file.Position = sampleDataOffset;
				sampleDataOffset += SampleFileConverter.ReadSample(smp, rs, file);
				file.Position = sampleHeaderOffset;
			}
		}

		return true;
	}

	/* ------------------------------------------------------------------------ */

	public override SaveResult SaveInstrument(Song song, SongInstrument instrument, Stream file)
	{
		XIFileHeader xi = new XIFileHeader();

		/* fill in sample numbers, epicly stolen from the iti code */
		int[] xiMap = new int[255];
		int[] xiInvMap = new int[255];
		int xiNumAllocated = 0;

		for (int j = 0; j < 255; j++)
			xiMap[j] = -1;

		xi.SampleHeader.SampleNumber = new byte[96];

		for (int j = 0; j < 96; j++)
		{
			int o = instrument.SampleMap[j + 12];

			if (o > 0 && o < 255 && xiMap[o] == -1)
			{
				xiMap[o] = xiNumAllocated;
				xiInvMap[xiNumAllocated] = o;
				xiNumAllocated++;
			}

			xi.SampleHeader.SampleNumber[j] = (byte)xiMap[o];
		}

		if (xiNumAllocated < 1)
			return SaveResult.FileError;

		/* now add header things */
		xi.Header = "Extended Instrument: ";
		xi.Name = instrument.Name ?? "";

		if (xi.Name.Length > 0x16)
			xi.Name = xi.Name.Substring(0, 0x16);

		xi.Magic = 0x1A;
		xi.Tracker = "Schism Tracker";
		xi.Version = 0x0102;

		/* envelope type */
		if (instrument.Flags.HasFlag(InstrumentFlags.VolumeEnvelope))         xi.SampleHeader.VolumeType |= XIEnvelopeFlags.Enabled;
		if (instrument.Flags.HasFlag(InstrumentFlags.VolumeEnvelopeSustain))  xi.SampleHeader.VolumeType |= XIEnvelopeFlags.Sustain;
		if (instrument.Flags.HasFlag(InstrumentFlags.VolumeEnvelopeLoop))     xi.SampleHeader.VolumeType |= XIEnvelopeFlags.Loop;
		if (instrument.Flags.HasFlag(InstrumentFlags.PanningEnvelope))        xi.SampleHeader.PanningType |= XIEnvelopeFlags.Enabled;
		if (instrument.Flags.HasFlag(InstrumentFlags.PanningEnvelopeSustain)) xi.SampleHeader.PanningType |= XIEnvelopeFlags.Sustain;
		if (instrument.Flags.HasFlag(InstrumentFlags.PanningEnvelopeLoop))    xi.SampleHeader.PanningType |= XIEnvelopeFlags.Loop;

		var volumeEnvelope = instrument.VolumeEnvelope ?? new Envelope(64);

		xi.SampleHeader.VolumeLoopStart = (byte)volumeEnvelope.LoopStart;
		xi.SampleHeader.VolumeLoopEnd = (byte)volumeEnvelope.LoopEnd;
		xi.SampleHeader.VolumeSustain = (byte)volumeEnvelope.SustainStart;
		xi.SampleHeader.VolumeEnvelopeNodeCount = (byte)Math.Min(volumeEnvelope.Nodes.Count, 12);

		var panningEnvelope = instrument.PanningEnvelope ?? new Envelope(128);

		xi.SampleHeader.PanningLoopStart = (byte)panningEnvelope.LoopStart;
		xi.SampleHeader.PanningLoopEnd = (byte)panningEnvelope.LoopEnd;
		xi.SampleHeader.PanningSustain = (byte)panningEnvelope.SustainStart;
		xi.SampleHeader.PanningEnvelopeNodeCount = (byte)Math.Min(panningEnvelope.Nodes.Count, 12);

		/* envelope nodes */
		xi.SampleHeader.VolumeEnvelope = new XIEnvelopeNode[12];
		for (int k = 0; k < xi.SampleHeader.VolumeEnvelopeNodeCount; k++)
		{
			xi.SampleHeader.VolumeEnvelope[k].Ticks = (ushort)volumeEnvelope.Nodes[k].Tick;
			xi.SampleHeader.VolumeEnvelope[k].Value = volumeEnvelope.Nodes[k].Value;
		}

		/* envelope nodes */
		xi.SampleHeader.PanningEnvelope = new XIEnvelopeNode[12];
		for (int k = 0; k < xi.SampleHeader.PanningEnvelopeNodeCount; k++) {
			xi.SampleHeader.PanningEnvelope[k].Ticks = (ushort)panningEnvelope.Nodes[k].Tick;
			xi.SampleHeader.PanningEnvelope[k].Value = panningEnvelope.Nodes[k].Value;
		}

		/* XXX volfade */

		/* Tuesday's coming! Did you bring your coat? */
		xi.SampleHeader.Reserved1 = new byte[0x16];
		Encoding.ASCII.GetBytes("ILiveInAGiantBucket").CopyTo(xi.SampleHeader.Reserved1.AsMemory());

		xi.SampleHeader.NumSamples = (ushort)xiNumAllocated;

		if (xiNumAllocated > 0)
		{
			/* nab the first sample's info */
			var smp = song.GetSample(xiInvMap[0]);

			if (smp == null)
				throw new Exception("Internal error");

			xi.SampleHeader.VibratoType = (byte)smp.VibratoType;
			xi.SampleHeader.VibratoRate = (byte)Math.Min(smp.VibratoSpeed, 63);
			xi.SampleHeader.VibratoDepth = (byte)Math.Min(smp.VibratoDepth, 15);

			if ((xi.SampleHeader.VibratoRate | xi.SampleHeader.VibratoDepth) != 0)
			{
				if (smp.VibratoRate != 0)
				{
					int s = smp.VibratoDepth * 256 / smp.VibratoRate;
					xi.SampleHeader.VibratoSweep = (byte)s.Clamp(0, 255);
				}
				else
					xi.SampleHeader.VibratoSweep = 255;
			}
		}

		/* now write the data... */
		file.WriteStructure(xi);

		for (int k = 0; k < xiNumAllocated; k++)
		{
			int o = xiInvMap[k];

			XMSampleHeader xmss = new XMSampleHeader();

			var smp = song.GetSample(o);

			if (smp == null)
				throw new Exception("Internal error");

			xmss.Name = smp.Name;

			if (xmss.Name.Length > 22)
				xmss.Name = xmss.Name.Substring(0, 22);

			xmss.SampleLength = smp.Length;

			xmss.LoopStart = smp.LoopStart;
			xmss.LoopLength = smp.LoopEnd - smp.LoopStart;

			if (smp.Flags.HasFlag(SampleFlags.PingPongLoop))
				xmss.Type |= XMSampleType.Loop | XMSampleType.PingPongLoop;
			else if (smp.Flags.HasFlag(SampleFlags.Loop))
				xmss.Type |= XMSampleType.Loop;

			if (smp.Flags.HasFlag(SampleFlags._16Bit))
			{
				xmss.Type |= XMSampleType._16Bit;
				xmss.LoopLength <<= 1;
				xmss.LoopStart <<= 1;
				xmss.SampleLength <<= 1;
			}

			if (smp.Flags.HasFlag(SampleFlags.Stereo))
			{
				xmss.Type |= XMSampleType.Stereo;
				xmss.LoopLength <<= 1;
				xmss.LoopStart <<= 1;
				xmss.SampleLength <<= 1;
			}

			xmss.Volume = (byte)(smp.Volume >> 2);

			if(instrument.Flags.HasFlag(InstrumentFlags.SetPanning))
				xmss.Panning = (byte)instrument.Panning;
			else if (smp.Flags.HasFlag(SampleFlags.Panning))
				xmss.Panning = (byte)smp.Panning;
			else
				// Default panning enabled for instrument and sample--pan to center.
				xmss.Panning = 0x80;

			int transp = SongNote.FrequencyToTranspose(smp.C5Speed);

			xmss.RelativeNote = (sbyte)(transp / 128);
			xmss.FineTune = (sbyte)(transp % 128);

			file.WriteStructure(xmss);
		}

		for (int k = 0; k < xiNumAllocated; k++)
		{
			int o = xiInvMap[k];

			var smp = song.GetSample(o);

			if (smp == null)
				throw new Exception("Internal error");

			SampleFileConverter.WriteSample(file, smp, SampleFormat.LittleEndian | SampleFormat.PCMDeltaEncoded
			 | (smp.Flags.HasFlag(SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8)
			 | (smp.Flags.HasFlag(SampleFlags.Stereo) ? SampleFormat.StereoSplit : SampleFormat.Mono),
			 uint.MaxValue);
		}

		return SaveResult.Success;
	}
}
