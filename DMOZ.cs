using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker;

public static class DMOZ
{
	public static string GetDotDirectoryPath()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			string personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

			string library = Path.Combine(personal, "Library");

			return Path.Combine(library, "Application Support");
		}

		return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
	}

	public static IEnumerable<string> EnumerateDotFolders()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
		 || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			yield return "Schism Tracker";
		else
		{
			yield return ".config/schism";
			yield return ".schism";
		}
	}
}
