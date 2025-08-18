using System;

namespace ChasmTracker.MIDI;

using ChasmTracker.Songs;

public interface IMIDISink
{
	void OutRaw(Song csf, Span<byte> data, int samplesDelay);
}
