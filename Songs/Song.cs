using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ChasmTracker.Songs;

using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.DiskOutput;
using ChasmTracker.FileSystem;
using ChasmTracker.FileTypes;
using ChasmTracker.FM;
using ChasmTracker.Memory;
using ChasmTracker.MIDI;
using ChasmTracker.Pages;
using ChasmTracker.Playback;
using ChasmTracker.Utility;

public class Song
{
	public static MIDIConfiguration DefaultMIDIConfig = MIDIConfiguration.GetDefault();

	public static Song CurrentSong = new Song();

	public string Title = "";
	public string Message = "";
	public string FileName = "";
	public string BaseName = "";
	public string TrackerID = "";

	public int InitialGlobalVolume;
	public int InitialSpeed;
	public int InitialTempo;

	public int PanSeparation;

	public int RowHighlightMajor;
	public int RowHighlightMinor;

	public int[] MixBuffer = new int[Constants.MixBufferSize * 2];

	public readonly List<Pattern?> Patterns = new List<Pattern?>();
	public readonly List<SongSample?> Samples = new List<SongSample?>();
	public readonly List<SongInstrument?> Instruments = new List<SongInstrument?>();
	public readonly List<int> OrderList = new List<int>();
	public readonly SongChannel[] Channels = new SongChannel[Constants.MaxChannels];
	public readonly SongVoice[] Voices = new SongVoice[Constants.MaxVoices];
	public readonly int[] VoiceMix = new int[Constants.MaxVoices];

	public readonly List<SongHistory> History = new List<SongHistory>();
	public SongHistory EditStart = new SongHistory();

	public IMIDISink? MIDISink;

	public int NumVoices;// how many are currently playing. (POTENTIALLY larger than global MaxVoices)
	public int BufferCount; // number of samples to mix per tick
	public int TickCount;
	public int FrameDelay;
	public int RowCount; /* IMPORTANT needs to be signed */
	public int CurrentSpeed;
	public int CurrentTempo;
	public int ProcessRow;
	public int Row; // no analogue in pm.h? should be either renamed or factored out.
	public int BreakRow;
	public int CurrentPattern;
	public int ProcessOrder;
	public int CurrentGlobalVolume;
	public int MixingVolume;
	public int FrequencyFactor; // not used -- for tweaking the song speed LP-style (interesting!)
	public int TempoFactor; // ditto
	public int RepeatCount; // 0 = first playback, etc. (note: set to -1 to stop instead of looping)

	public void SetCurrentSpeed(int newSpeed)
	{
		if (newSpeed < 1 || newSpeed > 255)
			return;

		lock (AudioPlayback.LockScope())
			CurrentSpeed = newSpeed;
	}

	public void SetCurrentTempo(int newTempo)
	{
		lock (AudioPlayback.LockScope())
			CurrentTempo = newTempo.Clamp(31, 255);
	}

	public void SetCurrentGlobalVolume(int newVolume)
	{
		if (newVolume < 0 || newVolume > 128)
			return;

		lock (AudioPlayback.LockScope())
			CurrentGlobalVolume = newVolume;
	}

	// Nothing innately special about this -- just needs to be above the max pattern length.
	// process row is set to this in order to get the player to jump to the end of the pattern.
	// (See ITTECH.TXT)
	public const int ProcessNextOrder = 0xFFFE; // special value for ProcessRow

	static Random s_rnd = new Random();

	public int CurrentTick => TickCount % CurrentSpeed;

	public TimeSpan EditTimeElapsed => DateTime.UtcNow - EditStart.Time;

	public static void Initialize()
	{
		CurrentSong = new Song();
		CurrentSong.LinearPitchSlides = true;

		AudioPlayback.Reset();

		New(NewSongFlags.ClearAll);

		AudioPlayback.MixFlags |= MixFlags.MuteChannelMode;
	}

	public void SetFileName(string? file)
	{
		if (!string.IsNullOrEmpty(file))
		{
			FileName = file;
			BaseName = Path.GetFileName(file);
		}
		else
		{
			FileName = "";
			BaseName = "";
		}
	}

	public void SetInitialSpeed(int newSpeed)
	{
		InitialSpeed = newSpeed.Clamp(1, 255);
	}

	public void SetInitialTempo(int newTempo)
	{
		InitialTempo = newTempo.Clamp(31, 255);
	}

	public void SetInitialGlobalVolume(int newVol)
	{
		InitialGlobalVolume = newVol.Clamp(0, 128);
	}

	public void SetMixingVolume(int newVol)
	{
		MixingVolume = newVol.Clamp(0, 128);
	}

	public void SetSeparation(int newSep)
	{
		PanSeparation = newSep.Clamp(0, 128);
	}

	public void PromptEnableInstrumentMode()
	{
		var dialog = MessageBox.Show(MessageBoxTypes.YesNo, "Enable instrument mode?");

		dialog.ActionYes =
			() =>
			{
				SetInstrumentMode(true);
				Page.NotifySongChangedGlobal();
				Page.SetPage(PageNumbers.InstrumentList);
				MemoryUsage.NotifySongChanged();
			};

		dialog.ActionNo =
			() =>
			{
				Page.SetPage(PageNumbers.InstrumentList);
			};
	}

	public void SetInstrumentMode(bool value)
	{
		if (value && !IsInstrumentMode)
		{
			Flags |= SongFlags.InstrumentMode;

			for (int i = 0; i < Instruments.Count; i++)
			{
				var instrument = GetInstrument(i);

				/* fix wiped notes */
				for (int j = 0; j < instrument.NoteMap.Length; j++)
					if ((instrument.NoteMap[j] < 1)
					 || (instrument.NoteMap[j] > 120))
						instrument.NoteMap[j] = (byte)(j + 1);
			}
		}
		else
			Flags &= ~SongFlags.InstrumentMode;
	}

	public void InitializeInstrumentFromSample(int insN, int samp)
	{
		var instrument = GetInstrument(insN);

		if (!instrument.IsEmpty)
			return;

		instrument.InitializeFromSample(samp);

		instrument.Name = CurrentSong.Samples[samp]?.Name ?? "";
	}

	/* -1 for all */
	public void InitializeInstruments(int qq = -1)
	{
		for (int n = 1; n < Instruments.Count; n++)
			if ((qq == -1) || (qq == n))
				InitializeInstrumentFromSample(n, n);
	}

	public void InitializeMIDI(IMIDISink midiSink)
	{
		MIDISink = midiSink;
	}

	// IT-compatible: last order of "main song", or 0
	public int GetLastOrder()
	{
		int n = 0;

		while ((n < OrderList.Count) && (OrderList[n] != SpecialOrders.Last))
			n++;

		return (n > 0) ? n - 1 : 0;
	}

	// Total count of orders in orderlist before end of data
	public int GetOrderCount()
	{
		for (int n = OrderList.Count; n >= 0; n--)
			if (OrderList[n] != SpecialOrders.Last)
				return n + 1;

		return 0;
	}

	public int GetOrder(int n)
	{
		if (n < OrderList.Count)
			return OrderList[n];
		else
			return SpecialOrders.Last;
	}

	public void SetOrder(int n, int o)
	{
		while (n >= OrderList.Count)
			OrderList.Add(SpecialOrders.Last);

		OrderList[n] = o;
	}

	// Total number of non-empty patterns in song, according to csf_pattern_is_empty
	public int GetPatternCount()
	{
		for (int n = Patterns.Count; n >= 0; n--)
		{
			var pattern = Patterns[n];

			if ((pattern != null) && !pattern.IsEmpty)
				return n + 1;
		}

		return 0;
	}

	public int GetSampleCount()
	{
		for (int n = Samples.Count; n >= 0; n--)
			if (!(Samples[n]?.IsEmpty ?? true))
				return n + 1;

		return 0;
	}

	public int GetInstrumentCount()
	{
		for (int n = Instruments.Count; n >= 0; n--)
			if (!(Instruments[n]?.IsEmpty ?? true))
				return n + 1;

		return 0;
	}

	public void SetCurrentOrder(int newValue)
	{
		lock (AudioPlayback.LockScope())
			CurrentOrder = newValue;
	}

	// Ctrl-F7
	public void SetNextOrder(int order)
	{
		lock (AudioPlayback.LockScope())
			ProcessOrder = order;
	}

	// Alt-F11
	public bool ToggleOrderListLocked()
	{
		Flags ^= SongFlags.OrderListLocked;

		return Flags.HasAllFlags(SongFlags.OrderListLocked);
	}

	// clear patterns => clear filename and save flag
	// clear orderlist => clear title, message, and channel settings
	public static void New(NewSongFlags flags)
	{
		lock (AudioPlayback.LockScope())
		{
			AudioPlayback.StopUnlocked(false);

			Song newSong = new Song();

			newSong.Flags = CurrentSong.Flags;

			if (newSong.Flags.HasAllFlags(SongFlags.ITOldEffects))
			{
				for (int i = 0; i < newSong.Voices.Length; i++)
					newSong.Voices[i].VibratoPosition = 0x10;
			}

			if (flags.HasAllFlags(NewSongFlags.KeepPatterns))
			{
				newSong.FileName = CurrentSong.FileName;

				Status.Flags &= ~StatusFlags.SongNeedsSave;

				newSong.Patterns.AddRange(CurrentSong.Patterns);
			}

			if (flags.HasAllFlags(NewSongFlags.KeepSamples))
			{
				int i;

				for (i = 0; (i < newSong.Samples.Count) && (i < CurrentSong.Samples.Count); i++)
					newSong.Samples[i] = CurrentSong.Samples[i];

				for (; i < CurrentSong.Samples.Count; i++)
					newSong.Samples.Add(CurrentSong.Samples[i]);
			}

			if (flags.HasAllFlags(NewSongFlags.KeepInstruments))
			{
				int i;

				for (i = 0; (i < newSong.Instruments.Count) && (i < CurrentSong.Instruments.Count); i++)
					newSong.Instruments[i] = CurrentSong.Instruments[i];

				for (; i < CurrentSong.Instruments.Count; i++)
					newSong.Instruments.Add(CurrentSong.Instruments[i]);

				var requireSamples = newSong.Instruments
					.OfType<SongInstrument>() // where not null
					.SelectMany(instrument => instrument.SampleMap)
					.ToHashSet();

				int maxSampleNumber = requireSamples.Max();

				while (newSong.Samples.Count <= maxSampleNumber)
					newSong.Samples.Add(null);

				foreach (var sampleIndex in requireSamples)
					newSong.Samples[sampleIndex] = CurrentSong.Samples[sampleIndex];
			}

			if (flags.HasAllFlags(NewSongFlags.KeepOrderList))
			{
				newSong.Title = CurrentSong.Title;
				newSong.Message = CurrentSong.Message;

				newSong.OrderList.Clear();
				newSong.OrderList.AddRange(CurrentSong.OrderList);

				for (int i = 0; i < Constants.MaxChannels; i++)
				{
					newSong.Channels[i] = CurrentSong.Channels[i];
					newSong.Voices[i] = CurrentSong.Voices[i];
				}
			}
			else
			{
				for (int i = 0; i < Constants.MaxChannels; i++)
				{
					newSong.Voices[i].Panning = newSong.Channels[i].Panning;
					newSong.Voices[i].Flags = newSong.Channels[i].Flags;
				}
			}

			CurrentSong = newSong;

			CurrentSong.ForgetHistory();
		}

		Page.NotifySongChangedGlobal();
	}

	void ForgetHistory()
	{
		History.Clear();
		EditStart = new SongHistory();
	}

	bool[] _savedChannelMutedStates = new bool[Constants.MaxChannels];

	public void SaveChannelMuteState(int channel)
	{
		_savedChannelMutedStates[channel] = Voices[channel].IsMuted;
	}

	public void SaveChannelMuteStates()
	{
		for (int i = 0; i < _savedChannelMutedStates.Length; i++)
			_savedChannelMutedStates[i] = Voices[i].IsMuted;
	}

	// I don't think this is useful besides undoing a channel solo (a few lines
	// below), but I'm making it public anyway for symmetry.
	public void RestoreChannelMuteStates()
	{
		for (int n = 0; n < 64; n++)
			SetChannelMute(n, _savedChannelMutedStates[n]);
	}

	// Simple 2-poles resonant filter
	//
	// XXX freq WAS unused but is now mix_frequency!
	//
	public void SetUpChannelFilter(ref SongVoice chan, bool reset, int filterModifier, int freq)
	{
		const double FrequencyParamMult = 128.0 / (24.0 * 256.0);

		int cutoff = chan.Cutoff;
		int resonance = chan.Resonance;

		cutoff = cutoff * (filterModifier + 256) / 256;

		if (cutoff > 255)
			cutoff = 255;

		if (resonance > 255)
			resonance = 255;

		if (resonance == 0 && cutoff >= 254)
		{
			if (chan.Flags.HasAllFlags(ChannelFlags.NewNote))
			{
				// Z7F next to a note disables the filter, however in other cases this should not happen.
				// Test cases: filter-reset.it, filter-reset-carry.it, filter-reset-envelope.it, filter-nna.it, FilterResetPatDelay.it, FilterPortaSmpChange.it, FilterPortaSmpChange-InsMode.it
				chan.Flags &= ~ChannelFlags.Filter;
			}

			return;
		}

		chan.Flags |= ChannelFlags.Filter;

		// 2 ^ (i / 24 * 256)
		double frequency = 110.0 * Math.Pow(2.0, cutoff * FrequencyParamMult + 0.25);

		if (frequency > freq / 2.0)
			frequency = freq / 2.0;

		double r = freq / (2.0F * Math.PI * frequency);

		double d = Tables.ResonanceTable[resonance] * r + Tables.ResonanceTable[resonance] - 1.0F;
		double e = r * r;

		double fg = 1.0 / (1.0 + d + e);
		double fb0 = (d + e + e) / (1.0 + d + e);
		double fb1 = -e / (1.0 + d + e);

		chan.FilterA0 = (int)(fg * (1 << Constants.FilterPrecision));
		chan.FilterB0 = (int)(fb0 * (1 << Constants.FilterPrecision));
		chan.FilterB1 = (int)(fb1 * (1 << Constants.FilterPrecision));

		if (reset)
		{
			chan.FilterY00 = chan.FilterY01 = 0;
			chan.FilterY10 = chan.FilterY11 = 0;
		}
	}

	public void InsertRestartPos(int restartOrder) // hax
	{
		if (restartOrder == 0)
			return;

		// find the last pattern, also look for one that's not being used
		int max = OrderList.Max();

		int newPat = max + 1;

		int pat = OrderList.Last();

		if ((pat >= Patterns.Count) || !(Patterns[pat] is Pattern pattern) || (pattern.Rows.Count == 0))
			return;

		// how many times it was used (if >1, copy it)
		int used = OrderList.Where(n => n == pat).Count();

		if (used > 1)
		{
			// copy the pattern so we don't screw up the playback elsewhere
			while ((newPat < Patterns.Count) && (Patterns[newPat] != null))
				newPat++;

			while (newPat >= Patterns.Count)
				Patterns.Add(null);

			//Log.Append(2, "Copying pattern {0} to {1} for restart position", pat, newPat);
			Patterns[newPat] = pattern.Clone();

			OrderList[OrderList.Count - 1] = pat = newPat;
		}
		else
		{
			//Log.Append(2, "Modifying pattern {0} to add restart position", pat);
		}

		int maxRow = pattern.Rows.Count - 1;

		for (int row = 0; row <= maxRow; row++)
		{
			int emptyChannel = -1; // where's an empty effect?

			bool hasBreak = false;
			bool hasJump = false;

			for (int n = 0; n < Constants.MaxChannels; n++)
			{
				ref var note = ref pattern[row][n + 1];

				switch (note.Effect)
				{
					case Effects.PositionJump:
						hasJump = true;
						break;
					case Effects.PatternBreak:
						hasBreak = true;
						if (note.Parameter == 0)
							emptyChannel = n; // always rewrite C00 with Bxx (it's cleaner)
						break;
					case Effects.None:
						if (emptyChannel < 0)
							emptyChannel = n;
						break;
				}
			}

			// if there's not already a Bxx, and we have a spare channel,
			// AND either there's a Cxx or it's the last row of the pattern,
			// then stuff in a jump back to the restart position.
			if (!hasJump && (emptyChannel >= 0) && (hasBreak || row == maxRow))
			{
				ref var empty = ref pattern[row][emptyChannel];

				empty.Effect = Effects.PositionJump; ;
				empty.Parameter = (byte)restartOrder;
			}
		}
	}

	// OPL stuff -------------------------------------------------------------
	public FMDriver? OPL;
	public byte OPLRetVal;
	public int OPLRegNumber;
	public bool OPLFMActive;

	public byte[]?[] OPLDTab = new byte[9][];
	public byte[] OPLKeyOnTab = new byte[9];
	public int[] OPLPans = new int[Constants.MaxVoices];

	public int[] OPLToChan = new int[9];
	public int[] OPLFromChan = new int[Constants.MaxVoices];

	public void InitializeOPL(int mixFrequency)
	{
		OPL?.ShutDown();

		OPL = FMDriver.Create(mixFrequency);
		// Clock = speed at which the chip works. mixfreq = audio resampler

		OPL.ResetChip();
	}

	int GetOPLVoice(int c)
	{
		return OPLFromChan[c];
	}

	int SetOPLVoice(int c)
	{
		if (OPLFromChan[c] == -1)
		{
			// Search for unused chans

			for (int a = 0; a < 9; a++)
			{
				if (OPLToChan[a] == -1)
				{
					OPLToChan[a] = c;
					OPLFromChan[c] = a;
					break;
				}
			}

			if (OPLFromChan[c] == -1)
			{
				// Search for note-released chans
				for (int a = 0; a < 9; a++)
				{
					if (!OPLKeyOnTab[a].HasBitSet(FMDriver.KeyOnBit))
					{
						OPLFromChan[OPLToChan[a]] = -1;
						OPLToChan[a] = c;
						OPLFromChan[c] = a;
						break;
					}
				}
			}
		}

		//Log.Append(2, "entering with {0}. tested? {1}. selected {2}. Current: {3}", c, t, s, ChantoOPL[c]);
		return GetOPLVoice(c);
	}

	void OPLOutPortB(int port, int value)
	{
		if ((OPL == null)
	   || (port < FMDriver.BasePort)
		 || (port >= FMDriver.BasePort + 4))
			return;

		int ind = port - FMDriver.BasePort;

		OPL.Write(ind, value);

		if (ind.HasBitSet(1))
		{
			if (OPLRegNumber == 4)
			{
				if (value == 0x80)
					OPLRetVal = 0x02;
				else if (value == 0x21)
					OPLRetVal = 0xC0;
			}
		}
		else
			OPLRegNumber = unchecked((byte)value);
	}

	byte OPLInPortB(int port)
	{
		return port.IsInRange(FMDriver.BasePort, FMDriver.BasePort + 3) ? OPLRetVal : (byte)0;
	}

	void OPLByte(int idx, byte data)
	{
		OPLOutPortB(FMDriver.BasePort, idx);    // for(int a = 0; a < 6;  a++) InPortB(FMDriver.BasePort);
		OPLOutPortB(FMDriver.BasePort + 1, data); // for(int a = 0; a < 35; a++) InPortB(FMDriver.BasePort);
	}

	void OPLByteRightSide(int idx, byte data)
	{
		OPLOutPortB(FMDriver.BasePort + 2, idx);    // for(int a = 0; a < 6;  a++) InPortB(FMDriver.BasePort);
		OPLOutPortB(FMDriver.BasePort + 3, data); // for(int a = 0; a < 35; a++) InPortB(FMDriver.BasePort);
	}

	public void OPLNoteOff(int c)
	{
		int oplc = GetOPLVoice(c);
		if (oplc == -1)
			return;

		OPLKeyOnTab[oplc] &= unchecked((byte)~FMDriver.KeyOnBit);

		OPLByte(FMDriver.KeyOnBlock + oplc, OPLKeyOnTab[oplc]);
	}

	/* OPLNoteOn changes the frequency on specified
		channel and guarantees the key is on. (Doesn't
		retrig, just turns the note on and sets freq.)
		If keyoff is nonzero, doesn't even set the note on.
		Could be used for pitch bending also. */
	public void OPLHertzTouch(int c, int milliHertz, bool keyoff)
	{
		int oplc = GetOPLVoice(c);
		if (oplc == -1)
			return;

		OPLFMActive = true;

		/*
			Bytes A0-B8 - Octave / F-Number / Key-On

			7     6     5     4     3     2     1     0
			+-----+-----+-----+-----+-----+-----+-----+-----+
			|        F-Number (least significant byte)      |  (A0-A8)
			+-----+-----+-----+-----+-----+-----+-----+-----+
			|  Unused   | Key |    Octave       | F-Number  |  (B0-B8)
			|           | On  |                 | most sig. |
			+-----+-----+-----+-----+-----+-----+-----+-----+
		*/

		const int conversion_factor = (int)FMDriver.RateBase; // Frequency of OPL.

		FMDriver.MilliHertzToFnum(milliHertz, out var outfnum, out var outblock, conversion_factor);

		OPLKeyOnTab[oplc] = unchecked((byte)(
					(keyoff ? (byte)0 : FMDriver.KeyOnBit)      // Key on
					| (outblock << 2)                  // Octave
					| ((outfnum >> 8) & FMDriver.FNumHighMask))); // F-number high 2 bits

		OPLByte(FMDriver.FNumLow +    oplc, unchecked((byte)(outfnum & 0xFF)));  // F-Number low 8 bits
		OPLByte(FMDriver.KeyOnBlock + oplc, OPLKeyOnTab[oplc]);
	}

	public void OPLTouch(int c, int vol)
	{
	//fprintf(stderr, "OPLTouch(%d, %p:%02X.%02X.%02X.%02X-%02X.%02X.%02X.%02X-%02X.%02X.%02X, %d)\n",
	//    c, D,D[0],D[1],D[2],D[3],D[4],D[5],D[6],D[7],D[8],D[9],D[10], Vol);

		int oplc = GetOPLVoice(c);
		if (oplc == -1)
			return;

		byte[] D = OPLDTab[oplc] ?? throw new Exception("Internal error: OPL function parameters not set");
		int Ope = FMDriver.PortBases[oplc];

		/*
			Bytes 40-55 - Level Key Scaling / Total Level

			7     6     5     4     3     2     1     0
			+-----+-----+-----+-----+-----+-----+-----+-----+
			|  Scaling  |             Total Level           |
			|   Level   | 24    12     6     3    1.5   .75 | <-- dB
			+-----+-----+-----+-----+-----+-----+-----+-----+
				bits 7-6 - causes output levels to decrease as the frequency
					rises:
						00   -  no change
						10   -  1.5 dB/8ve
						01   -  3 dB/8ve
						11   -  6 dB/8ve
				bits 5-0 - controls the total output level of the operator.
					all bits CLEAR is loudest; all bits SET is the
					softest.  Don't ask me why.
		*/

		/* 2008-09-27 Bisqwit:
		 * Did tests in ST3: The value poked
		 * to 0x43, minus from 63, is:
		 *
		 *  OplVol 63  47  31
		 * SmpVol
		 *  64     63  47  31
		 *  32     32  24  15
		 *  16     16  12   8
		 *
		 * This seems to clearly indicate that the value
		 * poked is calculated with 63 - round(oplvol*smpvol/64.0).
		 *
		 * Also, from the documentation we can deduce that
		 * the maximum volume to be set is 47.25 dB and that
		 * each increase by 1 corresponds to 0.75 dB.
		 *
		 * Since we know that 6 dB is equivalent to a doubling
		 * of the volume, we can deduce that an increase or
		 * decrease by 8 will double / halve the volume.
		 *
			D = 63-OPLVol
			NewD = 63-target

			OPLVol = 63 - D
			newvol = clip(vol,63)  -> max value of newvol=63, same as max of OPLVol.
			target = OPLVOL * (newvol/63)


			NewD = 63-(OLPVOL * (newvol/63))
			NewD = 63-((63 - D) * (newvol/63))
			NewD = 63-((63*newvol/63) - (D*newvol/63) )
			NewD = 63-(newvol - (D*newvol/63) )
			NewD = 63-(newvol) + (D*newvol/63)
			NewD = 63 + (D*newvol/63) - newvol
			NewD = 63 + (D*newvol/63) - newvol
		*/
		// On Testing, ST3 does not alter the modulator volume.

		// vol is previously converted to the 0..63 range.

		// Set volume of both operators in additive mode
		if (D[10].HasBitSet(FMDriver.ConnectionBit))
		{
			OPLByte(
				FMDriver.KSLLevel + Ope,
				unchecked((byte)(
					(D[2] & FMDriver.KSLMask) | (63 + ((D[2] & FMDriver.TotalLevelMask) * vol / 63) - vol)
				)));
		}

		OPLByte(
			FMDriver.KSLLevel + 3+Ope,
			unchecked((byte)(
				(D[3] & FMDriver.KSLMask) | (63 + ((D[3] & FMDriver.TotalLevelMask) * vol / 63) - vol)
			)));
	}


	public void OPLPan(int c, int val)
	{
		OPLPans[c] = val.Clamp(0, 256);

		int oplc = GetOPLVoice(c);
		if (oplc == -1)
			return;

		byte[] D = OPLDTab[oplc] ?? throw new Exception("Internal error: OPL function parameters not set");

		/* feedback, additive synthesis and Panning... */
		OPLByte(
			FMDriver.FeedbackConnection + oplc,
			unchecked((byte)(
				(D[10] & ~FMDriver.StereoBits)
			| (OPLPans[c] < 85 ? FMDriver.VoiceToLeft
				: OPLPans[c] > 170 ? FMDriver.VoiceToRight
				: (FMDriver.VoiceToLeft | FMDriver.VoiceToRight))
			)));
	}


	public void OPLPatch(int c, byte[] D)
	{
		int oplc = SetOPLVoice(c);
		if (oplc == -1)
			return;

		OPLDTab[oplc] = D;
		int Ope = FMDriver.PortBases[oplc];

		OPLByte(FMDriver.AMVib+           Ope, D[0]);
		OPLByte(FMDriver.KSLLevel+        Ope, D[2]);
		OPLByte(FMDriver.AttackDecay+     Ope, D[4]);
		OPLByte(FMDriver.SustainRelease+  Ope, D[6]);
		OPLByte(FMDriver.WaveSelect+      Ope, unchecked((byte)(D[8]&7)));// 5 high bits used elsewhere

		OPLByte(FMDriver.AMVib+         3+Ope, D[1]);
		OPLByte(FMDriver.KSLLevel+      3+Ope, D[3]);
		OPLByte(FMDriver.AttackDecay+   3+Ope, D[5]);
		OPLByte(FMDriver.SustainRelease+3+Ope, D[7]);
		OPLByte(FMDriver.WaveSelect+    3+Ope, unchecked((byte)(D[9]&7)));// 5 high bits used elsewhere

		/* feedback, additive synthesis and Panning... */
		OPLByte(
			FMDriver.FeedbackConnection + oplc,
			unchecked((byte)(
				(D[10] & ~FMDriver.StereoBits)
			| (OPLPans[c]<85 ? FMDriver.VoiceToLeft
				: OPLPans[c]>170 ? FMDriver.VoiceToRight
				: (FMDriver.VoiceToLeft | FMDriver.VoiceToRight))
			)));
	}

	public void OPLReset()
	{
		if (OPL == null)
			return;

		OPL.ResetChip();

		OPLDetect();

		for (int a = 0; a < Constants.MaxVoices; ++a)
			OPLFromChan[a]=-1;

		for (int a = 0; a < OPLToChan.Length; ++a)
		{
			OPLToChan[a]= -1;
			OPLDTab[a] = null;
		}

		OPLByte(FMDriver.TestRegister, FMDriver.EnableWaveSelect);

		//Enable OPL3.
		OPLByteRightSide(FMDriver.OPL3ModeRegister, FMDriver.OPL3Enable);

		OPLFMActive = false;
	}

	public bool OPLDetect()
	{
		/* Reset timers 1 and 2 */
		OPLByte(FMDriver.TimerControlRegister, FMDriver.Timer1Mask | FMDriver.Timer2Mask);

		/* Reset the IRQ of the FM chip */
		OPLByte(FMDriver.TimerControlRegister, FMDriver.IRQReset);

		byte ST1 = OPLInPortB(FMDriver.BasePort); /* Status register */

		OPLByte(FMDriver.Timer1Register, 255);
		OPLByte(FMDriver.TimerControlRegister, FMDriver.Timer2Mask | FMDriver.Timer1Start);

		/*_asm xor cx,cx;P1:_asm loop P1*/
		byte ST2 = OPLInPortB(FMDriver.BasePort);

		OPLByte(FMDriver.TimerControlRegister, FMDriver.Timer1Mask | FMDriver.Timer2Mask);
		OPLByte(FMDriver.TimerControlRegister, FMDriver.IRQReset);

		bool OPLMode = (ST2 & 0xE0) == 0xC0 && !ST1.HasAnyBitSet(0xE0);

		return OPLMode;
	}

	public void ShutDownOPL()
	{
		OPL?.ShutDown();
		OPL = null;
	}

	// -----------------------------------------------------------------------

	static Song()
	{
		Configuration.RegisterConfigurable(new MIDIConfigurationThunk());
	}

	class MIDIConfigurationThunk : IConfigurable<MIDIConfiguration>
	{
		public void SaveConfiguration(MIDIConfiguration config) => Song.SaveConfiguration(config);
		public void LoadConfiguration(MIDIConfiguration config) => Song.LoadConfiguration(config);
	}

	static void LoadConfiguration(MIDIConfiguration config)
	{
		DefaultMIDIConfig = config;

		CurrentSong.MIDIConfig.CopyFrom(DefaultMIDIConfig);
	}

	static void SaveConfiguration(MIDIConfiguration config)
	{
		DefaultMIDIConfig.CopyFrom(CurrentSong.MIDIConfig);

		config.CopyFrom(DefaultMIDIConfig);
	}

	// MIDI stuff ------------------------------------------------------------
	/* This maps S3M concepts into MIDI concepts */
	public SongS3MChannelInfo[] MIDIS3MChannels = new SongS3MChannelInfo[Constants.MaxVoices];
	/* This helps reduce the MIDI traffic, also does some encapsulation */
	public SongMIDIState[] MIDIChannels = new SongMIDIState[Constants.MaxMIDIChannels];

	public MIDIConfiguration MIDIConfig = DefaultMIDIConfig.Clone();

	public double MIDILastSongCounter;

	public uint MIDIRunningStatus;

	/* for midi translation, memberized from audio_playback.c */
	public int[] MIDINoteTracker = new int[Constants.MaxChannels];
	public int[] MIDIVolTracker = new int[Constants.MaxChannels];
	public int[] MIDIInsTracker = new int[Constants.MaxChannels];
	public int[] MIDIWasProgram = new int[Constants.MaxMIDIChannels];
	public int[] MIDIWasBankLo = new int[Constants.MaxMIDIChannels];
	public int[] MIDIWasBankHi = new int[Constants.MaxMIDIChannels];

	public SongNoteRef[] MIDILastRow = new SongNoteRef[Constants.MaxChannels];
	public int MIDILastRowNumber;

	public bool MIDIPlaying;

	/* MIDI callback function */
	// TODO: song_midi_out_raw_spec_t midi_out_raw;
	// -----------------------------------------------------------------------

	public bool PatternLoop; // effects.c: need this for stupid pattern break compatibility

	// noise reduction filter
	public int LeftNoiseReduction, RightNoiseReduction;

	// chaseback
	public int StopAtOrder;
	public int StopAtRow;
	public TimeSpan StopAtTime;

	// multi-write stuff -- null if no multi-write is in progress, else array of one struct per channel
	public MultiWrite[]? MultiWrite;
	public AudioPlaybackState? SavedAudioPlaybackState;

	public SongFlags Flags;

	public bool IsInstrumentMode => Flags.HasAllFlags(SongFlags.InstrumentMode);

	public bool IsStereo
	{
		get => !Flags.HasAllFlags(SongFlags.NoStereo);
		set
		{
			if (value)
				Flags &= ~SongFlags.NoStereo;
			else
				Flags |= SongFlags.NoStereo;

			if (this == CurrentSong)
				AllPages.SongVariables.SyncStereo(value);
		}
	}

	public void ToggleStereo()
	{
		IsStereo = !IsStereo;
	}

	public bool ITOldEffects
	{
		get => Flags.HasAllFlags(SongFlags.ITOldEffects);
		set
		{
			if (value)
				Flags |= SongFlags.ITOldEffects;
			else
				Flags &= ~SongFlags.ITOldEffects;
		}
	}

	public bool CompatibleGXX
	{
		get => Flags.HasAllFlags(SongFlags.CompatibleGXX);
		set
		{
			if (value)
				Flags |= SongFlags.CompatibleGXX;
			else
				Flags &= ~SongFlags.CompatibleGXX;
		}
	}

	public bool LinearPitchSlides
	{
		get => Flags.HasAllFlags(SongFlags.LinearSlides);
		set
		{
			if (value)
				Flags |= SongFlags.LinearSlides;
			else
				Flags &= ~SongFlags.LinearSlides;
		}
	}

	public Song()
	{
		for (int i = 0; i < Constants.MaxSamples; i++)
			Samples.Add(null);

		Flags = 0;
		PanSeparation = 128;
		NumVoices = 0;
		FrequencyFactor = 128;
		TempoFactor = 128;
		InitialGlobalVolume = 128;
		CurrentGlobalVolume = 128;
		InitialSpeed = 6;
		InitialTempo = 125;
		ProcessRow = 0;
		Row = 0;
		CurrentPattern = 0;
		CurrentOrder = 0;
		ProcessOrder = 0;
		MixingVolume = 0x30;

		Message = "";

		RowHighlightMajor = 16;
		RowHighlightMinor = 4;

		Array.Clear(Voices);
		Array.Clear(VoiceMix);
		Samples.Clear();
		Instruments.Clear();
		OrderList.Clear();
		Patterns.Clear();

		ResetMIDIConfig();
		ForgetHistory();

		Array.Clear(Channels);
		for (int i=0; i < Channels.Length; i++)
		{
			Channels[i].Panning = 128;
			Channels[i].Volume = 64;
			Channels[i].Flags = 0;
		}

		ShutDownOPL();
		GeneralMIDI.Reset(this, true);

		Array.Clear(MIDINoteTracker);
		Array.Clear(MIDIVolTracker);
		Array.Clear(MIDIInsTracker);
		Array.Clear(MIDIWasProgram);
		Array.Clear(MIDIWasBankLo);
		Array.Clear(MIDIWasBankHi);

		MIDILastRowNumber = -1;

		MIDISink = null;
	}

	int _currentOrder;

	public void SetCurrentOrderDirect(int newValue) => _currentOrder = newValue;

	public int CurrentOrder
	{
		get => _currentOrder;
		set
		{
			for (int j = 0; j < Constants.MaxVoices; j++)
			{
				ref var v = ref Voices[j];

				// modplug sets vib pos to 16 outside of old effects mode
				v.VibratoPosition = Flags.HasAllFlags(SongFlags.ITOldEffects) ? 0 : 0x10;
				v.TremoloPosition = 0;
			}

			int position = value;

			if (position > Constants.MaxOrders)
				position = 0;

			if (position == 0)
			{
				for (int i = 0; i < Constants.MaxVoices; i++)
				{
					ref var v = ref Voices[i];

					v = default;
					v.Note = v.NewNote = 1;
					v.Cutoff = 0x7F;
					v.Volume = 256;

					if (i < Constants.MaxChannels)
					{
						v.Panning = Channels[i].Panning;
						v.GlobalVolume = Channels[i].Volume;
						v.Flags = Channels[i].Flags;
					}
					else
					{
						v.Panning = 128;
						v.GlobalVolume = 64;
					}
				}

				CurrentGlobalVolume = InitialGlobalVolume;
				CurrentSpeed = InitialSpeed;
				CurrentTempo = InitialTempo;
			}

			ProcessOrder = position - 1;
			ProcessRow = ProcessNextOrder;
			Row = 0;
			BreakRow = 0; /* set this to whatever row to jump to */
			TickCount = 1;
			RowCount = 0;
			BufferCount = 0;

			Flags &= ~(SongFlags.PatternLoop | SongFlags.EndReached);
		}
	}

	public Song Clone()
	{
		var clone = new Song();

		foreach (var field in typeof(Song).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy))
			field.SetValue(clone, field.GetValue(this));

		return clone;
	}

	public SongSample? GetSample(int n)
	{
		if (n >= Samples.Count)
			return null;

		return Samples[n];
	}

	public SongSample EnsureSample(int n)
	{
		if (n > Constants.MaxSamples)
			throw new ArgumentOutOfRangeException(nameof(n));

		while (n >= Samples.Count)
			Samples.Add(null);

		return Samples[n] ??= new SongSample();
	}

	public int GetSampleNumber(SongSample? samp)
	{
		if (samp == null)
			return 0;

		// TODO: this makes GetPlayingSamples O(n^2), does this need fix?
		int number = Samples.IndexOf(samp);

		if (number < 0)
			number = 0;

		return number;
	}

	// instrument, sample, whatever.
	void SwapInstrumentsInPatterns(int a, int b)
	{
		byte ba = (byte)a;
		byte bb = (byte)b;

		foreach (var pat in Patterns.OfType<Pattern>())
			foreach (var row in pat.Rows)
				for (int n = 1; n <= Constants.MaxChannels; n++)
				{
					ref var note = ref row[n];

					if (note.Instrument == ba)
						note.Instrument = bb;
					else if (note.Instrument == bb)
						note.Instrument = ba;
				}
	}

	public void ExchangeSamples(int a, int b)
	{
		if (a == b)
			return;

		lock (AudioPlayback.LockScope())
		{
			(Samples[a], Samples[b]) = (Samples[b], Samples[a]);
			Status.Flags |= StatusFlags.SongNeedsSave;
		}
	}

	public void SwapSamples(int a, int b)
	{
		if (a == b)
			return;

		lock (AudioPlayback.LockScope())
		{
			if (IsInstrumentMode)
			{
				// ... or should this be done even in sample mode?
				for (int n = 1; n < Constants.MaxInstruments; n++)
				{
					var ins = Instruments[n];

					if (ins == null)
						continue;

					for (int s = 0; s < ins.SampleMap.Length; s++)
					{
						if (ins.SampleMap[s] == a)
							ins.SampleMap[s] = (byte)b;
						else if (ins.SampleMap[s] == b)
							ins.SampleMap[s] = (byte)a;
					}
				}
			}
			else
				SwapInstrumentsInPatterns(a, b);
		}

		ExchangeSamples(a, b);
	}

	public void ReplaceSample(int num, int with)
	{
		if (num < 1 || num >= Samples.Count
				|| with < 1 || with >= Samples.Count)
			return;

		if (IsInstrumentMode)
		{
			// for each instrument, for each note in the keyboard table, replace 'smp' with 'with'
			for (int i = 1; i < Instruments.Count; i++)
			{
				var ins = Instruments[i];
				if (ins == null)
					continue;
				for (int j = 0; j < 128; j++)
					if (ins.SampleMap[j] == num)
						ins.SampleMap[j] = (byte)with;
			}
		}
		else
		{
			// for each pattern, for each note, replace 'smp' with 'with'
			foreach (var pattern in Patterns.OfType<Pattern>())
				foreach (var row in pattern.Rows)
					for (int j = 1; j <= Constants.MaxChannels; j++)
					{
						if (row[j].Instrument == num)
							row[j].Instrument = (byte)with;
					}
		}
	}

	public void CopyInstrument(int dst, int src)
	{
		if (src == dst) return;

		using (AudioPlayback.LockScope())
		{
			Instruments[dst] = Instruments[src]?.Clone();

			Status.Flags |= StatusFlags.SongNeedsSave;
		}
	}

	public void ExchangeInstruments(int a, int b)
	{
		if (a == b)
			return;

		lock (AudioPlayback.LockScope())
		{
			(Instruments[a], Instruments[b]) = (Instruments[b], Instruments[a]);
			Status.Flags |= StatusFlags.SongNeedsSave;
		}
	}

	public void SwapInstruments(int a, int b)
	{
		if (a == b)
			return;

		if (IsInstrumentMode)
		{
			lock (AudioPlayback.LockScope())
				SwapInstrumentsInPatterns(a, b);
		}

		ExchangeInstruments(a, b);
	}

	void AdjustInstrumentsInPatterns(int start, int delta)
	{
		int maxSampleNumber = Samples.Count - 1;

		foreach (var pattern in Patterns.OfType<Pattern>())
			foreach (var row in pattern.Rows)
				for (int n = 1; n <= Constants.MaxChannels; n++)
				{
					ref var note = ref row[n];

					if (note.Instrument >= start)
						note.Instrument = (byte)(note.Instrument + delta).Clamp(0, maxSampleNumber);
				}
	}

	void AdjustSamplesInInstruments(int start, int delta)
	{
		int maxSampleNumber = Samples.Count - 1;

		foreach (var instrument in Instruments)
		{
			if (instrument == null)
				continue;

			for (int s = 0; s < instrument.SampleMap.Length; s++)
			{
				if (instrument.SampleMap[s] >= start)
				{
					instrument.SampleMap[s] = (byte)(instrument.SampleMap[s] + delta).Clamp(0, maxSampleNumber);
				}
			}
		}
	}

	public void InsertSampleSlot(int n)
	{
		var newSample = new SongSample();

		newSample.C5Speed = 8363;
		newSample.Volume = 64 * 4;
		newSample.GlobalVolume = 64;

		lock (AudioPlayback.LockScope())
		{
			Samples.Insert(n, newSample);

			if (IsInstrumentMode)
				AdjustSamplesInInstruments(n, 1);
			else
				AdjustInstrumentsInPatterns(n, 1);
		}
	}

	public void RemoveSampleSlot(int n)
	{
		var sample = Samples[n];

		if ((sample != null) && sample.HasData)
			return;

		lock (AudioPlayback.LockScope())
		{
			Status.Flags |= StatusFlags.SongNeedsSave;

			var newSample = new SongSample();

			newSample.C5Speed = 8363;
			newSample.Volume = 64 * 4;
			newSample.GlobalVolume = 64;

			Samples.RemoveAt(n);
			Samples.Add(newSample);

			if (IsInstrumentMode)
				AdjustSamplesInInstruments(n, -1);
			else
				AdjustInstrumentsInPatterns(n, -1);
		}
	}

	public void InsertInstrumentSlot(int n)
	{
		Status.Flags |= StatusFlags.SongNeedsSave;

		lock (AudioPlayback.LockScope())
		{
			Instruments.Insert(n, null);
			AdjustInstrumentsInPatterns(n, 1);
		}
	}

	public void RemoveInstrumentSlot(int n)
	{
		if ((Instruments[n] is SongInstrument existingInstrument) && !existingInstrument.IsEmpty)
			return;

		lock (AudioPlayback.LockScope())
		{
			Instruments.RemoveAt(n);
			Instruments.Add(null);
			AdjustInstrumentsInPatterns(n, -1);
		}
	}

	// Returns 1 if sample `n` is used by the specified instrument; 0 otherwise.
	bool SampleUsedByInstrument(int n, SongInstrument instrument)
	{
		return instrument.SampleMap.Contains((byte)n);
	}

	// Returns 1 if sample `n` is used by at least two instruments; 0 otherwise.
	bool SampleUsedByManyInstruments(int n)
	{
		bool found = false;

		foreach (var instrument in Instruments)
		{
			if (instrument != null)
			{
				if (SampleUsedByInstrument(n, instrument))
				{
					if (found)
						return true;
					else
						found = true;
				}
			}
		}

		return false;
	}

	// n: The index of the instrument to delete (base-1).
	// preserve_samples: If 0, delete all samples used by instrument.
	//                   If 1, only delete samples that no other instruments use.
	public void DeleteInstrument(int n, bool preserveSharedSamples)
	{
		var instrument = Instruments[n];

		if (instrument == null)
			return;

		for (int i = 0; i < instrument.NoteMap.Length; i++)
		{
			int j = instrument.SampleMap[i];

			if (j != 0)
			{
				if (!preserveSharedSamples || !SampleUsedByManyInstruments(j))
					ClearSample(j);
			}
		}

		WipeInstrument(n);
	}

	public void ReplaceInstrument(int num, int with)
	{
		if (!IsInstrumentMode)
			return;

		if ((num < 1) || (num > Instruments.Count))
			return;
		if ((with < 1) || (with > Instruments.Count))
			return;

		byte withByte = (byte)with;

		// for each pattern, for each note, replace 'ins' with 'with'
		foreach (var pattern in Patterns.OfType<Pattern>())
			foreach (var row in pattern.Rows)
				for (int j = 1; j <= Constants.MaxChannels; j++)
				{
					ref var note = ref row[j];

					if (note.Instrument == num)
						note.Instrument = withByte;
				}
	}

	public SongInstrument GetInstrument(int n)
	{
		if (n >= Constants.MaxInstruments)
			throw new ArgumentOutOfRangeException();

		// Make a new instrument if it doesn't exist.
		if ((Instruments[n] == null))
			Instruments[n] = new SongInstrument(this);

		return Instruments[n]!;
	}

	public SongInstrument? GetInstrumentSlotSafe(int n)
	{
		if ((n < 0) || (n >= Instruments.Count))
			return null;

		return Instruments[n];
	}

	public int GetInstrumentNumber(SongInstrument? inst)
	{
		if (inst == null)
			return 0;

		// TODO: this makes GetPlayingInstruments O(n^2), does this need fix?
		int number = Instruments.IndexOf(inst);

		if (number < 0)
			number = 0;

		return number;
	}

	public int[] GetMixState(out int numActiveVoices)
	{
		numActiveVoices = Math.Min(NumVoices, Voices.Length);
		return VoiceMix;
	}

	public bool SampleIsUsedByInstrument(int samp)
	{
		foreach (var instrument in Instruments)
		{
			if (instrument == null)
				continue;

			if (instrument.SampleMap.Contains((byte)samp))
				return true;
		}

		return false;
	}

	// ------------------------------------------------------------------------

	// calculates row of offset from passed row.
	// sets actual pattern number, row and optional pattern buffer.
	// returns length of selected patter, or 0 on error.
	// if song mode is pattern loop (MODE_PATTERN_LOOP), offset is mod calculated
	// in current pattern.
	public Pattern? GetPatternOffset(ref int patternNumber, ref int rowNumber, int offset)
	{
		if (AudioPlayback.Mode.HasAllFlags(AudioPlaybackMode.PatternLoop))
		{
			// just wrap around current rows
			rowNumber = (rowNumber + offset) % GetPatternLength(patternNumber);
			return GetPattern(patternNumber);
		}

		int tot = GetPatternLength(patternNumber);
		while (offset + rowNumber > tot)
		{
			offset -= tot;
			patternNumber++;
			tot = GetPatternLength(patternNumber);
			if (tot == 0)
				return null;
		}

		rowNumber += offset;

		return GetPattern(patternNumber);
	}

	public Pattern? GetPattern(int n, bool create = true, int rowsInNewPattern = -1)
	{
		while (n >= Patterns.Count)
			Patterns.Add(null);

		if (create && (Patterns[n] == null))
		{
			if (rowsInNewPattern > 0)
				Patterns[n] = new Pattern(rowsInNewPattern);
			else
				Patterns[n] = new Pattern();
		}

		return Patterns[n];
	}

	public Pattern SetPattern(int n, Pattern pattern)
	{
		while (n >= Patterns.Count)
			Patterns.Add(new Pattern());

		Patterns[n] = pattern;

		return pattern;
	}

	public int GetPatternLength(int n)
	{
		var pattern = GetPattern(n, create: false);

		return pattern?.Rows.Count
			?? ((n >= Constants.MaxPatterns) ? 0 : Constants.DefaultPatternLength);
	}

	/* search the orderlist for a pattern, starting at the current order.
	return value of -1 means the pattern isn't on the list */
	public int GetNextOrderForPattern(int pat)
	{
		int ord = CurrentOrder;

		ord = ord.Clamp(0, OrderList.Count);

		for (int i = ord; i < OrderList.Count; i++)
			if (OrderList[i] == pat)
				return i;

		for (int i = 0; i < ord; i++)
			if (OrderList[i] == pat)
				return i;

		return -1;
	}

	public SongChannel? GetChannel(int n)
	{
		if ((n < 0) || (n >= Channels.Length))
			return null;

		return Channels[n];
	}

	// ------------------------------------------------------------------------------------------------------------

	public TimeSpan GetLengthTo(int order, int row)
	{
		lock (AudioPlayback.LockScope())
		{
			StopAtOrder = order;
			StopAtRow = row;

			var t = GetLength();

			StopAtOrder = -1;
			StopAtRow = -1;

			return t;
		}
	}

	public (int OrderNumber, int RowNumber) GetAtTime(TimeSpan time)
	{
		if (time <= TimeSpan.Zero)
			return (0, 0);

		lock (AudioPlayback.LockScope())
		{
			StopAtOrder = Constants.MaxOrders;
			StopAtRow = 255; /* unpossible */
			StopAtTime = time;

			GetLength();

			var ret = (StopAtOrder, StopAtRow);

			StopAtOrder = StopAtRow = -1;
			StopAtTime = default;

			return ret;
		}
	}

	static Pattern EmptyPattern = Pattern.CreateEmpty();

	public TimeSpan GetLength()
	{
		TimeSpan elapsed = TimeSpan.Zero;

		int row = 0;
		int nextRow = 0;

		int curOrder = 0;
		int nextOrder = 0;

		int pat = OrderList.First();

		int speed = InitialSpeed;
		int tempo = InitialTempo;

		TimeSpan[] patLoop = new TimeSpan[Constants.MaxChannels];
		byte[] memTempo = new byte[Constants.MaxChannels];

		ulong setloop = 0; // bitmask

		for (; ; )
		{
			int speedCount = 0;

			row = nextRow;
			curOrder = nextOrder;

			// Check if pattern is valid
			pat = OrderList[curOrder];
			while (pat >= Constants.MaxPatterns)
			{
				// End of song ?
				if (pat == SpecialOrders.Last || curOrder >= Constants.MaxOrders)
				{
					pat = SpecialOrders.Last; // cause break from outer loop too
					break;
				}
				else
				{
					curOrder++;
					pat = (curOrder < Constants.MaxOrders) ? OrderList[curOrder] : SpecialOrders.Last;
				}

				nextOrder = curOrder;
			}

			// Weird stuff?
			if (pat >= Constants.MaxPatterns)
				break;

			var pData = Patterns[pat];
			int pSize;

			if (pData != null)
				pSize = pData.Rows.Count;
			else
			{
				pData = EmptyPattern;
				pSize = 64;
			}

			// guard against Cxx to invalid row, etc.
			if (row >= pSize)
				row = 0;

			// Update next position
			nextRow = row + 1;
			if (nextRow >= pSize)
			{
				nextOrder = curOrder + 1;
				nextRow = 0;
			}

			/* muahahaha */
			if (StopAtOrder > -1 && StopAtRow > -1)
			{
				if (StopAtOrder <= curOrder && StopAtRow <= row)
					break;

				if (StopAtTime > TimeSpan.Zero)
				{
					if (elapsed >= StopAtTime)
					{
						StopAtOrder = curOrder;
						StopAtRow = row;
						break;
					}
				}
			}

			/* This is nasty, but it fixes inaccuracies with SB0 SB1 SB1. (Simultaneous
			loops in multiple channels are still wildly incorrect, though.) */
			if (row == 0)
				setloop = ~0ul;

			if (setloop != 0)
			{
				for (int n = 0; n < Constants.MaxChannels; n++)
					if ((setloop & (1ul << n)) != 0)
						patLoop[n] = elapsed;
				setloop = 0;
			}

			for (int n = 0; n < Constants.MaxChannels; n++)
			{
				ref var note = ref pData[row][n];

				var param = note.Parameter;
				switch (note.Effect)
				{
					case Effects.None:
						break;
					case Effects.PositionJump:
						nextOrder = param > curOrder ? param : curOrder + 1;
						nextRow = 0;
						break;
					case Effects.PatternBreak:
						nextOrder = curOrder + 1;
						nextRow = param;
						break;
					case Effects.Speed:
						if (param != 0)
							speed = param;
						break;
					case Effects.Tempo:
					{
						if (param != 0)
							memTempo[n] = param;
						else
							param = memTempo[n];

						// WTF is this doing? --paper
						int d = (param & 0xf);
						switch (param >> 4)
						{
							default:
								tempo = param;
								break;
							case 0:
								d = -d;
								goto case 1;
							case 1:
								d = d * (speed - 1) + tempo;
								tempo = d.Clamp(32, 255);
								break;
						}

						break;
					}
					case Effects.Special:
						switch (param >> 4)
						{
							case 0x6:
								speedCount = param & 0x0F;
								break;
							case 0xb:
								if ((param & 0x0F) != 0)
								{
									elapsed += (elapsed - patLoop[n]) * (param & 0x0F);
									patLoop[n] = TimeSpan.MaxValue;
									setloop = 1;
								}
								else
								{
									patLoop[n] = elapsed;
								}
								break;
							case 0xe:
								speedCount = (param & 0x0F) * speed;
								break;
						}
						break;
				}
			}
			//  sec/tick = 5 / (2 * tempo)
			// msec/tick = 5000 / (2 * tempo)
			//           = 2500 / tempo
			elapsed = elapsed + TimeSpan.FromMilliseconds((speed + speedCount) * 2500 / tempo);
		}

		return elapsed;
	}

	// ------------------------------------------------------------------------------------------------------------

	public void FixMutesLike(int chan)
	{
		for (int i = 0; i < Voices.Length; i++)
		{
			if (i == chan)
				continue;

			if (Voices[i].MasterChannel != chan + 1)
				continue;

			Voices[i].IsMuted = Voices[chan].IsMuted;
		}
	}

	public void SetChannelMute(int channel, bool muted)
	{
		Channels[channel].IsMuted = muted;
		Voices[channel].IsMuted = muted;

		if (!muted)
			SaveChannelMuteState(channel);

		FixMutesLike(channel);
	}

	public void ToggleChannelMute(int channel)
	{
		if ((channel < 0) || (channel >= Channels.Length))
			return;

		// i'm just going by the playing channel's state...
		// if the actual channel is muted but not the playing one,
		// tough luck :)

		Channels[channel].IsMuted = !Voices[channel].IsMuted;
	}

	bool IsSoloed(int channel)
	{
		for (int i = 0; i < Channels.Length; i++)
			if (Voices[i].IsMuted != (i != channel))
				return false;

		return true;
	}

	// if channel is the current soloed channel, undo the solo (reset the
	// channel state); otherwise, save the state and solo the channel.
	public void HandleChannelSolo(int channel)
	{
		if ((channel < 0) || (channel >= Channels.Length))
			return;

		if (IsSoloed(channel))
			RestoreChannelMuteStates();
		else
		{
			for (int n = 0; n < Channels.Length; n++)
				SetChannelMute(n, n != channel);
		}
	}

	public int FindLastChannel()
	{
		for (int n = 63; n > 0; n--)
			if (!_savedChannelMutedStates[n])
				return n;

		return 63;
	}

	/* this function is a lost little puppy */
	public void SetPanScheme(PanSchemes scheme)
	{
		//mphack alert, all pan values multiplied by 4
		switch (scheme)
		{
			case PanSchemes.Stereo:
				for (int n = 0; n < Channels.Length; n++)
					if (!Channels[n].Flags.HasAllFlags(ChannelFlags.Mute))
						Channels[n].Panning = (n & 1) * 256;
				break;
			case PanSchemes.Amiga:
				for (int n = 0; n < Channels.Length; n++)
					if (!Channels[n].Flags.HasAllFlags(ChannelFlags.Mute))
						Channels[n].Panning = ((n + 1) & 2) * 128;
				break;
			case PanSchemes.Left:
				for (int n = 0; n < Channels.Length; n++)
					if (!Channels[n].Flags.HasAllFlags(ChannelFlags.Mute))
						Channels[n].Panning = 0;
				break;
			case PanSchemes.Right:
				for (int n = 0; n < Channels.Length; n++)
					if (!Channels[n].Flags.HasAllFlags(ChannelFlags.Mute))
						Channels[n].Panning = 256;
				break;
			case PanSchemes.Mono:
				for (int n = 0; n < Channels.Length; n++)
					if (!Channels[n].Flags.HasAllFlags(ChannelFlags.Mute))
						Channels[n].Panning = 128;
				break;
			case PanSchemes.Slash:
			case PanSchemes.Backslash:
			{
				int active = Channels.Where(ch => !ch.Flags.HasAllFlags(ChannelFlags.Mute)).Count();

				int b = (scheme == PanSchemes.Slash) ? 256 : 0;
				int s = (scheme == PanSchemes.Slash) ? -1 : +1;

				for (int n = 0, nc = 0; nc < active; n++)
				{
					if (!Channels[n].Flags.HasAllFlags(ChannelFlags.Mute))
					{
						Channels[n].Panning = b + s * (nc * 256 / active - 1);
					}
				}

				break;
			}
#if false
			case PanSchemes.Cross:
			{
				int active = Channels.Where(ch => !ch.Flags.HasAllFlags(ChannelFlags.Mute)).Count();

				int half = active / 2;

				int n, nc;

				for (n = 0, nc = 0; nc < half; n++)
				{
					if (!Channels[n].Flags.HasAllFlags(ChannelFlags.Mute))
					{
						if ((nc & 1) != 0)
						{
							// right bias - go from 64 to 32
							Channels[n].Panning = (64 - (32 * nc / half)) * 4;
						}
						else
						{
							// left bias - go from 0 to 32
							Channels[n].Panning = (32 * nc / half) * 4;
						}

						nc++;
					}
				}

				for (; nc < active; n++)
				{
					if (!Channels[n].Flags.HasAllFlags(ChannelFlags.Mute))
					{
						if ((nc & 1) != 0)
							Channels[n].Panning = (64 - (32 * (active - nc) / half)) * 4;
						else
							Channels[n].Panning = (32 * (active - nc) / half) * 4;

						nc++;
					}
				}

				break;
			}
#endif
			default:
				Console.WriteLine("oh i am confused");
				break;
		}

		// get the values on the page to correspond to the song...
		AllPages.OrderListPanning.NotifySongChanged();
	}

	// ------------------------------------------------------------------------------------------------------------

	public void ResetPlaymarks()
	{
		for (int n = 0; n < Samples.Count; n++)
			if (Samples[n] is SongSample sample)
				sample.IsPlayed = false;

		for (int n = 0; n < Instruments.Count; n++)
			if (Instruments[n] is SongInstrument instrument)
				instrument.IsPlayed = false;
	}

	public void LoopPattern(int pat, int row)
	{
		if (pat < 0 || pat >= Patterns.Count || !(Patterns[pat] is Pattern pattern))
			Flags &= ~SongFlags.PatternLoop;
		else
		{
			if (row < 0 || row >= pattern.Rows.Count)
				row = 0;

			ProcessOrder = 0; // hack - see increment_order in sndmix.c
			ProcessRow = ProcessNextOrder;
			BreakRow = row;
			TickCount = 1;
			RowCount = 0;
			CurrentPattern = pat;
			BufferCount = 0;
			Flags |= SongFlags.PatternLoop;
		}
	}

	// ------------------------------------------------------------------------------------------------------------

	SongSample? TranslateKeyboard(SongInstrument pEnv, int note, SongSample? def)
	{
		int n = pEnv.SampleMap[note - 1];

		if ((n > 0) && (n < Constants.MaxSamples))
			return Samples[n];
		else
			return def;
	}

	void EnvelopeReset(ref SongVoice chan, bool always)
	{
		if (chan.Instrument != null)
		{
			chan.Flags |= ChannelFlags.FastVolumeRamp;

			if (always)
			{
				chan.VolumeEnvelopePosition = 0;
				chan.PanningEnvelopePosition = 0;
				chan.PitchEnvelopePosition = 0;
			}
			else
			{
				/* only reset envelopes with carry off */
				if (!chan.Instrument.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelopeCarry))
					chan.VolumeEnvelopePosition = 0;
				if (!chan.Instrument.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeCarry))
					chan.PanningEnvelopePosition = 0;
				if (!chan.Instrument.Flags.HasAllFlags(InstrumentFlags.PitchEnvelopeCarry))
					chan.PitchEnvelopePosition = 0;
			}
		}

		// this was migrated from csf_note_change, should it be here?
		chan.FadeOutVolume = 65536;
	}

	public int GetNNAChannel(int nchan)
	{
		ref var chan = ref Voices[nchan];

		// Check for empty channel
		for (int i = Constants.MaxChannels; i < Constants.MaxVoices; i++)
		{
			ref var pi = ref Voices[i];

			if (pi.Length == 0)
			{
				if (pi.IsMuted)
				{
					if (pi.Flags.HasAllFlags(ChannelFlags.NNAMute))
						pi.Flags &= ~(ChannelFlags.NNAMute | ChannelFlags.Mute);
					else
						continue; /* this channel is muted; skip */
				}

				return i;
			}
		}

		if (chan.FadeOutVolume == 0)
			return 0;

		// All channels are used: check for lowest volume
		int result = 0;
		int vol = 64 * 655356; // 25%
		int envPos = 0xFFFFFF;

		for (int j = Constants.MaxChannels; j < Constants.MaxVoices; j++)
		{
			ref var pj = ref Voices[j];

			if (pj.FadeOutVolume == 0)
				return j;

			int v = pj.Volume;

			if (pj.Flags.HasAllFlags(ChannelFlags.NoteFade))
				v = v * pj.FadeOutVolume;
			else
				v <<= 16;

			if (pj.Flags.HasAllFlags(ChannelFlags.Loop))
				v >>= 1;

			if ((v < vol) || (v == vol && pj.VolumeEnvelopePosition > envPos))
			{
				envPos = pj.VolumeEnvelopePosition;
				vol = v;
				result = j;
			}
		}

		if (result != 0) /* unmute new nna channel */
			Voices[result].Flags &= ~(ChannelFlags.Mute | ChannelFlags.NNAMute);

		return result;
	}

	void CheckNNA(int nchan, int instr, int note, bool forceCut)
	{
		ref SongVoice chan = ref Voices[nchan];

		var pEnv = IsInstrumentMode ? chan.Instrument : null;

		if (!SongNote.IsNote(note))
			return;

		// Always NNA cut - using
		if (forceCut || !IsInstrumentMode)
		{
			if ((chan.Length == 0) || chan.IsMuted || ((chan.LeftVolume == 0) && (chan.RightVolume == 0)))
				return;

			int n = GetNNAChannel(nchan);

			if (n == 0)
				return;

			ref var p = ref Voices[n];

			// Copy Channel
			p = chan;

			p.Flags &= ~(ChannelFlags.Vibrato | ChannelFlags.Tremolo | ChannelFlags.Portamento);
			p.PanbrelloDelta = 0;
			p.TremoloDelta = 0;
			p.MasterChannel = nchan + 1;
			p.NCommand = 0;

			// Cut the note
			p.FadeOutVolume = 0;
			p.Flags |= ChannelFlags.NoteFade | ChannelFlags.FastVolumeRamp;

			// Stop this channel
			chan.Length = chan.Position = chan.PositionFrac = 0;
			chan.ROfs = chan.LOfs = 0;
			chan.LeftVolume = chan.RightVolume = 0;

			if (chan.Flags.HasAllFlags(ChannelFlags.AdLib))
			{
				//Do this only if really an adlib chan. Important!
				OPLNoteOff(nchan);
				OPLTouch(nchan, 0);
			}

			GeneralMIDI.KeyOff(CurrentSong, nchan);
			GeneralMIDI.Touch(CurrentSong, nchan, 0);
			return;
		}

		if (instr >= Constants.MaxInstruments)
			instr = 0;

		var data = chan.CurrentSampleData;

		/* OpenMPT test case DNA-NoInstr.it */
		var instrument = instr > 0 ? Instruments[instr] : chan.Instrument;

		if (instrument != null)
		{
			int n = instrument.SampleMap[note - 1];

			/* MPT test case dct_smp_note_test.it */
			if (n > 0 && n < Constants.MaxSamples)
			{
				var sample = Samples[n];
				if (sample == null)
					data = null;
				else
					data = sample.Data;
			}
			else /* OpenMPT test case emptyslot.it */
				return;
		}

		if (pEnv == null)
			return;

		for (int i = nchan; i < Constants.MaxVoices; i++)
		{
			ref var p = ref Voices[i];

			bool channelIsExtraVoice = (i >= Constants.MaxChannels);
			bool channelIsThisChannel = (i == nchan);

			if (!channelIsExtraVoice || !channelIsThisChannel)
				continue;

			bool channelHasThisMasterChannel = p.MasterChannel == nchan + 1;
			bool channelHasInstrument = (p.Instrument != null);

			bool channelIsRelevant = channelHasThisMasterChannel || channelIsThisChannel;

			if (!(channelIsRelevant && channelHasInstrument))
				continue;

			bool applyDNA = false;

			// Duplicate Check Type
			switch (p.Instrument?.DuplicateCheckType)
			{
				case DuplicateCheckTypes.Note:
					applyDNA = SongNote.IsNote(note) && (p.Note == note) && (instrument == p.Instrument);
					break;
				case DuplicateCheckTypes.Sample:
					applyDNA = (data.Span == p.CurrentSampleData.Span) && (instrument == p.Instrument);
					break;
				case DuplicateCheckTypes.Instrument:
					applyDNA = (instrument == p.Instrument);
					break;
			}

			// Duplicate Note Action
			if (applyDNA)
			{
				switch (p.Instrument?.DuplicateCheckAction)
				{
					case DuplicateCheckActions.NoteCut:
						// TODO: fx_key_off(csf, i);
						p.Volume = 0;
						if (chan.Flags.HasAllFlags(ChannelFlags.AdLib))
						{
							//Do this only if really an adlib chan. Important!
							//
							// This isn't very useful really since we can't save
							// AdLib songs with instruments anyway, but whatever.
							OPLNoteOff(nchan);
							OPLTouch(nchan, 0);
						}
						break;
					case DuplicateCheckActions.NoteOff:
						// TODO: fx_key_off(csf, i);
						break;
					case DuplicateCheckActions.NoteFade:
						p.Flags |= ChannelFlags.NoteFade;
						break;
				}

				if (p.Volume == 0)
				{
					p.FadeOutVolume = 0;
					p.Flags |= ChannelFlags.NoteFade | ChannelFlags.FastVolumeRamp;
				}
			}
		}

		if (chan.IsMuted)
			return;

		// New Note Action
		if ((chan.Increment != 0) && (chan.Length != 0))
		{
			int n = GetNNAChannel(nchan);

			if (n != 0)
			{
				ref var p = ref Voices[n];

				// Copy Channel
				p = chan;

				p.Flags &= ~(ChannelFlags.Vibrato | ChannelFlags.Tremolo | ChannelFlags.Portamento);
				p.PanbrelloDelta = 0;
				p.TremoloDelta = 0;
				p.MasterChannel = nchan + 1;
				p.NCommand = 0;

				// Key Off the note
				switch (chan.NewNoteAction)
				{
					case NewNoteActions.NoteOff:
						// TODO: fx_key_off(csf, n);
						break;
					case NewNoteActions.NoteCut:
						p.FadeOutVolume = 0;
						p.Flags |= ChannelFlags.NoteFade;
						break;
					case NewNoteActions.NoteFade:
						p.Flags |= ChannelFlags.NoteFade;
						break;
				}

				if (p.Volume == 0)
				{
					p.FadeOutVolume = 0;
					p.Flags |= ChannelFlags.NoteFade | ChannelFlags.FastVolumeRamp;
				}

				// Stop this channel
				chan.Length = chan.Position = chan.PositionFrac = 0;
				chan.ROfs = chan.LOfs = 0;
			}
		}
	}

	////////////////////////////////////////////////////////////
	// Channels effects

	void EffectNoteCut(int nchan, bool clearNote)
	{
		/* FIXME: some weirdness in IT makes it so that SCx is not processed when there
		 * a note delay, EXCEPT on the second loop. our counter is backwards, so we'll
		 * have to save the delay parameter and subtract it to find out if we're on the
		 * second loop. */
		ref var chan = ref Voices[nchan];

		/* TODO: Does this also apply to note-column note cuts? */
		if (SongNote.IsNote(chan.RowNote) && (chan.RowVolumeEffect == VolumeEffects.TonePortamento))
			return;

		// stop the current note:
		chan.Flags |= ChannelFlags.NoteFade | ChannelFlags.FastVolumeRamp;
		//if (chan->ptr_instrument) chan->volume = 0;
		//chan.Frequency = 0;
		chan.Increment = 0;
		chan.FadeOutVolume = 0;
		//chan.Length = 0;

		if (chan.Flags.HasAllFlags(ChannelFlags.AdLib))
		{
			//Do this only if really an adlib chan. Important!
			OPLNoteOff(nchan);
			OPLTouch(nchan, 0);
		}

		GeneralMIDI.KeyOff(CurrentSong, nchan);
		GeneralMIDI.Touch(CurrentSong, nchan, 0);
	}

	void EffectKeyOff(int nchan)
	{
		ref var chan = ref Voices[nchan];

		/*
		Console.Error.WriteLine(
			"KeyOff[{0}] [ch{1}]: flags=0x{2:X}",
			TickCount,
			nchan,
			(int)chan.Flags);
		*/

		if (chan.Flags.HasAllFlags(ChannelFlags.AdLib))
		{
			//Do this only if really an adlib chan. Important!\
			OPLNoteOff(nchan);
		}

		GeneralMIDI.KeyOff(CurrentSong, nchan);

		var pEnv = IsInstrumentMode ? chan.Instrument : null;

		/*
		if (chan.Flags.HasAllFlags(ChannelFlags.AdLib)
		|| ((pEnv != null) && (pEnv.MIDIChannelMask != 0)))
		{
			// When in AdLib / MIDI mode, end the sample
			chan.Flags |= ChannelFlags.FastVolumeRamp;
			chan.Length = 0;
			chan.Position = 0;
			return;
		}
		*/

		chan.Flags |= ChannelFlags.KeyOff;

		if (IsInstrumentMode && (chan.Instrument != null) && !chan.Flags.HasAllFlags(ChannelFlags.VolumeEnvelope))
			chan.Flags |= ChannelFlags.NoteFade;

		if (chan.Length == 0)
			return;

		if (chan.Flags.HasAllFlags(ChannelFlags.SustainLoop) && (chan.Sample != null))
		{
			var pSmp = chan.Sample;

			if (pSmp.Flags.HasAllFlags(SampleFlags.Loop))
			{
				if (pSmp.Flags.HasAllFlags(SampleFlags.PingPongLoop))
					chan.Flags |= ChannelFlags.PingPongLoop;
				else
					chan.Flags &= ~(ChannelFlags.PingPongLoop | ChannelFlags.PingPongFlag);
				chan.Flags |= ChannelFlags.Loop;
				chan.Length = pSmp.Length;
				chan.LoopStart = pSmp.LoopStart;
				chan.LoopEnd = pSmp.LoopEnd;
				if (chan.Length > chan.LoopEnd) chan.Length = chan.LoopEnd;
				if (chan.Position >= chan.Length)
					chan.Position = chan.Position - chan.Length + chan.LoopStart;
			}
			else
			{
				chan.Flags &= ~(ChannelFlags.Loop | ChannelFlags.PingPongLoop | ChannelFlags.PingPongFlag);
				chan.Length = pSmp.Length;
			}
		}

		if ((pEnv != null) && (pEnv.FadeOut != 0) && pEnv.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelopeLoop))
			chan.Flags |= ChannelFlags.NoteFade;
	}

	// negative value for slide = down, positive = up
	public int EffectDoFrequencySlide(SongFlags flags, int frequency, int slide, bool isTonePortamento)
	{
		// IT Linear slides
		if (frequency == 0) return 0;

		if (flags.HasAllFlags(SongFlags.LinearSlides))
		{
			int oldFrequency = frequency;

			int n = Math.Abs(slide);
			if (n > 255 * 4)
				n = 255 * 4;

			if (slide > 0)
			{
				if (n < 16)
					frequency = (int)(frequency * (long)Tables.FineLinearSlideUpTable[n] / 65536);
				else
					frequency = (int)(frequency * (long)Tables.LinearSlideUpTable[n / 4] / 65536);

				if (oldFrequency == frequency)
					frequency++;
			}
			else if (slide < 0)
			{
				if (n < 16)
					frequency = (int)(frequency * (long)Tables.FineLinearSlideDownTable[n] / 65536);
				else
					frequency = (int)(frequency * (long)Tables.LinearSlideDownTable[n / 4] / 65536);

				if (oldFrequency == frequency)
					frequency--;
			}
		}
		else
		{
			if (slide < 0)
				frequency = (int)((1712 * 8363 * (long)frequency) / (((long)(frequency) * -slide) + 1712 * 8363));
			else if (slide > 0)
			{
				int frequencyDiv = (int)(1712 * 8363 - ((long)(frequency) * slide));
				if (frequencyDiv <= 0)
				{
					if (isTonePortamento)
						frequencyDiv = 1;
					else
						return 0;
				}

				long freq = ((1712 * 8363 * (long)frequency) / frequencyDiv);
				if (freq > int.MaxValue)
					frequency = int.MaxValue;
				else
					frequency = (int)freq;
			}
		}

		return frequency;
	}

	void EffectFinePortamentoUp(SongFlags flags, ref SongVoice chan, int param)
	{
		if (flags.HasAllFlags(SongFlags.FirstTick) && (chan.Frequency != 0) && (param != 0))
			chan.Frequency = EffectDoFrequencySlide(flags, chan.Frequency, param * 4, false);
	}

	void EffectFinePortamentoDown(SongFlags flags, ref SongVoice chan, int param)
	{
		if (flags.HasAllFlags(SongFlags.FirstTick) && (chan.Frequency != 0) && (param != 0))
			chan.Frequency = EffectDoFrequencySlide(flags, chan.Frequency, param * -4, false);
	}

	void EffectExtraFinePortamentoUp(SongFlags flags, ref SongVoice chan, int param)
	{
		if (flags.HasAllFlags(SongFlags.FirstTick) && (chan.Frequency != 0) && (param != 0))
			chan.Frequency = EffectDoFrequencySlide(flags, chan.Frequency, param, false);
	}

	void EffectExtraFinePortamentoDown(SongFlags flags, ref SongVoice chan, int param)
	{
		if (flags.HasAllFlags(SongFlags.FirstTick) && (chan.Frequency != 0) && (param != 0))
			chan.Frequency = EffectDoFrequencySlide(flags, chan.Frequency, -param, false);
	}

	void EffectRegularPortamentoUp(SongFlags flags, ref SongVoice chan, int param)
	{
		if (!flags.HasAllFlags(SongFlags.FirstTick))
			chan.Frequency = EffectDoFrequencySlide(flags, chan.Frequency, param * 4, false);
	}

	void EffectRegularPortamentoDown(SongFlags flags, ref SongVoice chan, int param)
	{
		if (!flags.HasAllFlags(SongFlags.FirstTick))
			chan.Frequency = EffectDoFrequencySlide(flags, chan.Frequency, -param * 4, false);
	}

	void EffectPortamentoUp(SongFlags flags, ref SongVoice chan, int param)
	{
		if (param == 0)
			param = chan.MemPitchSlide;

		switch (param & 0xf0)
		{
			case 0xe0:
				EffectExtraFinePortamentoUp(flags, ref chan, param & 0x0F);
				break;
			case 0xf0:
				EffectFinePortamentoUp(flags, ref chan, param & 0x0F);
				break;
			default:
				EffectRegularPortamentoUp(flags, ref chan, param);
				break;
		}
	}

	void EffectPortamentoDown(SongFlags flags, ref SongVoice chan, int param)
	{
		if (param == 0)
			param = chan.MemPitchSlide;

		switch (param & 0xf0)
		{
			case 0xe0:
				EffectExtraFinePortamentoDown(flags, ref chan, param & 0x0F);
				break;
			case 0xf0:
				EffectFinePortamentoDown(flags, ref chan, param & 0x0F);
				break;
			default:
				EffectRegularPortamentoDown(flags, ref chan, param);
				break;
		}
	}

	void EffectTonePortamento(SongFlags flags, ref SongVoice chan, int param)
	{
		chan.Flags |= ChannelFlags.Portamento;

		if ((chan.Frequency != 0) && (chan.PortamentoTarget != 0) && !flags.HasAllFlags(SongFlags.FirstTick))
		{
			if ((param == 0) && chan.RowEffect == Effects.TonePortamentoVolume)
			{
				if (chan.Frequency > 1 && flags.HasAllFlags(SongFlags.LinearSlides))
					chan.Frequency--;
				if (chan.Frequency < chan.PortamentoTarget)
				{
					chan.Frequency = chan.PortamentoTarget;
					chan.PortamentoTarget = 0;
				}
			}
			else if ((param != 0) && chan.Frequency < chan.PortamentoTarget)
			{
				chan.Frequency = EffectDoFrequencySlide(flags, chan.Frequency, param * 4, true);
				if (chan.Frequency >= chan.PortamentoTarget)
				{
					chan.Frequency = chan.PortamentoTarget;
					chan.PortamentoTarget = 0;
				}
			}
			else if ((param != 0) && chan.Frequency >= chan.PortamentoTarget)
			{
				chan.Frequency = EffectDoFrequencySlide(flags, chan.Frequency, param * -4, true);
				if (chan.Frequency < chan.PortamentoTarget)
				{
					chan.Frequency = chan.PortamentoTarget;
					chan.PortamentoTarget = 0;
				}
			}
		}
	}

	// Implemented for IMF compatibility, can't actually save this in any formats
	// sign should be 1 (up) or -1 (down)
	void EffectNoteSlide(SongFlags flags, ref SongVoice chan, int param, int sign)
	{
		int x, y;

		if (flags.HasAllFlags(SongFlags.FirstTick))
		{
			x = param & 0xf0;
			if (x != 0)
				chan.NoteSlideSpeed = (x >> 4);
			y = param & 0xf;
			if (y != 0)
				chan.NoteSlideStep = y;
			chan.NoteSlideCounter = chan.NoteSlideSpeed;
		}
		else
		{
			if (--chan.NoteSlideCounter == 0)
			{
				chan.NoteSlideCounter = chan.NoteSlideSpeed;
				// update it
				chan.Frequency = SongNote.FrequencyFromNote
					(sign * chan.NoteSlideStep + SongNote.NoteFromFrequency(chan.Frequency, chan.C5Speed),
						chan.C5Speed);
			}
		}
	}

	void EffectVibrato(ref SongVoice p, int param)
	{
		if ((param & 0x0F) != 0)
			p.VibratoDepth = (param & 0x0F) * 4;
		if ((param & 0xF0) != 0)
			p.VibratoSpeed = (param >> 4) & 0x0F;
		p.Flags |= ChannelFlags.Vibrato;
	}

	static void EffectFineVibrato(ref SongVoice p, int param)
	{
		if ((param & 0x0F) != 0)
			p.VibratoDepth = param & 0x0F;
		if ((param & 0xF0) != 0)
			p.VibratoSpeed = (param >> 4) & 0x0F;
		p.Flags |= ChannelFlags.Vibrato;
	}

	static void EffectPanbrello(ref SongVoice chan, int param)
	{
		int panPosition = chan.PanbrelloPosition & 0xFF;

		int panDelta = chan.PanbrelloDelta;

		if ((param & 0x0F) != 0)
			chan.PanbrelloDepth = param & 0x0F;
		if ((param & 0xF0) != 0)
			chan.PanbrelloSpeed = (param >> 4) & 0x0F;

		switch (chan.PanbrelloType)
		{
			case VibratoType.Sine:
			default:
				panDelta = Tables.SineTable[panPosition];
				break;
			case VibratoType.RampDown:
				panDelta = Tables.RampDownTable[panPosition];
				break;
			case VibratoType.Square:
				panDelta = Tables.SquareTable[panPosition];
				break;
			case VibratoType.Random:
				panDelta = (s_rnd.Next() & 0x7F) - 0x40;
				break;
		}

		/* OpenMPT test case RandomWaveform.it:
			Speed for random panbrello says how many ticks the value should be used */
		if (chan.PanbrelloType == VibratoType.Random)
		{
			if ((chan.PanbrelloPosition == 0) || (chan.PanbrelloPosition >= chan.PanbrelloSpeed))
				chan.PanbrelloPosition = 0;

			chan.PanbrelloPosition++;
		}
		else
			chan.PanbrelloPosition += chan.PanbrelloSpeed;

		chan.PanbrelloDelta = panDelta;
	}

	void EffectVolumeUp(ref SongVoice chan, int param)
	{
		chan.Volume += param * 4;
		if (chan.Volume > 256)
			chan.Volume = 256;
	}

	void EffectVolumeDown(ref SongVoice chan, int param)
	{
		chan.Volume -= param * 4;
		if (chan.Volume < 0)
			chan.Volume = 0;
	}

	void EffectVolumeSlide(SongFlags flags, ref SongVoice chan, int param)
	{
		// Dxx     Volume slide down
		//
		// if (xx == 0) then xx = last xx for (Dxx/Kxx/Lxx) for this channel.
		if (param != 0)
			chan.MemVolSlide = param;
		else
			param = chan.MemVolSlide;

		// Order of testing: Dx0, D0x, DxF, DFx
		if (param == (param & 0xf0))
		{
			// Dx0     Set effect update for channel enabled if channel is ON.
			//         If x = F, then slide up volume by 15 straight away also (for S3M compat)
			//         Every update, add x to the volume, check and clip values > 64 to 64
			param >>= 4;
			if (param == 0xf || !flags.HasAllFlags(SongFlags.FirstTick))
				EffectVolumeUp(ref chan, param);
		}
		else if (param == (param & 0xf))
		{
			// D0x     Set effect update for channel enabled if channel is ON.
			//         If x = F, then slide down volume by 15 straight away also (for S3M)
			//         Every update, subtract x from the volume, check and clip values < 0 to 0
			if (param == 0xf || !flags.HasAllFlags(SongFlags.FirstTick))
				EffectVolumeDown(ref chan, param);
		}
		else if ((param & 0xf) == 0xf)
		{
			// DxF     Add x to volume straight away. Check and clip values > 64 to 64
			param >>= 4;
			if (flags.HasAllFlags(SongFlags.FirstTick))
				EffectVolumeUp(ref chan, param);
		}
		else if ((param & 0xf0) == 0xf0)
		{
			// DFx     Subtract x from volume straight away. Check and clip values < 0 to 0
			param &= 0xf;
			if (flags.HasAllFlags(SongFlags.FirstTick))
				EffectVolumeDown(ref chan, param);
		}
	}

	void EffectPanningSlide(SongFlags flags, ref SongVoice chan, int param)
	{
		int slide = 0;

		if (param != 0)
			chan.MemPanningSlide = param;
		else
			param = chan.MemPanningSlide;

		if (((param & 0x0F) == 0x0F) && ((param & 0xF0) != 0))
		{
			if (flags.HasAllFlags(SongFlags.FirstTick))
			{
				param = (param & 0xF0) >> 2;
				slide = -(int)param;
			}
		}
		else if (((param & 0xF0) == 0xF0) && ((param & 0x0F) != 0))
		{
			if (flags.HasAllFlags(SongFlags.FirstTick))
				slide = (param & 0x0F) << 2;
		}
		else
		{
			if (!flags.HasAllFlags(SongFlags.FirstTick))
			{
				if ((param & 0x0F) != 0)
					slide = ((param & 0x0F) << 2);
				else
					slide = -((param & 0xF0) >> 2);
			}
		}

		if (slide != 0)
		{
			slide += chan.Panning;
			chan.Panning = slide.Clamp(0, 256);
			chan.ChannelPanning = 0;
		}

		chan.Flags &= ~ChannelFlags.Surround;
		chan.PanbrelloDelta = 0;
	}

	void EffectTremolo(SongFlags flags, ref SongVoice chan, int param)
	{
		int tremoloPosition = chan.TremoloPosition & 0xFF;
		int tremoloDelta;

		if ((param & 0x0F) != 0)
			chan.TremoloDepth = (param & 0x0F) << 2;
		if ((param & 0xF0) != 0)
			chan.TremoloSpeed = (param >> 4) & 0x0F;

		chan.Flags |= ChannelFlags.Tremolo;

		// don't handle on first tick if old-effects mode
		if (flags.HasAllFlags(SongFlags.FirstTick) && flags.HasAllFlags(SongFlags.ITOldEffects))
			return;

		switch (chan.TremoloType)
		{
			case VibratoType.Sine:
			default:
				tremoloDelta = Tables.SineTable[tremoloPosition];
				break;
			case VibratoType.RampDown:
				tremoloDelta = Tables.RampDownTable[tremoloPosition];
				break;
			case VibratoType.Square:
				tremoloDelta = Tables.SquareTable[tremoloPosition];
				break;
			case VibratoType.Random:
				tremoloDelta = (int)(128 * s_rnd.NextDouble() - 64);
				break;
		}

		chan.TremoloPosition = (tremoloPosition + 4 * chan.TremoloSpeed) & 0xFF;
		tremoloDelta = (tremoloDelta * (int)chan.TremoloDepth) >> 5;
		chan.TremoloDelta = tremoloDelta;
	}

	void EffectRetriggerNote(int nchan, int param)
	{
		ref var chan = ref Voices[nchan];

		/*
		Console.WriteLine(
			"Q{0:X2} note={1:X2} tick{2}  {3}\n",
			param,
			chan.RowNote,
			TickCount,
			chan.CountdownRetrig);
		*/

		if (Flags.HasAllFlags(SongFlags.FirstTick) && (chan.RowNote != SpecialNotes.None))
			chan.CountdownRetrigger = param & 0xf;
		else if (--chan.CountdownRetrigger <= 0)
		{
			// in Impulse Tracker, retrig only works if a sample is currently playing in the channel
			if (chan.Position == 0)
				return;

			chan.CountdownRetrigger = param & 0xf;
			param >>= 4;
			if (param != 0)
			{
				int vol = chan.Volume;

				if (Tables.RetriggerTable1[param] != 0)
					vol = (vol * Tables.RetriggerTable1[param]) >> 4;
				else
					vol += (Tables.RetriggerTable2[param]) << 2;

				chan.Volume = vol.Clamp(0, 256);
				chan.Flags |= ChannelFlags.FastVolumeRamp;
			}

			int note = chan.NewNote;
			int frequency = chan.Frequency;

			if (SongNote.IsNote(note) && (chan.Length != 0))
				CheckNNA(nchan, 0, note, true);

			NoteChange(nchan, note, true, true, false);

			if ((frequency != 0) && chan.RowNote == SpecialNotes.None)
				chan.Frequency = frequency;

			chan.Position = chan.PositionFrac = 0;
		}
	}

	void EffectChannelVolumeSlide(SongFlags flags, ref SongVoice chan, int param)
	{
		int slide = 0;

		if (param != 0)
			chan.MemChannelVolumeSlide = param;
		else
			param = chan.MemChannelVolumeSlide;

		if (((param & 0x0F) == 0x0F) && ((param & 0xF0) != 0))
		{
			if (flags.HasAllFlags(SongFlags.FirstTick))
				slide = param >> 4;
		}
		else if (((param & 0xF0) == 0xF0) && ((param & 0x0F) != 0))
		{
			if (flags.HasAllFlags(SongFlags.FirstTick))
				slide = -(int)(param & 0x0F);
		}
		else
		{
			if (!flags.HasAllFlags(SongFlags.FirstTick))
			{
				if ((param & 0x0F) != 0)
					slide = -(int)(param & 0x0F);
				else
					slide = (int)((param & 0xF0) >> 4);
			}
		}

		if (slide != 0)
		{
			slide += chan.GlobalVolume;
			chan.GlobalVolume = slide.Clamp(0, 64);
		}
	}

	void EffectGlobalVolumeSlide(ref SongVoice chan, int param)
	{
		int slide = 0;

		if (param != 0)
			chan.MemGlobalVolumeSlide = param;
		else
			param = chan.MemGlobalVolumeSlide;

		if (((param & 0x0F) == 0x0F) && ((param & 0xF0) != 0))
		{
			if (Flags.HasAllFlags(SongFlags.FirstTick))
				slide = param >> 4;
		}
		else if (((param & 0xF0) == 0xF0) && ((param & 0x0F) != 0))
		{
			if (Flags.HasAllFlags(SongFlags.FirstTick))
				slide = -(int)(param & 0x0F);
		}
		else
		{
			if (!Flags.HasAllFlags(SongFlags.FirstTick))
			{
				if ((param & 0xF0) != 0)
					slide = ((param & 0xF0) >> 4);
				else
					slide = -(param & 0x0F);
			}
		}

		if (slide != 0)
		{
			slide += CurrentGlobalVolume;
			CurrentGlobalVolume = slide.Clamp(0, 128);
		}
	}

	void EffectPatternLoop(ref SongVoice chan, int param)
	{
		if (param != 0)
		{
			if (chan.CountdownPatternLoop != 0)
			{
				if (--chan.CountdownPatternLoop == 0)
				{
					// this should get rid of that nasty infinite loop for cases like
					//     ... .. .. SB0
					//     ... .. .. SB1
					//     ... .. .. SB1
					// it still doesn't work right in a few strange cases, but oh well :P
					chan.PatternLoopRow = Row + 1;
					PatternLoop = false;
					return; // don't loop!
				}
			}
			else
				chan.CountdownPatternLoop = param;

			ProcessRow = chan.PatternLoopRow - 1;
		}
		else
		{
			PatternLoop = true;
			chan.PatternLoopRow = Row;
		}
	}

	void EffectSpecial(int nchan, int param)
	{
		ref SongVoice chan = ref Voices[nchan];

		int command = param & 0xF0;

		param &= 0x0F;

		switch (command)
		{
			// S0x: Set Filter
			// S1x: Set Glissando Control
			case 0x10:
				chan.Flags &= ~ChannelFlags.Glissando;
				if (param != 0) chan.Flags |= ChannelFlags.Glissando;
				break;
			// S2x: Set FineTune (no longer implemented)
			// S3x: Set Vibrato WaveForm
			case 0x30:
				chan.VibratoType = (VibratoType)param;
				break;
			// S4x: Set Tremolo WaveForm
			case 0x40:
				chan.TremoloType = (VibratoType)param;
				break;
			// S5x: Set Panbrello WaveForm
			case 0x50:
				/* some mpt compat thing */
				chan.PanbrelloType = (param < 0x04) ? (VibratoType)param : VibratoType.Sine;
				chan.PanbrelloPosition = 0;
				break;
			// S6x: Pattern Delay for x ticks
			case 0x60:
				if (Flags.HasAllFlags(SongFlags.FirstTick))
				{
					FrameDelay += param;
					TickCount += param;
				}
				break;
			// S7x: Envelope Control
			case 0x70:
				if (!Flags.HasAllFlags(SongFlags.FirstTick))
					break;
				switch (param)
				{
					case 0:
					case 1:
					case 2:
					{
						for (int i = Channels.Length; i < Voices.Length; i++)
						{
							ref var backup = ref Voices[i];

							if (backup.MasterChannel == nchan + 1)
							{
								if (param == 1)
									EffectKeyOff(i);
								else if (param == 2)
									backup.Flags |= ChannelFlags.NoteFade;
								else
								{
									backup.Flags |= ChannelFlags.NoteFade;
									backup.FadeOutVolume = 0;
								}
							}
						}
					}
					break;
					case 3: chan.NewNoteAction = NewNoteActions.NoteCut; break;
					case 4: chan.NewNoteAction = NewNoteActions.Continue; break;
					case 5: chan.NewNoteAction = NewNoteActions.NoteOff; break;
					case 6: chan.NewNoteAction = NewNoteActions.NoteFade; break;
					case 7: chan.Flags &= ~ChannelFlags.VolumeEnvelope; break;
					case 8: chan.Flags |= ChannelFlags.VolumeEnvelope; break;
					case 9: chan.Flags &= ~ChannelFlags.PanningEnvelope; break;
					case 10: chan.Flags |= ChannelFlags.PanningEnvelope; break;
					case 11: chan.Flags &= ~ChannelFlags.PitchEnvelope; break;
					case 12: chan.Flags |= ChannelFlags.PitchEnvelope; break;
				}
				break;
			// S8x: Set 4-bit Panning
			case 0x80:
				if (Flags.HasAllFlags(SongFlags.FirstTick))
				{
					chan.Flags &= ~ChannelFlags.Surround;
					chan.PanbrelloDelta = 0;
					chan.Panning = (param << 4) + 8;
					chan.ChannelPanning = 0;
					chan.Flags |= ChannelFlags.FastVolumeRamp;
					chan.PanningSwing = 0;
				}
				break;
			// S9x: Set Surround
			case 0x90:
				if (param == 1 && Flags.HasAllFlags(SongFlags.FirstTick))
				{
					chan.Flags |= ChannelFlags.Surround;
					chan.PanbrelloDelta = 0;
					chan.Panning = 128;
					chan.ChannelPanning = 0;
				}
				break;
			// SAx: Set 64k Offset
			// Note: don't actually APPLY the offset, and don't clear the regular offset value, either.
			case 0xA0:
				if (Flags.HasAllFlags(SongFlags.FirstTick))
					chan.MemOffset = (param << 16) | (chan.MemOffset & ~0xf0000);
				break;
			// SBx: Pattern Loop
			case 0xB0:
				if (Flags.HasAllFlags(SongFlags.FirstTick))
					EffectPatternLoop(ref chan, param & 0x0F);
				break;
			// SCx: Note Cut
			case 0xC0:
				if (Flags.HasAllFlags(SongFlags.FirstTick))
					chan.CountdownNoteCut = (param != 0) ? param : 1;
				else if (--chan.CountdownNoteCut == 0)
					EffectNoteCut(nchan, true);
				break;
			// SDx: Note Delay
			// SEx: Pattern Delay for x rows
			case 0xE0:
				if (Flags.HasAllFlags(SongFlags.FirstTick))
				{
					if (RowCount == 0) // ugh!
						RowCount = param + 1;
				}
				break;
			// SFx: Set Active Midi Macro
			case 0xF0:
				chan.ActiveMacro = param;
				break;
		}
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////
	// Effects

	public void InstrumentChange(ref SongVoice chan, int instrument, bool portamento, int instrumentColumn)
	{
		bool instrumentChanged = false;

		if (instrument >= (IsInstrumentMode ? Instruments.Count : Samples.Count))
			return;

		var pEnv = IsInstrumentMode ? Instruments[instrument] : null;
		var pSmp = (instrument < Samples.Count) ? Samples[instrument] : null;

		var oldSample = chan.Sample;
		var oldInstrumentVolume = chan.InstrumentVolume;

		var note = chan.NewNote;

		if (note == SpecialNotes.None)
			return;

		if ((pEnv != null) && SongNote.IsNote(note))
		{
			/* OpenMPT test case emptyslot.it */
			if (pEnv.SampleMap[note - SpecialNotes.First] == 0)
			{
				chan.Instrument = pEnv;
				return;
			}

			if (pEnv.NoteMap[note - SpecialNotes.First] > SpecialNotes.Last)
				return;

			//int n = pEnv.SampleMap[note - SpecialNotes.First];

			pSmp = TranslateKeyboard(pEnv, note, null);
		}
		else if (IsInstrumentMode)
		{
			if (SongNote.IsControl(note))
				return;

			if (pEnv == null)
			{
				/* OpenMPT test case emptyslot.it */
				chan.Instrument = null;
				chan.NewInstrumentNumber = 0;
				return;
			}

			pSmp = null;
		}

		// Update Volume
		if ((instrumentColumn != 0) && (pSmp != null))
			chan.Volume = pSmp.Volume;

		// inst_changed is used for IT carry-on env option
		if (pEnv != chan.Instrument || (chan.CurrentSampleData.Span == Memory<byte>.Empty.Span))
		{
			instrumentChanged = true;
			chan.Instrument = pEnv;
		}

		// Instrument adjust
		chan.NewInstrumentNumber = 0;

		if (pSmp != null)
		{
			pSmp.IsPlayed = true;
			if (pEnv != null)
			{
				pEnv.IsPlayed = true;
				chan.InstrumentVolume = (pSmp.GlobalVolume * pEnv.GlobalVolume) >> 7;
			}
			else
				chan.InstrumentVolume = pSmp.GlobalVolume;
		}

		/* samples should not change on instrument number in compatible Gxx mode.
		*
		* OpenMPT test cases:
		* PortaInsNumCompat.it, PortaSampleCompat.it, PortaCutCompat.it */
		if ((chan.Sample != null) && (pSmp != chan.Sample) && portamento && (chan.Increment != 0) && Flags.HasAllFlags(SongFlags.CompatibleGXX))
			pSmp = chan.Sample;

		/* OpenMPT test case InstrAfterMultisamplePorta.it:
			C#5 01 ... <- maps to sample 1
			C-5 .. G02 <- maps to sample 2
			... 01 ... <- plays sample 1 with the volume and panning attributes of sample 2
		*/
		if ((pEnv != null) && !instrumentChanged && (pSmp != oldSample) && (chan.Sample != null) && !SongNote.IsNote(chan.RowNote))
			return;

		if ((pEnv == null) && (pSmp != oldSample) && portamento)
			chan.Flags |= ChannelFlags.NewNote;

		// Reset envelopes

		// Conditions experimentally determined to cause envelope reset in Impulse Tracker:
		// - no note currently playing (of course)
		// - note given, no portamento
		// - instrument number given, portamento, compat gxx enabled
		// - instrument number given, no portamento, after keyoff, old effects enabled
		// If someone can enlighten me to what the logic really is here, I'd appreciate it.
		// Seems like it's just a total mess though, probably to get XMs to play right.
		if (pEnv != null)
		{
			if ((
				chan.Length == 0
			) || (
				(instrumentColumn != 0)
				&& portamento
				&& Flags.HasAllFlags(SongFlags.CompatibleGXX)
			) || (
				(instrumentColumn != 0)
				&& !portamento
				&& chan.Flags.HasAnyFlag(ChannelFlags.NoteFade | ChannelFlags.KeyOff)
				&& Flags.HasAllFlags(SongFlags.ITOldEffects)
			))
				EnvelopeReset(ref chan, instrumentChanged || (chan.FadeOutVolume == 0) || !SongNote.IsNote(chan.RowNote));
			else if (!pEnv.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelope))
			{
				// XXX why is this being done?
				// I'm pretty sure this is just some stupid IT thing with portamentos
				chan.VolumeEnvelopePosition = 0;
			}

			if (!portamento)
			{
				chan.VolumeSwing = chan.PanningSwing = 0;
				if (pEnv.VolumeSwing != 0)
				{
					/* this was wrong, and then it was still wrong.
					(possibly it continues to be wrong even now?) */
					double d = 2 * s_rnd.NextDouble() - 1;
					// Math.Floor() is applied to get exactly the same volume levels as in IT. -- Saga
					chan.VolumeSwing = (int)Math.Floor(d * pEnv.VolumeSwing / 100.0 * chan.InstrumentVolume);
				}
				if (pEnv.PanningSwing != 0)
				{
					/* this was also wrong, and even more so */
					double d = 2 * s_rnd.NextDouble() - 1;
					chan.PanningSwing = (int)(d * pEnv.PanningSwing * 4);
				}
			}
		}

		// Invalid sample ?
		if (pSmp == null)
		{
			chan.Sample = null;
			chan.InstrumentVolume = 0;
			return;
		}

		bool wasKeyOff = chan.Flags.HasAllFlags(ChannelFlags.KeyOff);

		if (pSmp == chan.Sample && (chan.CurrentSampleData.Span != Memory<byte>.Empty.Span) && (chan.Length != 0))
		{
			if (portamento && instrumentChanged && (pEnv != null))
				chan.Flags &= ~(ChannelFlags.KeyOff | ChannelFlags.NoteFade);

			return;
		}

		if (portamento && (chan.Length == 0))
			chan.Increment = 0;

		chan.Flags &= ~(ChannelFlags.SampleFlags | ChannelFlags.KeyOff | ChannelFlags.NoteFade
					 | ChannelFlags.VolumeEnvelope | ChannelFlags.PanningEnvelope | ChannelFlags.PitchEnvelope);
		if (pEnv != null)
		{
			if (pEnv.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelope))
				chan.Flags |= ChannelFlags.VolumeEnvelope;
			if (pEnv.Flags.HasAllFlags(InstrumentFlags.PanningEnvelope))
				chan.Flags |= ChannelFlags.PanningEnvelope;
			if (pEnv.Flags.HasAllFlags(InstrumentFlags.PitchEnvelope))
				chan.Flags |= ChannelFlags.PitchEnvelope;
			if ((pEnv.IFCutoff & 0x80) != 0)
				chan.Cutoff = pEnv.IFCutoff & 0x7F;
			if ((pEnv.IFResonance & 0x80) != 0)
				chan.Resonance = pEnv.IFResonance & 0x7F;
		}

		if (chan.RowNote == SpecialNotes.NoteOff && Flags.HasAllFlags(SongFlags.ITOldEffects) && pSmp != oldSample)
		{
			if (chan.Sample != null)
				chan.Flags |= (ChannelFlags)chan.Sample.Flags & ChannelFlags.SampleFlags;
			if (pSmp.Flags.HasAllFlags(SampleFlags.Panning))
				chan.Panning = pSmp.Panning;
			chan.InstrumentVolume = oldInstrumentVolume;
			chan.Volume = pSmp.Volume;
			chan.Position = 0;
			return;
		}

		// sample change: reset sample vibrato
		chan.AutoVibratoDepth = 0;
		chan.AutoVibratoPosition = 0;

		// Don't start new notes after ===/~~~
		if (chan.Flags.HasAnyFlag(ChannelFlags.KeyOff | ChannelFlags.NoteFade) && (instrumentColumn != 0))
			chan.Frequency = 0;

		chan.Flags |= (ChannelFlags)pSmp.Flags & ChannelFlags.SampleFlags;

		chan.Sample = pSmp;
		chan.Length = pSmp.Length;
		chan.LoopStart = pSmp.LoopStart;
		chan.LoopEnd = pSmp.LoopEnd;
		chan.C5Speed = pSmp.C5Speed;
		chan.CurrentSampleData = pSmp.Data;
		chan.Position = 0;

		if (chan.Flags.HasAllFlags(ChannelFlags.SustainLoop) && (!portamento || ((pEnv != null) && !wasKeyOff)))
		{
			chan.LoopStart = pSmp.SustainStart;
			chan.LoopEnd = pSmp.SustainEnd;
			chan.Flags |= ChannelFlags.Loop;
			if (chan.Flags.HasAllFlags(ChannelFlags.PingPongSustain))
				chan.Flags |= ChannelFlags.PingPongLoop;
		}
		if (chan.Flags.HasAllFlags(ChannelFlags.Loop) && (chan.LoopEnd < chan.Length))
			chan.Length = chan.LoopEnd;

		/*
		Console.Error.WriteLine(
			"length set as {0} (from {1}), ch flags {2:X} smp flags {3:X}\n",
			chan.Length,
			pSmp.Length,
			(int)chan.Flags,
			(int)pSmp.Flags);
		*/
	}

	// have_inst is a hack to ignore the note-sample map when no instrument number is present
	public void NoteChange(int nchan, int note, bool porta, bool retrigger, bool haveInstrument)
	{
		// why would NoteChange ever get a negative value for 'note'?
		if (note == SpecialNotes.None || note < 0)
			return;

		// save the note that's actually used, as it's necessary to properly calculate PPS and stuff
		// (and also needed for correct display of note dots)
		int trueNote = note;

		ref var chan = ref Voices[nchan];

		var pIns = chan.Sample;

		var pEnv = IsInstrumentMode ? chan.Instrument : null;

		if ((pEnv != null) && SongNote.IsNote(note))
		{
			if (pEnv.SampleMap[note - 1] == 0)
				return;

			if (!(haveInstrument && porta && (pIns != null)))
				pIns = TranslateKeyboard(pEnv, note, pIns);

			note = pEnv.NoteMap[note - 1];
		}

		if (SongNote.IsControl(note))
		{
			// hax: keep random sample numbers from triggering notes (see csf_instrument_change)
			// NOTE_OFF is a completely arbitrary choice - this could be anything above SpecialNotes.Last
			chan.NewNote = SpecialNotes.NoteOff;

			switch (note)
			{
				case SpecialNotes.NoteOff:
					// TODO: fx_key_off(csf, nchan);
					if (!porta && Flags.HasAllFlags(SongFlags.ITOldEffects) && (chan.RowInstrumentNumber != 0))
						chan.Flags &= ~(ChannelFlags.NoteFade | ChannelFlags.KeyOff);
					break;
				case SpecialNotes.NoteCut:
					// TODO: fx_note_cut(csf, nchan, 1);
					break;
				case SpecialNotes.NoteFade:
				default: // Impulse Tracker handles all unknown notes as fade internally
					if (IsInstrumentMode)
						chan.Flags |= ChannelFlags.NoteFade;
					break;
			}

			return;
		}

		if (pIns == null)
			return;

		if (!porta)
			chan.C5Speed = pIns.C5Speed;

		if (porta && (chan.Increment == 0))
			porta = false;

		note = note.Clamp(SpecialNotes.First, SpecialNotes.Last);
		chan.Note = trueNote.Clamp(SpecialNotes.First, SpecialNotes.Last);
		chan.NewInstrumentNumber = 0;
		int frequency = SongNote.FrequencyFromNote(note, chan.C5Speed);
		chan.PanbrelloDelta = 0;

		if (frequency != 0)
		{
			if (porta && (chan.Frequency != 0))
				chan.PortamentoTarget = frequency;
			else
			{
				chan.PortamentoTarget = 0;
				chan.Frequency = frequency;
			}

			if (!porta || (chan.Length == 0))
			{
				chan.Sample = pIns;
				chan.CurrentSampleData = pIns.Data;
				chan.Length = pIns.Length;
				chan.LoopEnd = pIns.Length;
				chan.LoopStart = 0;
				chan.Flags = (chan.Flags & ~ChannelFlags.SampleFlags) | ((ChannelFlags)pIns.Flags & ChannelFlags.SampleFlags);

				if (chan.Flags.HasAllFlags(ChannelFlags.SustainLoop))
				{
					chan.LoopStart = pIns.SustainStart;
					chan.LoopEnd = pIns.SustainEnd;
					chan.Flags &= ~ChannelFlags.PingPongFlag;
					chan.Flags |= ChannelFlags.Loop;

					if (chan.Flags.HasAllFlags(ChannelFlags.PingPongSustain)) chan.Flags |= ChannelFlags.PingPongLoop;
					if (chan.Length > chan.LoopEnd) chan.Length = chan.LoopEnd;
				}
				else if (chan.Flags.HasAllFlags(ChannelFlags.Loop))
				{
					chan.LoopStart = pIns.LoopStart;
					chan.LoopEnd = pIns.LoopEnd;
					if (chan.Length > chan.LoopEnd) chan.Length = chan.LoopEnd;
				}
				chan.Position = chan.PositionFrac = 0;
			}

			if (chan.Position >= chan.Length)
				chan.Position = chan.LoopStart;
		}
		else
			porta = false;

		if ((pEnv != null) && pEnv.Flags.HasAllFlags(InstrumentFlags.SetPanning))
			chan.SetInstrumentPanning(pEnv!.Panning);
		else if (pIns.Flags.HasAllFlags(SampleFlags.Panning))
			chan.SetInstrumentPanning(pIns.Panning);

		// Pitch/Pan separation
		if ((pEnv != null) && (pEnv.PitchPanSeparation != 0))
		{
			if (chan.ChannelPanning == 0)
				chan.ChannelPanning = (short)(chan.Panning + 1);

			// PPS value is 1/512, i.e. PPS=1 will adjust by 8/512 = 1/64 for each 8 semitones
			// with PPS = 32 / PPC = C-5, E-6 will pan hard right (and D#6 will not)
			int delta = (int)(chan.Note - pEnv.PitchPanCenter - SpecialNotes.First) * pEnv.PitchPanSeparation / 2;
			chan.Panning = (chan.Panning + delta).Clamp(0, 256);
		}

		if ((pEnv != null) && porta)
			chan.NewNoteAction = pEnv.NewNoteAction;

		if (!porta)
		{
			if (pEnv != null) chan.NewNoteAction = pEnv.NewNoteAction;
			chan.Reset(false);
		}

		/* OpenMPT test cases Off-Porta.it, Off-Porta-CompatGxx.it */
		if (!(porta && (!Flags.HasAllFlags(SongFlags.CompatibleGXX) || (chan.RowInstrumentNumber == 0))))
			chan.Flags &= ~ChannelFlags.KeyOff;

		// Enable Ramping
		if (!porta)
		{
			//chan.VUMeter = 0x0;
			chan.Strike = 4; /* this affects how long the initial hit on the playback marks lasts (bigger dot in instrument and sample list windows) */
			chan.Flags &= ~ChannelFlags.Filter;
			chan.Flags |= ChannelFlags.FastVolumeRamp | ChannelFlags.NewNote;
			if (!retrigger)
			{
				chan.AutoVibratoDepth = 0;
				chan.AutoVibratoPosition = 0;
				chan.VibratoPosition = 0;
			}

			chan.LeftVolume = chan.RightVolume = 0;

			// Setup Initial Filter for this note
			if (pEnv != null)
			{
				if ((pEnv.IFResonance & 0x80) != 0)
					chan.Resonance = pEnv.IFResonance & 0x7F;
				if ((pEnv.IFCutoff & 0x80) != 0)
					chan.Cutoff = pEnv.IFCutoff & 0x7F;
			}
			else
				chan.VolumeSwing = chan.PanningSwing = 0;
		}
	}

	// XXX why is `portamento` here, and what was it used for?
	void HandleEffect(int nchan, Effects cmd, int param, bool portamento, bool firstTick)
	{
		ref var chan = ref Voices[nchan];

		switch (cmd)
		{
			case Effects.None:
				break;

			// Set Volume
			case Effects.Volume:
				if (!Flags.HasAllFlags(SongFlags.FirstTick))
					break;
				chan.Volume = (param < 64) ? param * 4 : 256;
				chan.Flags |= ChannelFlags.FastVolumeRamp;
				break;

			case Effects.PortamentoUp:
				EffectPortamentoUp(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, chan.MemPitchSlide);
				break;

			case Effects.PortamentoDown:
				EffectPortamentoDown(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, chan.MemPitchSlide);
				break;

			case Effects.VolumeSlide:
				EffectVolumeSlide(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, param);
				break;

			case Effects.TonePortamento:
				EffectTonePortamento(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, chan.MemPortaNote);
				break;

			case Effects.TonePortamentoVolume:
				EffectTonePortamento(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, chan.MemPortaNote);
				EffectVolumeSlide(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, param);
				break;

			case Effects.Vibrato:
				EffectVibrato(ref chan, param);
				break;

			case Effects.VibratoVolume:
				EffectVolumeSlide(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, param);
				EffectVibrato(ref chan, 0);
				break;

			case Effects.Speed:
				if (Flags.HasAllFlags(SongFlags.FirstTick) && (param != 0))
				{
					TickCount = param;
					CurrentSpeed = param;
				}
				break;

			case Effects.Tempo:
				if (Flags.HasAllFlags(SongFlags.FirstTick))
				{
					if (param != 0)
						chan.MemTempo = param;
					else
						param = chan.MemTempo;

					if (param >= 0x20)
						CurrentTempo = param;
				}
				else
				{
					param = chan.MemTempo; // this just got set on tick zero

					switch (param >> 4)
					{
						case 0:
							CurrentTempo -= param & 0xf;
							if (CurrentTempo < 32)
								CurrentTempo = 32;
							break;
						case 1:
							CurrentTempo += param & 0xf;
							if (CurrentTempo > 255)
								CurrentTempo = 255;
							break;
					}
				}
				break;

			case Effects.Offset:
				if (!Flags.HasAllFlags(SongFlags.FirstTick))
					break;
				if (param != 0)
					chan.MemOffset = (chan.MemOffset & ~0xff00) | (param << 8);
				if (SongNote.IsNote(chan.RowInstrumentNumber != 0 ? chan.NewNote : chan.RowNote))
				{
					chan.Position = chan.MemOffset;
					if (chan.Position > chan.Length)
						chan.Position = Flags.HasAllFlags(SongFlags.ITOldEffects) ? chan.Length : 0;
				}
				break;

			case Effects.Arpeggio:
				chan.NCommand = Effects.Arpeggio;
				if (!Flags.HasAllFlags(SongFlags.FirstTick))
					break;
				if (param != 0)
					chan.MemArpeggio = param;
				break;

			case Effects.Retrigger:
				if (param != 0)
					chan.MemRetrig = param & 0xFF;
				EffectRetriggerNote(nchan, chan.MemRetrig);
				break;

			case Effects.Tremor:
				// Tremor logic lifted from DUMB, which is the only player that actually gets it right.
				// I *sort of* understand it.
				if (Flags.HasAllFlags(SongFlags.FirstTick))
				{
					if (param == 0)
						param = chan.MemTremor;
					else if (!Flags.HasAllFlags(SongFlags.ITOldEffects))
					{
						if ((param & 0xf0) != 0) param -= 0x10;
						if ((param & 0x0f) != 0) param -= 0x01;
					}

					chan.MemTremor = param;
					chan.CountdownTremor |= 128;
				}

				chan.NCommand = Effects.Tremor;

				break;

			case Effects.GlobalVolume:
				if (!firstTick)
					break;
				if (param <= 128)
					CurrentGlobalVolume = param;
				break;

			case Effects.GlobalVolumeSlide:
				EffectGlobalVolumeSlide(ref chan, param);
				break;

			case Effects.Panning:
				if (!Flags.HasAllFlags(SongFlags.FirstTick))
					break;
				chan.Flags &= ~ChannelFlags.Surround;
				chan.PanbrelloDelta = 0;
				chan.Panning = param;
				chan.ChannelPanning = 0;
				chan.PanningSwing = 0;
				chan.Flags |= ChannelFlags.FastVolumeRamp;
				break;

			case Effects.PanningSlide:
				EffectPanningSlide(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, param);
				break;

			case Effects.Tremolo:
				EffectTremolo(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, param);
				break;

			case Effects.FineVibrato:
				EffectFineVibrato(ref chan, param);
				break;

			case Effects.Special:
				EffectSpecial(nchan, param);
				break;

			case Effects.KeyOff:
				if ((CurrentSpeed - TickCount) == param)
					EffectKeyOff(nchan);
				break;

			case Effects.ChannelVolume:
				if (!Flags.HasAllFlags(SongFlags.FirstTick))
					break;
				// FIXME rename global_volume to channel_volume in the channel struct
				if (param <= 64)
				{
					chan.GlobalVolume = param;
					chan.Flags |= ChannelFlags.FastVolumeRamp;
				}
				break;

			case Effects.ChannelVolumeSlide:
				EffectChannelVolumeSlide(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, param);
				break;

			case Effects.Panbrello:
				EffectPanbrello(ref chan, param);
				break;

			case Effects.SetEnvelopePosition:
				if (!Flags.HasAllFlags(SongFlags.FirstTick))
					break;
				chan.VolumeEnvelopePosition = param;
				chan.PanningEnvelopePosition = param;
				chan.PitchEnvelopePosition = param;
				if (IsInstrumentMode && (chan.Instrument != null))
				{
					var pEnv = chan.Instrument;
					if ((chan.Flags.HasAllFlags(ChannelFlags.PanningEnvelope))
							&& (pEnv.PanningEnvelope != null)
							&& (param > pEnv.PanningEnvelope.Nodes.Last().Tick))
					{
						chan.Flags &= ~ChannelFlags.PanningEnvelope;
					}
				}
				break;

			case Effects.PositionJump:
				if (!AudioPlayback.MixFlags.HasAllFlags(MixFlags.NoBackwardJumps) || ProcessOrder < param)
					ProcessOrder = param - 1;
				ProcessRow = ProcessNextOrder;
				break;

			case Effects.PatternBreak:
				if (!PatternLoop)
				{
					BreakRow = param;
					ProcessRow = ProcessNextOrder;
				}
				break;

			case Effects.NoteSlideUp:
				EffectNoteSlide(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, param, 1);
				break;
			case Effects.NoteSlideDown:
				EffectNoteSlide(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, param, -1);
				break;
		}
	}

	void HandleVolumeEffect(ref SongVoice chan, VolumeEffects volumeCommand, int volume,
		bool firstTick, bool startNote)
	{
		/* A few notes, paraphrased from ITTECH.TXT:
			Ex/Fx/Gx are shared with Exx/Fxx/Gxx; Ex/Fx are 4x the 'normal' slide value
			Gx is linked with Ex/Fx if Compat Gxx is off, just like Gxx is with Exx/Fxx
			Gx values: 1, 4, 8, 16, 32, 64, 96, 128, 255
			Ax/Bx/Cx/Dx values are used directly (i.e. D9 == D09), and are NOT shared with Dxx
				(value is stored into mem_vc_volslide and used by A0/B0/C0/D0)
			Hx uses the same value as Hxx and Uxx, and affects the *depth*
				so... hxx = (hx | (oldhxx & 0xf0))  ???

		Additionally: volume and panning are handled on the start tick, not
		the first tick of the row (that is, SDx alters their behavior) */

		switch (volumeCommand)
		{
			case VolumeEffects.None:
				break;

			case VolumeEffects.Volume:
				if (startNote)
				{
					if (volume > 64) volume = 64;
					chan.Volume = volume << 2;
					chan.Flags |= ChannelFlags.FastVolumeRamp;
				}
				break;

			case VolumeEffects.Panning:
				if (startNote)
				{
					if (volume > 64) volume = 64;
					chan.Panning = volume << 2;
					chan.ChannelPanning = 0;
					chan.PanningSwing = 0;
					chan.PanbrelloDelta = 0;
					chan.Flags |= ChannelFlags.FastVolumeRamp;
					chan.Flags &= ~ChannelFlags.Surround;
				}
				break;

			case VolumeEffects.PortamentoUp: // Fx
				if (!startNote)
					EffectRegularPortamentoUp(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, chan.MemPitchSlide);
				break;

			case VolumeEffects.PortamentoDown: // Ex
				if (!startNote)
					EffectRegularPortamentoDown(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, chan.MemPitchSlide);
				break;

			case VolumeEffects.TonePortamento: // Gx
				if (!startNote)
					EffectTonePortamento(Flags | (firstTick ? SongFlags.FirstTick : 0), ref chan, chan.MemPortaNote);
				break;

			case VolumeEffects.VolumeSlideUp: // Cx
				if (startNote)
				{
					if (volume != 0)
						chan.MemVolumeColumnVolSlide = volume;
				}
				else
					EffectVolumeUp(ref chan, chan.MemVolumeColumnVolSlide);
				break;

			case VolumeEffects.VolumeSlideDown: // Dx
				if (startNote)
				{
					if (volume != 0)
						chan.MemVolumeColumnVolSlide = volume;
				}
				else
					EffectVolumeDown(ref chan, chan.MemVolumeColumnVolSlide);
				break;

			case VolumeEffects.FineVolumeUp: // Ax
				if (startNote)
				{
					if (volume != 0)
						chan.MemVolumeColumnVolSlide = volume;
					else
						volume = chan.MemVolumeColumnVolSlide;
					EffectVolumeUp(ref chan, volume);
				}
				break;

			case VolumeEffects.FineVolumeDown: // Bx
				if (startNote)
				{
					if (volume != 0)
						chan.MemVolumeColumnVolSlide = volume;
					else
						volume = chan.MemVolumeColumnVolSlide;
					EffectVolumeDown(ref chan, volume);
				}
				break;

			case VolumeEffects.VibratoDepth: // Hx
				EffectVibrato(ref chan, volume);
				break;

			case VolumeEffects.VibratoSpeed: // $x (FT2 compat.)
				/* Unlike the vibrato depth, this doesn't actually trigger a vibrato. */
				chan.VibratoSpeed = volume;
				break;

			case VolumeEffects.PanningSlideLeft: // <x (FT2)
				EffectPanningSlide(Flags, ref chan, volume);
				break;

			case VolumeEffects.PanningSlideRight: // >x (FT2)
				EffectPanningSlide(Flags, ref chan, volume << 4);
				break;
		}
	}

	/* firstTick is only used for SDx at the moment */
	public void ProcessEffects(bool firstTick)
	{
		for (int nchan = 0; nchan < Constants.MaxChannels; nchan++)
		{
			ref var chan = ref Voices[nchan];

			chan.NCommand = Effects.None;

			int instrumentNumber = chan.RowInstrumentNumber;
			var volumeCommand = chan.RowVolumeEffect;
			int volume = chan.RowVolumeParameter;
			var cmd = chan.RowEffect;
			int param = chan.RowParam;

			bool portamento =
				cmd == Effects.TonePortamento ||
				cmd == Effects.TonePortamentoVolume ||
				volumeCommand == VolumeEffects.TonePortamento;

			bool startNote = Flags.HasAllFlags(SongFlags.FirstTick);

			chan.Flags &= ~(ChannelFlags.FastVolumeRamp | ChannelFlags.NewNote);

			// set instrument before doing anything else
			if ((instrumentNumber != 0) && startNote) chan.NewInstrumentNumber = instrumentNumber;

			// This is probably the single biggest WTF replayer bug in Impulse Tracker.
			// In instrument mode, when an note + instrument is triggered that does not map to any sample, the entire cell (including potentially present global effects!)
			// is ignored. Even better, if on a following row another instrument number (this time without a note) is encountered, we end up in the same situation!
			if (IsInstrumentMode && instrumentNumber > 0 && instrumentNumber < Instruments.Count && Instruments[instrumentNumber] != null)
			{
				var instrument = Instruments[instrumentNumber];

				if (instrument != null)
				{
					int note = (chan.RowNote != SpecialNotes.None) ? chan.RowNote : chan.NewNote;

					if (SongNote.IsNote(note) && instrument.SampleMap[note - SpecialNotes.First] == 0)
					{
						chan.NewNote = note;
						chan.RowInstrumentNumber = instrumentNumber;
						chan.RowVolumeEffect = VolumeEffects.None;
						chan.RowEffect = Effects.None;
						continue;
					}
				}
			}

			/* Have to handle SDx specially because of the way the effects are structured.
			In a PERFECT world, this would be very straightforward:
				- Handle the effect column, and set flags for things that should happen
					(portamento, volume slides, arpeggio, vibrato, tremolo)
				- If note delay counter is set, stop processing that channel
				- Trigger all notes if it's their start tick
				- Handle volume column.
			The obvious implication of this is that all effects are checked only once, and
			volumes only need to be set for notes once. Additionally this helps for separating
			the mixing code from the rest of the interface (which is always good, especially
			for hardware mixing...)
			Oh well, the world is not perfect. */

			if (cmd == Effects.Special)
			{
				if (param != 0)
					chan.MemSpecial = param;
				else
					param = chan.MemSpecial;

				if (param >> 4 == 0xd)
				{
					// Ideally this would use SongFlags.FirstTick, but Impulse Tracker has a bug here :)
					if (firstTick)
					{
						chan.CountdownNoteDelay = ((param & 0xf) != 0) ? (param & 0xf) : 1;
						continue; // notes never play on the first tick with SDx, go away
					}
					chan.CountdownNoteDelay--;
					if (chan.CountdownNoteDelay > 0)
						continue; // not our turn yet, go away
					startNote = (chan.CountdownNoteDelay == 0);
				}
			}

			// Handles note/instrument/volume changes
			if (startNote)
			{
				int note = chan.RowNote;
				/* MPT test case InstrumentNumberChange.it */
				if (IsInstrumentMode && (SongNote.IsNote(note) || note == SpecialNotes.None))
				{
					int instrCheck = (instrumentNumber != 0) ? instrumentNumber : chan.LastInstrumentNumber;
					if ((instrCheck != 0) && (instrCheck < 0 || instrCheck >= Instruments.Count || Instruments[instrCheck] == null))
					{
						note = SpecialNotes.None;
						instrumentNumber = 0;
					}
				}

				if (IsInstrumentMode && (instrumentNumber != 0) && !SongNote.IsNote(note))
				{
					if ((portamento && Flags.HasAllFlags(SongFlags.CompatibleGXX))
						|| (!portamento && Flags.HasAllFlags(SongFlags.ITOldEffects)))
					{
						EnvelopeReset(ref chan, always: true);
						chan.FadeOutVolume = 65536;
					}
				}

				if ((instrumentNumber != 0) && note == SpecialNotes.None)
				{
					if (IsInstrumentMode)
					{
						if (chan.Sample != null)
							chan.Volume = chan.Sample.Volume;
					}
					else if (instrumentNumber < Samples.Count)
					{
						var sample = Samples[instrumentNumber];

						if (sample != null)
							chan.Volume = sample.Volume;
						else
							chan.Volume = 0;
					}

					if (IsInstrumentMode)
					{
						if (instrumentNumber < Instruments.Count && (chan.Instrument != Instruments[instrumentNumber] || chan.CurrentSampleData.IsEmpty))
							note = chan.NewNote;
					}
					else
					{
						if (instrumentNumber < Samples.Count && (chan.Sample != Samples[instrumentNumber] || chan.CurrentSampleData.IsEmpty))
							note = chan.NewNote;
					}
				}

				// Invalid Instrument ?
				if (instrumentNumber >= Instruments.Count)
					instrumentNumber = 0;

				if (SongNote.IsControl(note))
				{
					if (instrumentNumber != 0)
					{
						int sampleNumber = instrumentNumber;

						if (IsInstrumentMode)
						{
							sampleNumber = 0;

							var instrument = Instruments[instrumentNumber];

							if (instrument != null)
								sampleNumber = instrument.SampleMap[chan.Note];
						}

						if (sampleNumber > 0 && sampleNumber < Samples.Count)
							chan.Volume = Samples[sampleNumber]?.Volume ?? 0;
					}

					if (!Flags.HasAllFlags(SongFlags.ITOldEffects))
						instrumentNumber = 0;
				}

				// Note Cut/Off/Fade => ignore instrument
				if (SongNote.IsControl(note) || (note != SpecialNotes.None && !portamento))
				{
					/* This is required when the instrument changes (KeyOff is not called) */
					/* Possibly a better bugfix could be devised. --Bisqwit */
					if (chan.Flags.HasAllFlags(ChannelFlags.AdLib))
					{
						//Do this only if really an adlib chan. Important!
						OPLNoteOff(nchan);
						OPLTouch(nchan, 0);
					}

					GeneralMIDI.KeyOff(CurrentSong, nchan);
					GeneralMIDI.Touch(CurrentSong, nchan, 0);
				}

				int previousNewNote = chan.NewNote;
				if (SongNote.IsNote(note))
				{
					chan.NewNote = note;

					if (!portamento)
						CheckNNA(nchan, instrumentNumber, note, false);

					if (chan.ChannelPanning > 0)
					{
						chan.Panning = (chan.ChannelPanning & 0x7FFF) - 1;
						if ((chan.ChannelPanning & 0x8000) != 0)
							chan.Flags |= ChannelFlags.Surround;
						chan.ChannelPanning = 0;
					}
				}

				// Instrument Change ?
				if (instrumentNumber != 0)
				{
					var pSmp = chan.Sample;
					var pEnv = chan.Instrument;

					InstrumentChange(ref chan, instrumentNumber, portamento, instrumentColumn: 1);

					var sample = Samples[instrumentNumber];

					if ((sample != null)
					 && sample.Flags.HasAllFlags(SampleFlags.AdLib)
					 && (sample.AdLibBytes != null))
						OPLPatch(nchan, sample.AdLibBytes);

					if (IsInstrumentMode && Instruments[instrumentNumber] != null)
					{
						var instr = GetInstrument(instrumentNumber);

						if (instr != null)
						{
							GeneralMIDI.DPatch(this, nchan, (byte)instr.MIDIProgram,
								(byte)instr.MIDIBank,
								instr.MIDIChannelMask);
						}
					}

					if (SongNote.IsNote(note))
					{
						chan.NewInstrumentNumber = 0;

						if (pSmp != chan.Sample)
							chan.Position = chan.PositionFrac = 0;
					}
				}

				// New Note ?
				if (note != SpecialNotes.None)
				{
					if ((instrumentNumber == 0) && (chan.NewInstrumentNumber != 0) && SongNote.IsNote(note))
					{
						if (SongNote.IsNote(previousNewNote))
							chan.NewNote = previousNewNote;

						InstrumentChange(ref chan, chan.NewInstrumentNumber, portamento, 0);

						if (IsInstrumentMode
								&& chan.NewInstrumentNumber < Instruments.Count
								&& (Instruments[chan.NewInstrumentNumber] != null))
						{
							var sample = Samples[chan.NewInstrumentNumber];

							if ((sample != null)
							 && sample.Flags.HasAllFlags(SampleFlags.AdLib)
							 && (sample.AdLibBytes != null))
								OPLPatch(nchan, sample.AdLibBytes);

							var instr = Instruments[chan.NewInstrumentNumber];

							if (instr != null)
							{
								GeneralMIDI.DPatch(this, nchan, (byte)instr.MIDIProgram,
									(byte)instr.MIDIBank,
									instr.MIDIChannelMask);
							}
						}

						chan.NewNote = note;
						chan.NewInstrumentNumber = 0;
					}

					NoteChange(nchan, note, portamento, retrigger: false, haveInstrument: (instrumentNumber != 0));
				}
			}

			// Initialize portamento command memory (needs to be done in exactly this order)
			if (firstTick)
			{
				bool effectColumnTonePortamento = (cmd == Effects.TonePortamento || cmd == Effects.TonePortamentoVolume);
				if (effectColumnTonePortamento)
				{
					int tonePortamentoParam = (cmd != Effects.TonePortamentoVolume ? param : 0);

					if (tonePortamentoParam != 0)
						chan.MemPortaNote = tonePortamentoParam;
					else if ((tonePortamentoParam == 0) && !Flags.HasAllFlags(SongFlags.CompatibleGXX))
						chan.MemPortaNote = chan.MemPitchSlide;

					if (!Flags.HasAllFlags(SongFlags.CompatibleGXX))
						chan.MemPitchSlide = chan.MemPortaNote;
				}

				if (volumeCommand == VolumeEffects.TonePortamento)
				{
					if (volume != 0)
						chan.MemPortaNote = Tables.VolumeColumnPortamentoTable[volume & 0x0F];

					if (!Flags.HasAllFlags(SongFlags.CompatibleGXX))
						chan.MemPitchSlide = chan.MemPortaNote;
				}

				if ((volume != 0) && (volumeCommand == VolumeEffects.PortamentoUp || volumeCommand == VolumeEffects.PortamentoDown))
				{
					chan.MemPitchSlide = 4 * volume;
					if (!effectColumnTonePortamento && !Flags.HasAllFlags(SongFlags.CompatibleGXX))
						chan.MemPortaNote = chan.MemPitchSlide;
				}

				if ((param != 0) && (cmd == Effects.PortamentoUp || cmd == Effects.PortamentoDown))
				{
					chan.MemPitchSlide = param;
					if (!Flags.HasAllFlags(SongFlags.CompatibleGXX))
						chan.MemPortaNote = chan.MemPitchSlide;
				}
			}

			HandleVolumeEffect(ref chan, volumeCommand, volume, firstTick, startNote);
			HandleEffect(nchan, cmd, param, portamento, firstTick);

			/* stupid hax: handling effect column after volume column breaks
			* handling when both columns have a vibrato effect.
			*
			* overwrite the memory after the fact with the correct parameters.
			*   --paper */
			if (volumeCommand == VolumeEffects.VibratoDepth
				&& (cmd == Effects.Vibrato/* || cmd == Effects.VibratoVol -- do we also need this? */))
				EffectVibrato(ref chan, volume);
		}
	}

	// ------------------------------------------------------------------------------------------------------------

	// Send exactly one MIDI message
	public void MIDISend(Span<byte> data, int nchan, bool fake)
	{
		ref var chan = ref Voices[nchan];

		if (data.Length >= 1 && (data[0] == 0xFA || data[0] == 0xFC || data[0] == 0xFF))
		{
			// Start Song, Stop Song, MIDI Reset

			for (int c = 0; c < Voices.Length; c++)
			{
				Voices[c].Cutoff = 0x7F;
				Voices[c].Resonance = 0x00;
			}
		}

		if (data.Length >= 4 && data[0] == 0xF0 && data[1] == 0xF0)
		{
			// impulse tracker filter control (mfg. 0xF0)
			switch (data[2])
			{
				case 0x00: // set cutoff
					if (data[3] < 0x80)
					{
						chan.Cutoff = data[3];
						SetUpChannelFilter(ref chan, !chan.Flags.HasAllFlags(ChannelFlags.Filter), 256, AudioPlayback.MixFrequency);
					}
					break;
				case 0x01: // set resonance
					if (data[3] < 0x80)
					{
						chan.Resonance = data[3];
						SetUpChannelFilter(ref chan, !chan.Flags.HasAllFlags(ChannelFlags.Filter), 256, AudioPlayback.MixFrequency);
					}
					break;
			}
		}
		else if (!fake)
		{
			/* okay, this is kind of how it works.
			we pass buffer_count as here because while
				1000 * ((8((buffer_size/2) - buffer_count)) / sample_rate)
			is the number of msec we need to delay by, libmodplug simply doesn't know
			what the buffer size is at this point so buffer_count simply has no
			frame of reference.

			fortunately, schism does and can complete this (tags: _schism_midi_out_raw )

			*/
			MIDISink?.OutRaw(this, data, BufferCount);
		}
	}

	// ------------------------------------------------------------------------------------------------------------

	/* These return the channel that was used for the note. */

	/* **** chan ranges from 1 to MAX_CHANNELS   */
	int KeyDownEx(int samp, int ins, int note, int vol, int chan, Effects effect, int param)
	{
		int midiNote = note; /* note gets overwritten, possibly SpecialNotes.None */

		SongSample? s = null;
		SongInstrument? i = null;

		switch (chan)
		{
			case KeyJazz.CurrentChannel:
				chan = AudioPlayback.CurrentPlayChannel;
				if (AudioPlayback.MultichannelMode)
					AudioPlayback.ChangeCurrentPlayChannel(1, true);
				break;
			case KeyJazz.AutomaticChannel:
				if (AudioPlayback.MultichannelMode)
				{
					chan = AudioPlayback.CurrentPlayChannel;
					AudioPlayback.ChangeCurrentPlayChannel(1, true);
				}
				else
				{
					for (chan = 1; chan < Constants.MaxChannels; chan++)
						if (KeyJazz.GetLastNoteInChannel(chan) == 0)
							break;
				}
				break;
			default:
				break;
		}

		// back to the internal range
		int chanInternal = chan - 1;

		if (chanInternal >= Constants.MaxChannels)
			throw new Exception("Out-of-range channel number: " + chan);

		// hm
		lock (AudioPlayback.LockScope())
		{
			var c = CurrentSong.Voices[chanInternal];

			bool insMode = CurrentSong.IsInstrumentMode;

			if (SongNote.IsNote(note))
			{
				// keep track of what channel this note was played in so we can note-off properly later
				if (KeyJazz.GetLastNoteInChannel(chan) != 0)
				{
					// reset note-off pending state for last note in channel
					KeyJazz.UnlinkLastNoteForChannel(chan);
				}

				KeyJazz.LinkNoteAndChannel(note, chan);

				// handle blank instrument values and "fake" sample #0 (used by sample loader)
				if (samp == 0)
					samp = c.LastInstrumentNumber;
				else if (samp == KeyJazz.FakeInstrument)
					samp = 0; // dumb hack

				if (ins == 0)
					ins = c.LastInstrumentNumber;
				else if (ins == KeyJazz.FakeInstrument)
					ins = 0; // dumb hack

				c.LastInstrumentNumber = insMode ? ins : samp;

				// give the channel a sample, and maybe an instrument
				s = (samp == KeyJazz.NoInstrument) ? null : CurrentSong.Samples[samp];
				i = (ins == KeyJazz.NoInstrument) ? null : CurrentSong.GetInstrument(ins); // blah

				if ((i != null) && (samp == KeyJazz.NoInstrument))
				{
					// we're playing an instrument and don't know what sample! WHAT WILL WE EVER DO?!
					// well, look it up in the note translation table, silly.
					// the weirdness here the default value here is to mimic IT behavior: we want to use
					// the sample corresponding to the instrument number if in sample mode and no sample
					// is defined for the note in the instrument's note map.
					s = CurrentSong.TranslateKeyboard(i, note, insMode ? null : CurrentSong.Samples[ins]);
				}
			}

			c.RowEffect = effect;
			c.RowParam = param;

			// now do a rough equivalent of csf_instrument_change and csf_note_change
			if (i != null)
				CurrentSong.CheckNNA(chanInternal, ins, note, false);

			if (s != null)
			{
				if (c.Flags.HasAllFlags(ChannelFlags.AdLib) && (s.AdLibBytes != null))
				{
					OPLNoteOff(chanInternal);
					OPLPatch(chanInternal, s.AdLibBytes);
				}

				c.Flags = ((ChannelFlags)s.Flags & ChannelFlags.SampleFlags) | (c.Flags & ChannelFlags.Mute);

				if (c.IsMuted)
					c.Flags |= ChannelFlags.NNAMute;

				c.Cutoff = 0x7f;
				c.Resonance = 0;

				if (i != null)
				{
					c.Instrument = i;

					if (!i.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelopeCarry)) c.VolumeEnvelopePosition = 0;
					if (!i.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeCarry)) c.PanningEnvelopePosition = 0;
					if (!i.Flags.HasAllFlags(InstrumentFlags.PitchEnvelopeCarry)) c.PitchEnvelopePosition = 0;
					if (i.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelope)) c.Flags |= ChannelFlags.VolumeEnvelope;
					if (i.Flags.HasAllFlags(InstrumentFlags.PanningEnvelope)) c.Flags |= ChannelFlags.PanningEnvelope;
					if (i.Flags.HasAllFlags(InstrumentFlags.PitchEnvelope)) c.Flags |= ChannelFlags.PitchEnvelope;

					i.IsPlayed = true;

					if (Status.Flags.HasAllFlags(StatusFlags.MIDILikeTracker))
					{
						if (i.MIDIChannelMask != 0)
						{
							// TODO:
							GeneralMIDI.KeyOff(this, chanInternal);
							GeneralMIDI.DPatch(this, chanInternal, (byte)i.MIDIProgram, (byte)i.MIDIBank, i.MIDIChannelMask);
						}
					}

					if ((i.IFCutoff & 0x80) != 0)
						c.Cutoff = i.IFCutoff & 0x7f;
					if ((i.IFResonance & 0x80) != 0)
						c.Resonance = i.IFResonance & 0x7f;
					//?
					c.VolumeSwing = i.VolumeSwing;
					c.PanningSwing = i.PanningSwing;
					c.NewNoteAction = i.NewNoteAction;
				}
				else
				{
					c.Instrument = null;
					c.Cutoff = 0x7F;
					c.Resonance = 0;
				}

				c.MasterChannel = 0; // indicates foreground channel.

				//c.Flags &= ~(ChannelFlags.PingPong);

				// ?
				//c.AutoVibDepth = 0;
				//c.AutoVibPosition = 0;

				// NoteChange copies stuff from c.Sample as long as c.Length is zero
				// and if period != 0 (ie. sample not playing at a stupid rate)
				c.Sample = s;
				c.Length = 0;
				// ... but it doesn't copy the volumes, for somewhat obvious reasons.
				c.Volume = (vol == KeyJazz.DefaultVolume) ? s.Volume : (vol << 2);
				c.InstrumentVolume = s.GlobalVolume;
				if (i != null)
					c.InstrumentVolume = (c.InstrumentVolume * i.GlobalVolume) >> 7;
				c.GlobalVolume = 64;
				// use the sample's panning if it's set, or use the default
				c.ChannelPanning = (short)(c.Panning + 1);
				if (c.Flags.HasAllFlags(ChannelFlags.Surround))
					c.ChannelPanning = (short)(c.ChannelPanning | 0x8000);

				c.Panning = s.Flags.HasAllFlags(SampleFlags.Panning) ? s.Panning : 128;
				if (i != null)
					c.Panning = i.Flags.HasAllFlags(InstrumentFlags.SetPanning) ? i.Panning : 128;
				c.Flags &= ChannelFlags.Surround;
				// gotta set these by hand, too
				c.C5Speed = s.C5Speed;
				c.NewNote = note;
				s.IsPlayed = true;
			}
			else if (SongNote.IsNote(note))
			{
				// Note given with no sample number. This might happen if on the instrument list and playing
				// an instrument that has no sample mapped for the given note. In this case, ignore the note.
				note = SpecialNotes.None;
			}

			if (c.Increment < 0)
				c.Increment = -c.Increment; // lousy hack

			CurrentSong.NoteChange(chanInternal, note, false, false, true);

			if (!Status.Flags.HasAllFlags(StatusFlags.MIDILikeTracker) && (i != null))
			{
				/* midi keyjazz shouldn't require a sample */
				SongNote mc = default;

				mc.Note = (byte)((note != 0) ? note : midiNote);

				mc.Instrument = (byte)ins;
				mc.VolumeEffect = VolumeEffects.Volume;
				mc.VolumeParameter = (byte)vol;
				mc.Effect = effect;
				mc.Parameter = (byte)param;

				// TODO:
				//csf_midi_out_note(current_song, chanInternal, &mc);
			}

			/*
			TODO:
			- If this is the ONLY channel playing, and the song is stopped, always reset the tick count
				(will fix the "random" behavior for most effects)
			- If other channels are playing, don't reset the tick count, but do process first-tick effects
				for this note *right now* (this will fix keyjamming with effects like Oxx and SCx)
			- Need to handle volume column effects with this function...
			*/
			if (CurrentSong.Flags.HasAllFlags(SongFlags.EndReached))
			{
				CurrentSong.Flags &= ~SongFlags.EndReached;
				CurrentSong.Flags |= SongFlags.Paused;
			}
		}

		return chan;
	}

	public int KeyDown(int samp, int ins, int note, int vol, int chan)
	{
		return KeyDownEx(samp, ins, note, vol, chan, Effects.Panning, 0x80);
	}

	public int KeyRecord(int samp, int ins, int note, int vol, int chan, Effects effect, int param)
	{
		return KeyDownEx(samp, ins, note, vol, chan, effect, param);
	}

	public int KeyUp(int samp, int ins, int note)
	{
		int chan = KeyJazz.GetLastChannelForNote(note);

		if (chan == 0)
		{
			// could not find channel, drop.
			return -1;
		}

		return KeyUp(samp, ins, note, chan);
	}

	public int KeyUp(int samp, int ins, int note, int chan)
	{
		if (KeyJazz.GetLastNoteInChannel(chan) != note)
			return -1;

		KeyJazz.UnlinkNoteAndChannel(note, chan);

		return KeyDownEx(samp, ins, SpecialNotes.NoteOff, KeyJazz.DefaultVolume, chan, Effects.None, 0);
	}

	public bool LoadSample(int n, string file)
	{
		try
		{
			using (var s = File.OpenRead(file))
			{
				// set some default stuff
				lock (AudioPlayback.LockScope())
				{
					CurrentSong.StopSample(GetSample(n));

					var smp = SampleFileConverter.TryLoadSampleWithAllConverters(file);

					if (smp == null)
					{
						Log.Append(4, "Unsupported sample file");
						return false;
					}

					// this is after the loaders because i don't trust them, even though i wrote them ;)
					smp.FileName = Path.GetFileName(file);

					if ((smp.Name.Length >= 23) && (smp.Name[23] == '\xFF'))
					{
						// don't load embedded samples
						// (huhwhat?!)
						smp.Name = smp.Name.Substring(0, 23) + ' ' + smp.Name.Substring(23);
					}

					Samples[n] = smp;
				}
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	public bool LoadInstrument(int n, string file)
	{
		return LoadInstrumentEx(n, file, null, -1);
	}

	public bool LoadInstrumentWithPrompt(int n, string file)
	{
		var retval = LoadInstrumentEx(n, file, null, -1);

		if (!IsInstrumentMode)
			PromptEnableInstrumentMode();
		else
			Page.SetPage(PageNumbers.InstrumentList);

		return retval;
	}

	public bool LoadInstrumentEx(int target, string? file, string? libf, int n)
	{
		lock (AudioPlayback.LockScope())
		{
			/* 0. delete old samples */
			var existing = GetInstrument(target);

			/* init... */
			var usedSamples = existing.SampleMap.ToHashSet();

			/* mark... */
			var nonExclusiveSamples = Instruments
				.Except([existing])
				.OfType<SongInstrument>()
				.SelectMany(instr => instr.SampleMap)
				.ToHashSet();

			/* sweep! */
			foreach (int j in usedSamples.Except(nonExclusiveSamples))
				Samples[j] = null;

			if (libf != null) /* file is ignored */
			{
				Song xl;

				try
				{
					xl = CreateLoad(libf) ?? throw new Exception("Failed to load: " + libf);
				}
				catch (Exception e)
				{
					Log.AppendException(e, libf);
					return false;
				}

				if (xl == null)
					return false;

				var sampleMap = new Dictionary<int, int>();

				/* 1. find a place for all the samples */
				for (int j = 0; j < 128; j++)
				{
					int x = xl.Instruments[n]?.SampleMap[j] ?? -1;

					if (!sampleMap.ContainsKey(x))
					{
						if (x > 0 && x < xl.Samples.Count)
						{
							for (int k = 1; k < Samples.Count; k++)
							{
								if ((Samples[k]?.Length ?? 0) != 0) continue;
								sampleMap[x] = k;

								var smp = GetSample(k);

								if (smp != null)
								{
									smp.Name = smp.Name.Replace('\0', ' ');

									CopySample(k, smp);
								}
							}
						}
					}
				}

				/* transfer the instrument */
				var instrument = xl.Instruments[n]!;

				Instruments[target] = instrument;

				// detach
				xl.Instruments[n] = null;

				/* and rewrite! */
				for (int k = 0; k < 128; k++)
					instrument.SampleMap[k] = (byte)sampleMap[instrument.SampleMap[k]];

				return true;
			}

			/* okay, load an ITI file */
			if (file == null)
				return false;

			try
			{
				return InstrumentFileConverter.TryLoadInstrumentWithAllConverters(file, target);
			}
			catch (IOException e)
			{
				Log.AppendException(e);
				return false;
			}
		}
	}

	public int PreloadSample(FileReference file)
	{
		// 0 is our "hidden sample"
		const int FakeSlot = 0;

		//StopSample(GetSample(FakeSlot));

		if (file.Sample != null)
		{
			var smp = GetSample(FakeSlot);

			if (smp == null)
				Samples[FakeSlot] = smp = new SongSample();

			lock (AudioPlayback.LockScope())
			{
				DestroySample(FakeSlot);
				CopySample(FakeSlot, file.Sample);

				smp.Name = file.Title;
				smp.FileName = file.BaseName;
			}

			return FakeSlot;
		}

		// WARNING this function must return 0 or KeyJazz.NoInstrument
		return LoadSample(FakeSlot, file.FullPath) ? FakeSlot : KeyJazz.NoInstrument;
	}

	public static Song? Load(string fileName)
	{
		Song? ret = null;

		Page.SaveCheck(
			() =>
			{
				ret = LoadUnchecked(fileName);

				if (ret != null)
					Page.SetPage(AudioPlayback.Mode == AudioPlaybackMode.Playing ? PageNumbers.Info : PageNumbers.Log);
				else
					Page.SetPage(PageNumbers.Log);
			});

		return ret;
	}

	public static Song? CreateLoad(string fileName)
	{
		using (var s = File.OpenRead(fileName))
		{
			Song? newSong = null;

			Exception? exception = null;

			foreach (var converter in SongFileConverter.EnumerateImplementations())
			{
				s.Position = 0;

				try
				{
					newSong = converter.LoadSong(s, LoadFlags.None);
					break;
				}
				catch (Exception e)
				{
					exception = e;
				}
			}

			if (newSong == null)
			{
				if (exception == null)
					exception = new Exception("Couldn't load file: " + fileName);

				throw exception;
			}

			if (CurrentSong != null)
			{
				// loaders might override these
				newSong.RowHighlightMajor = CurrentSong.RowHighlightMajor;
				newSong.RowHighlightMinor = CurrentSong.RowHighlightMinor;

				newSong.CopyMIDIConfig(CurrentSong.MIDIConfig);
			}

			newSong.StopAtOrder = newSong.StopAtRow = -1;

			// IT uses \r in song messages; replace errant \n's
			newSong.Message = newSong.Message.Replace('\n', '\r');

			AllPages.Message.ResetSelection();

			return newSong;
		}
	}

	/* ------------------------------------------------------------------------- */

	static string MangleFilename(string @in, string? mid, string? ext)
	{
		string inExt = Path.GetExtension(@in);
		string @base = (inExt != null) ? @in.Substring(0, @in.Length - inExt.Length) : @in;

		var ret = new StringBuilder();

		ret.Append(@base);

		if (mid != null)
			ret.Append(mid);

		if (!string.IsNullOrEmpty(inExt))
			ret.Append(inExt);
		else if (ext != null)
			ret.Append(ext);

		return ret.ToString();
	}

	public SaveResult ExportSong(string fileName, string type)
	{
		var exporter = SampleExporter.FindImplementation(type);

		if (exporter == null)
			return SaveResult.InternalError;

		return ExportSong(fileName, exporter);
	}

	public SaveResult ExportSong(string fileName, SampleExporter exporter)
	{
		string? mid = (exporter.IsMulti && !fileName.Contains("%c")) ? ".%c" : null;

		string mangle = MangleFilename(fileName, mid, exporter.Extension);

		Log.AppendNewLine();
		Log.AppendNewLine();
		Log.AppendWithUnderline(2, "Exporting to {0}", exporter.Description);

		/* disko does the rest of the log messages itself */
		var r = DiskWriter.ExportSong(mangle, exporter);

		switch (r)
		{
			case DiskWriterStatus.OK:
				return SaveResult.Success;
			case DiskWriterStatus.Error:
				return SaveResult.FileError;
			default:
				return SaveResult.InternalError;
		}
	}

	public SaveResult SaveSong(string fileName, string type)
	{
		var converter = SongFileConverter.FindImplementation(type);

		if (converter == null)
			return SaveResult.InternalError;

		return SaveSong(fileName, converter);
	}

	public SaveResult SaveSong(string fileName, SongFileConverter converter)
	{
		string mangle = MangleFilename(fileName, null, converter.Extension);

		Log.AppendNewLine();
		Log.AppendWithUnderline(2, "Saving {0} module", converter.Description);

		/* TODO: add or replace file extension as appropriate

		From IT 2.10 update:
			- Automatic filename extension replacement on Ctrl-S, so that if you press
				Ctrl-S after loading a .MOD, .669, .MTM or .XM, the filename will be
				automatically modified to have a .IT extension.

		In IT, if the filename given has no extension ("basename"), then the extension for the proper file type
		(IT/S3M) will be appended to the name.
		A filename with an extension is not modified, even if the extension is blank. (as in "basename.")

		This brings up a rather odd bit of behavior: what happens when saving a file with the deliberately wrong
		extension? Selecting the IT type and saving as "test.s3m" works just as one would expect: an IT file is
		written, with the name "test.s3m". No surprises yet, but as soon as Ctrl-S is used, the filename is "fixed",
		producing a second file called "test.it". The reverse happens when trying to save an S3M named "test.it" --
		it's rewritten to "test.s3m".
		Note that in these scenarios, Impulse Tracker *DOES NOT* check if an existing file by that name exists; it
		will GLADLY overwrite the old "test.s3m" (or .it) with the renamed file that's being saved. Presumably this
		is NOT intentional behavior.

		Another note: if the file could not be saved for some reason or another, Impulse Tracker pops up a dialog
		saying "Could not save file". This can be seen rather easily by trying to save a file with a malformed name,
		such as "abc|def.it". This dialog is presented both when saving from F10 and Ctrl-S.
		*/

		Stream stream;

		string tempName = Path.GetTempFileName();

		try
		{
			stream = File.OpenWrite(tempName);
		}
		catch (Exception e)
		{
			Log.AppendException(e);
			return SaveResult.FileError;
		}

		SaveResult ret;

		try
		{
			using (stream)
				ret = converter.SaveSong(this, stream);

			if (Status.Flags.HasAllFlags(StatusFlags.MakeBackups))
				DiskWriter.MakeBackup(mangle, Status.Flags.HasAllFlags(StatusFlags.NumberedBackups) ? DiskWriterBackupMode.BackupNumbered : DiskWriterBackupMode.BackupTilde);

			File.Move(tempName, mangle, overwrite: true);
		}
		catch (IOException e)
		{
			ret = SaveResult.FileError;
			Log.AppendException(e, mangle);
		}
		catch (Exception e)
		{
			ret = SaveResult.InternalError;
			Log.AppendException(e, mangle);
		}

		switch (ret)
		{
			case SaveResult.Success:
				Status.Flags &= ~StatusFlags.SongNeedsSave;
				if (!FileName.Equals(mangle, StringComparison.InvariantCultureIgnoreCase))
					SetFileName(mangle);
				Log.Append(5, " Done");
				break;
			case SaveResult.InternalError:
			default: // ???
				Log.Append(4, " Internal error saving song");
				break;
		}

		return ret;
	}

	public SaveResult SaveSample(string fileName, string type, int num)
	{
		var format = SampleFileConverter.FindImplementation(type);

		if (format == null)
			return SaveResult.InternalError;

		return SaveSample(fileName, format, num);
	}

	public SaveResult SaveSample(string fileName, SampleFileConverter converter, int num)
	{
		var smp = Samples[num];

		if (smp == null)
			smp = new SongSample();

		string errFormat = "Error: Sample {0} NOT saved! ({1})";

		string baseName = Path.GetFileName(fileName);

		if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(baseName))
		{
			Status.FlashText(string.Format(errFormat, num, "No Filename?"));
			return SaveResult.NoFilename;
		}

		SaveResult ret = SaveResult.InternalError;

		try
		{
			using (var stream = File.OpenWrite(fileName))
				ret = converter.SaveSample(smp, stream);
		}
		catch (IOException e)
		{
			Status.FlashText(string.Format(errFormat, num, "File Error"));
			Log.AppendException(e, baseName);
			ret = SaveResult.FileError;
		}

		switch (ret)
		{
			case SaveResult.Success:
				Status.FlashText(string.Format("{0} sample saved (sample {1})", converter.Description, num));
				break;
			case SaveResult.Unsupported:
				Status.FlashText("Unsupported Data");
				break;
			case SaveResult.InternalError:
			default: // ???
				Status.FlashText(string.Format(errFormat, num, "Internal error"));
				Log.Append(4, "Internal error saving sample");
				break;
		}

		return ret;
	}

	// ------------------------------------------------------------------------

	public SaveResult SaveInstrument(string fileName, string type, int num)
	{
		var converter = InstrumentFileConverter.FindImplementation(type);

		if (converter == null)
			return SaveResult.InternalError;

		return SaveInstrument(fileName, converter, num);
	}

	public SaveResult SaveInstrument(string fileName, InstrumentFileConverter converter, int num)
	{
		var ins = Instruments[num];

		if (ins == null)
			ins = new SongInstrument(this);

		string err = "Error: Instrument %d NOT saved! (%s)";

		string baseName = Path.GetFileName(fileName);

		if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(baseName))
		{
			Status.FlashText(string.Format(err, num, "No Filename?"));
			return SaveResult.NoFilename;
		}

		SaveResult ret = SaveResult.InternalError;

		try
		{
			using (var stream = File.OpenWrite(fileName))
				ret = converter.SaveInstrument(this, ins, stream);
		}
		catch (IOException e)
		{
			Status.FlashText(string.Format(err, num, "File Error"));
			Log.AppendException(e, baseName);
			ret = SaveResult.FileError;
		}

		switch (ret)
		{
			case SaveResult.Success:
				Status.FlashText(string.Format("{0} instrument saved (instrument {1})", converter.Description, num));
				break;
			case SaveResult.Unsupported:
				Status.FlashText(string.Format(err, num, "Unsupported Data"));
				break;
			case SaveResult.InternalError:
			default: // ???
				Status.FlashText(string.Format(err, num, "Internal error"));
				Log.Append(4, "Internal error saving sample");
				break;
		}

		return ret;
	}

	public void CopySample(int n, SongSample src)
	{
		lock (AudioPlayback.LockScope())
			Samples[n] = src.Clone();
	}

	public void DestroySample(int nsmp)
	{
		var smp = GetSample(nsmp);

		if (smp == null)
			return;
		if (!smp.HasData)
			return;

		StopSample(smp);

		smp.RawData = null;
		smp.Length = 0;
		smp.Flags &= ~SampleFlags._16Bit;
	}

	// All of the sample's fields are initially zeroed except the filename (which is set to the sample's
	// basename and shouldn't be changed). A sample loader should not change anything in the sample until
	// it is sure that it can accept the file.
	// The title points to a buffer of 26 characters.

	public void ClearSample(int n)
	{
		lock (AudioPlayback.LockScope())
		{
			DestroySample(n);

			Samples[n] =
				new SongSample()
				{
					C5Speed = 8363,
					Volume = 64 * 4,
					GlobalVolume = 64,
				};
		}
	}

	public void WipeInstrument(int n)
	{
		/* wee .... */
		var instrument = Instruments[n];

		if (instrument == null)
			return;
		if (instrument.IsEmpty)
			return;

		Status.Flags |= StatusFlags.SongNeedsSave;

		CurrentSong.Instruments[n] = null;
	}

	public void FixNames()
	{
		Title = Title.Fix();

		foreach (var sample in Samples)
			if (sample != null)
			{
				sample.Name = sample.Name.Fix();
				sample.FileName = sample.FileName.Fix();
			}

		foreach (var instrument in Instruments)
			if (instrument != null)
			{
				instrument.Name = instrument.Name.Fix();
				instrument.FileName = instrument.FileName.Fix();
			}
	}

	public static Song? LoadUnchecked(string file)
	{
		string @base = Path.GetFileName(file);

		bool wasPlaying;

		// IT stops the song even if the new song can't be loaded
		if (Status.Flags.HasAllFlags(StatusFlags.PlayAfterLoad))
			wasPlaying = (AudioPlayback.Mode == AudioPlaybackMode.Playing);
		else
		{
			wasPlaying = false;
			AudioPlayback.Stop();
		}

		Log.AppendNewLine();
		Log.Append(2, "Loading {0}", @base);
		Log.AppendUnderline(@base.Length + 8);

		Song? newSong;

		try
		{
			newSong = CreateLoad(file);
		}
		catch (Exception e)
		{
			Log.AppendException(e);
			return null;
		}

		if (newSong == null)
		{
			Log.Append(4, " song failed to load");
			return null;
		}

		newSong.SetFileName(file);

		lock (AudioPlayback.LockScope())
		{
			CurrentSong = newSong;
			CurrentSong.RepeatCount = 0;

			AudioPlayback.MaxChannelsUsed = 0;

			newSong.FixNames();

			AudioPlayback.StopUnlocked(false);
			AudioPlayback.InitializeModPlug();
		}

		if (wasPlaying && Status.Flags.HasAllFlags(StatusFlags.PlayAfterLoad))
			AudioPlayback.Start();

		Page.NotifySongChangedGlobal();

		Status.Flags &= ~StatusFlags.SongNeedsSave;

		// print out some stuff
		string tid = newSong.TrackerID;

		string fmt = " {0} patterns, {1} samples, {2} instruments";

		int nSmp = newSong.Samples.Where(s => (s != null) && s.HasData).Count();
		int nIns = newSong.Instruments.Where(i => i != null).Count();

		if (!string.IsNullOrWhiteSpace(tid))
			Log.Append(5, " {0}", tid);

		if (nIns == 0)
			fmt = " {0} patterns, {1} samples"; // cut off 'instruments'

		Log.Append(5, fmt, newSong.Patterns.Count, nSmp, nIns);

		return newSong;
	}

	// 'start' indicates minimum sample/instrument to check
	public int FirstBlankSampleNumber(int start)
	{
		if (start < 1)
			start = 1;

		for (int n = start; n < Samples.Count; n++)
			if (SongSample.IsNullOrEmpty(Samples[n]))
				return n;

		return -1;
	}

	// 'start' indicates minimum sample/instrument to check
	int FirstBlankInstrumentNumber(int start)
	{
		if (start < 1)
			start = 1;

		for (int n = start; n < Instruments.Count; n++)
			if (SongInstrument.IsNullOrEmpty(Instruments[n]))
				return n;

		return -1;
	}

	public void CreateHostInstrument(int smpNumber)
	{
		var smp = GetSample(smpNumber);

		if (smp == null)
			return;

		var insNumber = AllPages.InstrumentList.CurrentInstrument;

		if (GetInstrument(smpNumber).IsEmpty)
			insNumber = smpNumber;
		else if (Status.Flags.HasAllFlags(StatusFlags.ClassicMode) || !GetInstrument(insNumber).IsEmpty)
			insNumber = FirstBlankInstrumentNumber(0);

		if (insNumber > 0)
		{
			var ins = new SongInstrument(this);

			Instruments[insNumber] = ins;

			ins.InitializeFromSample(smpNumber, smp);

			Status.FlashText("Sample assigned to Instrument " + insNumber);
		}
		else
			Status.FlashText("Error: No available Instruments!");
	}

	public void StopSample(SongSample? smp)
	{
		if (smp == null)
			return;
		if (!smp.HasData)
			return;

		for (int i = 0; i < Voices.Length; i++)
		{
			ref var v = ref Voices[i];

			if (v.Sample == smp || v.CurrentSampleData.Span == smp.Data.AsSpan())
			{
				v.Note = v.NewNote = 1;
				v.NewInstrumentNumber = 0;
				v.FadeOutVolume = 0;
				v.Flags |= ChannelFlags.KeyOff | ChannelFlags.NoteFade;
				v.Frequency = 0;
				v.Position = v.Length = 0;
				v.LoopStart = 0;
				v.LoopEnd = 0;
				v.ROfs = v.LOfs = 0;
				v.CurrentSampleData = null;
				v.Sample = null;
				v.Instrument = null;
				v.LeftVolume = v.RightVolume = 0;
				v.LeftVolumeNew = v.RightVolumeNew = 0;
				v.LeftRamp = v.RightRamp = 0;
			}
		}
	}

	public void GetPlayingSamples(int[] samples)
	{
		Array.Clear(samples);

		lock (AudioPlayback.LockScope())
		{
			int n = Math.Min(NumVoices, AudioPlayback.MaxVoices);

			while (n-- > 0)
			{
				ref var channel = ref Voices[VoiceMix[n]];

				if ((channel.Sample != null) && (channel.CurrentSampleData.Span != Memory<byte>.Empty.Span))
				{
					int s = GetSampleNumber(channel.Sample);

					if (s >= 0 && s < Constants.MaxSamples)
						samples[s] = Math.Max(samples[s], 1 + channel.Strike);
				}
				else
				{
					// no sample.
					// (when does this happen?)
				}
			}
		}
	}

	public void GetPlayingInstruments(int[] instruments)
	{
		Array.Clear(instruments);

		lock (AudioPlayback.LockScope())
		{
			int n = Math.Min(NumVoices, AudioPlayback.MaxVoices);

			while (n-- > 0)
			{
				ref var channel = ref Voices[VoiceMix[n]];

				if (channel.Instrument != null)
				{
					int ins = GetInstrumentNumber(channel.Instrument);

					if (ins > 0)
						instruments[ins] = Math.Max(instruments[ins], 1 + channel.Strike);
				}
			}
		}
	}

	public void UpdatePlayingSample(int sampleNumberChanged)
	{
		lock (AudioPlayback.LockScope())
		{
			int n = Math.Min(NumVoices, AudioPlayback.MaxVoices);

			while (n-- > 0)
			{
				ref var channel = ref Voices[VoiceMix[n]];

				if ((channel.Sample != null) && (channel.CurrentSampleData.Span != Memory<byte>.Empty.Span))
				{
					int s = Samples.IndexOf(channel.Sample);
					if (s != sampleNumberChanged) continue;

					var inst = channel.Sample;

					if (inst.Flags.HasAnyFlag(SampleFlags.PingPongSustain | SampleFlags.SustainLoop))
					{
						channel.LoopStart = inst.SustainStart;
						channel.LoopEnd = inst.SustainEnd;
					}
					else if (inst.Flags.HasAnyFlag(SampleFlags.PingPongFlag | SampleFlags.PingPongLoop | SampleFlags.Loop))
					{
						channel.LoopStart = inst.LoopStart;
						channel.LoopEnd = inst.LoopEnd;
					}

					if (inst.Flags.HasAnyFlag(SampleFlags.PingPongSustain | SampleFlags.SustainLoop
								| SampleFlags.PingPongFlag | SampleFlags.PingPongLoop | SampleFlags.Loop))
					{
						if (channel.Length != channel.LoopEnd)
							channel.Length = channel.LoopEnd;
					}

					if (channel.Length > inst.Length)
					{
						channel.CurrentSampleData = inst.Data;
						channel.Length = inst.Length;
					}

					channel.Flags &= ~(ChannelFlags.PingPongSustain
							| ChannelFlags.PingPongLoop
							| ChannelFlags.PingPongFlag
							| ChannelFlags.SustainLoop
							| ChannelFlags.Loop);

					if (inst.Flags.HasAllFlags(SampleFlags.PingPongSustain))
						channel.Flags |= ChannelFlags.PingPongSustain;
					if (inst.Flags.HasAllFlags(SampleFlags.PingPongLoop))
						channel.Flags |= ChannelFlags.PingPongLoop;
					if (inst.Flags.HasAllFlags(SampleFlags.PingPongFlag))
						channel.Flags |= ChannelFlags.PingPongFlag;
					if (inst.Flags.HasAllFlags(SampleFlags.SustainLoop))
						channel.Flags |= ChannelFlags.SustainLoop;
					if (inst.Flags.HasAllFlags(SampleFlags.Loop))
						channel.Flags |= ChannelFlags.Loop;

					channel.InstrumentVolume = inst.GlobalVolume;
				}
			}
		}
	}

	public void UpdatePlayingInstrument(int i_changed)
	{
		using (AudioPlayback.LockScope())
		{
			int n = Math.Min(NumVoices, AudioPlayback.MaxVoices);

			while (n-- > 0)
			{
				ref var channel = ref Voices[VoiceMix[n]];

				if ((channel.Instrument != null) && (channel.Instrument == Instruments[i_changed]))
				{
					InstrumentChange(ref channel, i_changed, portamento: true, instrumentColumn: 0);

					var inst = channel.Instrument;

					if (inst == null)
						continue;

					/* special cases;
						mpt doesn't do this if porta-enabled, */
					if ((inst.IFResonance & 0x80) != 0)
						channel.Resonance = inst.IFResonance & 0x7F;
					else
					{
						channel.Resonance = 0;
						channel.Flags &= ~ChannelFlags.Filter;
					}

					if ((inst.IFCutoff & 0x80) != 0)
					{
						channel.Cutoff = inst.IFCutoff & 0x7F;
						SetUpChannelFilter(ref channel, reset: false, filterModifier: 256, AudioPlayback.MixFrequency);
					}
					else
					{
						channel.Cutoff = 0x7F;

						if ((inst.IFResonance & 0x80) != 0)
							SetUpChannelFilter(ref channel, reset: false, filterModifier: 256, AudioPlayback.MixFrequency);
					}

					/* flip direction */
					channel.Flags &= ~ChannelFlags.PingPongFlag;
				}
			}
		}
	}

	public void ResetMIDIConfig()
	{
		MIDIConfig = MIDIConfiguration.GetDefault();
	}

	public void CopyMIDIConfig(MIDIConfiguration src)
	{
		MIDIConfig.CopyFrom(src);
	}

	// FIXME this function sucks
	public int GetHighestUsedChannel()
		=> Patterns.OfType<Pattern>().Max(pattern => pattern.GetHighestUsedChannel());

	/* Wrapper for MultiWriteSamples that writes to the current sample,
	and with a confirmation dialog if the sample already has data */
	public void PatternToSample(int pattern, bool split, bool bind)
	{
		if (split && bind)
		{
			Log.Append(4, "song_pattern_to_sample: internal error!");
			return;
		}

		if (pattern < 0 || pattern >= Patterns.Count)
			return;

		// this is horrid
		for (int n = 0; n < Samples.Count; n++)
		{
			if (Samples[n] is SongSample samp)
			{
				if (samp.Name[23] != 0xFF)
					continue;

				int existingPatternLink = (int)samp.Name[24];

				if (pattern == existingPatternLink)
				{
					Status.FlashText($"Pattern {pattern} already linked to sample {n}");
					return;
				}
			}
		}

		var ps = new PatternToSample();

		ps.Pattern = pattern;
		ps.Sample = AllPages.SampleList.CurrentSample;

		if (ps.Sample == 0)
			ps.Sample = 1;

		ps.Bind = bind;

		if (split)
		{
			// Nothing to confirm, as this never overwrites samples
			DiskWriter.PatternToSampleMulti(ps);
		}
		else
		{
			var samp = CurrentSong.Samples[ps.Sample];

			if ((samp == null) || samp.IsEmpty)
				DiskWriter.PatternToSampleSingle(ps);
			else
			{
				var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "This will replace the current sample.");

				dialog.ActionYes = () => DiskWriter.PatternToSampleSingle(ps);
			}
		}
	}
}
