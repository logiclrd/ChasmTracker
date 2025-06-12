
using System;
using System.Collections.Generic;

namespace ChasmTracker.Memory;

public static class MemoryUsage
{
	static List<(string Name, Func<int> Get)> s_pressures = new List<(string Name, Func<int> Get)>();

	public static void RegisterMemoryPressure(string name, Func<int> get)
	{
		s_pressures.Add((name, get));
	}

	public static void NotifySongChanged()
	{
		// TODO
	}
}
