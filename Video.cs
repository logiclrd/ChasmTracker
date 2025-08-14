using System;

namespace ChasmTracker;

using ChasmTracker.Configurations;
using ChasmTracker.Input;
using ChasmTracker.Pages;
using ChasmTracker.Utility;
using ChasmTracker.SDLBackends;
using ChasmTracker.VGA;

public static class Video
{
	static DateTime s_nextUpdate;
	static VideoBackend s_backend = new SDLVideoBackend();

	static ChannelData s_tcBGR32 = new ChannelData();

	public static ref ChannelData TC_BGR32 => ref s_tcBGR32;

	public static int Width => s_backend.Width;
	public static int Height => s_backend.Height;

	public class MouseFields
	{
		public MouseCursorMode Visible;
		public MouseCursorShapes Shape;
	}

	public static readonly MouseFields Mouse =
		new MouseFields()
		{
			Visible = MouseCursorMode.Emulated,
			Shape = MouseCursorShapes.Arrow,
		};

	static bool s_started = false;

	public static bool Startup()
	{
		if (!s_started)
			return s_started = (s_backend.Initialize() && s_backend.Startup());
		else
			return true;
	}

	public static void ShutDown()
	{
		if (s_started)
		{
			s_backend.Shutdown();
			s_backend.Quit();

			s_started = false;
		}
	}

	public static string DriverName => s_backend.DriverName ?? "(null)";

	public static void Report()
	{
		Log.AppendNewLine();

		Log.Append(2, "Video initialized");
		Log.AppendUnderline(17);

		s_backend.Report();
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

	public static bool IsFullScreen
		=> s_backend.IsFullScreen;

	public static void SetHardware(bool newHWFlag)
	{
		s_backend.SetHardware(newHWFlag);
	}

	public static void SetUp(VideoInterpolationMode interpolation)
	{
		Configuration.Video.Interpolation = interpolation;
		s_backend.SetUp(interpolation);
	}

	public static bool IsFocused => s_backend.IsFocused;
	public static bool IsVisible => s_backend.IsVisible;
	public static bool IsWindowManagerAvailable => s_backend.IsWindowManagerAvailable;
	public static bool IsHardware => s_backend.IsHardware;

	/* -------------------------------------------------------- */

	public static void Resize(Size newSize) => s_backend.Resize(newSize);

	/* -------------------------------------------------------- */

	public static bool IsScreenSaverEnabled => s_backend.IsScreenSaverEnabled;
	public static void ToggleScreenSaver(bool enabled) => s_backend.ToggleScreenSaver(enabled);

	/* -------------------------------------------------------- */
	/* coordinate translation */

	public static Point Translate(Point v) => s_backend.Translate(v);
	public static Point GetLogicalCoordinates(Point p) => s_backend.GetLogicalCoordinates(p);

	/* -------------------------------------------------------- */
	/* input grab */

	public static bool IsInputGrabbed() => s_backend.IsInputGrabbed;
	public static void SetInputGrabbed(bool newValue) => s_backend.SetInputGrabbed(newValue);

	/* -------------------------------------------------------- */
	/* menu toggling */

	public static bool HaveMenu => s_backend.HaveMenu;
	public static void ToggleMenu(bool on) => s_backend.ToggleMenu(on);

	/* -------------------------------------------------------- */

	public static void Blit() => s_backend.Blit();

	/* -------------------------------------------------------- */

	public static void SetMouseCursor(MouseCursorMode vis)
	{
		switch (vis)
		{
			case MouseCursorMode.CycleState:
				vis = (MouseCursorMode)((((int)Mouse.Visible) + 1) % (int)MouseCursorMode.CycleState);

				goto case MouseCursorMode.Disabled;
			case MouseCursorMode.Disabled:
			case MouseCursorMode.System:
			case MouseCursorMode.Emulated:
				Mouse.Visible = vis;

				switch (vis)
				{
					case MouseCursorMode.Disabled: Status.FlashText("Mouse disabled"); break;
					case MouseCursorMode.System: Status.FlashText("Hardware mouse cursor enabled"); break;
					case MouseCursorMode.Emulated: Status.FlashText("Software mouse cursor enabled"); break;
				}

				break;
			case MouseCursorMode.ResetState:
				break;
			default:
				Mouse.Visible = MouseCursorMode.Emulated;
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

		if (s_backend.IsVisible && Status.Flags.HasFlag(StatusFlags.NeedUpdate))
		{
			Status.Flags &= ~StatusFlags.NeedUpdate;

			if (!s_backend.IsFocused && Status.Flags.HasFlag(StatusFlags.LazyRedraw))
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

			Page.RedrawScreen();
			Refresh();
			s_backend.Blit();
		}
		else if (Status.Flags.HasFlag(StatusFlags.SoftwareMouseMoved))
		{
			s_backend.Blit();
			Status.Flags &= ~StatusFlags.SoftwareMouseMoved;
		}
	}

	public static WMData? GetWMData() => s_backend.GetWMData();

	public static void ShowCursor(bool enabled) => s_backend.ShowCursor(enabled);

	public static void WarpMouse(double x, double y)
	{
		s_backend.WarpMouse(new Point((int)x, (int)y));
	}

	// was: video_colors
	public static void SetPalette(Palette palette)
	{
		GenerateColours(
			palette,
			(idx, rgb) => s_tcBGR32[idx] = rgb | 0xFF000000);

		s_backend.SetPalette(ref s_tcBGR32);
	}

	/* calls back to a function receiving all the colors :) */
	// was: video_colors_iterate
	public static void GenerateColours(Palette palette, Action<int, uint> fun)
	{
		/* this handles all of the ACTUAL color stuff, and the callback handles the backend-specific stuff */

		/* VGA palettes use components from 0..63, but the RGBA pixel colours we're generating need 0..255 */
		int[,] scaled = new int[16, 3];

		for (int i=0; i < 16; i++)
			for (int c=0; c < 3; c++)
				scaled[i, c] = (palette[i, c] << 2) | (palette[i, c] >> 6);

		/* make our "base" space */
		for (int i = 0; i < 16; i++)
		{
			uint rgb = unchecked((uint)(
				(scaled[i, 0] << 16) |
				(scaled[i, 1] <<  8) |
				(scaled[i, 2] <<  0)));

			fun(i, rgb);
		}

		/* make our "gradient" space; this is used exclusively for the waterfall page (Alt-F12) */
		int[] lastmap = new[] { 0, 1, 2, 3, 5 };

		for (int i = 0; i < 128; i++)
		{
			int p = lastmap[i >> 5];

			byte r = unchecked((byte)((scaled[p, 0] + (scaled[p + 1, 0] - scaled[p, 0]) * (i & 0x1F)) / 0x20));
			byte g = unchecked((byte)((scaled[p, 1] + (scaled[p + 1, 1] - scaled[p, 1]) * (i & 0x1F)) / 0x20));
			byte b = unchecked((byte)((scaled[p, 2] + (scaled[p + 1, 2] - scaled[p, 2]) * (i & 0x1F)) / 0x20));

			uint rgb = unchecked((uint)(
				(r << 16) |
				(g << 8) |
				(b << 0)));

			fun(i + 128, rgb);
		}
	}
}
