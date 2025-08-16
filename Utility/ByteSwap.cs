using System;

namespace ChasmTracker.Utility;

public class ByteSwap
{
	static readonly ushort[] s_swap16;

	static ByteSwap()
	{
		s_swap16 = new ushort[65536];

		for (int i = 0; i < 256; i++)
			for (int j = 0; j < 256; j++)
				s_swap16[i + (j << 8)] = unchecked((ushort)((i << 8) + j));
	}

	public static ushort Swap(ushort value)
	{
		return s_swap16[value];
	}

	public static short Swap(short value)
	{
		return unchecked((short)Swap((ushort)value));
	}

	public static uint Swap(uint value)
	{
		ushort hi = unchecked((ushort)(value >> 16));
		ushort lo = unchecked((ushort)(value & 0xFFFF));

		(hi, lo) = (Swap(lo), Swap(hi));

		return unchecked(((uint)hi << 16) | lo);
	}

	public static int Swap(int value)
	{
		return unchecked((int)Swap((uint)value));
	}

	public static T Swap<T>(T value)
		where T : Enum
	{
		if (typeof(T).BaseType == typeof(int))
			return (T)(object)Swap((int)(object)value);
		if (typeof(T).BaseType == typeof(short))
			return (T)(object)Swap((short)(object)value);

		throw new NotSupportedException();
	}

	public static ulong Swap(ulong value)
	{
		uint hi = unchecked((uint)(value >> 32));
		uint lo = unchecked((uint)(value & 0xFFFF));

		(hi, lo) = (Swap(lo), Swap(hi));

		return unchecked(((ulong)hi << 32) | lo);
	}

	public static long Swap(long value)
	{
		return unchecked((long)Swap((ulong)value));
	}
}
