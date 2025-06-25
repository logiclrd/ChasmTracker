namespace ChasmTracker.MIDI;

using System.Threading;
using ChasmTracker.Songs;

public interface IMIDISink
{
	void OutRaw(Song csf, byte[] data, int len, int samplesDelay);
}
