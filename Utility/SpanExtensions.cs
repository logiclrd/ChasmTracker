using System;
using System.Text;

namespace ChasmTracker.Utility;

public static class SpanExtensions
{
	public static string ToStringZ(this Span<byte> array)
	{
		for (int i = 0; i < array.Length; i++)
			if (array[i] == 0)
				return Encoding.ASCII.GetString(array.Slice(0, i));

		return Encoding.ASCII.GetString(array);
	}
}
