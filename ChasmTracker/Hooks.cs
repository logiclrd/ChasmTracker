using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ChasmTracker.Configurations;

namespace ChasmTracker;

public class Hooks
{
	public static void Startup()
	{
#if ENABLE_HOOKS
		RunHook("startup-hook");
#endif
	}

	public static void DiskWriterOutputComplete()
	{
#if ENABLE_HOOKS
		RunHook("diskwriter-hook");
#endif
	}

	public static void Exit()
	{
#if ENABLE_HOOKS
		RunHook("exit-hook");
#endif
	}

	/* in order of preference */
	static readonly string[] Win32BatchFileExtensions =
		[
			".bat", /* prioritize .bat for legacy */
			".cmd", /* because steel is heavier than feathers */
		];

	static void RunHook(string name)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			foreach (var extension in Win32BatchFileExtensions)
			{
				string batchFile = Path.Combine(
					Configuration.Directories.DotSchism,
					name + extension);

				if (File.Exists(batchFile))
				{
					Process.Start("cmd.exe", "/c \"" + batchFile + "\"");
					break;
				}
			}
		}
		else
		{
			string scriptFile = Path.Combine(
				Configuration.Directories.DotSchism,
				name);

			if (File.Exists(scriptFile))
			{
				if (Mono.Unix.Native.Syscall.access(scriptFile, Mono.Unix.Native.AccessModes.X_OK) == 0)
					Process.Start(scriptFile);
			}
		}
	}
}
