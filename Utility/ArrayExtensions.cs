using System;
using System.Text;

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

	public static Span<T> Segment<T>(this T[] array, int index)
	{
		return array.Segment(index, array.Length - index);
	}

	public static ArraySegment<T> Segment<T>(this T[] array, int index, int length)
	{
		return new ArraySegment<T>(array, index, length);
	}

	public static bool Contains<T>(this T[] array, T element)
	{
		return Array.IndexOf(array, element) >= 0;
	}

	public static string ToStringZ(this byte[]? array, Encoding? encoding = null)
		=> array.ToStringZ(array?.Length ?? 0, encoding);

	public static string ToStringZ(this byte[]? array, int length, Encoding? encoding = null)
		=> array.ToStringZ(0, length, encoding);

	public static string ToStringZ(this byte[]? array, int offset, int length, Encoding? encoding = null)
	{
		if (array == null)
			return "";

		encoding ??= Encoding.ASCII;

		for (int i = 0; i < length; i++)
			if (array[i] == 0)
				return encoding.GetString(array, offset, i);

		return encoding.GetString(array, offset, length);
	}
}
