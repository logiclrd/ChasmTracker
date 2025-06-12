using System;

namespace ChasmTracker.Songs;

[Flags]
public enum SongNewFlags
{
	KeepPatterns = 1,
	KeepSamples = 2,
	KeepInstruments = 4,
	KeepOrderList = 8,
}