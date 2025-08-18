using System;

namespace ChasmTracker.Utility;

public static class SByteExtensions
{
	public static sbyte Clamp(this sbyte value, sbyte min, sbyte max)
		=> Math.Max(min, Math.Min(max, value));

	public static sbyte Clamp(this sbyte value, int min, int max)
		=> Math.Max((sbyte)min, Math.Min((sbyte)max, value));
}