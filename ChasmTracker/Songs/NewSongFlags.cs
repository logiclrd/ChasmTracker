using System;

namespace ChasmTracker.Songs;

[Flags]
public enum NewSongFlags
{
	ClearAll = 0,

	KeepPatterns    = 0b0001,
	KeepSamples     = 0b0010,
	KeepInstruments = 0b0100,
	KeepOrderList   = 0b1000,
}
