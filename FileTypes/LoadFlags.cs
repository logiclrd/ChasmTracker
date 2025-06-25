using System;

namespace ChasmTracker.FileTypes;

[Flags]
public enum LoadFlags
{
	None,

	NoSamples = 1,
	NoPatterns = 2,
}
