using System;

namespace ChasmTracker.Utility;

public static class Int16Extensions
{
	public static short Clamp(this short value, short min, short max)
		=> Math.Max(min, Math.Min(max, value));

	public static short Cycle(this short value, short loopBackAt)
		=> unchecked((short)((value + 1) % loopBackAt));

	public static bool HasBitSet(this short value, short flag)
	{
		return (value & flag) == flag;
	}
}
