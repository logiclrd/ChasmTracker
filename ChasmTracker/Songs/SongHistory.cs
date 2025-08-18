using System;

namespace ChasmTracker.Songs;

public class SongHistory
{
	public bool TimeValid;

	// what time the file was opened
	public DateTime Time = DateTime.UtcNow;

	// the amount of time the file was opened for
	public TimeSpan Runtime;
}
