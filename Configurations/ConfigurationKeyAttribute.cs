using System;

namespace ChasmTracker.Configurations;

[AttributeUsage(AttributeTargets.Field)]
public class ConfigurationKeyAttribute : Attribute
{
	string _key;
	int _firstIndex;

	public string Key => _key;

	public int FirstIndex
	{
		get => _firstIndex;
		set => _firstIndex = value;
	}

	public ConfigurationKeyAttribute(string key)
	{
		_key = key;
	}
}
