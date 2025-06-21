using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileSystem;

public static class Paths
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
		{
			yield return "Chasm Tracker";
			yield return "Schism Tracker";
		}
		else
		{
			yield return ".config/chasm";
			yield return ".chasm";
			yield return ".config/schism";
			yield return ".schism";
		}
	}

	public static string GetPrettyName(string filename)
		=> Path.GetFileName(filename.TrimEnd('/', '\\')).Replace('_', ' ').Trim();

	[DllImport("c")]
	static extern int lstat(string path, ref Stat buf);

	[StructLayout(LayoutKind.Sequential)]
	struct Stat
	{
		public long st_dev;
		public IntPtr st_ino;
		public int st_mode;
		public IntPtr st_nlink;
		public int st_uid;
		public int st_gid;
		public long st_rdev;
		public long st_size;
		public IntPtr st_blksize;
		public IntPtr st_blocks;
		public IntPtr st_atim_lo;
		public IntPtr st_atim_hi;
		public IntPtr st_mtim_lo;
		public IntPtr st_mtim_hi;
		public IntPtr st_ctim_lo;
		public IntPtr st_ctim_hi;
	}

	const int S_IFMT = 0xF000;
	const int S_IFREG = 0x8000;

	public static bool IsRegularFile(string path)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return true;
		else
		{
			try
			{
				Stat buf = default;

				int result = lstat(path, ref buf);

				if (result == 0)
					return (buf.st_mode & S_IFMT) == S_IFREG;
				else
					return File.Exists(path);
			}
			catch
			{
				return File.Exists(path);
			}
		}
	}
}
