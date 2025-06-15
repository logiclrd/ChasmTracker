using System;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Reflection;

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
}
