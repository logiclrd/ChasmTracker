using System;
using System.Collections.Generic;

namespace ChasmTracker.Utility;

public static class ListExtensions
{
	public static T? FindPreviousWithLoop<T>(this IList<T> list, int startIndex, Predicate<T> predicate)
	{
		for (int i = startIndex - 1; i >= 0; i--)
			if (predicate(list[i]))
				return list[i];

		for (int i = list.Count - 1; i > startIndex; i--)
			if (predicate(list[i]))
				return list[i];

		return default;
	}

	public static T? FindNextWithLoop<T>(this IList<T> list, int startIndex, Predicate<T> predicate)
	{
		for (int i = startIndex + 1; i < list.Count; i++)
			if (predicate(list[i]))
				return list[i];

		for (int i = 0; i < startIndex - 1; i++)
			if (predicate(list[i]))
				return list[i];

		return default;
	}
}
