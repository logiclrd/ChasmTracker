namespace ChasmTracker.Songs;

public enum VolumeEffects
{
	None             = 0,
	Volume           = 1,
	Panning          = 2,
	VolSlideUp       = 3, // C
	VolSlideDown     = 4, // D
	FineVolUp        = 5, // A
	FineVolDown      = 6, // B
	VibratoSpeed     = 7, // $ (FT2 Ax)
	VibratoDepth     = 8, // H
	PanSlideLeft     = 9, // < (FT2 Dx)
	PanSlideRight    = 10, // > (FT2 Ex)
	TonePortamento   = 11, // G
	PortaUp          = 12, // F
	PortaDown        = 13, // E
}
