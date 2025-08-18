using System;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ValueWhenInvalidAttribute : Attribute
{
	object? _value;

	public object? Value => _value;

	public ValueWhenInvalidAttribute(object? value)
	{
		_value = value;
	}
}