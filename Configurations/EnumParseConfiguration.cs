using System;
using System.Collections.Generic;

public class EnumParseConfiguration
{
	public Dictionary<string, Enum> ValueByName = new Dictionary<string, Enum>(StringComparer.InvariantCultureIgnoreCase);
	public Dictionary<Enum, string> NameByValue = new Dictionary<Enum, string>();
	public Enum? ValueWhenNull;
	public Enum? WildcardValue;

	enum Dummy { }

	public bool TryParse(string? name, out Enum parsed)
	{
		if (name == null)
		{
			if (ValueWhenNull != null)
			{
				parsed = ValueWhenNull;
				return true;
			}
		}
		else if (ValueByName.TryGetValue(name, out var value))
		{
			parsed = value;
			return true;
		}
		else
		{
			if (WildcardValue != null)
			{
				parsed = WildcardValue;
				return true;
			}
		}

		parsed = default(Dummy);
		return false;
	}
}
