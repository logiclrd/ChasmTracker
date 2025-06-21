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
}
