using System;

using ChasmTracker.Songs;

namespace ChasmTracker.Playback;

public interface IMixerKernel
{
	void Mix(ref SongVoice chan, Span<int> pVol);
}
