using System;

using SDL3;

namespace ChasmTracker;

public class Video
{
	static DateTime s_nextUpdate;
	static VideoBackend s_backend;
	static IntPtr s_window; // SDL_Window *
	static IntPtr s_renderer; // SDL_Renderer *
	static IntPtr s_texture; // SDL_Texture *

	public static int Width => 640; // TODO
	public static int Height => 400;

	class MouseFields
	{
		public MouseCursorState Visible;
		public MouseCursorShapes Shape;
	}

	static MouseFields s_mouse = new MouseFields();

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

	public static void SetMouseCursorState(MouseCursorState vis)
	{
		switch (vis)
		{
			case MouseCursorState.CycleState:
				vis = (MouseCursorState)((((int)s_mouse.Visible) + 1) % (int)MouseCursorState.CycleState);

				goto case MouseCursorState.Disabled;
			case MouseCursorState.Disabled:
			case MouseCursorState.System:
			case MouseCursorState.Emulated:
				s_mouse.Visible = vis;

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
				s_mouse.Visible = MouseCursorState.Emulated;
				break;
		}

		s_backend.NotifyMouseCursorChanged();
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
			PublishFramebuffer();
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
}
