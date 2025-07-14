using System;

namespace ChasmTracker.Utility;

public static class Int64Extensions
{
	public static long Clamp(this long value, long min, long max)
		=> Math.Max(min, Math.Min(max, value));

	public static long Cycle(this long value, long loopBackAt)
		=> (value + 1) % loopBackAt;

	public static bool HasFlag(this long value, long flag)
	{
		return (value & flag) == flag;
	}
}
