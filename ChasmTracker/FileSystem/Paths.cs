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

	[DllImport("c", EntryPoint = "lstat", CallingConvention = CallingConvention.Cdecl)]
	static extern int lstat_32bit(string path, ref Stat32 buf);
	[DllImport("c", EntryPoint = "lstat", CallingConvention = CallingConvention.Cdecl)]
	static extern int lstat_64bit(string path, ref Stat64 buf);

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Stat32
	{
		public long st_dev;

		ushort __pad1;

		public long st_ino;
		public int st_mode;
		public int st_nlink;
		public int st_uid;
		public int st_gid;
		public long st_rdev;

		ushort __pad2;

		public long st_size;
		public long st_blksize;
		public long st_blocks;

		public long st_atim_lo;
		public long st_atim_hi;
		public long st_mtim_lo;
		public long st_mtim_hi;
		public long st_ctim_lo;
		public long st_ctim_hi;

		long __glibc_reserved4;
		long __glibc_reserved5;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct Stat64
	{
		public long st_dev;
		public long st_ino;
		public long st_nlink;
		public int st_mode;
		public int st_uid;
		public int st_gid;

		int __pad0;

		public long st_rdev;
		public long st_size;
		public long st_blksize;
		public long st_blocks;
		public long st_atim_lo;
		public long st_atim_hi;
		public long st_mtim_lo;
		public long st_mtim_hi;
		public long st_ctim_lo;
		public long st_ctim_hi;

		long __glibc_reserved4;
		long __glibc_reserved5;
		long __glibc_reserved6;
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
				if (IntPtr.Size == 4)
				{
					Stat32 buf = default;

					int result = lstat_32bit(path, ref buf);

					if (result == 0)
						return (buf.st_mode & S_IFMT) == S_IFREG;
					else
						return File.Exists(path);
				}
				else
				{
					Stat64 buf = default;

					int result = lstat_64bit(path, ref buf);

					if (result == 0)
						return (buf.st_mode & S_IFMT) == S_IFREG;
					else
						return File.Exists(path);
				}
			}
			catch
			{
				return File.Exists(path);
			}
		}
	}
}
