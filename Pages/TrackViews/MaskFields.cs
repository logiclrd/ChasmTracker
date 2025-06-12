using System;

namespace ChasmTracker.Pages.TrackViews;

[Flags]
public enum MaskFields
{
	Note = 1, /* immutable */
	Instrument = 2,
	Volume = 4,
	Effect = 8,
}
