using System;

namespace ChasmTracker.Configurations;

[AttributeUsage(AttributeTargets.Field)]
public class ConfigurationValueAttribute : Attribute
{
	public readonly string Value;
	public readonly bool NotSet;
	public readonly bool EverythingElse;

	public ConfigurationValueAttribute(string value)
	{
		Value = value;
	}

	public ConfigurationValueAttribute(bool notSet = false, bool everythingElse = false)
	{
		Value = "";
		NotSet = notSet;
		EverythingElse = everythingElse;
	}
}

