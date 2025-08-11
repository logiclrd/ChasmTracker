using System;

namespace ChasmTracker.Utility;

public static class UInt32Extensions
{
	public static uint Clamp(this uint value, uint min, uint max)
		=> Math.Max(min, Math.Min(max, value));

	public static uint Cycle(this uint value, uint loopBackAt)
		=> (value + 1) % loopBackAt;

	public static bool HasBitSet(this uint value, uint flag)
	{
		return (value & flag) == flag;
	}

	public static bool HasBitSet(this uint value, int flag)
	{
		return (value & flag) == flag;
	}

	public static bool HasAnyBitSet(this uint value, uint bits)
	{
		return (value & bits) != 0;
	}

	public static bool HasAnyBitSet(this uint value, int bits)
	{
		return (value & unchecked((uint)bits)) != 0;
	}
}
