namespace ChasmTracker.Songs;

public enum Effects
{
	None                 = 0, // .
	Arpeggio             = 1, // J
	PortamentoUp         = 2, // F
	PortamentoDown       = 3, // E
	TonePortamento       = 4, // G
	Vibrato              = 5, // H
	TonePortamentoVolume = 6, // L
	VibratoVolume        = 7, // K
	Tremolo              = 8, // R
	Panning              = 9, // X
	Offset               = 10, // O
	VolumeSlide          = 11, // D
	PositionJump         = 12, // B
	Volume               = 13, // ! (FT2/IMF Cxx)
	PatternBreak         = 14, // C
	Retrigger            = 15, // Q
	Speed                = 16, // A
	Tempo                = 17, // T
	Tremor               = 18, // I
	Special              = 20, // S
	ChannelVolume        = 21, // M
	ChannelVolumeSlide   = 22, // N
	GlobalVolume         = 23, // V
	GlobalVolumeSlide    = 24, // W
	KeyOff               = 25, // $ (FT2 Kxx)
	FineVibrato          = 26, // U
	Panbrello            = 27, // Y
	PanningSlide         = 29, // P
	SetEnvelopePosition  = 30, // & (FT2 Lxx)
	MIDI                 = 31, // Z
	NoteSlideUp          = 32, // ( (IMF Gxy)
	NoteSlideDown        = 33, // ) (IMF Hxy)

	Unimplemented // no-op, displayed as "?"
}