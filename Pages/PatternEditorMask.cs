using System;

namespace ChasmTracker.Pages;

[Flags]
public enum PatternEditorMask
{
	Note = 1, /* immutable */
	Instrument = 2,
	Volume = 4,
	Effect = 8,
}
