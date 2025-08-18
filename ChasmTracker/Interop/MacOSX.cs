using System;
using System.Runtime.InteropServices;

namespace ChasmTracker.Interop;

public class MacOSX
{
	public enum FnKeyMode
	{
		Check = -1,

		AppleMode = 0,
		TheOtherMode = 1,
	}

	public static FnKeyMode FnKeyModeAtStartup;

	public static void Initialize()
	{
		FnKeyModeAtStartup = GetSetFnKeyMode(FnKeyMode.Check);
	}

	public static void Quit()
	{
		GetSetFnKeyMode(FnKeyModeAtStartup);
	}

	const string kMyDriversKeyboardClassName = "AppleADBKeyboard";
	const int kfnSwitchError = 200;
	const string kIOHIDFKeyModeKey = "HIDFKeyMode";

	public static void RestoreFnKeyMode()
		=> GetSetFnKeyMode(FnKeyModeAtStartup);

	public static FnKeyMode GetSetFnKeyMode(FnKeyMode setting)
	{
		// TODO: I don't know how to translate this, because it seems to
		// depend on static linking to the exported symbol `bootstrap_port`.
		return FnKeyMode.AppleMode;

		/*
		int kr; // kern_return_t
		IntPtr mp; // mach_port_t
		IntPtr so; // io_service_t
		IntPtr dp; // io_connect_t
		IntPtr it; // io_iterator_t
		IntPtr classToMatch; // CFDictionaryRef
		int res, dummy;

		kr = IOMasterPort(bootstrap_port, &mp);
		if (kr != KERN_SUCCESS) return -1;

		classToMatch = IOServiceMatching(kIOHIDSystemClass);
		if (classToMatch == NULL)
		{
			return -1;
		}
		kr = IOServiceGetMatchingServices(mp, classToMatch, &it);
		if (kr != KERN_SUCCESS) return -1;

		so = IOIteratorNext(it);
		IOObjectRelease(it);

		if (!so) return -1;

		kr = IOServiceOpen(so, mach_task_self(), kIOHIDParamConnectType, &dp);
		if (kr != KERN_SUCCESS) return -1;

		kr = IOHIDGetParameter(dp, CFSTR(kIOHIDFKeyModeKey), sizeof(res),
							&res, (IOByteCount*)&dummy);
		if (kr != KERN_SUCCESS)
		{
			IOServiceClose(dp);
			return -1;
		}

		if (setting == kfnAppleMode || setting == kfntheOtherMode)
		{
			dummy = setting;
			kr = IOHIDSetParameter(dp, CFSTR(kIOHIDFKeyModeKey),
						&dummy, sizeof(dummy));
			if (kr != KERN_SUCCESS)
			{
				IOServiceClose(dp);
				return -1;
			}
		}

		IOServiceClose(dp);
		// old setting...
		return res;
		*/
	}

	/*
	const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";

	enum IOReturn
	{
		Success = 0,
		ExclusiveAccess = -536870203,
		NotSupported = -536870201,
		Offline = -536870185,
		NotPermitted = -536870174
	}
	
	[DllImport(IOKit, EntryPoint = "IOMasterPort")]
	static extern IOReturn IOMasterPort(int bootstrapPort, out int masterPort);
	*/
}
