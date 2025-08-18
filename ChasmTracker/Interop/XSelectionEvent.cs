using System;
using System.Runtime.InteropServices;

namespace ChasmTracker.Interop;

public class XSelectionEvent
{
	public X11EventNames Type;
	public uint Serial; /* # of last request processed by server */
	[MarshalAs(UnmanagedType.I4)]
	public bool SendEvent; /* true if this came from a SendEvent request */
	public IntPtr Display; /* Display the event was read from */
	public uint Requestor;
	public uint Selection;
	public uint Target;
	public uint Property; /* ATOM or None */
	public uint Time;
}
