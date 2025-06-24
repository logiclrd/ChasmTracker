using System;

namespace ChasmTracker.MIDI;

using ChasmTracker.Songs;

public interface IMIDISink
{
	void OutRaw(Song csf, byte[] data, int len, TimeSpan pos);
}
