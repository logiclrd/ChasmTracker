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

	void RunHook(string name)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			string batchFile = Path.Combine(
				Configuration.ConfigurationDirectoryDotSchism,
				name + ".bat");

			if (File.Exists(batchFile))
				Process.Start("cmd.exe", "/c \"" + batchFile + "\"");
		}
		else
		{
			string scriptFile = Path.Combine(
				Configuration.ConfigurationDirectoryDotSchism,
				name);

			if (File.Exists(scriptFile))
			{
				if (Mono.Unix.Native.Syscall.access(scriptFile, Mono.Unix.Native.AccessModes.X_OK) == 0)
					Process.Start(scriptFile);
			}
		}
	}
}
