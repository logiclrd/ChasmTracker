using System;

namespace ChasmTracker;

public static class Int32Extensions
{
	public static int Clamp(this int value, int min, int max)
		=> Math.Max(min, Math.Min(max, value));

	public static int Cycle(this int value, int loopBackAt)
		=> (value + 1) % loopBackAt;
}
