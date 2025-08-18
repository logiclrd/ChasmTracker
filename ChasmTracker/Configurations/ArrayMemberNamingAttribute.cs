using System;

namespace ChasmTracker.Configurations;

[AttributeUsage(AttributeTargets.Field)]
public class ArrayMemberNamingAttribute : Attribute
{
	public string Prefix = "";
	public string IndexFormat = "";
	public int Length = 0;

	public ArrayMemberNamingAttribute()
	{
	}

	public ArrayMemberNamingAttribute(string prefix, string indexFormat)
	{
		Prefix = prefix;
		IndexFormat = indexFormat;
	}
}

