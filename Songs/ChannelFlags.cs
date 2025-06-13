using System;

namespace ChasmTracker.Songs;

[Flags]
public enum ChannelFlags
{
	_16bit              = 0x01, // 16-bit sample
	Loop                = 0x02, // looped sample
	PingPongLoop        = 0x04, // bi-directional (useless unless CHN_LOOP is also set)
	SustainLoop         = 0x08, // sample with sustain loop
	PingPongSustain     = 0x10, // bi-directional (useless unless CHN_SUSTAINLOOP is also set)
	Panning             = 0x20, // sample with default panning set
	Stereo              = 0x40, // stereo sample
	PingPongFlag        = 0x80, // when flag is on, sample is processed backwards
	Mute                = 0x100, // muted channel
	KeyOff              = 0x200, // exit sustain (note-off encountered)
	NoteFade            = 0x400, // fade note (~~~ or end of instrument envelope)
	Surround            = 0x800, // use surround channel (S91)
	// XXX What does IDO stand for??
	NoIDO               = 0x1000 // near enough to an exact multiple of c5speed that interpolation
	                             // won't be noticeable (or interpolation is disabled completely)
	HQSource            = 0x2000, // ???
	Filter              = 0x4000, // filtered output (i.e., Zxx)
	VolumeRamp          = 0x8000, // ramp volume
	Vibrato             = 0x10000, // apply vibrato
	Tremolo             = 0x20000, // apply tremolo
	//Panbrello           = 0x40000, // apply panbrello (handled elsewhere now)
	Portamento          = 0x80000, // apply portamento
	Glissando           = 0x100000, // glissando mode ("stepped" pitch slides)
	VolumeEnvelope      = 0x200000, // volume envelope is active
	PanEnvelope         = 0x400000, // pan envelope is active
	PitchEnvelope       = 0x800000, // pitch/filter envelope is active
	FastVolumeRamp      = 0x1000000, // ramp volume very fast (XXX this is a dumb flag)
	NewNote             = 0x2000000, // note was triggered, reset filter
	//Reverb              = 0x4000000,
	//Noreverb            = 0x8000000,
	NNAMute             = 0x10000000, // turn off mute, but have it reset later
	Adlib               = 0x20000000, // OPL mode
	LoopWrapped         = 0x40000000, // loop has just wrapped to the beginning

	SampleFlags =
		_16bit | Loop | PingPongLoop | SustainLoop | PingPongSustain |
		Panning | Stereo | PingPongFlag | Adlib;
}
