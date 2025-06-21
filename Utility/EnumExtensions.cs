using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace ChasmTracker.Utility;

public static class EnumExtensions
{
	public static bool HasAnyFlag<T>(this T value, T flags)
		where T : Enum
		=> ((int)(ValueType)value & (int)(ValueType)flags) != 0;

	public static string GetDescription<T>(this T value)
		where T : Enum
	{
		var field = typeof(T).GetField(value.ToString());

		if (field != null)
		{
			var descriptionAttribute = field.GetCustomAttribute<DescriptionAttribute>();

			if (descriptionAttribute != null)
				return descriptionAttribute.Description;
		}

		return value.ToString();
	}

	static ConcurrentDictionary<Type, object> s_loopBackAtValues = new ConcurrentDictionary<Type, object>();

	static T FindLoopBackAtValue<T>()
		where T : struct, Enum
	{
		return (T)(object)(1 + Enum.GetValuesAsUnderlyingType<T>().OfType<int>().Max());
	}

	public static T Cycle<T>(this T value)
		where T : struct, Enum
	{
		var loopBackAtValue = (T)s_loopBackAtValues.GetOrAdd(typeof(T), FindLoopBackAtValue<T>());

		return Cycle(value, loopBackAtValue);
	}

	public static T Cycle<T>(this T value, T loopBackAtValue)
		where T : struct, Enum
	{
		int iValue = (int)(object)value;
		int iLoopBackAt = (int)(object)loopBackAtValue;

		iValue = (iValue + 1) % iLoopBackAt;

		return (T)(object)iValue;
	}
}
