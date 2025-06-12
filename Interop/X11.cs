using System;
using System.Runtime.InteropServices;

namespace ChasmTracker.Interop;

public class X11
{
	[DllImport("libX11")]
	public static extern uint XInternAtom(IntPtr display, string atom_name, [MarshalAs(UnmanagedType.I4)] bool onlyIfExists);
	[DllImport("libX11")]
	public static extern void XChangeProperty(IntPtr display, uint w, uint property, uint type, int format, XPropMode mode, uint[] data, int nelements);
	[DllImport("libX11")]
	public static extern void XChangeProperty(IntPtr display, uint w, uint property, uint type, int format, XPropMode mode, IntPtr data, int nelements);
	[DllImport("libX11")]
	public static extern XStatus XGetWindowProperty(IntPtr display, uint w, uint property, int longOffset, int longLength, [MarshalAs(UnmanagedType.I4)] bool delete, uint reqType,
		out uint actualTypeReturn, out int actualFormatReturn, out int nItemsReturn, out int bytesAfterReturn,
		out IntPtr propReturn);
	[DllImport("libX11")]
	public static extern XStatus XSendEvent(IntPtr display, uint w, [MarshalAs(UnmanagedType.I4)] bool propagate, int eventMask, XSelectionEvent event_send);
	[DllImport("libX11")]
	public static extern int XSync(IntPtr display, [MarshalAs(UnmanagedType.I4)] bool discard);
	[DllImport("libX11")]
	public static extern int XFree(IntPtr data);

	public const int None = 0;

	public const uint XA_PRIMARY = 1;
	public const uint XA_ATOM = 4;
	public const uint XA_STRING = 31;

	public static uint DefaultRootWindow(IntPtr display)
	{
		var screenPointer = ScreenOfDisplay(display, DefaultScreen(display));

		return (uint)Marshal.ReadInt32(screenPointer, 16);
	}

	static int DefaultScreen(IntPtr display)
			=> Marshal.ReadInt32(display, 224);

	static IntPtr ScreenOfDisplay(IntPtr display, int screen)
	{
		var screensPointer = Marshal.ReadIntPtr(display, 232);

		return Marshal.ReadIntPtr(screensPointer, IntPtr.Size * screen);
	}

	/* We use our own cut-buffer for intermediate storage instead of
	 * XA_CUT_BUFFER0 because their use isn't really defined for holding UTF-8. */
	public static uint GetCutBufferType(IntPtr display, X11ClipboardMIMETypes mimeType, uint selectionType)
	{
		switch (mimeType)
		{
			case X11ClipboardMIMETypes.String:
			case X11ClipboardMIMETypes.TextPlain:
			case X11ClipboardMIMETypes.TextPlainUTF8:
			case X11ClipboardMIMETypes.Text:
				return XInternAtom(display, selectionType == XA_PRIMARY ? "SCHISM_CUTBUFFER_SELECTION" : "SCHISM_CUTBUFFER_CLIPBOARD", false);
			default:
				return XA_STRING;
		}
	}

	public static uint GetCutBufferExternalFmt(IntPtr display, X11ClipboardMIMETypes mimeType)
	{
		switch (mimeType)
		{
			case X11ClipboardMIMETypes.String:
				/* If you don't support UTF-8, you might use XA_STRING here... */
				return XInternAtom(display, "UTF8_STRING", false);
			case X11ClipboardMIMETypes.TextPlain:
				return XInternAtom(display, "text/plain", false);
			case X11ClipboardMIMETypes.TextPlainUTF8:
				return XInternAtom(display, "text/plain;charset=utf-8", false);
			case X11ClipboardMIMETypes.Text:
				return XInternAtom(display, "TEXT", false);
			default:
				return XA_STRING;
		}
	}
}
