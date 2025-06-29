using System;

using SDL3;

namespace ChasmTracker;

using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class Video
{
	static DateTime s_nextUpdate;
	static VideoBackend s_backend = new SDLVideoBackend();
	static IntPtr s_window; // SDL_Window *
	static IntPtr s_renderer; // SDL_Renderer *
	static IntPtr s_texture; // SDL_Texture *

	static int[] s_tcBGR32 = new int[256];

	public static int Width => 640; // TODO
	public static int Height => 400;

	public class MouseFields
	{
		public MouseCursorState Visible;
		public MouseCursorShapes Shape;
	}

	public static readonly MouseFields Mouse =
		new MouseFields()
		{
			Visible = MouseCursorState.Emulated,
			Shape = MouseCursorShapes.Arrow,
		};

	public static bool Startup()
	{
		return s_backend.Initialize() && s_backend.Startup();
	}

	public static string DriverName => s_backend.DriverName ?? "(null)";

	public static void Report()
	{
		Log.AppendNewLine();

		Log.Append(2, "Video initialized");
		Log.AppendUnderline(17);

		s_backend.Report();
	}

	public static void ToggleScreenSaver(bool enabled)
	{
		s_backend.ToggleScreenSaver(enabled);
	}

	public static void ToggleDisplayFullscreen()
	{
		Fullscreen(null);
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public static void Fullscreen(bool? newFSFlag = null)
	{
		s_backend.Fullscreen(newFSFlag);
	}

	public static bool IsFullScreen()
	{
		return s_backend.IsFullScreen();
	}

	public static bool IsInputGrabbed()
	{
		return s_backend.IsInputGrabbed();
	}

	public static void SetInputGrabbed(bool newValue)
	{
		s_backend.SetInputGrabbed(newValue);
	}

	public static void SetMouseCursor(MouseCursorMode cursor)
	{
		// TODO
	}

	public static void SetMouseCursorState(MouseCursorState vis)
	{
		switch (vis)
		{
			case MouseCursorState.CycleState:
				vis = (MouseCursorState)((((int)Mouse.Visible) + 1) % (int)MouseCursorState.CycleState);

				goto case MouseCursorState.Disabled;
			case MouseCursorState.Disabled:
			case MouseCursorState.System:
			case MouseCursorState.Emulated:
				Mouse.Visible = vis;

				switch (vis)
				{
					case MouseCursorState.Disabled: Status.FlashText("Mouse disabled"); break;
					case MouseCursorState.System: Status.FlashText("Hardware mouse cursor enabled"); break;
					case MouseCursorState.Emulated: Status.FlashText("Software mouse cursor enabled"); break;
				}

				break;
			case MouseCursorState.ResetState:
				break;
			default:
				Mouse.Visible = MouseCursorState.Emulated;
				break;
		}

		s_backend.NotifyMouseCursorChanged();
	}

	public static void Refresh()
	{
		VGAMem.Flip();
		VGAMem.Clear();
	}

	public static void CheckUpdate()
	{
		var now = DateTime.UtcNow;

		if (s_backend.IsVisible() && Status.Flags.HasFlag(StatusFlags.NeedUpdate))
		{
			Status.Flags &= ~StatusFlags.NeedUpdate;

			if (!s_backend.IsFocused() && Status.Flags.HasFlag(StatusFlags.LazyRedraw))
			{
				if (now < s_nextUpdate)
					return;

				s_nextUpdate = now.AddMilliseconds(500);
			}
			else if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive) || Status.Flags.HasFlag(StatusFlags.DiskWriterActiveForPattern))
			{
				if (now < s_nextUpdate)
					return;

				s_nextUpdate = now.AddMilliseconds(100);
			}

			RedrawScreen();
			Refresh();
			s_backend.Blit();
		}
		else if (Status.Flags.HasFlag(StatusFlags.SoftwareMouseMoved))
		{
			s_backend.Blit();
			Status.Flags &= ~StatusFlags.SoftwareMouseMoved;
		}
	}

	public static void RedrawScreen()
	{
		// TODO
	}

	public static WMData? GetWMData()
	{
		var wprops = SDL.GetWindowProperties(s_window);

		if (wprops == 0)
			return null;

		var wmData = new WMData();

		switch (SDL.GetCurrentVideoDriver())
		{
			case "windows":
			{
				wmData.Subsystem = WMSubsystem.Windows;
				wmData.WindowHandle = SDL.GetPointerProperty(wprops, SDL.Props.WindowWin32HWNDPointer, IntPtr.Zero);

				if (wmData.WindowHandle != IntPtr.Zero)
					return wmData;

				break;
			}
			case "x11":
			{
				wmData.Subsystem = WMSubsystem.X11;
				wmData.XDisplay = SDL.GetPointerProperty(wprops, SDL.Props.WindowX11DisplayPointer, IntPtr.Zero);
				wmData.XWindow = SDL.GetNumberProperty(wprops, SDL.Props.WindowX11WindowNumber, 0);
				wmData.XLock = null;
				wmData.XUnlock = null;

				if ((wmData.XDisplay != IntPtr.Zero) && (wmData.XWindow != 0))
					return wmData;

				break;
			}

		}

		// maybe the real WM data was the friends we made along the way
		return null;
	}

	public static void WarpMouse(double x, double y)
	{
		s_backend.WarpMouse(new Point((int)x, (int)y));
	}

	// was: video_colors
	public static void SetPalette(Palette palette)
	{
		GenerateColours(
			palette,
			(idx, rgb) => s_tcBGR32[idx] = rgb | unchecked((int)0xFF000000));

		s_backend.SetPalette(s_tcBGR32);
	}

	/* calls back to a function receiving all the colors :) */
	// was: video_colors_iterate
	public static void GenerateColours(Palette palette, Action<int, int> fun)
	{
		/* this handles all of the ACTUAL color stuff, and the callback handles the backend-specific stuff */

		/* make our "base" space */
		for (int i = 0; i < 16; i++)
		{
			int rgb =
				(palette[i, 0] << 0) |
				(palette[i, 1] << 8) |
				(palette[i, 2] << 16);

			fun(i, rgb);
		}

		/* make our "gradient" space; this is used exclusively for the waterfall page (Alt-F12) */
		int[] lastmap = new[] { 0, 1, 2, 3, 5 };

		for (int i = 0; i < 128; i++)
		{
			int p = lastmap[i >> 5];

			byte r = unchecked((byte)((palette[p].Red + (palette[p + 1].Red - palette[p].Red) * (i & 0x1F)) / 0x20));
			byte g = unchecked((byte)((palette[p].Green + (palette[p + 1].Green - palette[p].Green) * (i & 0x1F)) / 0x20));
			byte b = unchecked((byte)((palette[p].Blue + (palette[p + 1].Blue - palette[p].Blue) * (i & 0x1F)) / 0x20));

			int rgb =
				(r << 0) |
				(g << 8) |
				(b << 16);

			fun(i + 128, rgb);
		}
	}
}
