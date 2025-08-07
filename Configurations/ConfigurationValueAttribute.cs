using System;

namespace ChasmTracker.Configurations;

[AttributeUsage(AttributeTargets.Field)]
public class ConfigurationValueAttribute : Attribute
{
	public readonly string Value;

	public ConfigurationValueAttribute(string value)
	{
		Value = value;
	}
}

