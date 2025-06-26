using System;

namespace ChasmTracker.Playback;

[Flags]
public enum AudioPlaybackMode
{
	Stopped = 0,
	Playing = 1,
	PatternLoop = 2,
	SingleStep = 4,
}
