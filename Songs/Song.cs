using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ChasmTracker.Songs;

public class Song
{
	public static Song CurrentSong = new Song();

	public static SongMode Mode;

	public static bool IsPlaying => Mode.HasAnyFlag(SongMode.Playing | SongMode.PatternLoop);

	public static int PanSeparation;
	public static int NumVoices; // how many are currently playing. (POTENTIALLY larger than global max_voices)
	public static int MixStat; // number of channels being mixed (not really used)
	public static int BufferCount; // number of samples to mix per tick
	public static int TickCount;
	public static int FrameDelay;
	public static int RowCount; /* IMPORTANT needs to be signed */
	public static int CurrentSpeed;
	public static int CurrentTempo;
	public static int ProcessRow;
	public static int Row; // no analogue in pm.h? should be either renamed or factored out.
	public static int BreakRow;
	public static int CurrentPattern;
	public static int CurrentOrder;
	public static int ProcessOrder;
	public static int CurrentGlobalVolume;
	public static int MixingVolume;
	public static int FreqFactor; // not used -- for tweaking the song speed LP-style (interesting!)
	public static int TempoFactor; // ditto
	public static int RepeatCount; // 0 = first playback, etc. (note: set to -1 to stop instead of looping)

	public static int CurrentTick => TickCount % CurrentSpeed;

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

	public int InitialSpeed;
	public int InitialTempo;

	public int RowHighlightMajor;
	public int RowHighlightMinor;

	public readonly List<Pattern> Patterns = new List<Pattern>();
	public readonly List<SongSample?> Samples = new List<SongSample?>();
	public readonly List<SongInstrument?> Instruments = new List<SongInstrument?>();
	public readonly List<int> OrderList = new List<int>();
	public readonly SongChannel[] Channels = new SongChannel[Constants.MaxChannels];
	public readonly SongVoice[] Voices = new SongVoice[Constants.MaxVoices];

	public SongFlags Flags;

	public bool IsInstrumentMode => Flags.HasFlag(SongFlags.InstrumentMode);

	public SongSample? GetSample(int n)
	{
		if (n > Constants.MaxSamples)
			return null;

		return Samples[n];
	}

	public SongInstrument? GetInstrument(int n)
	{
		if (n >= Constants.MaxInstruments)
			return null;

		// Make a new instrument if it doesn't exist.
		if (Instruments[n] == null)
			Instruments[n] = new SongInstrument(this);

		return Instruments[n];
	}

	// ------------------------------------------------------------------------

	// calculates row of offset from passed row.
	// sets actual pattern number, row and optional pattern buffer.
	// returns length of selected patter, or 0 on error.
	// if song mode is pattern loop (MODE_PATTERN_LOOP), offset is mod calculated
	// in current pattern.
	public Pattern? GetPatternOffset(ref int patternNumber, ref int rowNumber, int offset)
	{
		if (Mode.HasFlag(SongMode.PatternLoop))
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

	public Pattern? GetPattern(int n, bool create = true)
	{
		if (n >= Patterns.Count)
			return null;

		if (create && (Patterns[n] == null))
			Patterns[n] = new Pattern();

		return Patterns[n];
	}

	public int GetPatternLength(int n)
	{
		var pattern = GetPattern(n, create: false);

		return pattern?.Rows.Count
			?? ((n >= Constants.MaxPatterns) ? 0 : Constants.DefaultPatternLength);
	}

	public SongChannel? GetChannel(int n)
	{
		if ((n < 0) || (n >= Channels.Length))
			return null;

		return Channels[n];
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

	// ------------------------------------------------------------------------------------------------------------

	SongSample? TranslateKeyboard(SongInstrument pEnv, int note, SongSample? def)
	{
		int n = pEnv.SampleMap[note - 1];

		if ((n > 0) && (n < Constants.MaxSamples))
			return Samples[n];
		else
			return def;
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
					if (pi.Flags.HasFlag(ChannelFlags.NNAMute))
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

			if (pj.Flags.HasFlag(ChannelFlags.NoteFade))
				v = v * pj.FadeOutVolume;
			else
				v <<= 16;

			if (pj.Flags.HasFlag(ChannelFlags.Loop))
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

		var penv = IsInstrumentMode ? chan.Instrument : null;

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

			if (chan.Flags.HasFlag(ChannelFlags.Adlib))
			{
				//Do this only if really an adlib chan. Important!
				// TODO
				//OPL_NoteOff(csf, nchan);
				//OPL_Touch(csf, nchan, 0);
			}

			// TODO
			//GM_KeyOff(csf, nchan);
			//GM_Touch(csf, nchan, 0);
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
					data = sample.Flags.HasFlag(SampleFlags._16Bit) ? sample.Data16 : sample.Data8;
			}
			else /* OpenMPT test case emptyslot.it */
				return;
		}

		if (penv == null)
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
			switch (p.Instrument?.DuplicateCheckTypes)
			{
				case DuplicateCheckTypes.Note:
					applyDNA = (SongNote.IsNote(note) && (p.Note == note) && (instrument == p.Instrument));
					break;
				case DuplicateCheckTypes.Sample:
					applyDNA = ((data != null) && (data == p.CurrentSampleData) && (instrument == p.Instrument));
					break;
				case DuplicateCheckTypes.Instrument:
					applyDNA = (instrument == p.Instrument);
					break;
			}

			// Duplicate Note Action
			if (applyDNA)
			{
				switch (p.Instrument?.DuplicateCheckActions)
				{
					case DuplicateCheckActions.NoteCut:
						// TODO: fx_key_off(csf, i);
						p.Volume = 0;
						if (chan.Flags.HasFlag(ChannelFlags.Adlib))
						{
							//Do this only if really an adlib chan. Important!
							//
							// This isn't very useful really since we can't save
							// Adlib songs with instruments anyway, but whatever.
							// TODO
							//OPL_NoteOff(csf, nchan);
							//OPL_Touch(csf, nchan, 0);
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
			// NOTE_OFF is a completely arbitrary choice - this could be anything above NOTE_LAST
			chan.NewNote = SpecialNotes.NoteOff;

			switch (note)
			{
				case SpecialNotes.NoteOff:
					// TODO: fx_key_off(csf, nchan);
					if (!porta && Flags.HasFlag(SongFlags.ITOldEffects) && (chan.RowInstrumentNumber != 0))
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
				chan.Flags = (chan.Flags & ~ChannelFlags.SampleFlags) | (pIns.Flags & ChannelFlags.SampleFlags);

				if (chan.Flags.HasFlag(ChannelFlags.SustainLoop))
				{
					chan.LoopStart = pIns.SustainStart;
					chan.LoopEnd = pIns.SustainEnd;
					chan.Flags &= ~ChannelFlags.PingPongFlag;
					chan.Flags |= ChannelFlags.Loop;

					if (chan.Flags.HasFlag(ChannelFlags.PingPongSustain)) chan.Flags |= ChannelFlags.PingPongLoop;
					if (chan.Length > chan.LoopEnd) chan.Length = chan.LoopEnd;
				}
				else if (chan.Flags.HasFlag(ChannelFlags.Loop))
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

		if ((pEnv != null) && pEnv.Flags.HasFlag(InstrumentFlags.SetPanning))
			SetInstrumentPanning(chan, pEnv!.Panning);
		else if (pIns.Flags.HasFlag(SampleFlags.Panning))
			SetInstrumentPanning(chan, pIns.Panning);

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
			Envelope.Reset(chan, 0);
		}

		/* OpenMPT test cases Off-Porta.it, Off-Porta-CompatGxx.it */
		if (!(porta && (!Flags.HasFlag(SongFlags.CompatibleGXX) || (chan.RowInstrumentNumber == 0))))
			chan.Flags &= ~ChannelFlags.KeyOff;

		// Enable Ramping
		if (!porta) {
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

	// ------------------------------------------------------------------------------------------------------------

	/* These return the channel that was used for the note. */

	/* **** chan ranges from 1 to MAX_CHANNELS   */
	static int KeyDownEx(int samp, int ins, int note, int vol, int chan, Effects effect, int param)
	{
		int midiNote = note; /* note gets overwritten, possibly NOTE_NONE */

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
		AudioPlayback.LockAudio();

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
				samp = c.LastInstrument;
			else if (samp == KeyJazz.FakeInstrument)
				samp = 0; // dumb hack

			if (ins == 0)
				ins = c.LastInstrument;
			else if (ins == KeyJazz.FakeInstrument)
				ins = 0; // dumb hack

			c.LastInstrument = insMode ? ins : samp;

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
			if (c.Flags.HasFlag(ChannelFlags.Adlib))
			{
				// TODO:
				//OPL_NoteOff(current_song, chanInternal);
				//OPL_Patch(current_song, chanInternal, s->adlib_bytes);
			}

			c.Flags = ((ChannelFlags)s.Flags & ChannelFlags.SampleFlags) | (c.Flags & ChannelFlags.Mute);

			if (c.IsMuted)
				c.Flags |= ChannelFlags.NNAMute;

			c.Cutoff = 0x7f;
			c.Resonance = 0;

			if (i != null)
			{
				c.Instrument = i;

				if (!i.Flags.HasFlag(InstrumentFlags.VolumeEnvelopeCarry)) c.VolumeEnvelopePosition = 0;
				if (!i.Flags.HasFlag(InstrumentFlags.PanningEnvelopeCarry)) c.PanningEnvelopePosition = 0;
				if (!i.Flags.HasFlag(InstrumentFlags.PitchEnvelopeCarry)) c.PitchEnvelopePosition = 0;
				if (i.Flags.HasFlag(InstrumentFlags.VolumeEnvelope)) c.Flags |= ChannelFlags.VolumeEnvelope;
				if (i.Flags.HasFlag(InstrumentFlags.PanningEnvelope)) c.Flags |= ChannelFlags.PanningEnvelope;
				if (i.Flags.HasFlag(InstrumentFlags.PitchEnvelope)) c.Flags |= ChannelFlags.PitchEnvelope;

				i.IsPlayed = true;

				if (Status.Flags.HasFlag(StatusFlags.MIDILikeTracker))
				{
					if (i.MIDIChannelMask != 0)
					{
						// TODO:
						//GM_KeyOff(current_song, chanInternal);
						//GM_DPatch(current_song, chanInternal, i->midi_program, i->midi_bank, i->midi_channel_mask);
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

			// csf_note_change copies stuff from c->ptr_sample as long as c->length is zero
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
			if (c.Flags.HasFlag(ChannelFlags.Surround))
				c.ChannelPanning = (short)(c.ChannelPanning | 0x8000);

			c.Panning = s.Flags.HasFlag(SampleFlags.Panning) ? s.Panning : 128;
			if (i != null)
				c.Panning = i.Flags.HasFlag(InstrumentFlags.SetPanning) ? i.Panning : 128;
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

		if (!Status.Flags.HasFlag(StatusFlags.MIDILikeTracker) && (i != null))
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
		if (CurrentSong.Flags.HasFlag(SongFlags.EndReached))
		{
			CurrentSong.Flags &= ~SongFlags.EndReached;
			CurrentSong.Flags |= SongFlags.Paused;
		}

		AudioPlayback.UnlockAudio();

		return chan;
	}

	public static int KeyDown(int samp, int ins, int note, int vol, int chan)
	{
		return KeyDownEx(samp, ins, note, vol, chan, Effects.Panning, 0x80);
	}

	public static int KeyRecord(int samp, int ins, int note, int vol, int chan, Effects effect, int param)
	{
		return KeyDownEx(samp, ins, note, vol, chan, effect, param);
	}

	public static int KeyUp(int samp, int ins, int note)
	{
		int chan = KeyJazz.GetLastChannelForNote(note);

		if (chan == 0)
		{
			// could not find channel, drop.
			return -1;
		}

		return KeyUp(samp, ins, note, chan);
	}

	public static int KeyUp(int samp, int ins, int note, int chan)
	{
		if (KeyJazz.GetLastNoteInChannel(chan) != note)
			return -1;

		KeyJazz.UnlinkNoteAndChannel(note, chan);

		return KeyDownEx(samp, ins, SpecialNotes.NoteOff, KeyJazz.DefaultVolume, chan, Effects.None, 0);
	}
}

