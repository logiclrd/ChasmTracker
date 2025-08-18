using System;

namespace ChasmTracker.MIDI;

[Flags]
public enum MIDIFlags
{
	TickQuantize      = 0x00000001,
	BaseProgram1      = 0x00000002,
	RecordNoteOff     = 0x00000004,
	RecordVelocity    = 0x00000008,
	RecordAftertouch  = 0x00000010,
	CutNoteOff        = 0x00000020,
	PitchBend         = 0x00000040,
	DisableRecord     = 0x00010000,
}
