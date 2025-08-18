using System;

namespace ChasmTracker.Songs;

[Flags]
public enum InstrumentFlags
{
	VolumeEnvelope         = 0x0001,
	VolumeEnvelopeSustain  = 0x0002,
	VolumeEnvelopeLoop     = 0x0004,
	PanningEnvelope        = 0x0008,
	PanningEnvelopeSustain = 0x0010,
	PanningEnvelopeLoop    = 0x0020,
	PitchEnvelope          = 0x0040,
	PitchEnvelopeSustain   = 0x0080,
	PitchEnvelopeLoop      = 0x0100,
	SetPanning             = 0x0200,
	Filter                 = 0x0400,
	VolumeEnvelopeCarry    = 0x0800,
	PanningEnvelopeCarry   = 0x1000,
	PitchEnvelopeCarry     = 0x2000,
	Mute                   = 0x4000,
}
