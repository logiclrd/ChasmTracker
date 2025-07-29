using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.FileTypes.Converters;

using ChasmTracker.Configurations;
using ChasmTracker.FileSystem;
using ChasmTracker.FileTypes.InstrumentConverters;
using ChasmTracker.FileTypes.SampleConverters;
using ChasmTracker.MIDI;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class IT : SongFileConverter
{
	public override string Label => "IT";
	public override string Description => "Impulse Tracker";
	public override string Extension => ".it";

	public override int SortOrder => 5;
	public override int SaveOrder => 1;

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct ITFile
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public byte[] ID;                    // 0x4D504D49
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 26)]
		public byte[] SongName;
		public byte HilightMinor;
		public byte HilightMajor;
		public short OrdNum;
		public short InsNum;
		public short SmpNum;
		public short PatNum;
		public ushort CreatedWithTrackerVersion;
		public ushort CompatibleWithTrackerVersion;
		public short Flags;
		public short Special;
		public byte GlobalVolume;
		public byte MixVolume;
		public byte Speed;
		public byte Tempo;
		public byte StereoSeparation;
		public byte PitchWheelDepth;
		public short MsgLength;
		public int MsgOffset;
		public uint Reserved;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
		public byte[] ChannelPan;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
		public byte[] ChannelVolume;
	}

	static ITFile LoadHeader(Stream stream)
	{
		var hdr = stream.ReadStructure<ITFile>();

		if (hdr.ID.ToStringZ() != "IMPM")
			throw new FormatException();

		/* replace NUL bytes with spaces */
		for (int n = 0; n < hdr.SongName.Length; n++)
			if (hdr.SongName[n] == 0)
				hdr.SongName[n] = 0x20;

		return hdr;
	}

	static bool WriteHeader(ITFile hdr, Stream fp)
	{
		try
		{
			fp.WriteStructure(hdr);
			return true;
		}
		catch
		{
			return false;
		}
	}

	/* --------------------------------------------------------------------- */

	const int EOF = -1;

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			long startPosition = stream.Position;

			var hdr = LoadHeader(stream);

			if (hdr.SmpNum >= Constants.MaxSamples)
				return false;

			// Skip order
			stream.Position += hdr.OrdNum;

			// Skip instrument parapointers
			stream.Position += 4 * hdr.InsNum;

			byte[] paraSmpBytes = new byte[4 * hdr.SmpNum];

			stream.ReadExactly(paraSmpBytes);

			int paraMin = (hdr.Special.HasBitSet(1) && (hdr.MsgLength != 0)) ? hdr.MsgOffset : (int)stream.Length;

			int[] paraSmp = new int[hdr.SmpNum];

			for (int n = 0; n < hdr.SmpNum; n++)
			{
				paraSmp[n] = BitConverter.ToInt32(paraSmpBytes, n * 4);
				if (paraSmp[n] < paraMin)
					paraMin = paraSmp[n];
			}

			/* try to find a compressed sample and set
			 * the description accordingly */
			file.Description = "Impulse Tracker";
			for (int n = 0; n < hdr.SmpNum; n++)
			{
				stream.Position = startPosition + paraSmp[n];

				stream.Position += 18; // skip to flags

				int flags = stream.ReadByte();
				if (flags == EOF)
					return false;

				// compressed ?
				if (flags.HasBitSet(8))
				{
					file.Description = "Compressed Impulse Tracker";
					break;
				}
			}

			/*file.Extension = "it";*/
			file.Title = hdr.SongName.ToStringZ().TrimEnd();
			file.Type = FileSystem.FileTypes.ModuleIT;

			return true;
		}
		catch
		{
			return false;
		}
	}

	/* --------------------------------------------------------------------- */

	/* pattern mask variable bits */
	[Flags]
	enum NoteFields : byte
	{
		Note = 1,
		Sample = 2,
		Volume = 4,
		Effect = 8,
		SameNote = 16,
		SameSample = 32,
		SameVolume = 64,
		SameEffect = 128,
	}

	/* --------------------------------------------------------------------- */

	static void ImportVolumeEffect(ref SongNote note, byte v)
	{
		byte adj;

		if (v <= 64)                   { adj =   0; note.VolumeEffect = VolumeEffects.Volume; }
		else if (v >= 128 && v <= 192) { adj = 128; note.VolumeEffect = VolumeEffects.Panning; }
		else if (v >= 65 && v <= 74)   { adj =  65; note.VolumeEffect = VolumeEffects.FineVolumeUp; }
		else if (v >= 75 && v <= 84)   { adj =  75; note.VolumeEffect = VolumeEffects.FineVolumeDown; }
		else if (v >= 85 && v <= 94)   { adj =  85; note.VolumeEffect = VolumeEffects.VolumeSlideUp; }
		else if (v >= 95 && v <= 104)  { adj =  95; note.VolumeEffect = VolumeEffects.VolumeSlideDown; }
		else if (v >= 105 && v <= 114) { adj = 105; note.VolumeEffect = VolumeEffects.PortamentoDown; }
		else if (v >= 115 && v <= 124) { adj = 115; note.VolumeEffect = VolumeEffects.PortamentoUp; }
		else if (v >= 193 && v <= 202) { adj = 193; note.VolumeEffect = VolumeEffects.TonePortamento; }
		else if (v >= 203 && v <= 212) { adj = 203; note.VolumeEffect = VolumeEffects.VibratoDepth; }
		else { return; }

		v -= adj;

		note.VolumeParameter = v;
	}

	const int MaxChannels = 64;

	static void LoadPattern(Pattern pattern, Stream fp, int rows, int createdWithTrackerVersion)
	{
		SongNote[] lastNote = new SongNote[MaxChannels];
		NoteFields[] lastMask = new NoteFields[64];

		int row = 0;

		while (row < rows)
		{
			var patternRow = pattern.Rows[row];

			int chanVar = fp.ReadByte();

			if (chanVar == 255 && (fp.Position >= fp.Length))
			{
				/* truncated file? we might want to complain or something ... eh. */
				return;
			}

			if (chanVar == 0)
			{
				row++;
				continue;
			}

			int chan = (chanVar - 1) & 63;

			NoteFields maskVar;

			if (chanVar.HasBitSet(128))
			{
				maskVar = (NoteFields)fp.ReadByte();
				lastMask[chan] = maskVar;
			}
			else
				maskVar = lastMask[chan];

			if (maskVar.HasFlag(NoteFields.Note))
			{
				int c = fp.ReadByte();

				if (c == 255)
					c = SpecialNotes.NoteOff;
				else if (c == 254)
					c = SpecialNotes.NoteCut;
				// internally IT uses note 253 as its blank value, but loading it as such is probably
				// undesirable since old Schism Tracker used this value incorrectly for note fade
				//else if (c == 253)
				//      c = SpecialNotes.None;
				else if (c > 119)
					c = SpecialNotes.NoteFade;
				else
					c += SpecialNotes.First;

				patternRow[chan].Note = (byte)c;
				lastNote[chan].Note = patternRow[chan].Note;
			}

			if (maskVar.HasFlag(NoteFields.Sample))
			{
				patternRow[chan].Instrument = (byte)fp.ReadByte();
				lastNote[chan].Instrument = patternRow[chan].Instrument;
			}

			if (maskVar.HasFlag(NoteFields.Volume))
			{
				ImportVolumeEffect(ref patternRow[chan], (byte)fp.ReadByte());
				lastNote[chan].VolumeEffect = patternRow[chan].VolumeEffect;
				lastNote[chan].VolumeParameter = patternRow[chan].VolumeParameter;
			}

			if (maskVar.HasFlag(NoteFields.Effect))
			{
				patternRow[chan].EffectByte = (byte)(fp.ReadByte() & 0x1f);
				patternRow[chan].Parameter = (byte)fp.ReadByte();

				EffectUtility.ImportS3MEffect(ref patternRow[chan], true);

				if (patternRow[chan].Effect == Effects.Special && (patternRow[chan].Parameter & 0xf0) == 0xa0 && createdWithTrackerVersion < 0x0200)
				{
					// IT 1.xx does not support high offset command
					patternRow[chan].Effect = Effects.None;
				}
				else if (patternRow[chan].Effect == Effects.GlobalVolume && patternRow[chan].Parameter > 0x80 && createdWithTrackerVersion >= 0x1000 && createdWithTrackerVersion <= 0x1050)
				{
					// Fix handling of commands V81-VFF in ITs made with old Schism Tracker versions
					// (fixed in commit ab5517d4730d4c717f7ebffb401445679bd30888 - one of the last versions to identify as v0.50)
					patternRow[chan].Parameter = 0x80;
				}

				lastNote[chan].Effect = patternRow[chan].Effect;
				lastNote[chan].Parameter = patternRow[chan].Parameter;
			}

			if (maskVar.HasFlag(NoteFields.SameNote))
				patternRow[chan].Note = lastNote[chan].Note;
			if (maskVar.HasFlag(NoteFields.SameSample))
				patternRow[chan].Instrument = lastNote[chan].Instrument;

			if (maskVar.HasFlag(NoteFields.SameVolume))
			{
				patternRow[chan].VolumeEffect = lastNote[chan].VolumeEffect;
				patternRow[chan].VolumeParameter = lastNote[chan].VolumeParameter;
			}

			if (maskVar.HasFlag(NoteFields.SameEffect))
			{
				patternRow[chan].Effect = lastNote[chan].Effect;
				patternRow[chan].Parameter = lastNote[chan].Parameter;
			}
		}
	}

	public static MIDIConfiguration? ReadMIDIConfig(Stream fp)
	{
		var midi = new MIDIConfiguration();

		/* preserving this just for compat with old behavior  --paper */
		if (fp.Position + 4896 > fp.Length)
			return null;

		byte[] buffer = new byte[32];

		string ReadMIDIMacro()
		{
			fp.ReadExactly(buffer);
			return buffer.ToStringZ();
		}

		try
		{
			/* everything in this structure *should* be word
			 * aligned on basically every platform imaginable,
			 * but its trivial to get around the implementation
			 * defined behavior here if some stupid back-asswards
			 * platform decides to put random padding. */
			midi.Start = ReadMIDIMacro();
			midi.Stop = ReadMIDIMacro();
			midi.Tick = ReadMIDIMacro();
			midi.NoteOn = ReadMIDIMacro();
			midi.NoteOff = ReadMIDIMacro();
			midi.SetVolume = ReadMIDIMacro();
			midi.SetPanning = ReadMIDIMacro();
			midi.SetBank = ReadMIDIMacro();
			midi.SetProgram = ReadMIDIMacro();
			for (int i = 0; i < midi.SFx.Length; i++)
				midi.SFx[i] = ReadMIDIMacro();
			for (int i = 0; i < midi.Zxx.Length; i++)
				midi.Zxx[i] = ReadMIDIMacro();

			return midi;
		}
		catch
		{
			return null;
		}
	}

	void WriteMIDIConfig(MIDIConfiguration midi, Stream fp)
	{
		byte[] buffer = new byte[32];

		void WriteMIDIMacro(string? macro)
		{
			Array.Clear(buffer);
			if (macro != null)
				Encoding.ASCII.GetBytes(macro, 0, macro.Length, buffer, 0);
			fp.Write(buffer);
		}

		WriteMIDIMacro(midi.Start);
		WriteMIDIMacro(midi.Stop);
		WriteMIDIMacro(midi.Tick);
		WriteMIDIMacro(midi.NoteOn);
		WriteMIDIMacro(midi.NoteOff);
		WriteMIDIMacro(midi.SetVolume);
		WriteMIDIMacro(midi.SetPanning);
		WriteMIDIMacro(midi.SetBank);
		WriteMIDIMacro(midi.SetProgram);
		for (int i = 0; i < midi.SFx.Length; i++)
			WriteMIDIMacro(midi.SFx[i]);
		for (int i = 0; i < midi.Zxx.Length; i++)
			WriteMIDIMacro(midi.Zxx[i]);
	}

	public override Song LoadSong(Stream stream, LoadFlags flags)
	{
		var hdr = LoadHeader(stream);

		// Screwy limits?
		if (hdr.InsNum > Constants.MaxInstruments || hdr.SmpNum > Constants.MaxSamples
			|| hdr.PatNum > Constants.MaxPatterns)
			throw new FormatException();

		var song = new Song();

		song.Title = hdr.SongName.ToStringZ().TrimEnd();

		bool ignoreMIDI = false;
		MIDIFlags midiFlags = default;
		byte midiPitchWheelDepth = default;

		if (hdr.CompatibleWithTrackerVersion < 0x0214 && hdr.CreatedWithTrackerVersion < 0x0214)
			ignoreMIDI = true;

		if (hdr.Special.HasBitSet(4))
		{
			/* "reserved" bit, experimentally determined to indicate presence of otherwise-documented row
			highlight information - introduced in IT 2.13. Formerly checked cwtv here, but that's lame :)
			XXX does any tracker save highlight but *not* set this bit? (old Schism versions maybe?) */
			song.RowHighlightMinor = hdr.HilightMinor;
			song.RowHighlightMajor = hdr.HilightMajor;
		}
		else
		{
			song.RowHighlightMinor = 4;
			song.RowHighlightMajor = 16;
		}

		if (!hdr.Flags.HasBitSet(1))
			song.Flags |= SongFlags.NoStereo;
		// (hdr.Flags.HasFlag(2)) no longer used (was vol0 optimizations)
		if (hdr.Flags.HasBitSet(4))
			song.Flags |= SongFlags.InstrumentMode;
		if (hdr.Flags.HasBitSet(8))
			song.Flags |= SongFlags.LinearSlides;
		if (hdr.Flags.HasBitSet(16))
			song.Flags |= SongFlags.ITOldEffects;
		if (hdr.Flags.HasBitSet(32))
			song.Flags |= SongFlags.CompatibleGXX;

		if (hdr.Flags.HasBitSet(64))
		{
			midiFlags |= MIDIFlags.PitchBend;
			midiPitchWheelDepth = hdr.PitchWheelDepth;
		}
		if (hdr.Flags.HasBitSet(128) && !ignoreMIDI)
			song.Flags |= SongFlags.EmbedMIDIConfig;
		else
			song.Flags &= ~SongFlags.EmbedMIDIConfig;

		song.InitialGlobalVolume = Math.Min(hdr.GlobalVolume, (byte)128);
		song.MixingVolume = Math.Min(hdr.MixVolume, (byte)128);
		song.InitialSpeed = (hdr.Speed != 0) ? hdr.Speed : 6;
		song.InitialTempo = Math.Max(hdr.Tempo, (byte)31);
		song.PanSeparation = hdr.StereoSeparation;

		for (int n = 0; n < MaxChannels; n++)
		{
			var channel = song.Channels[n];

			int pan = hdr.ChannelPan[n];

			if (pan.HasBitSet(128))
			{
				channel.Flags |= ChannelFlags.Mute;
				pan &= ~128;
			}

			if (pan == 100)
			{
				channel.Flags |= ChannelFlags.Surround;
				channel.Panning = 32;
			}
			else
				channel.Panning = Math.Min(pan, 64);

			channel.Panning *= 4; //mphack
			channel.Volume = Math.Min(hdr.ChannelVolume[n], (byte)64);
		}

		byte[] orderList = new byte[hdr.OrdNum];

		stream.ReadExactly(orderList);

		foreach (int order in orderList)
			song.OrderList.Add(order);

		/* show a warning in the message log if there's too many orders */
		if (hdr.OrdNum > Constants.MaxOrders)
		{
			bool showWarning = true;

			/* special exception: ordnum == 257 is valid ONLY if the final order is ORDER_LAST */
			if ((hdr.OrdNum == Constants.MaxOrders + 1) && song.OrderList.Last() == SpecialOrders.Last)
				showWarning = false;

			if (showWarning)
				Log.Append(4, " Warning: Too many orders in the order list");
		}

		byte[] paraPointerBytes = new byte[4 * (hdr.InsNum + hdr.SmpNum + hdr.PatNum)];

		stream.ReadExactly(paraPointerBytes);

		int[] paraIns = new int[hdr.InsNum];
		int[] paraSmp = new int[hdr.SmpNum];
		int[] paraPat = new int[hdr.PatNum];

		for (int i = 0; i < hdr.InsNum; i++)
			paraIns[i] = BitConverter.ToInt32(paraPointerBytes, i * 4);
		for (int i = 0; i < hdr.SmpNum; i++)
			paraSmp[i] = BitConverter.ToInt32(paraPointerBytes, (i + hdr.InsNum) * 4);
		for (int i = 0; i < hdr.PatNum; i++)
			paraPat[i] = BitConverter.ToInt32(paraPointerBytes, (i + hdr.InsNum + hdr.PatNum) * 4);

		int paraMin = (hdr.Special.HasBitSet(1) && (hdr.MsgLength != 0)) ? hdr.MsgOffset : (int)stream.Length;

		paraMin = Math.Min(paraMin, paraIns.Min());
		paraMin = Math.Min(paraMin, paraSmp.Min());
		paraMin = Math.Min(paraMin, paraPat.Min());

		byte[] shortBuffer = new byte[2];
		byte[] intBuffer = new byte[4];

		int hist = 0;

		if (hdr.Special.HasBitSet(2))
		{
			stream.ReadExactly(shortBuffer);

			hist = BitConverter.ToInt16(shortBuffer);

			if (paraMin < stream.Position + 8 * hist)
			{
				/* History data overlaps the parapointers. Discard it, it's probably broken.
				Some programs, notably older versions of Schism Tracker, set the history flag
				but didn't actually write any data, so the "length" we just read is actually
				some other data in the file. */
				hist = 0;
			}
		}
		else
		{
			// History flag isn't even set. Probably an old version of Impulse Tracker.
		}

		if (hist != 0)
		{
			for (int i = 0; i < hist; i++)
			{
				var historyEntry = new SongHistory();

				// handle the date
				stream.ReadExactly(shortBuffer);
				var fatDate = BitConverter.ToUInt16(shortBuffer);

				stream.ReadExactly(shortBuffer);
				var fatTime = BitConverter.ToUInt16(shortBuffer);

				if (fatDate != 0)
				{
					historyEntry.Time = DateTimeConversions.FATDateToDateTime(fatDate, fatTime);
					historyEntry.TimeValid = true;
				}

				// now deal with the runtime
				stream.ReadExactly(intBuffer);

				uint dosRunTime = BitConverter.ToUInt32(intBuffer);

				historyEntry.Runtime = DateTimeConversions.DOSTimeToTimeSpan(dosRunTime);

				song.History.Add(historyEntry);
			}
		}

		if (ignoreMIDI)
		{
			if (hdr.Special.HasBitSet(8))
			{
				Log.Append(4, " Warning: ignoring embedded MIDI data (CWTV/CMWT is too old)");
				stream.Position += 4896;
			}

			song.MIDIConfig = new MIDIConfiguration();
		}
		else if (hdr.Special.HasBitSet(8))
			song.MIDIConfig = ReadMIDIConfig(stream) ?? song.MIDIConfig;

		string? trackerID = null;

		if (song.History.Count == 0)
		{
			// berotracker check
			stream.ReadExactly(intBuffer);

			if (intBuffer.ToStringZ() == "MODU")
				trackerID = "BeRoTracker";
		}

		if (hdr.Special.HasBitSet(1) && (hdr.MsgLength != 0) && (hdr.MsgOffset + hdr.MsgLength < stream.Length))
		{
			int messageLength = Math.Min(Constants.MaxMessage, (int)hdr.MsgLength);

			stream.Position = hdr.MsgOffset;

			byte[] buffer = new byte[messageLength];

			stream.ReadExactly(buffer);

			song.Message = buffer.ToStringZ();
		}

		if (!flags.HasFlag(LoadFlags.NoSamples))
		{
			var iti = new ITI();
			var its = new ITS();

			for (int n = 0; n < hdr.InsNum; n++)
			{
				if (paraIns[n] == 0)
					continue;

				stream.Position = paraIns[n];

				var inst = song.GetInstrument(n + 1);

				if (hdr.CompatibleWithTrackerVersion >= 0x0200)
					iti.LoadInstrument(null, inst, stream);
				else
					iti.LoadInstrumentOld(inst, stream);
			}

			for (int n = 0; n < hdr.SmpNum; n++)
			{
				stream.Position = paraSmp[n];

				its.TryLoadSample(stream, hdr.CreatedWithTrackerVersion, out var sample);

				song.Samples[n + 1] = sample;
			}
		}

		if (!flags.HasFlag(LoadFlags.NoPatterns))
		{
			for (int n = 0; n < hdr.PatNum; n++)
			{
				if (paraPat[n] == 0)
					continue;

				stream.Position = paraPat[n];

				stream.ReadExactly(shortBuffer);
				int bytes = BitConverter.ToInt16(shortBuffer);

				stream.ReadExactly(shortBuffer);
				int rows = BitConverter.ToInt16(shortBuffer);

				stream.Position += 4;

				song.Patterns[n] = new Pattern(rows);

				LoadPattern(song.Patterns[n], stream, rows, hdr.CreatedWithTrackerVersion);

				int got = (int)(stream.Position - paraPat[n] - 8);

				if (bytes != got)
					Log.Append(4, " Warning: Pattern {0}: size mismatch" +
						" (expected {1} bytes, got {2})",
						n, bytes, got);
			}
		}

		// XXX 32 CHARACTER MAX XXX

		bool modplug = false;

		if (trackerID != null)
		{
			// BeroTracker (detected above)
		}
		else if ((hdr.CreatedWithTrackerVersion >> 12) == 1)
		{
			trackerID = null;
			song.TrackerID = "Schism Tracker " + Version.DecodeCreatedWithTrackerVersion(hdr.CreatedWithTrackerVersion, hdr.Reserved);
		}
		else if ((hdr.CreatedWithTrackerVersion >> 12) == 0 && hist != 0 && (hdr.Reserved != 0))
		{
			// early catch to exclude possible false positives without repeating a bunch of stuff.
		}
		else if (hdr.CreatedWithTrackerVersion == 0x0214 && hdr.CompatibleWithTrackerVersion == 0x0200
				 && hdr.Flags == 9 && hdr.Special == 0
				 && hdr.HilightMajor == 0 && hdr.HilightMinor == 0
				 && hdr.InsNum == 0 && hdr.PatNum + 1 == hdr.OrdNum
				 && hdr.GlobalVolume == 128 && hdr.MixVolume == 100 && hdr.Speed == 1 && hdr.StereoSeparation == 128 && hdr.PitchWheelDepth == 0
				 && hdr.MsgLength == 0 && hdr.MsgOffset == 0 && hdr.Reserved == 0)
		{
			// :)
			trackerID = "OpenSPC conversion";
		}
		else if ((hdr.CreatedWithTrackerVersion >> 12) == 5)
		{
			if (hdr.Reserved == 0x54504d4f)
				trackerID = "OpenMPT %d.%02x";
			else if (hdr.CreatedWithTrackerVersion < 0x5129 || !hdr.Reserved.HasAnyBitSet(0xffff))
				trackerID = "OpenMPT %d.%02x (compat.)";
			else
				song.TrackerID = string.Format("OpenMPT {0}.{1:x2}.{2:x2}.{3:x2} (compat.)", (hdr.CreatedWithTrackerVersion & 0xf00) >> 8, hdr.CreatedWithTrackerVersion & 0xff, (hdr.Reserved >> 8) & 0xff, (hdr.Reserved & 0xff));
			modplug = true;
		}
		else if (hdr.CreatedWithTrackerVersion == 0x0888 && hdr.CompatibleWithTrackerVersion == 0x0888 && hdr.Reserved == 0/* && hdr.OrdNum == 256*/)
		{
			// erh.
			// There's a way to identify the exact version apparently, but it seems too much trouble
			// (ordinarily ordnum == 256, but I have encountered at least one file for which this is NOT
			// the case (trackit_r2.it by dsck) and no other trackers I know of use 0x0888)
			trackerID = "OpenMPT 1.17+";
			modplug = true;
		}
		else if (hdr.CreatedWithTrackerVersion == 0x0300 && hdr.CompatibleWithTrackerVersion == 0x0300 && hdr.Reserved == 0 && hdr.OrdNum == 256 && hdr.StereoSeparation == 128 && hdr.PitchWheelDepth == 0)
		{
			trackerID = "OpenMPT 1.17.02.20 - 1.17.02.25";
			modplug = true;
		}
		else if (hdr.CreatedWithTrackerVersion == 0x0217 && hdr.CompatibleWithTrackerVersion == 0x0200 && hdr.Reserved == 0)
		{
			bool ompt = false;

			if (hdr.InsNum > 0)
			{
				// check trkvers -- OpenMPT writes 0x0220; older MPT writes 0x0211
				stream.Position = paraIns[0] + 0x1C;

				stream.ReadExactly(shortBuffer);

				int tmp = BitConverter.ToInt16(shortBuffer);

				if (tmp == 0x0220)
					ompt = true;
			}

			if (!ompt && (Array.IndexOf(hdr.ChannelPan, (byte)0xFF) < 0))
			{
				// MPT 1.16 writes 0xff for unused channels; OpenMPT never does this
				// XXX this is a false positive if all 64 channels are actually in use
				// -- but then again, who would use 64 channels and not instrument mode?
				ompt = true;
			}

			trackerID = (ompt
				? "OpenMPT (compatibility mode)"
				: "Modplug Tracker 1.09 - 1.16");
			modplug = true;
		}
		else if (hdr.CreatedWithTrackerVersion == 0x0214 && hdr.CompatibleWithTrackerVersion == 0x0200 && hdr.Reserved == 0)
		{
			// instruments 560 bytes apart
			trackerID = "Modplug Tracker 1.00a5";
			modplug = true;
		}
		else if (hdr.CreatedWithTrackerVersion == 0x0214 && hdr.CompatibleWithTrackerVersion == 0x0202 && hdr.Reserved == 0)
		{
			// instruments 557 bytes apart
			trackerID = "Modplug Tracker b3.3 - 1.07";
			modplug = true;
		}
		else if (hdr.CreatedWithTrackerVersion == 0x0214 && hdr.CompatibleWithTrackerVersion == 0x0214 && hdr.Reserved == 0x49424843)
		{
			// sample data stored directly after header
			// all sample/instrument filenames say "-DEPRECATED-"
			// 0xa for message newlines instead of 0xd
			trackerID = "ChibiTracker";
		}
		else if (hdr.CreatedWithTrackerVersion == 0x0214 && hdr.CompatibleWithTrackerVersion == 0x0214 && (hdr.Flags & 0x10C6) == 4 && hdr.Special <= 1 && hdr.Reserved == 0)
		{
			// sample data stored directly after header
			// all sample/instrument filenames say "XXXXXXXX.YYY"
			trackerID = "CheeseTracker?";
		}
		else if ((hdr.CreatedWithTrackerVersion >> 12) == 0)
		{
			// Catch-all. The above IT condition only works for newer IT versions which write something
			// into the reserved field; older IT versions put zero there (which suggests that maybe it
			// really is being used for something useful)
			// (handled below)
		}
		else
			trackerID = "Unknown tracker";

		// argh
		if ((trackerID == null) && (hdr.CreatedWithTrackerVersion >> 12) == 0)
		{
			trackerID = "Impulse Tracker {0}.{1:x2}";

			if (hdr.CompatibleWithTrackerVersion > 0x0214)
				hdr.CreatedWithTrackerVersion = 0x0215;
			else if (hdr.CreatedWithTrackerVersion >= 0x0215 && hdr.CreatedWithTrackerVersion <= 0x0217)
			{
				trackerID = null;
				string[] versions = { "1-2", "3", "4-5" };
				song.TrackerID = "Impulse Tracker 2.14p" + versions[hdr.CreatedWithTrackerVersion - 0x0215];
			}

			if (hdr.CreatedWithTrackerVersion >= 0x0207 && (song.History.Count == 0) && (hdr.Reserved != 0))
			{
				// Starting from version 2.07, IT stores the total edit
				// time of a module in the "reserved" field
				song.History.Clear();

				uint runtime = DateTimeConversions.DecodeEditTimer(hdr.CreatedWithTrackerVersion, hdr.Reserved);

				song.History.Add(new SongHistory() { Runtime = DateTimeConversions.DOSTimeToTimeSpan(runtime) });
			}

			//"saved {0} time{1}", hist, (hist == 1) ? "" : "s"
		}

		if (trackerID != null)
			song.TrackerID = string.Format(trackerID, (hdr.CreatedWithTrackerVersion & 0xf00) >> 8, hdr.CreatedWithTrackerVersion & 0xff);

		if (modplug)
		{
			/* The encoding of songs saved by Modplug (and OpenMPT) is dependent
			 * on the current system encoding, which will be Windows-1252 in 99%
			 * of cases. However, some modules made in other (usually Asian) countries
			 * will be encoded in other character sets like Shift-JIS or UHC (Korean).
			 *
			 * There really isn't a good way to detect this aside from some heuristics
			 * (JIS is fairly easy to detect, for example) and honestly it's a bit
			 * more trouble than its really worth to deal with those edge cases when
			 * we can't even display those characters correctly anyway.
			 *
			 *  - paper */
			var windows1252 = Encoding.GetEncoding(1252);

			string Convert(string str)
			{
				// TODO: is this doing the right thing??
				byte[] bytes = Encoding.ASCII.GetBytes(str);

				return windows1252.GetString(bytes);
			}

			string? ConvertN(string? str)
			{
				if (str == null)
					return null;
				else
					return Convert(str);
			}

			song.Title = Convert(song.Title);

			for (int n = 0; n < hdr.InsNum; n++)
			{
				var inst = song.Instruments[n + 1];

				if (inst == null)
					continue;

				inst.Name = ConvertN(inst.Name);
				inst.FileName = ConvertN(inst.FileName);
			}

			for (int n = 0; n < hdr.SmpNum; n++)
			{
				var sample = song.Samples[n + 1];

				if (sample == null)
					continue;

				sample.Name = Convert(sample.Name);
				sample.FileName = Convert(sample.FileName);
			}

			song.Message = Convert(song.Message);
		}

		return song;
	}

	/* ---------------------------------------------------------------------- */
	/* saving routines */

	[Flags]
	enum Warnings
	{
		AdLibSamples = 1,
	}

	static Dictionary<Warnings, string> ITWarnings =
		new Dictionary<Warnings, string>()
		{
			{ Warnings.AdLibSamples, "AdLib samples" },
		};

	// NOBODY expects the Spanish Inquisition!
	static void SavePattern(Stream fp, Pattern pat)
	{
		SongNote[] lastNote = new SongNote[64];
		byte[] initMask = new byte[64];
		byte[] lastMask = new byte[64];
		int pos = 0;
		byte[] data = new byte[65536];

		for (int i = 0; i < lastMask.Length; i++)
			lastMask[i] = 0xFF;

		for (int row = 0; row < pat.Rows.Count; row++)
		{
			for (int chan = 0; chan < 64; chan++)
			{
				ref var noteptr = ref pat[row][chan + 1];

				byte m = 0;  // current mask
				int vol = -1;
				int note = noteptr.Note;
				byte effect = noteptr.EffectByte, param = noteptr.Parameter;

				if (note != 0)
				{
					m |= 1;
					if (note < 0x80)
						note--;
				}

				if (noteptr.Instrument != 0)
					m |= 2;

				switch (noteptr.VolumeEffect)
				{
					default: break;
					case VolumeEffects.Volume: vol = Math.Min(noteptr.VolumeParameter, (byte)64); break;
					case VolumeEffects.FineVolumeUp: vol = Math.Min(noteptr.VolumeParameter, (byte)9) + 65; break;
					case VolumeEffects.FineVolumeDown: vol = Math.Min(noteptr.VolumeParameter, (byte)9) + 75; break;
					case VolumeEffects.VolumeSlideUp: vol = Math.Min(noteptr.VolumeParameter, (byte)9) + 85; break;
					case VolumeEffects.VolumeSlideDown: vol = Math.Min(noteptr.VolumeParameter, (byte)9) + 95; break;
					case VolumeEffects.PortamentoDown: vol = Math.Min(noteptr.VolumeParameter, (byte)9) + 105; break;
					case VolumeEffects.PortamentoUp: vol = Math.Min(noteptr.VolumeParameter, (byte)9) + 115; break;
					case VolumeEffects.Panning: vol = Math.Min(noteptr.VolumeParameter, (byte)64) + 128; break;
					case VolumeEffects.VibratoDepth: vol = Math.Min(noteptr.VolumeParameter, (byte)9) + 203; break;
					case VolumeEffects.VibratoSpeed: vol = 203; break;
					case VolumeEffects.TonePortamento: vol = Math.Min(noteptr.VolumeParameter, (byte)9) + 193; break;
				}

				if (vol != -1)
					m |= 4;

				EffectUtility.ExportS3MEffect(ref effect, ref param, true);

				if ((effect != 0) || (param != 0))
					m |= 8;

				if (m == 0)
					continue;

				if (m.HasBitSet(1))
				{
					if ((note == lastNote[chan].Note) && initMask[chan].HasBitSet(1))
					{
						m &= unchecked((byte)~1);
						m |= 0x10;
					}
					else
					{
						lastNote[chan].Note = (byte)note;
						initMask[chan] |= 1;
					}
				}

				if (m.HasBitSet(2))
				{
					if ((noteptr.Instrument == lastNote[chan].Instrument) && initMask[chan].HasBitSet(2))
					{
						m &= unchecked((byte)~2);
						m |= 0x20;
					}
					else
					{
						lastNote[chan].Instrument = noteptr.Instrument;
						initMask[chan] |= 2;
					}
				}

				if (m.HasBitSet(4))
				{
					if ((vol == lastNote[chan].VolumeParameter) && initMask[chan].HasBitSet(4))
					{
						m &= unchecked((byte)~4);
						m |= 0x40;
					}
					else
					{
						lastNote[chan].VolumeParameter = (byte)vol;
						initMask[chan] |= 4;
					}
				}

				if (m.HasBitSet(8))
				{
					if ((effect == lastNote[chan].EffectByte) && (param == lastNote[chan].Parameter)
							&& initMask[chan].HasBitSet(8))
					{
						m &= unchecked((byte)~8);
						m |= 0x80;
					}
					else
					{
						lastNote[chan].EffectByte = effect;
						lastNote[chan].Parameter = param;
						initMask[chan] |= 8;
					}
				}

				if (m == lastMask[chan])
					data[pos++] = (byte)(chan + 1);
				else
				{
					lastMask[chan] = m;
					data[pos++] = (byte)((chan + 1) | 0x80);
					data[pos++] = m;
				}

				if (m.HasBitSet(1)) data[pos++] = (byte)note;
				if (m.HasBitSet(2)) data[pos++] = noteptr.Instrument;
				if (m.HasBitSet(4)) data[pos++] = (byte)vol;
				if (m.HasBitSet(8)) { data[pos++] = effect; data[pos++] = param; }
			} // end channel
			data[pos++] = 0;
		} // end row

		// write the data to the file (finally!)
		byte[] buffer = new byte[8];

		BitConverter.TryWriteBytes(buffer, pos);
		BitConverter.TryWriteBytes(buffer.Slice(2), pos);
		// buffer[4..7] are meaningless

		fp.Write(buffer);
		fp.Write(data, 0, pos);
	}

	public override bool CanSave => true;

	public override SaveResult SaveSong(Song song, Stream fp)
	{
		ITFile hdr = new ITFile();

		int[] paraIns = new int[256];
		int[] paraSmp = new int[256];
		int[] paraPat = new int[256];

		// how much extra data is stuffed between the parapointers and the rest of the file
		// (2 bytes for edit history length, and 8 per entry including the current session)
		int extra = 2 + 8 * song.History.Count + 8;

		// warnings for unsupported features
		Warnings warn = 0;

		// TODO complain about nonstandard stuff? or just stop saving it to begin with

		/* IT always saves at least two orders -- and requires an extra order at the end (which gets chopped!)
		However, the loader refuses to load files with too much data in the orderlist, so in the pathological
		case where order 255 has data, writing an extra 0xFF at the end will result in a file that can't be
		loaded back (for now). Eventually this can be fixed, but at least for a while it's probably a great
		idea not to save things that other versions won't load. */
		int nOrd = song.GetOrderCount();

		nOrd = (nOrd + 1).Clamp(2, Constants.MaxOrders);

		int nIns = song.GetInstrumentCount();
		int nSmp = song.GetSampleCount();

		// IT always saves at least one pattern.
		int nPat = song.GetPatternCount();
		if (nPat == 0)
			nPat = 1;

		hdr.ID = BitConverter.GetBytes(0x4D504D49); // IMPM
		hdr.SongName = new byte[26];
		Encoding.ASCII.GetBytes(song.Title, 0, song.Title.Length, hdr.SongName, 0);
		hdr.SongName[25] = 0; // why ?
		hdr.HilightMajor = (byte)song.RowHighlightMajor;
		hdr.HilightMinor = (byte)song.RowHighlightMinor;
		hdr.OrdNum = (byte)nOrd;
		hdr.InsNum = (byte)nIns;
		hdr.SmpNum = (byte)nSmp;
		hdr.PatNum = (byte)nPat;
		// No one else seems to be using the cwtv's tracker id number, so I'm gonna take 1. :)
		hdr.CreatedWithTrackerVersion = (ushort)(0x1000 | Version.CreatedWithTrackerVersion); // cwtv 0xtxyy = tracker id t, version x.yy

		// compat:
		//     really simple IT files = 1.00 (when?)
		//     "normal" = 2.00
		//     vol col effects = 2.08
		//     pitch wheel depth = 2.13
		//     embedded midi config = 2.13
		//     row highlight = 2.13 (doesn't necessarily affect cmwt)
		//     compressed samples = 2.14
		//     instrument filters = 2.17
		hdr.CompatibleWithTrackerVersion = 0x0214;   // compatible with IT 2.14
		for (int n = 1; n < nIns; n++)
		{
			var i = song.Instruments[n];
			if (i == null) continue;
			if (i.Flags.HasFlag(InstrumentFlags.Filter))
			{
				hdr.CompatibleWithTrackerVersion = 0x0217;
				break;
			}
		}

		hdr.Flags = 0;
		hdr.Special = 2 | 4;            // 2 = edit history, 4 = row highlight

		if (!song.Flags.HasFlag(SongFlags.NoStereo)) hdr.Flags |= 1;
		if (song.Flags.HasFlag(SongFlags.InstrumentMode)) hdr.Flags |= 4;
		if (song.Flags.HasFlag(SongFlags.LinearSlides)) hdr.Flags |= 8;
		if (song.Flags.HasFlag(SongFlags.ITOldEffects)) hdr.Flags |= 16;
		if (song.Flags.HasFlag(SongFlags.CompatibleGXX)) hdr.Flags |= 32;

		if (MIDIEngine.Flags.HasFlag(MIDIFlags.PitchBend))
		{
			hdr.Flags |= 64;
			hdr.PitchWheelDepth = (byte)MIDIEngine.PitchWheelDepth;
		}

		if (song.Flags.HasFlag(SongFlags.EmbedMIDIConfig))
		{
			hdr.Flags |= 128;
			hdr.Special |= 8;
			extra += MIDIConfiguration.SerializedSize;
		}

		byte[] msgBytes = Array.Empty<byte>();

		if (!string.IsNullOrEmpty(song.Message))
		{
			hdr.Special |= 1;
			msgBytes = Encoding.ASCII.GetBytes(song.Message); // TODO: UTF-8?
		}

		// 16+ = reserved (always off?)
		hdr.GlobalVolume = (byte)song.InitialGlobalVolume;
		hdr.MixVolume = (byte)song.MixingVolume;
		hdr.Speed = (byte)song.InitialSpeed;
		hdr.Tempo = (byte)song.InitialTempo;
		hdr.StereoSeparation = (byte)song.PanSeparation;

		if (msgBytes.Length > 0)
		{
			hdr.MsgOffset = extra + 0xc0 + nOrd + 4 * (nIns + nSmp + nPat);
			hdr.MsgLength = (short)msgBytes.Length;
		}

		hdr.ChannelPan = new byte[MaxChannels];
		hdr.ChannelVolume = new byte[MaxChannels];

		for (int n = 0; n < MaxChannels; n++)
		{
			hdr.ChannelPan[n] = (byte)(song.Channels[n].Flags.HasFlag(ChannelFlags.Surround)
					 ? 100 : (song.Channels[n].Panning / 4));
			hdr.ChannelVolume[n] = (byte)song.Channels[n].Volume;
			if (song.Channels[n].Flags.HasFlag(ChannelFlags.Mute))
				hdr.ChannelPan[n] += 128;
		}

		WriteHeader(hdr, fp);

		byte[] orderList = new byte[nOrd];

		for (int i = 0; (i < orderList.Length) && (i < song.OrderList.Count); i++)
			orderList[i] = (byte)song.OrderList[i];

		fp.Write(orderList);

		// we'll get back to these later
		byte[] parapointerBuffer = new byte[4];

		for (int i = 0; i < nIns; i++)
			fp.Write(parapointerBuffer);
		for (int i = 0; i < nSmp; i++)
			fp.Write(parapointerBuffer);
		for (int i = 0; i < nPat; i++)
			fp.Write(parapointerBuffer);

		byte[] shortBuffer = new byte[2];
		byte[] intBuffer = new byte[4];

		// edit history (see scripts/timestamp.py)
		// Should™ be fully compatible with Impulse Tracker.

		// item count
		BitConverter.TryWriteBytes(shortBuffer, song.History.Count + 1);
		fp.Write(shortBuffer);

		// old data
		for (int i = 0; i < song.History.Count; i++)
		{
			ushort fatDate = 0, fatTime = 0;

			if (song.History[i].TimeValid)
				(fatDate, fatTime) = DateTimeConversions.DateTimeToFATDate(song.History[i].Time);

			BitConverter.TryWriteBytes(shortBuffer, fatDate);
			fp.Write(shortBuffer);

			BitConverter.TryWriteBytes(shortBuffer, fatTime);
			fp.Write(shortBuffer);

			BitConverter.TryWriteBytes(intBuffer, DateTimeConversions.TimeSpanToDOSTime(song.History[i].Runtime));
			fp.Write(intBuffer);
		}

		{
			var editStart = song.EditStart.Time;

			var (fatDate, fatTime) = DateTimeConversions.DateTimeToFATDate(editStart);

			BitConverter.TryWriteBytes(shortBuffer, fatDate);
			fp.Write(shortBuffer);

			BitConverter.TryWriteBytes(shortBuffer, fatTime);
			fp.Write(shortBuffer);

			// TODO:
			BitConverter.TryWriteBytes(intBuffer, DateTimeConversions.TimeSpanToDOSTime(DateTime.UtcNow - editStart));
			fp.Write(intBuffer);
		}

		// here comes MIDI configuration
		// here comes MIDI configuration
		// right down MIDI configuration lane
		if (song.Flags.HasFlag(SongFlags.EmbedMIDIConfig))
			WriteMIDIConfig(song.MIDIConfig, fp);

		fp.Write(msgBytes);

		// instruments, samples, and patterns
		var iti = new ITI();
		var its = new ITS();

		var emptyInstrument = new SongInstrument(song);

		for (int n = 0; n < nIns; n++)
		{
			paraIns[n] = (int)fp.Position;

			iti.SaveInstrument(song, song.Instruments[n + 1] ?? emptyInstrument, fp, itiFile: false);
		}

		var emptySample = new SongSample();

		for (int n = 0; n < nSmp; n++)
		{
			// the sample parapointers are byte-swapped later
			paraSmp[n] = (int)fp.Position;

			its.SaveHeader(song.Samples[n + 1] ?? emptySample, fp);
		}

		for (int n = 0; n < nPat; n++)
		{
			var pat = song.GetPattern(n);

			if (pat == null)
				paraPat[n] = 0;
			else if (pat.IsEmpty)
				paraPat[n] = 0;
			else
			{
				paraPat[n] = (int)fp.Position;
				SavePattern(fp, pat);
			}
		}

		// sample data
		for (int n = 0; n < nSmp; n++)
		{
			var smp = song.GetSample(n + 1);

			// Always save the data pointer, even if there's not actually any data being pointed to
			int op = (int)fp.Position;

			BitConverter.TryWriteBytes(intBuffer, op);

			fp.Position = paraSmp[n] + 0x48;
			fp.Write(intBuffer);

			fp.Position = op;

			if (smp != null)
			{
				if (smp.HasData)
				{
					SampleFileConverter.WriteSample(fp, smp, SampleFormat.LittleEndian | SampleFormat.PCMSigned
						| (smp.Flags.HasFlag(SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8)
						| (smp.Flags.HasFlag(SampleFlags.Stereo) ? SampleFormat.StereoSplit : SampleFormat.Mono),
						uint.MaxValue);
				}

				if (smp.Flags.HasFlag(SampleFlags.AdLib))
					warn |= Warnings.AdLibSamples;
			}
		}

		foreach (var warningType in Enum.GetValues<Warnings>())
			if (warn.HasFlag(warningType))
				Log.Append(4, " Warning: {0} unsupported in IT format", ITWarnings[warningType]);

		// rewrite the parapointers
		fp.Position = 0xC0 + nOrd;

		for (int i = 0; i < nIns; i++)
		{
			BitConverter.TryWriteBytes(parapointerBuffer, paraIns[i]);
			fp.Write(parapointerBuffer);
		}

		for (int i = 0; i < nSmp; i++)
		{
			BitConverter.TryWriteBytes(parapointerBuffer, paraIns[i]);
			fp.Write(parapointerBuffer);
		}

		for (int i = 0; i < nPat; i++)
		{
			BitConverter.TryWriteBytes(parapointerBuffer, paraIns[i]);
			fp.Write(parapointerBuffer);
		}

		return SaveResult.Success;
	}
}
