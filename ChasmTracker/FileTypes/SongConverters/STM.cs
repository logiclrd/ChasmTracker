// sort order 19
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.SongConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class STM : SongFileConverter
{
	public override string Label => "STM";
	public override string Description => "Scream Tracker 2";
	public override string Extension => ".stm";

	public override int SortOrder => 19;

	public static readonly Effects[] STMEffects =
		{
			Effects.None,               // .
			Effects.Speed,              // A
			Effects.PositionJump,       // B
			Effects.PatternBreak,       // C
			Effects.VolumeSlide,        // D
			Effects.PortamentoDown,     // E
			Effects.PortamentoUp,       // F
			Effects.TonePortamento,     // G
			Effects.Vibrato,            // H
			Effects.Tremor,             // I
			Effects.Arpeggio,           // J
			// KLMNO can be entered in the editor but don't do anything
		};

	protected static void ImportSTMEffectParameter(ref SongNote note)
	{
		switch (note.Effect)
		{
			case Effects.Speed:
				/* do nothing; this is handled later */
				break;
			case Effects.VolumeSlide:
				// Scream Tracker 2 checks for the lower nibble first for some reason...
				if (note.Parameter.HasAnyBitSet(0x0f) && ((note.Parameter >> 4) != 0))
					note.Parameter &= 0x0f;
				goto case Effects.PortamentoDown;
			case Effects.PortamentoDown:
			case Effects.PortamentoUp:
				if (note.Parameter == 0)
					note.Effect = Effects.None;
				break;
			case Effects.PatternBreak:
					note.Parameter = (byte)((note.Parameter & 0xf0) * 10 + (note.Parameter & 0xf));
				break;
			case Effects.PositionJump:
				// This effect is also very weird.
				// Bxx doesn't appear to cause an immediate break -- it merely
				// sets the next order for when the pattern ends (either by
				// playing it all the way through, or via Cxx effect)
				// I guess I'll "fix" it later...
				break;
			case Effects.Tremor:
				// this actually does something with zero values, and has no
				// effect memory. which makes SENSE for old-effects tremor,
				// but ST3 went and screwed it all up by adding an effect
				// memory and IT followed that, and those are much more popular
				// than STM so we kind of have to live with this effect being
				// broken... oh well. not a big loss.
				break;
			default:
				// Anything not listed above is a no-op if there's no value.
				// (ST2 doesn't have effect memory)
				if (note.Parameter == 0)
					note.Effect = Effects.None;
				break;
		}
	}

	protected static byte ConvertSTMTempoToBPM(int tempo)
	{
		int tpr = ((tempo >> 4) != 0) ? (tempo >> 4) : 1;
		int scale = (tempo & 15);

		return Tables.ST2TempoTable[tpr - 1][scale];
	}

	protected static void HandleSTMTempoPattern(Pattern pattern, int row, int tempo)
	{
		for (int i = 1; i <= 32; i++)
		{
			ref var note = ref pattern[row][i];

			if (note.Effect == Effects.None)
			{
				note.Effect = Effects.Tempo;
				note.Parameter = ConvertSTMTempoToBPM(tempo);
				break;
			}
		}
	}

	/* --------------------------------------------------------------------- */

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		stream.Position = 28;

		int what = stream.ReadByte();
		int type = stream.ReadByte();
		int version = stream.ReadByte();

		/* data[29] is the type: 1 = song, 2 = module (with samples) */
		if ((what != 0x1a && what != 0x02) || (type != 1 && type != 2)
			|| version != 2)
			return false;

		stream.Position = 20;

		for (int i = 0; i < 8; i++)
		{
			/* the ID should be all safe ASCII */
			int id = stream.ReadByte(); ;
			if (id < 0x20 || id > 0x7E)
				return false;
		}

		stream.Position = 0;

		string title = stream.ReadString(20);

		/* I used to check whether it was a 'song' or 'module' and set the description
		* accordingly, but it's fairly pointless information :) */
		file.Description = Description;
		/*file.Extension = "stm";*/
		file.Type = FileSystem.FileTypes.ModuleMOD;
		file.Title = title;

		return true;
	}

	/* --------------------------------------------------------------------- */

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct STMSample
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
		public string Name;
		public byte Reserved1; // zero
		public byte InstDisk; // ?
		public ushort PCMParapointer; // in the official documentation, this is misleadingly labelled reserved...
		public ushort Length;
		public ushort LoopStart;
		public ushort LoopEnd;
		public byte Volume;
		public byte Reserved2; // reserved
		public ushort C5Speed;
		public uint Reserved3; // morejunk
		public ushort Reserved4; // paragraphs (? what)
	}

	static bool ReadSample(Stream stream, [NotNullWhen(true)] out STMSample smp)
	{
		try
		{
			smp = stream.ReadStructure<STMSample>();
			return true;
		}
		catch
		{
			smp = default!;
			return false;
		}
	}

	/* ST2 says at startup:
	"Remark: the user ID is encoded in over ten places around the file!"
	I wonder if this is interesting at all. */
	void LoadPattern(Stream stream, Pattern pattern)
	{
		byte[] v = new byte[4];

		for (int row = 0; row < 64; row++)
		{
			for (int chan = 1; chan <= 4; chan++)
			{
				ref var chanNote = ref pattern[row][chan];

				stream.ReadExactly(v);

				// mostly copied from modplug...
				if (v[0] < 251)
					chanNote.Note = (byte)((v[0] >> 4) * 12 + (v[0] & 0xf) + 37);
				chanNote.Instrument = (byte)(v[1] >> 3);
				if (chanNote.Instrument > 31)
					chanNote.Instrument = 0; // oops never mind, that was crap

				chanNote.VolumeParameter = (byte)((v[1] & 0x7) + ((v[2] & 0xf0) >> 1));

				if (chanNote.VolumeParameter <= 64)
					chanNote.VolumeEffect = VolumeEffects.Volume;
				else
					chanNote.VolumeParameter = 0;

				chanNote.Parameter = v[3]; // easy!

				chanNote.Effect = STMEffects[v[2] & 0x0f];

				ImportSTMEffectParameter(ref chanNote);
			}

			for (int chan = 1; chan <= 4; chan++)
			{
				ref var chanNote = ref pattern[row][chan];

				if (chanNote.Effect == Effects.Speed)
				{
					int tempo = chanNote.Parameter;
					chanNote.Parameter >>= 4;

					HandleSTMTempoPattern(pattern, row, tempo);
				}
			}
		}
	}

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		stream.Position = 20;

		string id = stream.ReadString(8);

		byte[] tmp = new byte[4];

		stream.ReadExactly(tmp);

		if (!(
			// this byte is *usually* guaranteed to be 0x1a,
			// however putup10.stm and putup11.stm are outliers
			// for some reason?...
			(tmp[0] == 0x1a || tmp[0] == 0x02)
			// from the doc:
			//      1 - song (contains no samples)
			//      2 - module (contains samples)
			// I'm not going to care about "songs".
			&& tmp[1] == 2
			// do version 1 STM's even exist?...
			&& tmp[2] == 2
		))
			throw new NotSupportedException();

		// check the file tag for printable ASCII
		for (int n = 0; n < 8; n++)
			if (id[n] < 0x20 || id[n] > 0x7E)
				throw new FormatException();

		var song = new Song();

		// and the next two bytes are the tracker version.
		if (id == "!Scream!")
			// Unfortunately tools never differentiated themselves...
			if (tmp[3] > 20)
				// Future Crew chose to never increase their version numbers after 2.21 it seems!
				song.TrackerID = "Scream Tracker 2.2+ or compatible";
			else
				song.TrackerID = string.Format("Scream Tracker {0}.{1:00} or compatible", tmp[2].Clamp(0, 9), tmp[3].Clamp(0, 99));
		else if (id == "BMOD2STM")
			song.TrackerID = "BMOD2STM";
		else if (id == "WUZAMOD!")
			song.TrackerID = "Wuzamod"; // once a MOD always a MOD
		else if (id == "SWavePro")
			song.TrackerID = string.Format("SoundWave Pro {0}.{1:00}", tmp[2].Clamp(0, 9), tmp[3].Clamp(0, 99));
		else if (id == "!Scrvrt!")
			song.TrackerID = "Screamverter";
		else
			song.TrackerID = "Unknown";

		stream.Position = 0;
		song.Title = stream.ReadString(20);

		stream.Position += 12; // skip the tag and stuff

		int tempo = stream.ReadByte();

		if (tmp[3] < 21)
			tempo = ((tempo / 10) << 4) + tempo % 10;

		song.InitialSpeed = ((tempo >> 4) != 0) ? (tempo >> 4) : 1;
		song.InitialTempo = ConvertSTMTempoToBPM(tempo);

		int nPat = stream.ReadByte();

		song.InitialGlobalVolume = 2 * stream.ReadByte();

		stream.Position += 13; // junk

		if (nPat > 64)
			throw new FormatException();

		ushort[] sampleDataParapointers = new ushort[32];

		for (int n = 1; n <= 31; n++)
		{
			if (!ReadSample(stream, out var stmSample))
				throw new FormatException();

			var sample = song.EnsureSample(n);

			sample.FileName = stmSample.Name.Replace('\xFF', ' ');
			sample.Name = sample.FileName;

			// the strncpy here is intentional -- ST2 doesn't show the '3' after the \0 bytes in the first
			// sample of pm_fract.stm, for example
			sample.Length = stmSample.Length;
			sample.LoopStart = stmSample.LoopStart;
			sample.LoopEnd = stmSample.LoopEnd;
			sample.C5Speed = stmSample.C5Speed;
			sample.Volume = stmSample.Volume * 4; //mphack
			if (sample.LoopStart < sample.Length
				&& sample.LoopEnd != 0xffff
				&& sample.LoopStart < sample.LoopEnd)
			{
				sample.Flags |= SampleFlags.Loop;
				sample.LoopEnd = sample.LoopEnd.Clamp(sample.LoopStart, sample.Length);
			}

			sampleDataParapointers[n] = stmSample.PCMParapointer;
		}

		int orderListSize = (tmp[3] != 0) ? 128 : 64;

		byte[] orderListBytes = new byte[orderListSize];

		stream.ReadExactly(orderListBytes);

		for (int n = 0; n < orderListSize; n++)
		{
			if (orderListBytes[n] < 64)
				song.OrderList.Add(orderListBytes[n]);
			else
			{
				song.OrderList.Add(SpecialOrders.Last);
				break;
			}
		}

		if (lflags.HasAllFlags(LoadFlags.NoPatterns))
			stream.Position += nPat * 64 * 4 * 4;
		else
		{
			for (int n = 0; n < nPat; n++)
			{
				var pattern = song.GetPattern(n, create: true, rowsInNewPattern: 64)!;

				LoadPattern(stream, pattern);
			}
		}

		if (!lflags.HasAllFlags(LoadFlags.NoSamples))
		{
			for (int n = 1; n <= 31; n++)
			{
				var sample = song.EnsureSample(n);

				// Garbage?
				if (sample.Length < 3)
					sample.Length = 0;
				else
				{
					stream.Position = sampleDataParapointers[n] << 4;

					SampleFileConverter.ReadSample(sample, SampleFormat.LittleEndian | SampleFormat.PCMSigned | SampleFormat._8 | SampleFormat.Mono, stream);
				}
			}
		}

		for (int n = 0; n < 4; n++)
			song.Channels[n].Panning = (int)((n.HasBitSet(1) ? 64 : 0) * 4); //mphack
		for (int n = 4; n < song.Channels.Length; n++)
			song.Channels[n].Flags |= ChannelFlags.Mute;

		song.PanSeparation = 64;
		song.Flags = SongFlags.ITOldEffects | SongFlags.CompatibleGXX | SongFlags.NoStereo;

		return song;
	}

}
