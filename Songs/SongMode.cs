using System;

namespace ChasmTracker.Songs;

[Flags]
public enum SongMode
{
	Stopped = 0,
	Playing = 1,
	PatternLoop = 2,
	SingleStep = 4,
}
