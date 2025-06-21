using System;

namespace ChasmTracker.Configurations;

[AttributeUsage(AttributeTargets.Field)]
public class ConfigurationKeyAttribute : Attribute
{
	string _key;

	public string Key => _key;

	public ConfigurationKeyAttribute(string key)
	{
		_key = key;
	}
}
