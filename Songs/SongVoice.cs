using System;

namespace ChasmTracker.Songs;

// (TODO write decent descriptions of what the various volume
// variables are used for - are all of them *really* necessary?)
// (TODO also the majority of this is irrelevant outside of the "main" 64 channels;
// this struct should really only be holding the stuff actually needed for mixing)
public struct SongVoice
{
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
	public int Volume, Panning; // range 0-256 (?); these are the current values set for the channel
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
	public uint AutoVibratoPosition, VibratoPosition, TremoloPosition, PanbrelloPosition;
	// 16-bit members

	// these were `int', so I'm keeping them as `int'.
	//   - paper
	public int VolumeSwing, PanningSwing;
	public short ChannelPanning;

	// formally 8-bit members
	public int Note; // the note that's playing
	public NewNoteActions NewNoteAction;
	public int NewNote, NewInstrumentNumber; // ?
																			// Effect memory and handling
	public uint NCommand; // This sucks and needs to go away (dumb "flag" for arpeggio / tremor)
	public uint MemVcVolslide; // Ax Bx Cx Dx (volume column)
	public uint MemArpeggio; // Axx
	public uint MemVolslide; // Dxx
	public uint MemPitchslide; // Exx Fxx (and Gxx maybe)
	public int MemPortanote; // Gxx (synced with memPitchslide if compat gxx is set)
	public uint MemTremor; // Ixx
	public uint MemChannelVolslide; // Nxx
	public uint MemOffset; // final, combined yxx00h from Oxx and SAy
	public uint MemPanslide; // Pxx
	public uint MemRetrig; // Qxx
	public uint MemSpecial; // Sxx
	public uint MemTempo; // Txx
	public uint MemGlobalVolslide; // Wxx
	public uint NoteSlideCounter, NoteSlideSpeed, NoteSlideStep; // IMF effect
	public uint VibType, VibratoSpeed, VibratoDepth;
	public uint TremoloType, TremoloSpeed, TremoloDepth;
	public uint PanbrelloType, PanbrelloSpeed, PanbrelloDepth;
	public int TremoloDelta, PanbrelloDelta;

	public int Cutoff;
	public int Resonance;
	public int CountdownNoteDelay; // countdown: note starts when this hits zero
	public int CountdownNoteCut; // countdown: note stops when this hits zero
	public int CountdownRetrig; // countdown: note retrigs when this hits zero
	public uint CountdownTremor; // (weird) countdown + flag: see sndFx.c and sndmix.c
	public uint PatloopRow; // row number that SB0 was on
	public uint CountdownPatloop; // countdown: pattern loops back when this hits zero

	public uint RowNote, RowInstrumentNumber;
	public VolumeEffects RowVolumeEffect;
	public int RowVolparam;
	public Effects RowEffect;
	public int RowParam;
	public int ActiveMacro, LastInstrument;

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
}
