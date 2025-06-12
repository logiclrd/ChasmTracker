using System;

public static class EnumExtensions
{
	public static bool HasAnyFlag<T>(this T value, T flags)
		where T : Enum
		=> ((int)(ValueType)value & (int)(ValueType)flags) != 0;
}
