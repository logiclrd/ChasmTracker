using System;

namespace ChasmTracker;

[Flags]
public enum ExitFlags
{
	Hook = 1,
	SaveConfiguration = 4,
	SDLQuit = 16,
}
