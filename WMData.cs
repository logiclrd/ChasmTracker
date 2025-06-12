using System;

namespace ChasmTracker;

public class WMData
{
	public WMSubsystem Subsystem;

	public IntPtr WindowHandle;

	public IntPtr XDisplay;
	public long XWindow;
	public Action? XLock, XUnlock;
}
