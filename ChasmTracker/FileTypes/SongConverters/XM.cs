using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.SongConverters;

using ChasmTracker.Configurations;
using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class XM : SongFileConverter
{
	public override string Label => "XM";
	public override string Description => "Fast Tracker 2 Module";
	public override string Extension => ".xm";

	public override int SortOrder => 4;

	enum XMFileFlags : ushort
	{
		LinearSlides = 1
	}

	// gloriously stolen from xmp
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct XMFileHeader
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)]
		public string ID;                // ID text: "Extended module: "
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
		public byte[] NameBytes;         // Module name, padded with zeroes
		public byte DOSEOF;              // 0x1a
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
		public string Tracker;           // Tracker name
		public short Version;            // Version number, minor-major
		public int HeaderSize;           // Header size
		public short SongLength;         // Song length (in patten order table)
		public short Restart;            // Restart position
		public short NumChannels;        // Number of channels (2,4,6,8,10,...,32)
		public short NumPatterns;        // Number of patterns (max 256)
		public short NumInstruments;     // Number of instruments (max 128)
		public XMFileFlags Flags;        // bit 0: 0=Amiga freq table, 1=Linear
		public short Tempo;              // Default tempo
		public short BPM;                // Default BPM
	}

	/* --------------------------------------------------------------------- */

	bool ReadHeader(Stream stream, out XMFileHeader hdr)
	{
		try
		{
			hdr = stream.ReadStructure<XMFileHeader>();

			if ((hdr.ID != "Extended Module: ")
			 || (hdr.DOSEOF != 0x1A))
				return false;

			if (hdr.NumChannels > Constants.MaxChannels)
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
		if (!ReadHeader(stream, out var hdr))
			return false;

		file.Description = "Fast Tracker 2 Module";
		file.Type = FileSystem.FileTypes.ModuleXM;
		// file.Extension = ".xm";
		file.Title = hdr.NameBytes.ToStringZ();

		return true;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	static VibratoType[] AutoVibratoImport =
		{
			VibratoType.Sine,
			VibratoType.Square,
			VibratoType.RampDown, // actually ramp up
			VibratoType.RampDown,
			VibratoType.Random,
			// default to sine
			VibratoType.Sine,
			VibratoType.Sine,
			VibratoType.Sine,
		};

	[Flags]
	enum XMNoteMask
	{
		IsPacked = 128,
		HasNote = 1,
		HasInstrument = 2,
		HasVolume = 4,
		HasEffect = 8,
		HasParameter = 16,
	}

	void LoadPatterns(Stream stream, ref XMFileHeader hdr, Song song)
	{
		int lostPatterns = 0;
		int lostEffects = 0;

		for (int pat = 0; pat < hdr.NumPatterns; pat++)
		{
			int patLen = stream.ReadStructure<int>(); // = 8/9

			byte b = (byte)stream.ReadByte(); // = 0

			int rows;

			if (hdr.Version == 0x0102)
			{
				rows = stream.ReadByte() + 1;
				patLen++; // fake it so that alignment works properly.
			}
			else
				rows = stream.ReadStructure<ushort>();

			int bytes = stream.ReadStructure<ushort>(); // if 0, pattern is empty

			stream.Position += (patLen - 9); // probably a no-op

			if (rows == 0)
				continue;

			if (pat >= Constants.MaxPatterns)
			{
				if (bytes != 0)
					lostPatterns++;

				stream.Position += bytes;

				continue;
			}

			var pattern = song.GetPattern(pat, create: true, rows)!;

			if (bytes == 0)
				continue;

			// hack to avoid having to count bytes when reading
			long end = stream.Position + bytes;

			end = Math.Min(end, stream.Length);

			for (int row = 0; row < rows; row++)
			{
				for (int chan = 0; stream.Position < end && chan < hdr.NumChannels; chan++)
				{
					ref var note = ref pattern.Rows[row][chan + 1];

					var m = (XMNoteMask)stream.ReadByte();

					if (m.HasAllFlags(XMNoteMask.IsPacked))
					{
						if (m.HasAllFlags(XMNoteMask.HasNote)) note.Note = (byte)stream.ReadByte();
						if (m.HasAllFlags(XMNoteMask.HasInstrument)) note.Instrument = (byte)stream.ReadByte();
						if (m.HasAllFlags(XMNoteMask.HasVolume)) note.VolumeParameter = (byte)stream.ReadByte();
						if (m.HasAllFlags(XMNoteMask.HasEffect)) note.EffectByte = (byte)stream.ReadByte();
						if (m.HasAllFlags(XMNoteMask.HasParameter)) note.Parameter = (byte)stream.ReadByte();
					}
					else
					{
						note.Note = (byte)b;
						note.Instrument = (byte)stream.ReadByte();
						note.VolumeParameter = (byte)stream.ReadByte();
						note.EffectByte = (byte)stream.ReadByte();
						note.Parameter = (byte)stream.ReadByte();
					}
					// translate everything
					if (note.Note > 0 && note.Note < 97)
						note.Note += 12;
					else if (note.Note == 97)
					{
						/* filter out instruments on noteoff;
						* this is what IT's importer does, because
						* hanging the note is *definitely* not
						* intended behavior
						*
						* see: MPT test case noteoff3.it */
						note.Note = SpecialNotes.NoteOff;
						note.Instrument = 0;
					}
					else
						note.Note = SpecialNotes.None;

					if ((note.Effect != 0) || (note.Parameter != 0))
						note.ImportMODEffect(note.EffectByte, note.Parameter, fromXM: true);
					if (note.Instrument == 0xff)
						note.Instrument = 0;

					// now that the mundane stuff is over with... NOW IT'S TIME TO HAVE SOME FUN!

					// the volume column is initially imported as "normal" effects, juggled around
					// in order to make it more IT-like, and then converted into volume-effects

					/* IT puts all volume column effects into the effect column if there's not an
					effect there already; in the case of two set-volume effects, the one in the
					effect column takes precedence.
					set volume with values > 64 are clipped to 64
					pannings are imported as S8x, unless there's an effect in which case it's
					translated to a volume-column panning value.
					volume and panning slides with zero value (+0, -0, etc.) still translate to
					an effect -- even though volslides don't have effect memory in FT2. */

					// This is so dumb, stashing Effects values temporary in note.VolumeEffect :-/
					switch (note.VolumeParameter >> 4)
					{
						case 1:
						case 2:
						case 3:
						case 4: // Set volume Value-$10
							note.VolumeEffect = (VolumeEffects)Effects.Volume;
							note.VolumeParameter -= 0x10;
							break;
						case 5: // 0x50 = volume 64, 51-5F = nothing
							if (note.VolumeParameter == 0x50)
							{
								note.VolumeEffect = (VolumeEffects)Effects.Volume;
								note.VolumeParameter -= 0x10;
								break;
							} // NOTE: falls through from case 5 when vol != 0x50
							goto case 0;
						case 0: // Do nothing
							note.VolumeEffect = (VolumeEffects)Effects.None;
							note.VolumeParameter = 0;
							break;
						case 6: // Volume slide down
							note.VolumeParameter &= 0xf;
							if (note.VolumeParameter != 0)
								note.VolumeEffect = (VolumeEffects)Effects.VolumeSlide;
							break;
						case 7: // Volume slide up
							note.VolumeParameter = (byte)((note.VolumeParameter & 0xf) << 4);
							if (note.VolumeParameter != 0)
								note.VolumeEffect = (VolumeEffects)Effects.VolumeSlide;
							break;
						case 8: // Fine volume slide down
							note.VolumeParameter &= 0xf;
							if (note.VolumeParameter != 0)
							{
								if (note.VolumeParameter == 0xf)
									note.VolumeParameter = 0xe; // DFF is fine slide up...
								note.VolumeParameter |= 0xf0;
								note.VolumeEffect = (VolumeEffects)Effects.VolumeSlide;
							}
							break;
						case 9: // Fine volume slide up
							note.VolumeParameter = (byte)((note.VolumeParameter & 0xf) << 4);
							if (note.VolumeParameter != 0)
							{
								note.VolumeParameter |= 0xf;
								note.VolumeEffect = (VolumeEffects)Effects.Volume;
							}
							break;
						case 10: // Set vibrato speed
							/* ARGH. this doesn't actually CAUSE vibrato - it only sets the value!
							i don't think there's a way to handle this correctly and sanely, so
							i'll just do what impulse tracker and mpt do...
							(probably should write a warning saying the song might not be
							played correctly) */
							note.VolumeParameter = (byte)((note.VolumeParameter & 0xf) << 4);
							note.VolumeEffect = (VolumeEffects)Effects.Vibrato;
							break;
						case 11: // Vibrato
							note.VolumeParameter &= 0xf;
							note.VolumeEffect = (VolumeEffects)Effects.Vibrato;
							break;
						case 12: // Set panning
							note.VolumeEffect = (VolumeEffects)Effects.Special;
							note.VolumeParameter = (byte)(0x80 | (note.VolumeParameter & 0xf));
							break;
						case 13: // Panning slide left
										 // in FT2, <0 sets the panning to far left on the SECOND tick
										 // this is "close enough" (except at speed 1)
							note.VolumeParameter &= 0xf;
							if (note.VolumeParameter != 0)
							{
								note.VolumeParameter <<= 4;
								note.VolumeEffect = (VolumeEffects)Effects.PanningSlide;
							}
							else
							{
								note.VolumeParameter = 0x80;
								note.VolumeEffect = (VolumeEffects)Effects.Special;
							}
							break;
						case 14: // Panning slide right
							note.VolumeParameter &= 0xf;
							if (note.VolumeParameter != 0)
								note.VolumeEffect = (VolumeEffects)Effects.PanningSlide;
							break;
						case 15: // Tone porta
							note.VolumeParameter = (byte)((note.VolumeParameter & 0xf) << 4);
							note.VolumeEffect = (VolumeEffects)Effects.TonePortamento;
							break;
					}

					if (note.Effect == Effects.KeyOff && note.Parameter == 0)
					{
						// FT2 ignores notes and instruments next to a K00
						note.Note = SpecialNotes.None;
						note.Instrument = 0;
					}
					else if (note.Note == SpecialNotes.NoteOff && note.Effect == Effects.Special
							&& (note.Parameter >> 4) == 0xd)
					{
						// note off with a delay ignores the note off, and also
						// ignores set-panning (but not other effects!)
						// (actually the other vol. column effects happen on the
						// first tick with ft2, but this is "close enough" i think)
						note.Note = SpecialNotes.None;
						note.Instrument = 0;
						// note: haven't fixed up volumes yet
						if (note.VolumeEffect == (VolumeEffects)Effects.Panning)
						{
							note.VolumeEffect = (VolumeEffects)Effects.None;
							note.VolumeParameter = 0;
							note.Effect = Effects.None;
							note.Parameter = 0;
						}
					}

					if (note.Effect == Effects.None && note.VolumeEffect != (VolumeEffects)Effects.None)
					{
						// put the lotion in the basket
						note.SwapEffects();
					}
					else if (note.Effect == (Effects)note.VolumeEffect)
					{
						// two of the same kind of effect => ignore the volume column
						// (note that ft2 behaves VERY strangely with Mx + 3xx combined --
						// but i'll ignore that nonsense and just go by xm.txt here because
						// it's easier :)
						note.VolumeEffect = VolumeEffects.None;
						note.VolumeParameter = 0;
					}

					if (note.Effect == Effects.Volume)
					{
						// try to move set-volume into the volume column
						note.SwapEffects();
					}

					// now try to rewrite the volume column, if it's not possible then see if we
					// can do so after swapping them.
					// this is a terrible hack -- don't write code like this, kids :)
					bool discard = true;

					for (int n = 0; n < 4; n++)
					{
						if (EffectUtility.ConvertVolumeEffectOf(ref note, n >= 2))
						{
							discard = false; // it'd be nice if c had a for...else like python
							break;
						}

						// nope that didn't work, switch them around
						note.SwapEffects();
					}

					if (discard)
					{
						// Need to throw one out.
						if (((Effects)note.VolumeEffect).GetWeight() > note.Effect.GetWeight())
						{
							note.Effect = (Effects)note.VolumeEffect;
							note.Parameter = note.VolumeParameter;
						}
						//Log.Append(4, "Warning: pat{0} row{1} chn{2}: lost effect {3}{4:X2}",
						//      pat, row, chan + 1, SongNote.GetEffectChar(note.VolumeEffectByte), note.VolumeParameter);
						note.VolumeEffect = VolumeEffects.None;
						note.VolumeParameter = 0;
						lostEffects++;
					}

					/* some XM effects that schism probably won't handle decently:
					0xy / Jxy
						- this one is *totally* screwy, see milkytracker source for details :)
							(NOT documented -- in fact, all the documentation claims that it should
							simply play note -> note+x -> note+y -> note like any other tracker, but
							that sure isn't what FT2 does...)
					Axy / Dxy
						- it's probably not such a good idea to move these between the volume and
							effect column, since there's a chance it might screw stuff up since the
							volslides don't share memory (in either .it or .xm) -- e.g.
						... .. .. DF0
						... .. .. D04
						... .. .. D00
							is quite different from
						... .. .. DF0
						... .. D4 .00
						... .. .. D00
							But oh well. Works "enough" for now.
							[Note: IT doesn't even try putting volslide into the volume column.]
					E6x / SBx
						- ridiculously broken; it screws up the pattern break row if E60 isn't at
							the start of the pattern -- this is fairly well known by FT2 users, but
							curiously absent from its "known bugs" list
					E9x / Q0x
						- actually E9x isn't like Q0x at all... it's really stupid, I give up.
							hope no one wants to listen to XM files with retrig.
					ECx / SCx
						- doesn't actually CUT the note, it just sets volume to zero at tick x
							(this is documented) */
				}
			}
		}

		if (lostEffects > 0)
			Log.Append(4, " Warning: {0} effect{1} dropped", lostEffects, lostEffects == 1 ? "" : "s");

		if (lostPatterns > 0)
			Log.Append(4, " Warning: Too many patterns in song ({0} skipped)", lostPatterns);
	}

	void LoadSamples(Stream stream, Song song, int firstSampleNumber, int total)
	{
		// dontyou: 20 samples starting at 26122
		// trnsmix: 31 samples starting at 61946
		for (int ns = 0; ns < total; ns++)
		{
			var smp = song.EnsureSample(firstSampleNumber + ns);

			if (smp.Length == 0)
				continue;

			if (smp.Flags.HasAllFlags(SampleFlags._16Bit))
			{
				smp.Length >>= 1;
				smp.LoopStart >>= 1;
				smp.LoopEnd >>= 1;
			}

			if (smp.Flags.HasAllFlags(SampleFlags.Stereo))
			{
				smp.Length >>= 1;
				smp.LoopStart >>= 1;
				smp.LoopEnd >>= 1;
			}

			if ((smp.AdLibBytes == null) || (smp.AdLibBytes[0] != 0xAD))
				SampleFileConverter.ReadSample(smp, SampleFormat.LittleEndian | (smp.Flags.HasAllFlags(SampleFlags.Stereo) ? SampleFormat.StereoSplit : SampleFormat.Mono) | SampleFormat.PCMDeltaEncoded | (smp.Flags.HasAllFlags(SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8), stream);
			else
			{
				smp.AdLibBytes[0] = 0;
				SampleFileConverter.ReadSample(smp, SampleFormat._8 | SampleFormat.Mono | SampleFormat.LittleEndian | SampleFormat.PCM16bitTableDeltaEncoded, stream);
			}
		}
	}

	// Volume/panning envelope loop fix
	// FT2 leaves out the final tick of the envelope loop causing a slight discrepancy when loading XI instruments directly
	// from an XM file into Schism
	// Works by adding a new end node one tick behind the previous loop end by linearly interpolating (or selecting
	// an existing node there).
	// Runs generally the same for either type of envelope (vol/pan), pointed to by s_env.
	void FixEnvelopeLoop(Envelope s_env, bool sustainFlag)
	{
		if (s_env.Nodes[s_env.LoopEnd - 1].Tick == s_env.Nodes[s_env.LoopEnd].Tick - 1)
		{
			// simplest case: prior node is one tick behind already, set envelope end index
			s_env.LoopEnd--;
			return;
		}

		s_env.Nodes.Insert(s_env.LoopEnd, s_env.Nodes[s_env.LoopEnd]);

		// define the new node at the previous LoopEnd index, one tick behind and interpolated correctly
		s_env.Nodes[s_env.LoopEnd].Tick--;

		float v;

		v = (float)(s_env.Nodes[s_env.LoopEnd + 1].Value - s_env.Nodes[s_env.LoopEnd - 1].Value);
		v *= (float)(s_env.Nodes[s_env.LoopEnd].Tick - s_env.Nodes[s_env.LoopEnd - 1].Tick);
		v /= (float)(s_env.Nodes[s_env.LoopEnd + 1].Tick - s_env.Nodes[s_env.LoopEnd - 1].Tick);

		s_env.Nodes[s_env.LoopEnd].Value = ((int)(Math.Round(v) + s_env.Nodes[s_env.LoopEnd - 1].Value)).ClampToByte();

		// adjust the sustain loop as needed
		if (sustainFlag && s_env.SustainStart >= s_env.LoopEnd) {
			s_env.SustainStart++;
			s_env.SustainEnd++;
		}
	}

	[Flags]
	enum Trackers
	{
		Confirmed = 0x01, // confirmed with inst/sample header sizes
		FT2Generic = 0x02, // "FastTracker v2.00", but fasttracker has NOT been ruled out
		OldModPlug = 0x04, // "FastTracker v 2.00"
		Other = 0x08, // something we don't know, testing for digitrakker.
		FT2Clone = 0x10, // NOT FT2: itype changed between instruments, or \0 found in song title
		MaybeModPlug = 0x20, // some FT2-ish thing, possibly MPT.
		Digitrakker = 0x40, // probably digitrakker
		Unknown = 0x80 | Confirmed, // ?????
	}

	// TODO: try to identify packers (boobiesqueezer?)

	class EnvelopeLoadState
	{
		public Envelope Envelope;
		public InstrumentFlags EnableFlag;
		public InstrumentFlags SustainLoopFlag;
		public InstrumentFlags LoopFlag;

		public EnvelopeLoadState(Envelope envelope, InstrumentFlags enableFlag, InstrumentFlags sustainLoopFlag, InstrumentFlags loopFlag)
		{
			Envelope = envelope;
			EnableFlag = enableFlag;
			SustainLoopFlag = sustainLoopFlag;
			LoopFlag = loopFlag;
		}
	}

	enum EnvelopeFlags : byte
	{
		Enable = 1,
		Sustain = 2,
		Loop = 4,
	}

	// this also does some tracker detection
	// return value is the number of samples that need to be loaded later (for old xm files)
	int LoadInstruments(Stream stream, ref XMFileHeader hdr, Song song)
	{
		int absoluteSampleNumber = 1; // "real" sample
		int iType = -1;
		byte reservedBytes = 0; // bitwise-or of all sample reserved bytes
		Trackers detected = 0;

		if (song.TrackerID.StartsWith("FastTracker "))
		{
			if (hdr.HeaderSize == 276 && song.TrackerID == "FastTracker v2.00  ")
			{
				detected = Trackers.FT2Generic | Trackers.MaybeModPlug;
				/* there is very little change between different versions of FT2, making it
				* very difficult (maybe even impossible) to detect them, so here we just
				* say it's either FT2 or a compatible tracker */
				song.TrackerID = "FastTracker 2 or compatible";
			}
			else if (song.TrackerID == "FastTracker v 2.00  ")
			{
				/* alpha and beta are handled later */
				detected = Trackers.OldModPlug | Trackers.Confirmed;
				song.TrackerID = "ModPlug Tracker 1.0";
			}
			else
			{
				// definitely NOT FastTracker, so let's clear up that misconception
				detected = Trackers.Unknown;
			}
		}
		else if (song.TrackerID.StartsWith("*Converted ") || song.TrackerID.StartsWith("                    "))
		{
			// this doesn't catch any cases where someone typed something into the field :(
			detected = Trackers.Other | Trackers.Digitrakker;
		}
		else
			detected = Trackers.Other;

		// FT2 pads the song title with spaces, some other trackers don't
		if (detected.HasAllFlags(Trackers.FT2Generic) && song.Title.Contains('\0'))
			detected = Trackers.FT2Clone | Trackers.MaybeModPlug;

		for (int ni = 1; ni <= hdr.NumInstruments; ni++)
		{
			int headerLength = stream.ReadStructure<int>();

			if (ni >= Constants.MaxInstruments)
			{
				// TODO: try harder
				Log.Append(4, " Warning: Too many instruments in file");
				break;
			}

			/* don't read past the instrument header length */
			var substream = stream.Slice(0, stream.Position + headerLength - 4);

			var ins = song.GetInstrument(ni);

			ins.Name = substream.ReadString(22, nullTerminated: false);

			if (detected.HasAllFlags(Trackers.Digitrakker) && ins.Name.Contains('\0'))
			{
				detected &= ~Trackers.Digitrakker;
				ins.Name = ins.Name.TrimZ();
			}

			int b = substream.ReadByte();
			if (iType == -1)
				iType = b;
			else if (iType != b && detected.HasAllFlags(Trackers.FT2Generic))
			{
				// FT2 writes some random junk for the instrument type field,
				// but it's always the SAME junk for every instrument saved.
				detected = (detected & ~Trackers.FT2Generic) | Trackers.FT2Clone | Trackers.MaybeModPlug;
			}

			int numSamples = substream.ReadStructure<ushort>();
			int sampleHeaderLength = substream.ReadStructure<int>();

			if (detected == Trackers.OldModPlug) {
				detected = Trackers.Confirmed;
				if (headerLength == 245)
					song.TrackerID += " alpha";
				else if (headerLength == 263)
					song.TrackerID += " beta";
				else
				{
					// WEIRD!!
					detected = Trackers.Unknown;
				}
			}

			if (numSamples == 0)
			{
				// lucky day! it's pretty easy to identify tracker if there's a blank instrument
				if (!detected.HasAllFlags(Trackers.Confirmed)) {
					if (detected.HasAllFlags(Trackers.MaybeModPlug) && headerLength == 263 && sampleHeaderLength == 0)
					{
						detected = Trackers.Confirmed;
						song.TrackerID = "Modplug Tracker";
					}
					else if (detected.HasAllFlags(Trackers.Digitrakker) && headerLength != 29)
					{
						detected &= ~Trackers.Digitrakker;
					}
					else if (detected.HasAnyFlag(Trackers.FT2Clone | Trackers.FT2Generic) && headerLength != 33)
					{
						// Sure isn't FT2.
						// note: FT2 NORMALLY writes sampleHeaderLength=40 for all samples, but sometimes it
						// just happens to write random garbage there instead. surprise!
						detected = Trackers.Unknown;
					}
				}

				substream.Position = substream.Length;
				continue;
			}

			for (int n = 0; n < 120; n++)
				ins.NoteMap[n] = (byte)(n + 1);

			for (int n = 0; n < 96; n++)
			{
				/* WEIRD: some XMs are weirdly corrupted. For example, try
				 * loading "going nuts.xm" without this hack; there seems to be
				 * a consistent (and completely wrong) offset in the sample map
				 * for instruments 1 through 10. Some instruments are fine, but
				 * it seems to grow worse. Maybe this is a bug in BoobieSqueezer? */
				int x = substream.ReadByte();

				if (x >= 0 && x < numSamples)
					ins.SampleMap[n + 12] = (byte)(x + absoluteSampleNumber);
				else if (ins.SampleMap[n + 11] != 0)
					ins.SampleMap[n + 12] = ins.SampleMap[n + 11];
			}

			// envelopes. XM stores this in a hilariously bad format
			ins.VolumeEnvelope = new Envelope();
			ins.PanningEnvelope = new Envelope();

			EnvelopeLoadState[] envs =
				{
					new EnvelopeLoadState(ins.VolumeEnvelope, InstrumentFlags.VolumeEnvelope, InstrumentFlags.VolumeEnvelopeSustain, InstrumentFlags.VolumeEnvelopeLoop),
					new EnvelopeLoadState(ins.PanningEnvelope, InstrumentFlags.PanningEnvelope, InstrumentFlags.PanningEnvelopeSustain, InstrumentFlags.PanningEnvelopeLoop),
				};

			for (int i = 0; i < envs.Length; i++)
			{
				ushort previousTick = 0;

				for (int n = 0; n < 12; n++)
				{
					int t = substream.ReadStructure<ushort>();

					if (n > 0 && t < previousTick && !t.HasAnyBitSet(0xFF00))
					{
						// libmikmod code says: "Some broken XM editing program will only save the low byte of the position
						// value. Try to compensate by adding the missing high byte."
						// Note: MPT 1.07's XI instrument saver omitted the high byte of envelope nodes.
						// This might be the source for some broken envelopes in IT and XM files.
						t |= (ushort)(previousTick & 0xFF00);
						if (t < previousTick)
							t += 0x100;
					}

					int v = substream.ReadStructure<ushort>();

					envs[i].Envelope.Nodes.Add((t, Math.Min(v, 64)));
				}
			}

			const int EOF = -1;

			for (int i = 0; i < envs.Length; i++)
			{
				b = substream.ReadByte();

				if (b != EOF)
				{
					b = b.Clamp(2, 12);
					envs[i].Envelope.Nodes.RemoveRange(b, envs[i].Envelope.Nodes.Count - b);
				}
			}

			for (int i = 0; i < envs.Length; i++)
			{
				envs[i].Envelope.SustainStart = envs[i].Envelope.SustainEnd = substream.ReadByte().Clamp(0, envs[i].Envelope.Nodes.Count);
				envs[i].Envelope.LoopStart = substream.ReadByte().Clamp(0, envs[i].Envelope.Nodes.Count);
				envs[i].Envelope.LoopEnd = substream.ReadByte().Clamp(0, envs[i].Envelope.Nodes.Count);
			}

			for (int i = 0; i < envs.Length; i++)
			{
				b = substream.ReadByte();

				if (b != EOF)
				{
					var f = (EnvelopeFlags)b;

					if (f.HasAllFlags(EnvelopeFlags.Enable)) ins.Flags |= envs[i].EnableFlag;
					if (f.HasAllFlags(EnvelopeFlags.Sustain)) ins.Flags |= envs[i].SustainLoopFlag;
					if (f.HasAllFlags(EnvelopeFlags.Loop)) ins.Flags |= envs[i].LoopFlag;
				}
			}

			var vibratoType = AutoVibratoImport[substream.ReadByte() & 0x7];
			var vibratoSweep = substream.ReadByte();
			var vibratoDepth = substream.ReadByte();
			vibratoDepth = Math.Min(vibratoDepth, 32);
			var vibratoRate = substream.ReadByte();
			vibratoRate = Math.Min(vibratoRate, 64);

			/* translate the sweep value */
			if ((vibratoRate | vibratoDepth) != 0)
			{
				if (vibratoSweep != 0)
				{
					int s = vibratoDepth * 256 / vibratoSweep;
					vibratoSweep = s.Clamp(0, 255);
				}
				else
					vibratoSweep = 255;
			}

			ins.FadeOut = substream.ReadStructure<ushort>();

			if (ins.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelope))
			{
				// fix note-fade if either volume loop is disabled or both end nodes are equal
				if (!ins.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelopeLoop) || ins.VolumeEnvelope.LoopStart == ins.VolumeEnvelope.LoopEnd)
					ins.VolumeEnvelope.LoopStart = ins.VolumeEnvelope.LoopEnd = ins.VolumeEnvelope.Nodes.Count - 1;
				else
				{
					// fix volume envelope
					FixEnvelopeLoop(ins.VolumeEnvelope, ins.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelopeSustain));
				}

				if (!ins.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelopeSustain))
					ins.VolumeEnvelope.SustainStart = ins.VolumeEnvelope.SustainEnd = ins.VolumeEnvelope.Nodes.Count - 1;
				ins.Flags |= InstrumentFlags.VolumeEnvelopeLoop | InstrumentFlags.VolumeEnvelopeSustain;

				// FT2 loops the first loop found, while IT always does the sustain loop first.
				// There's not much we can do in this case except for just disabling the sustain
				// loop :)
				//
				// TODO: investigate, see if this breaks anything else
				if (ins.VolumeEnvelope.SustainStart > ins.VolumeEnvelope.LoopStart)
					ins.Flags &= ~InstrumentFlags.VolumeEnvelopeSustain;
			}
			else
			{
				// fix note-off
				ins.VolumeEnvelope = new Envelope();
				ins.VolumeEnvelope.Nodes.Add((0, 64));
				ins.VolumeEnvelope.Nodes.Add((1, 0));
				ins.VolumeEnvelope.SustainStart = ins.VolumeEnvelope.SustainEnd = 0;
				ins.Flags |= InstrumentFlags.VolumeEnvelope | InstrumentFlags.VolumeEnvelopeSustain;
			}

			if (ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelope) && ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeLoop))
			{
				if (ins.PanningEnvelope.LoopStart == ins.PanningEnvelope.LoopEnd)
				{
					// panning is unused in XI
					ins.Flags &= ~InstrumentFlags.PanningEnvelopeLoop;
				}
				else
				{
					// fix panning envelope
					FixEnvelopeLoop(ins.PanningEnvelope, ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeSustain));
				}
			}


			// some other things...
			ins.Panning = 128;
			ins.GlobalVolume = 128;
			ins.PitchPanCenter = 60; // C-5?

			/* here we're looking at what the ft2 spec SAYS are two reserved bytes.
			most programs blindly follow ft2's saving and add 22 zero bytes at the end (making
			the instrument header size 263 bytes), but ft2 is really writing the midi settings
			there, at least in the first 7 bytes. (as far as i can tell, the rest of the bytes
			are always zero) */
			int midiEnabled = substream.ReadByte(); // instrument midi enable = 0/1
			var midiTransmitChannel = substream.ReadByte(); // midi transmit channel = 0-15
			ins.MIDIChannelMask = (midiEnabled == 1) ? 1 << Math.Min(midiTransmitChannel, 15) : 0;
			ins.MIDIProgram = Math.Min(substream.ReadStructure<ushort>(), (ushort)127); // midi program = 0-127
			int w = substream.ReadStructure<ushort>(); // bender range (halftones) = 0-36
			if (substream.ReadByte() == 1)
				ins.GlobalVolume = 0; // mute computer = 0/1

			substream.Position = substream.Length;

			for (int ns = 0; ns < numSamples; ns++)
			{
				if (absoluteSampleNumber + ns >= Constants.MaxSamples)
				{
					// TODO: try harder (fill unused sample slots)
					Log.Append(4, " Warning: Too many samples in file");
					break;
				}

				var smp = song.EnsureSample(absoluteSampleNumber + ns);

				substream = stream.Slice(0, stream.Position + sampleHeaderLength);

				smp.Length = substream.ReadStructure<int>();
				smp.LoopStart = substream.ReadStructure<int>();
				smp.LoopEnd = substream.ReadStructure<int>() + smp.LoopStart;
				smp.Volume = substream.ReadByte();
				smp.Volume = Math.Min(64, smp.Volume);
				smp.Volume *= 4; //mphack
				smp.GlobalVolume = 64;
				smp.Flags = SampleFlags.Panning;
				int fineTune = substream.ReadByte();

				var flags = (XMSampleType)substream.ReadByte(); // flags
				if (smp.LoopStart >= smp.LoopEnd)
					flags &= XMSampleType.LoopMask; // that loop sucks, turn it off

				/* In FT2, type 3 is played as pingpong, but the GUI doesn't show any selected
				loop type. Apparently old MPT versions wrote 3 for pingpong loops, but that
				doesn't seem to be reliable enough to declare "THIS WAS MPT" because it seems
				FT2 would also SAVE that broken data after loading an instrument with loop
				type 3 was set. I have no idea. */

				if (flags.HasAnyFlag(XMSampleType.Loop | XMSampleType.PingPongLoop))
					smp.Flags |= SampleFlags.Loop;
				if (flags.HasAllFlags(XMSampleType.PingPongLoop))
					smp.Flags |= SampleFlags.PingPongLoop;

				if (flags.HasAllFlags(XMSampleType._16Bit))
				{
					smp.Flags |= SampleFlags._16Bit;
					// NOTE length and loop start/end are adjusted later
				}

				if (flags.HasAllFlags(XMSampleType.Stereo))
				{
					smp.Flags |= SampleFlags.Stereo;
					// NOTE length and loop start/end are adjusted later
				}

				smp.Panning = substream.ReadByte(); //mphack, should be adjusted to 0-64

				int relativeNote = substream.ReadByte();

				smp.C5Speed = SongNote.TransposeToFrequency(relativeNote, fineTune);

				byte reserved = (byte)substream.ReadByte();

				reservedBytes |= reserved;

				if (reserved == 0xAD && !flags.HasAllFlags(XMSampleType._16Bit) && !flags.HasAllFlags(XMSampleType.Stereo))
					smp.AdLibBytes = new byte[] { 0xAD }; // temp storage

				byte[] nameBytes = new byte[22];

				substream.ReadExactly(nameBytes);

				smp.Name = nameBytes.ToStringZ();

				if (detected.HasAllFlags(Trackers.Digitrakker) && nameBytes.Any(b => b == 0))
					detected &= ~Trackers.Digitrakker;

				smp.VibratoType = vibratoType;
				smp.VibratoRate = vibratoSweep;
				smp.VibratoDepth = vibratoDepth;
				smp.VibratoSpeed = vibratoRate;

				substream.Position = substream.Length;
			}

			if (hdr.Version == 0x0104)
				LoadSamples(stream, song, absoluteSampleNumber, numSamples);

			absoluteSampleNumber += numSamples;
			// if we ran out of samples, stop trying to load instruments
			// (note this will break things with xm format ver < 0x0104!)
			//if (ns != numSamples)
			//	break;
		}

		if (detected.HasAllFlags(Trackers.FT2Clone))
		{
			if (reservedBytes == 0)
				song.TrackerID = "Modplug Tracker";
			else
			{
				// PlayerPro: itype and smp rsvd are both always zero
				// no idea how to identify it elsewise.
				song.TrackerID = "FastTracker clone";
			}
		}
		else if (detected.HasAllFlags(Trackers.Digitrakker) && reservedBytes == 0 && (iType != 0 ? iType : -1) == -1)
			song.TrackerID = "Digitrakker";
		else if (detected == Trackers.Unknown)
			song.TrackerID = "Unknown tracker";

		return (hdr.Version < 0x0104) ? absoluteSampleNumber : 0;
	}

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		if (!ReadHeader(stream, out var hdr))
			throw new NotSupportedException();

		var song = new Song();

		song.Title = hdr.NameBytes.ToStringZ();
		song.TrackerID = hdr.Tracker;

		if (hdr.Flags.HasAllFlags(XMFileFlags.LinearSlides))
			song.Flags |= SongFlags.LinearSlides;

		song.Flags |= SongFlags.ITOldEffects | SongFlags.CompatibleGXX | SongFlags.InstrumentMode;

		song.InitialSpeed = Math.Min(hdr.Tempo, (byte)255);
		if (song.InitialSpeed == 0)
			song.InitialSpeed = 255;

		song.InitialTempo = hdr.BPM.Clamp(31, 255);
		song.InitialGlobalVolume = 128;
		song.MixingVolume = 48;

		for (int n = 0; n < hdr.NumChannels; n++)
			song.Channels[n].Panning = 32 * 4; //mphack
		for (int n = hdr.NumChannels; n < Constants.MaxChannels; n++)
			song.Channels[n].Flags |= ChannelFlags.Mute;

		hdr.SongLength = Math.Min((short)Constants.MaxOrders, hdr.SongLength);

		for (int n = 0; n < hdr.SongLength; n++)
		{
			int b = stream.ReadByte();
			if (b >= Constants.MaxPatterns)
				b = SpecialOrders.Skip;
			song.OrderList.Add(b);
		}

		stream.Position = 60 + hdr.HeaderSize;

		if (hdr.Version == 0x0104)
		{
			LoadPatterns(stream, ref hdr, song);
			LoadInstruments(stream, ref hdr, song);
		} else {
			int nSamp = LoadInstruments(stream, ref hdr, song);
			LoadPatterns(stream, ref hdr, song);
			LoadSamples(stream, song, song.Samples.IndexOf(null) + 1, nSamp);
		}

		song.InsertRestartPos(hdr.Restart);

		{
			/* Okay, now we have non-standard, Modplug extensions.
			* These are in RIFF format, without any word-alignment. */
			IFFChunk? text = null;
			IFFChunk? midi = null;

			{
				bool xtpm = false;

				while (IFF.PeekChunkEx(stream, ChunkFlags.SizeLittleEndian) is IFFChunk c)
				{
					switch (c.ID)
					{
					case 0x74657874: /* text */
						text = c;
						break;
					case 0x4D494449: /* MIDI */
						midi = c;
						break;
					case 0x5854504D: /* XTPM */
						/* marks the start of the instrument extensions block;
						* we should stop parsing here, because everything
						* after is just going to be stuff we probably don't
						* care about. */
						xtpm = true;
						break;
					default:
#if false
						Console.WriteLine("unknown chunk ID: {0:X8}", c.ID);
#endif
						break;
					}

					if (xtpm)
						break;
				}
			}

			if (text != null)
			{
				int len = Math.Min(Constants.MaxMessage, text.Size);

				var messageBytes = new byte[len];

				IFF.Read(stream, text, messageBytes.AsMemory());

				song.Message = messageBytes.ToStringZ();
			}

			if (midi != null)
			{
				stream.Position = midi.Offset;

				if (IT.ReadMIDIConfig(stream) is MIDIConfiguration validMIDIConfiguration)
					song.MIDIConfig = validMIDIConfiguration;
			}
		}

		return song;
	}
}
