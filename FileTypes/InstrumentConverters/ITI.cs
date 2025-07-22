using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.Converters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class ITI : InstrumentFileConverter, IFileInfoReader
{
	public override string Label => "ITI";
	public override string Description => "Impulse Tracker";
	public override string Extension => ".iti";

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct ITEnvelopeNode
	{
		public byte Value; // signed (-32 -> 32 for pan and pitch; 0 -> 64 for vol and filter)
		public ushort Tick;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct ITEnvelope
	{
		public ITEnvelopeFlags Flags;
		public byte NumberOfNodes;
		public byte LoopBegin;
		public byte LoopEnd;
		public byte SustainLoopBegin;
		public byte SustainLoopEnd;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
		public ITEnvelopeNode[] Nodes;
		public byte Reserved;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct ITNoteTranslation
	{
		public byte Note;
		public byte Sample;
	}

	// Old Impulse Instrument Format (cmwt < 0x200)
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct ITInstrumentOld
	{
		public int ID;                    // IMPI = 0x49504D49
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
		public byte[] FileName;
		public byte Zero;
		public ITEnvelopeFlags Flags;
		public byte VolumeLoopStart;
		public byte VolumeLoopEnd;
		public byte SustainLoopStart;
		public byte SustainLoopEnd;
		public short Reserved1;
		public short FadeOut;
		public byte NewNoteAction;
		public byte DuplicateNoteCheck;
		public ushort TrackerVersion;
		public byte NumberOfSamples;
		public byte Reserved2;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 26)]
		public byte[] Name;
		public short Reserved3;
		public int Reserved4;
		//[MarshalAs(UnmanagedType.ByValArray, SizeConst = 120)] ITNoteTranslation[] Keyboard;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 200)]
		public byte[] VolumeEnvelope;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
		public byte[] Nodes;
	}

	// Impulse Instrument Format
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct ITInstrumentHeader
	{
		public int ID;                    // IMPI = 0x49504D49
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
		public byte[] FileName;
		public byte Zero;
		public byte NewNoteAction;
		public byte DuplicateCheckType;
		public byte DuplicateCheckAction;
		public ushort FadeOut;
		public sbyte PitchPanSeparation;
		public byte PitchPanCenter;
		public byte GlobalVolume;
		public byte DefaultPanning;
		public byte VolumeSwing;
		public byte PanningSwing;
		public ushort TrackerVersion;
		public byte NumberOfSamples;
		public byte Reserved1;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 26)]
		public byte[] Name;
		public byte IFCutoff;
		public byte IFResonance;
		public byte MIDIChannelMask;
		public byte MIDIProgram;
		public ushort MIDIBank;
	}

	struct ITInstrument
	{
		public ITInstrumentHeader Header;

		//struct ITNoteTranslation[] Keyboard = new[120];
		public ITEnvelope VolumeEnvelope;
		public ITEnvelope PanningEnvelope;
		public ITEnvelope PitchEnvelope;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public byte[] Dummy; // was 7, but IT v2.17 saves 554 bytes
	}

	/* --------------------------------------------------------------------- */

	public bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			var iti = stream.ReadStructure<ITInstrumentHeader>();

			if (iti.ID != 0x49504D49) // IMPI
				return false;

			file.Description = "Impulse Tracker Instrument";
			file.Title = iti.Name.ToStringZ();
			file.Type = FileTypes.InstrumentITI;

			return true;
		}
		catch
		{
			return false;
		}
	}

	[Flags]
	enum ITEnvelopeFlags : byte
	{
		Enable = 1,
		Loop = 2,
		SustainLoop = 4,
		Carry = 8,

		Filter = 0x80,
	}

	class EnvelopeFlagSet : Dictionary<ITEnvelopeFlags, InstrumentFlags>
	{
		public EnvelopeFlagSet(InstrumentFlags enable, InstrumentFlags loop, InstrumentFlags sustainLoop, InstrumentFlags carry)
		{
			this[ITEnvelopeFlags.Enable] = enable;
			this[ITEnvelopeFlags.Loop] = loop;
			this[ITEnvelopeFlags.SustainLoop] = sustainLoop;
			this[ITEnvelopeFlags.Carry] = carry;
		}
	}

	static readonly Dictionary<EnvelopeType, EnvelopeFlagSet> EnvelopeFlagTranslation =
		new Dictionary<EnvelopeType, EnvelopeFlagSet>()
		{
			{ EnvelopeType.Volume, new EnvelopeFlagSet(InstrumentFlags.VolumeEnvelope, InstrumentFlags.VolumeEnvelopeLoop, InstrumentFlags.VolumeEnvelopeSustain, InstrumentFlags.VolumeEnvelopeCarry) },
			{ EnvelopeType.Panning, new EnvelopeFlagSet(InstrumentFlags.PanningEnvelope, InstrumentFlags.PanningEnvelopeLoop, InstrumentFlags.PanningEnvelopeSustain, InstrumentFlags.VolumeEnvelopeCarry) },
			{ EnvelopeType.Pitch, new EnvelopeFlagSet(InstrumentFlags.PitchEnvelope, InstrumentFlags.PitchEnvelopeLoop, InstrumentFlags.PitchEnvelopeSustain, InstrumentFlags.VolumeEnvelopeCarry) },
		};

	const int EOF = -1;

	bool LoadNoteTranslation(InstrumentLoader? ii, SongInstrument instrument, Stream fp)
	{
		for (int n = 0; n < 120; n++)
		{
			int note = fp.ReadByte();
			int smp = fp.ReadByte();

			if (note == EOF || smp == EOF)
				return false;

			note += SpecialNotes.First;
			// map invalid notes to themselves
			if (!SongNote.IsNote(note))
				note = n + SpecialNotes.First;

			instrument.NoteMap[n] = (byte)note;
			instrument.SampleMap[n] = (byte)(ii?.GetNewSampleNumber(smp) ?? smp);
		}

		return true;
	}

	// XXX need to check slurp return values
	InstrumentFlags LoadEnvelope(Stream fp, out Envelope env, EnvelopeType envType, int adj)
	{
		InstrumentFlags flags = default;

		var itEnv = fp.ReadStructure<ITEnvelope>();

		int numNodes = itEnv.NumberOfNodes.Clamp(2, 25);

		env = new Envelope();

		env.LoopStart = Math.Min(itEnv.LoopBegin, numNodes);
		env.LoopEnd = itEnv.LoopEnd.Clamp(env.LoopStart, numNodes);
		env.SustainStart = Math.Min(itEnv.SustainLoopBegin, numNodes);
		env.SustainEnd = itEnv.SustainLoopEnd.Clamp(env.SustainStart, numNodes);

		for (int n = 0; n < numNodes; n++)
		{
			int v = itEnv.Nodes[n].Value + adj;

			v = v.Clamp(0, 64);

			int t = itEnv.Nodes[n].Tick;

			env.Nodes.Add((v, t));
		}

		env.Nodes[0].Tick = 0; // sanity check

		foreach (var flag in Enum.GetValues<ITEnvelopeFlags>())
			if (itEnv.Flags.HasFlag(flag))
				flags |= EnvelopeFlagTranslation[envType][flag];

		if ((envType == EnvelopeType.Pitch) && (itEnv.Flags.HasFlag(ITEnvelopeFlags.Filter)))
			flags |= InstrumentFlags.Filter;

		return flags;
	}

	public bool LoadInstrumentOld(SongInstrument instrument, Stream stream)
	{
		var ihdr = stream.ReadStructure<ITInstrumentOld>();

		/* err */
		if (ihdr.ID != 0x49504D49)
			return false;

		instrument.Name = ihdr.Name.ToStringZ();
		instrument.FileName = ihdr.FileName.ToStringZ();

		instrument.NewNoteAction = (NewNoteActions)(ihdr.NewNoteAction % 4);

		if (ihdr.DuplicateNoteCheck != 0)
		{
			// XXX is this right?
			instrument.DuplicateCheckType = DuplicateCheckTypes.Note;
			instrument.DuplicateCheckAction = DuplicateCheckActions.NoteCut;
		}

		instrument.FadeOut = ihdr.FadeOut << 6;
		instrument.PitchPanSeparation = 0;
		instrument.PitchPanCenter = SpecialNotes.MiddleC;
		instrument.GlobalVolume = 128;
		instrument.Panning = 32 * 4; //mphack

		if (!LoadNoteTranslation(null, instrument, stream))
			return false;

		if (ihdr.Flags.HasFlag(ITEnvelopeFlags.Enable))
			instrument.Flags |= InstrumentFlags.VolumeEnvelope;
		if (ihdr.Flags.HasFlag(ITEnvelopeFlags.Loop))
			instrument.Flags |= InstrumentFlags.VolumeEnvelopeLoop;
		if (ihdr.Flags.HasFlag(ITEnvelopeFlags.SustainLoop))
			instrument.Flags |= InstrumentFlags.VolumeEnvelopeSustain;

		instrument.VolumeEnvelope = new Envelope();

		instrument.VolumeEnvelope.LoopStart = ihdr.VolumeLoopStart;
		instrument.VolumeEnvelope.LoopEnd = ihdr.VolumeLoopEnd;
		instrument.VolumeEnvelope.SustainStart = ihdr.SustainLoopStart;
		instrument.VolumeEnvelope.SustainEnd = ihdr.SustainLoopEnd;

		// this seems totally wrong... why isn't this using ihdr.vol_env at all?
		// apparently it works, though.
		for (int n = 0; n < 25; n++)
		{
			int tick = ihdr.Nodes[2 * n];

			if (tick == 0xff)
				break;

			int value = ihdr.Nodes[2 * n + 1];

			instrument.VolumeEnvelope.Nodes.Add((tick, value));
		}

		return true;
	}

	public bool LoadInstrument(InstrumentLoader? instrumentLoader, SongInstrument instrument, Stream stream)
	{
		var ihdr = stream.ReadStructure<ITInstrumentHeader>();

		if (ihdr.ID != 0x49504D49)
			return false;

		instrument.Name = ihdr.Name.ToStringZ();
		instrument.FileName = ihdr.FileName.ToStringZ();

		instrument.NewNoteAction = (NewNoteActions)(ihdr.NewNoteAction % 4);
		instrument.DuplicateCheckType = (DuplicateCheckTypes)(ihdr.DuplicateCheckType % 4);
		instrument.DuplicateCheckAction = (DuplicateCheckActions)(ihdr.DuplicateCheckType % 3);
		instrument.FadeOut = ihdr.FadeOut << 5;
		instrument.PitchPanSeparation = ihdr.PitchPanSeparation.Clamp(-32, 32);
		instrument.PitchPanCenter = Math.Min(ihdr.PitchPanCenter, (byte)119); // I guess
		instrument.GlobalVolume = Math.Min(ihdr.GlobalVolume, (byte)128);
		instrument.Panning = Math.Min((ihdr.DefaultPanning & 127), 64) * 4; //mphack
		if (!ihdr.DefaultPanning.HasBitSet(128))
			instrument.Flags |= InstrumentFlags.SetPanning;
		instrument.VolumeSwing = Math.Min(ihdr.VolumeSwing, (byte)100);
		instrument.PanningSwing = Math.Min(ihdr.PanningSwing, (byte)64);

		instrument.IFCutoff = ihdr.IFCutoff;
		instrument.IFResonance = ihdr.IFResonance;

		// (blah... this isn't supposed to be a mask according to the
		// spec. where did this code come from? and what is 0x10000?)
		instrument.MIDIChannelMask =
				((ihdr.MIDIChannelMask > 16)
				? (0x10000 + ihdr.MIDIChannelMask)
				: ((ihdr.MIDIChannelMask > 0)
						? (1 << (ihdr.MIDIChannelMask - 1))
						: 0));
		instrument.MIDIProgram = ihdr.MIDIProgram;
		instrument.MIDIBank = ihdr.MIDIBank;

		if (!LoadNoteTranslation(instrumentLoader, instrument, stream))
			return false;

		instrument.Flags |= LoadEnvelope(stream, out instrument.VolumeEnvelope, EnvelopeType.Volume, 0);
		instrument.Flags |= LoadEnvelope(stream, out instrument.PanningEnvelope, EnvelopeType.Panning, 32);
		instrument.Flags |= LoadEnvelope(stream, out instrument.PanningEnvelope, EnvelopeType.Pitch, 32);

		stream.Position += 4;

		return true;
	}

	public override bool LoadInstrument(Stream file, int slot)
	{
		var ii = new InstrumentLoader(Song.CurrentSong, slot);
		var ins = ii.Instrument;

		if (!LoadInstrument(ii, ins, file))
			return false;

		/* okay, on to samples */
		var its = new ITS();

		for (int j = 0; j < ii.ExpectSamples; j++)
		{
			if (j >= Song.CurrentSong.Samples.Count)
				continue;

			if (!its.TryLoadSample(file, 0x214, out var smp))
			{
				Log.Append(4, "Could not load sample {0} from ITI file", j);
				ii.Abort();
				return false;
			}

			Song.CurrentSong.Samples[j] = smp;
		}

		return true;
	}

	bool SaveEnvelope(ITEnvelope itEnv, Stream stream)
	{
		stream.WriteStructure(itEnv);
		return true;
	}

	bool SaveEnvelopes(SongInstrument ins, Stream stream)
	{
		ITEnvelope vol = default;
		ITEnvelope pan = default;
		ITEnvelope pitch = default;

		var volumeEnvelope = ins.VolumeEnvelope ?? new Envelope(64);
		var panningEnvelope = ins.PanningEnvelope ?? new Envelope(0);
		var pitchEnvelope = ins.PitchEnvelope ?? new Envelope(0);

		vol.Flags = ((ins.Flags.HasFlag(InstrumentFlags.VolumeEnvelope) ? ITEnvelopeFlags.Enable : 0))
			| ((ins.Flags.HasFlag(InstrumentFlags.VolumeEnvelopeLoop) ? ITEnvelopeFlags.Loop : 0))
			| ((ins.Flags.HasFlag(InstrumentFlags.VolumeEnvelopeSustain) ? ITEnvelopeFlags.SustainLoop : 0))
			| ((ins.Flags.HasFlag(InstrumentFlags.VolumeEnvelopeCarry) ? ITEnvelopeFlags.Carry : 0));
		vol.NumberOfNodes = (byte)volumeEnvelope.Nodes.Count;
		vol.LoopBegin = (byte)volumeEnvelope.LoopStart;
		vol.LoopEnd = (byte)volumeEnvelope.LoopEnd;
		vol.SustainLoopBegin = (byte)volumeEnvelope.SustainStart;
		vol.SustainLoopEnd = (byte)volumeEnvelope.SustainEnd;

		pan.Flags = ((ins.Flags.HasFlag(InstrumentFlags.PanningEnvelope) ? ITEnvelopeFlags.Enable : 0))
			| ((ins.Flags.HasFlag(InstrumentFlags.PanningEnvelopeLoop) ? ITEnvelopeFlags.Loop : 0))
			| ((ins.Flags.HasFlag(InstrumentFlags.PanningEnvelopeSustain) ? ITEnvelopeFlags.SustainLoop : 0))
			| ((ins.Flags.HasFlag(InstrumentFlags.PanningEnvelopeCarry) ? ITEnvelopeFlags.Carry : 0));
		pan.NumberOfNodes = (byte)panningEnvelope.Nodes.Count;
		pan.LoopBegin = (byte)panningEnvelope.LoopStart;
		pan.LoopEnd = (byte)panningEnvelope.LoopEnd;
		pan.SustainLoopBegin = (byte)panningEnvelope.SustainStart;
		pan.SustainLoopEnd = (byte)panningEnvelope.SustainEnd;

		pitch.Flags = ((ins.Flags.HasFlag(InstrumentFlags.PitchEnvelope) ? ITEnvelopeFlags.Enable : 0))
			| ((ins.Flags.HasFlag(InstrumentFlags.PitchEnvelopeLoop) ? ITEnvelopeFlags.Loop : 0))
			| ((ins.Flags.HasFlag(InstrumentFlags.PitchEnvelopeSustain) ? ITEnvelopeFlags.SustainLoop : 0))
			| ((ins.Flags.HasFlag(InstrumentFlags.PitchEnvelopeCarry) ? ITEnvelopeFlags.Carry : 0))
			| ((ins.Flags.HasFlag(InstrumentFlags.Filter) ? ITEnvelopeFlags.Filter : 0));
		pitch.NumberOfNodes = (byte)pitchEnvelope.Nodes.Count;
		pitch.LoopBegin = (byte)pitchEnvelope.LoopStart;
		pitch.LoopEnd = (byte)pitchEnvelope.LoopEnd;
		pitch.SustainLoopBegin = (byte)pitchEnvelope.SustainStart;
		pitch.SustainLoopEnd = (byte)pitchEnvelope.SustainEnd;

		vol.Nodes = new ITEnvelopeNode[25];
		pan.Nodes = new ITEnvelopeNode[25];
		pitch.Nodes = new ITEnvelopeNode[25];

		for (int j = 0; j < 25; j++)
		{
			vol.Nodes[j].Value = volumeEnvelope.Nodes[j].Value;
			vol.Nodes[j].Tick = (ushort)volumeEnvelope.Nodes[j].Tick;

			pan.Nodes[j].Value = (byte)(panningEnvelope.Nodes[j].Value - 32);
			pan.Nodes[j].Tick = (ushort)panningEnvelope.Nodes[j].Tick;

			pitch.Nodes[j].Value = (byte)(pitchEnvelope.Nodes[j].Value - 32);
			pitch.Nodes[j].Tick = (ushort)pitchEnvelope.Nodes[j].Tick;
		}

		if (!SaveEnvelope(vol, stream))   return false;
		if (!SaveEnvelope(pan, stream))   return false;
		if (!SaveEnvelope(pitch, stream)) return false;

		return true;
	}

	// set itiFile if saving an instrument to disk by itself
	public void SaveInstrument(Song song, SongInstrument? instrument, Stream file, bool itiFile)
	{
		/* don't have an instrument? make one up! */
		if (instrument == null)
			instrument = new SongInstrument(song);

		// envelope: flags num lpb lpe slb sle data[25*3] reserved

		var iti = new ITInstrumentHeader();

		iti.ID = 0x49504D49; // IMPI
		iti.FileName = instrument.FileName.ToBytes(12);
		iti.Zero = 0;
		iti.NewNoteAction = (byte)instrument.NewNoteAction;
		iti.DuplicateCheckType = (byte)instrument.DuplicateCheckType;
		iti.DuplicateCheckType = (byte)instrument.DuplicateCheckType;
		iti.FadeOut = (ushort)(instrument.FadeOut >> 5);
		iti.PitchPanSeparation = (sbyte)instrument.PitchPanSeparation;
		iti.PitchPanCenter = (byte)instrument.PitchPanCenter;
		iti.GlobalVolume = (byte)instrument.GlobalVolume;

		iti.DefaultPanning = (byte)(instrument.Panning / 4);
		if (!instrument.Flags.HasFlag(InstrumentFlags.SetPanning))
			iti.DefaultPanning |= 0x80;

		iti.VolumeSwing = (byte)instrument.VolumeSwing;
		iti.PanningSwing = (byte)instrument.PanningSwing;

		if (itiFile)
			iti.TrackerVersion = unchecked((ushort)(0x1000 | ChasmTracker.Version.CreatedWithTrackerVersion));

		// reserved1

		iti.Name = instrument.Name.ToBytes(26);
		iti.Name[25] = 0;
		iti.IFCutoff = (byte)instrument.IFCutoff;
		iti.IFResonance = (byte)instrument.IFResonance;
		iti.MIDIChannelMask = 0;
		if (instrument.MIDIChannelMask >= 0x10000)
		{
			iti.MIDIChannelMask = unchecked((byte)(instrument.MIDIChannelMask - 0x10000));
			if (iti.MIDIChannelMask <= 16) iti.MIDIChannelMask = 16;
		}
		else if (instrument.MIDIChannelMask.HasAnyBitSet(0xFFFF))
		{
			iti.MIDIChannelMask = 1;
			while (!instrument.MIDIChannelMask.HasBitSet(1 << (iti.MIDIChannelMask - 1))) ++iti.MIDIChannelMask;
		}
		iti.MIDIProgram = (byte)instrument.MIDIProgram;
		iti.MIDIBank = (ushort)instrument.MIDIBank;

		ITNoteTranslation[] noteTrans = new ITNoteTranslation[120];

		int[] itiMap = new int[255];
		int[] itiInvMap = new int[255];
		int itiNumAllocated = 0;

		for (int j = 0; j < 255; j++)
			itiMap[j] = -1;

		for (int j = 0; j < 120; j++)
		{
			noteTrans[j].Note = (byte)(instrument.NoteMap[j] - 1);

			if (itiFile)
			{
				int o = instrument.SampleMap[j];

				if (o > 0 && o < 255 && itiMap[o] == -1)
				{
					itiMap[o] = itiNumAllocated;
					itiInvMap[itiNumAllocated] = o;
					itiNumAllocated++;
				}

				noteTrans[j].Sample = (byte)(itiMap[o]+1);
			}
			else
				noteTrans[j].Sample = instrument.SampleMap[j];
		}

		if (itiFile)
			iti.NumberOfSamples = (byte)itiNumAllocated;

		for (int i = 0; i < 120; i++)
			file.WriteStructure(noteTrans[i]);

		SaveEnvelopes(instrument, file);

		/* unused padding */
		file.WriteStructure(0);

		// ITI files *need* to write 554 bytes due to alignment, but in a song it doesn't matter
		if (itiFile)
		{
			var its = new ITS();

			long pos = file.Position;

			Assert.IsTrue(() => pos == 554, "ITI file headers should always be 554 bytes long");

			/* okay, now go through samples */
			for (int j = 0; j < itiNumAllocated; j++)
			{
				int o = itiInvMap[j];

				itiMap[o] = (int)pos;
				pos += 80; /* header is 80 bytes */
				its.SaveHeader(song.Samples[o]!, file);
			}

			for (int j = 0; j < itiNumAllocated; j++)
			{
				int o = itiInvMap[j];

				var smp = song.Samples[o];

				if (smp == null)
					throw new Exception("Internal error: Sample was null and shouldn't have been");

				long op = file.Position;

				// inject sample data pointer
				file.Position = itiMap[o] + 0x48;
				file.WriteStructure((int)op);
				file.Position = op;

				SampleFileConverter.WriteSample(file, smp, SampleFormat.LittleEndian | SampleFormat.PCMSigned
					| (smp.Flags.HasFlag(SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8)
					| (smp.Flags.HasFlag(SampleFlags.Stereo) ? SampleFormat.StereoSplit : SampleFormat.Mono),
					uint.MaxValue);
			}
		}
	}

	public override SaveResult SaveInstrument(Song song, SongInstrument instrument, Stream file)
	{
		SaveInstrument(song, instrument, file, itiFile: true);

		return SaveResult.Success;
	}
}
