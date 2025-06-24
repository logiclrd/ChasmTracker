using System;

namespace ChasmTracker.MIDI;

[Flags]
public enum MIDIIO
{
	None = 0,

	Input = 1,
	Output = 2,
}
