using System;

namespace ChasmTracker.Utility;

public static class Int32Extensions
{
	public static bool IsInRange(this int value, int min, int max)
		=> (value >= min) && (value <= max);

	public static int Clamp(this int value, int min, int max)
		=> Math.Max(min, Math.Min(max, value));

	public static byte ClampToByte(this int value)
		=> (byte)value.Clamp(0, 255);

	public static int Cycle(this int value, int loopBackAt)
		=> (value + 1) % loopBackAt;

	public static bool HasBitSet(this int value, int flag)
	{
		return (value & flag) == flag;
	}

	public static bool HasAnyBitSet(this int value, int bits)
	{
		return (value & bits) != 0;
	}
}
