using System;
using System.Threading.Channels;

namespace ChasmTracker.Songs;

// (TODO write decent descriptions of what the various volume
// variables are used for - are all of them *really* necessary?)
// (TODO also the majority of this is irrelevant outside of the "main" 64 channels;
// this struct should really only be holding the stuff actually needed for mixing)
public struct SongVoice
{
	public SongVoice() { }

	// First 32-bytes: Most used mixing information: don't change it
	public Array? CurrentSampleData;
	public int Position; // sample position, fixed-point -- integer part
	public int PositionFrac; // fractional part
	public int Increment; // 16.16 fixed point, how much to add to position per sample-frame of output
	public int RightVolume; // volume of the left channel
	public int LeftVolume; // volume of the right channel
	public int RightRamp; // amount to ramp the left channel
	public int LeftRamp; // amount to ramp the right channel
											 // 2nd cache line
	public int Length; // only to the end of the loop
	public ChannelFlags Flags;
	public ChannelFlags OldFlags;
	public int LoopStart; // loop or sustain, whichever is active
	public int LoopEnd;
	public int RightRampVolume; // ?
	public int LeftRampVolume; // ?
	public int Strike; // decremented to zero. this affects how long the initial hit on the playback marks lasts (bigger dot in instrument and sample list windows)

	public int FilterY1, FilterY2, FilterY3, FilterY4;
	public int FilterA0, FilterB0, FilterB1;

	public int ROfs, LOfs; // ?
	public int RampLength;
	public uint VUMeter; // moved this up -paper
											 // Information not used in the mixer
	public int RightVolumeNew, LeftVolumeNew; // ?
	public int FinalVolume; // range 0-16384 (?), accounting for sample+channel+global+etc. volumes
	public int FinalPanning; // range 0-256 (but can temporarily exceed that range during calculations)
	public int Volume = 256, Panning; // range 0-256 (?); these are the current values set for the channel
	public int CalcVolume; // calculated volume for midi macros
	public int FadeOutVolume;
	public int Frequency;
	public int C5Speed;
	public int SampleFreq; // only used on the info page (F5)
	public int PortamentoTarget;

	public SongInstrument? Instrument;
	public SongSample? Sample;

	public int VolumeEnvelopePosition;
	public int PanningEnvelopePosition;
	public int PitchEnvelopePosition;

	public int MasterChannel; // nonzero = background/NNA voice, indicates what channel it "came from"

	// TODO: As noted elsewhere, this means current channel volume.
	public int GlobalVolume;

	// FIXME: Here instrumentVolume means the value calculated from sample global volume and instrument global volume.
	//  And we miss a value for "running envelope volume" for the pageInfo
	public int InstrumentVolume;
	public int AutoVibratoDepth;
	public int AutoVibratoPosition;
	public int VibratoPosition;
	public int TremoloPosition;
	public int PanbrelloPosition;

	// 16-bit members

	// these were `int', so I'm keeping them as `int'.
	//   - paper
	public int VolumeSwing, PanningSwing;
	public short ChannelPanning;

	// formally 8-bit members
	public int Note = 1; // the note that's playing
	public NewNoteActions NewNoteAction;
	public int NewNote = 1, NewInstrumentNumber; // ?
																					 // Effect memory and handling
	public Effects NCommand; // This sucks and needs to go away (dumb "flag" for arpeggio / tremor)
	public int MemVolumeColumnVolSlide; // Ax Bx Cx Dx (volume column)
	public int MemArpeggio; // Axx
	public int MemVolSlide; // Dxx
	public int MemPitchSlide; // Exx Fxx (and Gxx maybe)
	public int MemPortaNote; // Gxx (synced with memPitchslide if compat gxx is set)
	public int MemTremor; // Ixx
	public int MemChannelVolumeSlide; // Nxx
	public int MemOffset; // final, combined yxx00h from Oxx and SAy
	public int MemPanningSlide; // Pxx
	public int MemRetrig; // Qxx
	public int MemSpecial; // Sxx
	public int MemTempo; // Txx
	public int MemGlobalVolumeSlide; // Wxx
	public int NoteSlideCounter, NoteSlideSpeed, NoteSlideStep; // IMF effect
	public VibratoType VibratoType;
	public int VibratoSpeed, VibratoDepth;
	public VibratoType TremoloType;
	public int TremoloSpeed, TremoloDepth;
	public VibratoType PanbrelloType;
	public int PanbrelloSpeed, PanbrelloDepth;
	public int TremoloDelta, PanbrelloDelta;

	public int Cutoff = 0x7F;
	public int Resonance;
	public int CountdownNoteDelay; // countdown: note starts when this hits zero
	public int CountdownNoteCut; // countdown: note stops when this hits zero
	public int CountdownRetrigger; // countdown: note retrigs when this hits zero
	public uint CountdownTremor; // (weird) countdown + flag: see sndFx.c and sndmix.c
	public int PatternLoopRow; // row number that SB0 was on
	public int CountdownPatternLoop; // countdown: pattern loops back when this hits zero

	public int RowNote, RowInstrumentNumber;
	public VolumeEffects RowVolumeEffect;
	public int RowVolumeParameter;
	public Effects RowEffect;
	public int RowParam;
	public int ActiveMacro, LastInstrumentNumber;

	public bool IsMuted
	{
		get => Flags.HasFlag(ChannelFlags.Mute);
		set
		{
			if (value)
				Flags |= ChannelFlags.Mute;
			else
				Flags &= ~ChannelFlags.Mute;
		}
	}

	public void Reset(bool always)
	{
		if (Instrument != null)
		{
			Flags |= ChannelFlags.FastVolumeRamp;

			if (always)
			{
				VolumeEnvelopePosition = 0;
				PanningEnvelopePosition = 0;
				PitchEnvelopePosition = 0;
			}
			else
			{
				/* only reset envelopes with carry off */
				if (!Instrument.Flags.HasFlag(InstrumentFlags.VolumeEnvelopeCarry))
					VolumeEnvelopePosition = 0;
				if (!Instrument.Flags.HasFlag(InstrumentFlags.PanningEnvelopeCarry))
					PanningEnvelopePosition = 0;
				if (!Instrument.Flags.HasFlag(InstrumentFlags.PitchEnvelopeCarry))
					PitchEnvelopePosition = 0;
			}
		}


		// this was migrated from csf_note_change, should it be here?
		FadeOutVolume = 65536;
	}

	public void SetInstrumentPanning(int panning)
	{
		ChannelPanning = (short)(Panning + 1);

		if (Flags.HasFlag(ChannelFlags.Surround))
			ChannelPanning |= -0x8000;

		Panning = panning;
		Flags &= ~ChannelFlags.Surround;
	}
}
