namespace ChasmTracker.Songs;

public enum VolumeEffects
{
	None              = 0,
	Volume            = 1,
	Panning           = 2,
	VolumeSlideUp     = 3, // C
	VolumeSlideDown   = 4, // D
	FineVolumeUp      = 5, // A
	FineVolumeDown    = 6, // B
	VibratoSpeed      = 7, // $ (FT2 Ax)
	VibratoDepth      = 8, // H
	PanningSlideLeft  = 9, // < (FT2 Dx)
	PanningSlideRight = 10, // > (FT2 Ex)
	TonePortamento    = 11, // G
	PortamentoUp      = 12, // F
	PortamentoDown    = 13, // E
}
