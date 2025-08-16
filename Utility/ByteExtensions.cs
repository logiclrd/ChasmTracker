using System;

namespace ChasmTracker.Utility;

public static class ByteExtensions
{
	public static byte Clamp(this byte value, byte min, byte max)
		=> Math.Max(min, Math.Min(max, value));

	public static byte Clamp(this byte value, int min, int max)
		=> Math.Max((byte)min, Math.Min((byte)max, value));

	public static bool HasBitSet(this byte value, byte flag)
	{
		return (value & flag) == flag;
	}

	public static bool HasBitSet(this byte value, int flag)
	{
		return (value & flag) == flag;
	}

	public static bool HasAnyBitSet(this byte value, byte flag)
	{
		return (value & flag) != 0;
	}
}