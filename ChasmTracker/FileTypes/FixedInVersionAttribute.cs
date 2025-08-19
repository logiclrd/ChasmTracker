using System;

namespace ChasmTracker.FileTypes;

public class FixedInVersionAttribute : Attribute
{
	public readonly uint Version;

	public FixedInVersionAttribute(int year, int month, int day)
	{
		Version = ChasmTracker.Version.MKTime(new DateTime(year, month, day));
	}
}
