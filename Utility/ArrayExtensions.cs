using System;

namespace ChasmTracker.Utility;

public static class ArrayExtensions
{
	public static T[] MakeCopy<T>(this T[] array)
	{
		var ret = new T[array.Length];

		Array.Copy(array, ret, ret.Length);

		return ret;
	}

	public static Span<T> Slice<T>(this T[] array, int index)
	{
		return array.Slice(index, array.Length - index);
	}

	public static Span<T> Slice<T>(this T[] array, int index, int length)
	{
		return new Span<T>(array, index, length);
	}
}
