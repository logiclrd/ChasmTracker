using System;

namespace ChasmTracker;

/* startup flags */
[Flags]
public enum StartupFlags
{
	Play = 1, /* -p: start playing after loading initial_song */
	Hooks = 2, /* --no-hooks: don't run startup/exit scripts */
	FontEdit = 4,
	Classic = 8,
	Network = 16,
	Headless = 32,
}
