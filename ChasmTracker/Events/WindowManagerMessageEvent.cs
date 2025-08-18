using System;
using ChasmTracker.Interop;

namespace ChasmTracker.Events;

public class WindowManagerMessageEvent : Event
{
	public WMSubsystem Subsystem;

	public class WinMessage
	{
		public IntPtr hWnd;
		public int msg;
		public IntPtr wParam;
		public IntPtr lParam;
	}

	public WinMessage? Win;

	public class X11Message
	{
		public X11EventNames Type;
		public int Serial;
		public bool SendEvent;
		public IntPtr Display;
		public uint Owner;
		public uint Requestor;
		public uint Selection;
		public uint Target;
		public uint Property;
		public uint Time;
	}

	public X11Message? X11;
}
