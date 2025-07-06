using System;

namespace ChasmTracker.Utility;

public static class ByteExtensions
{
	public static byte Clamp(this byte value, byte min, byte max)
		=> Math.Max(min, Math.Min(max, value));
}