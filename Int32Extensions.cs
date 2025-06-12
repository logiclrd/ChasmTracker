using System;

namespace ChasmTracker;

public static class Int32Extensions
{
	public static int Clamp(this int value, int min, int max)
		=> Math.Max(min, Math.Min(max, value));
}
