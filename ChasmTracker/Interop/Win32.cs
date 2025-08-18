using System;
using System.Runtime.InteropServices;

using ChasmTracker.Configurations;

namespace ChasmTracker.Interop;

public static class Win32
{
	public const int WM_COMMAND = 0x111;
	public const int WM_DROPFILES = 0x233;

	public const int IDM_FILE_NEW = 101;
	public const int IDM_FILE_LOAD = 102;
	public const int IDM_FILE_SAVE_CURRENT = 103;
	public const int IDM_FILE_SAVE_AS = 104;
	public const int IDM_FILE_EXPORT = 105;
	public const int IDM_FILE_MESSAGE_LOG = 106;
	public const int IDM_FILE_QUIT = 107;
	public const int IDM_PLAYBACK_SHOW_INFOPAGE = 201;
	public const int IDM_PLAYBACK_PLAY_SONG = 202;
	public const int IDM_PLAYBACK_PLAY_PATTERN = 203;
	public const int IDM_PLAYBACK_PLAY_FROM_ORDER = 204;
	public const int IDM_PLAYBACK_PLAY_FROM_MARK_CURSOR = 205;
	public const int IDM_PLAYBACK_STOP = 206;
	public const int IDM_PLAYBACK_CALCULATE_LENGTH = 207;
	public const int IDM_SAMPLES_SAMPLE_LIST = 301;
	public const int IDM_SAMPLES_SAMPLE_LIBRARY = 302;
	public const int IDM_SAMPLES_RELOAD_SOUNDCARD = 303;
	public const int IDM_INSTRUMENTS_INSTRUMENT_LIST = 401;
	public const int IDM_INSTRUMENTS_INSTRUMENT_LIBRARY = 402;
	public const int IDM_VIEW_HELP = 501;
	public const int IDM_VIEW_VIEW_PATTERNS = 502;
	public const int IDM_VIEW_ORDERS_PANNING = 503;
	public const int IDM_VIEW_VARIABLES = 504;
	public const int IDM_VIEW_MESSAGE_EDITOR = 505;
	public const int IDM_VIEW_TOGGLE_FULLSCREEN = 506;
	public const int IDM_SETTINGS_PREFERENCES = 601;
	public const int IDM_SETTINGS_MIDI_CONFIGURATION = 602;
	public const int IDM_SETTINGS_PALETTE_EDITOR = 603;
	public const int IDM_SETTINGS_FONT_EDITOR = 604;
	public const int IDM_SETTINGS_SYSTEM_CONFIGURATION = 605;

	public const int VK_SCROLL = 0x91;

	public enum MenuFlags
	{
		MF_ENABLED = 0x00000000,
		MF_STRING = 0x00000000,
		MF_UNCHECKED = 0x00000000,

		MF_GRAYED = 0x00000001,
		MF_DISABLED = 0x00000002,
		MF_BITMAP = 0x00000004,
		MF_CHECKED = 0x00000008,
		MF_POPUP = 0x00000010,
		MF_MENUBARBREAK = 0x00000020,
		MF_MENUBREAK = 0x00000040,
		MF_OWNERDRAW = 0x00000100,
		MF_SEPARATOR = 0x00000800,
	}

	[DllImport("shell32", CharSet = CharSet.Unicode)]
	public static extern int DragQueryFileW(IntPtr hDrop, int iFile, char[]? lpszFile, int cch);

	[DllImport("user32", CharSet = CharSet.Unicode)]
	public static extern short GetKeyState(int nVirtKey);

	[DllImport("user32")]
	public static extern IntPtr CreateMenu();
	[DllImport("user32")]
	public static extern IntPtr CreatePopupMenu();
	[DllImport("user32", CharSet = CharSet.Unicode)]
	public static extern bool AppendMenu(IntPtr hMenu, MenuFlags uFlags, IntPtr uIDNewItem, string lpNewItem);
	[DllImport("user32")]
	public static extern bool SetMenu(IntPtr hWnd, IntPtr hMenu);
	[DllImport("user32")]
	public static extern bool DrawMenuBar(IntPtr hWnd);

	static IntPtr s_menu; // HMENU
	static bool s_init;

	public static bool InitializeMenu()
	{
		/* TODO check return values here */
		s_menu = CreateMenu();

		{
			var file = CreatePopupMenu();
			AppendMenu(file, MenuFlags.MF_STRING, IDM_FILE_NEW, "&New\tCtrl+N");
			AppendMenu(file, MenuFlags.MF_STRING, IDM_FILE_LOAD, "&Load\tF9");
			AppendMenu(file, MenuFlags.MF_STRING, IDM_FILE_SAVE_CURRENT, "&Save Current\tCtrl+S");
			AppendMenu(file, MenuFlags.MF_STRING, IDM_FILE_SAVE_AS, "Save &As...\tF10");
			AppendMenu(file, MenuFlags.MF_STRING, IDM_FILE_EXPORT, "&Export...\tShift+F10");
			AppendMenu(file, MenuFlags.MF_STRING, IDM_FILE_MESSAGE_LOG, "&Message Log\tCtrl+F11");
			AppendMenu(file, MenuFlags.MF_SEPARATOR, 0, "");
			AppendMenu(file, MenuFlags.MF_STRING, IDM_FILE_QUIT, "&Quit\tCtrl+Q");
			AppendMenu(s_menu, MenuFlags.MF_POPUP, file, "&File");
		}
		{
			/* this is equivalent to the "Schism Tracker" menu on Mac OS X */
			var view = CreatePopupMenu();
			AppendMenu(view, MenuFlags.MF_STRING, IDM_VIEW_HELP, "Help\tF1");
			AppendMenu(view, MenuFlags.MF_SEPARATOR, 0, "");
			AppendMenu(view, MenuFlags.MF_STRING, IDM_VIEW_VIEW_PATTERNS, "View Patterns\tF2");
			AppendMenu(view, MenuFlags.MF_STRING, IDM_VIEW_ORDERS_PANNING, "Orders/Panning\tF11");
			AppendMenu(view, MenuFlags.MF_STRING, IDM_VIEW_VARIABLES, "Variables\tF12");
			AppendMenu(view, MenuFlags.MF_STRING, IDM_VIEW_MESSAGE_EDITOR, "Message Editor\tShift+F9");
			AppendMenu(view, MenuFlags.MF_SEPARATOR, 0, "");
			AppendMenu(view, MenuFlags.MF_STRING, IDM_VIEW_TOGGLE_FULLSCREEN, "Toggle Fullscreen\tCtrl+Alt+Return");
			AppendMenu(s_menu, MenuFlags.MF_POPUP, view, "&View");
		}
		{
			var playback = CreatePopupMenu();
			AppendMenu(playback, MenuFlags.MF_STRING, IDM_PLAYBACK_SHOW_INFOPAGE, "Show Infopage\tF5");
			AppendMenu(playback, MenuFlags.MF_STRING, IDM_PLAYBACK_PLAY_SONG, "Play Song\tCtrl+F5");
			AppendMenu(playback, MenuFlags.MF_STRING, IDM_PLAYBACK_PLAY_PATTERN, "Play Pattern\tF6");
			AppendMenu(playback, MenuFlags.MF_STRING, IDM_PLAYBACK_PLAY_FROM_ORDER, "Play from Order\tShift+F6");
			AppendMenu(playback, MenuFlags.MF_STRING, IDM_PLAYBACK_PLAY_FROM_MARK_CURSOR, "Play from Mark/Cursor\tF7");
			AppendMenu(playback, MenuFlags.MF_STRING, IDM_PLAYBACK_STOP, "Stop\tF8");
			AppendMenu(playback, MenuFlags.MF_STRING, IDM_PLAYBACK_CALCULATE_LENGTH, "Calculate Length\tCtrl+P");
			AppendMenu(s_menu, MenuFlags.MF_POPUP, playback, "&Playback");
		}
		{
			var samples = CreatePopupMenu();
			AppendMenu(samples, MenuFlags.MF_STRING, IDM_SAMPLES_SAMPLE_LIST, "&Sample List\tF3");
			AppendMenu(samples, MenuFlags.MF_STRING, IDM_SAMPLES_SAMPLE_LIBRARY, "Sample &Library\tCtrl+F3");
			AppendMenu(samples, MenuFlags.MF_STRING, IDM_SAMPLES_RELOAD_SOUNDCARD, "&Reload Soundcard\tCtrl+G");
			AppendMenu(s_menu, MenuFlags.MF_POPUP, samples, "&Samples");
		}
		{
			var instruments = CreatePopupMenu();
			AppendMenu(instruments, MenuFlags.MF_STRING, IDM_INSTRUMENTS_INSTRUMENT_LIST, "Instrument List\tF4");
			AppendMenu(instruments, MenuFlags.MF_STRING, IDM_INSTRUMENTS_INSTRUMENT_LIBRARY, "Instrument Library\tCtrl+F4");
			AppendMenu(s_menu, MenuFlags.MF_POPUP, instruments, "&Instruments");
		}
		{
			var settings = CreatePopupMenu();
			AppendMenu(settings, MenuFlags.MF_STRING, IDM_SETTINGS_PREFERENCES, "Preferences\tShift+F5");
			AppendMenu(settings, MenuFlags.MF_STRING, IDM_SETTINGS_MIDI_CONFIGURATION, "MIDI Configuration\tShift+F1");
			AppendMenu(settings, MenuFlags.MF_STRING, IDM_SETTINGS_PALETTE_EDITOR, "Palette Editor\tCtrl+F12");
			AppendMenu(settings, MenuFlags.MF_STRING, IDM_SETTINGS_FONT_EDITOR, "Font Editor\tShift+F12");
			AppendMenu(settings, MenuFlags.MF_STRING, IDM_SETTINGS_SYSTEM_CONFIGURATION, "System Configuration\tCtrl+F1");
			AppendMenu(s_menu, MenuFlags.MF_POPUP, settings, "S&ettings");
		}

		return true;
	}

	public static bool NTVerAtLeast(int major, int minor, int build)
	{
		var ntver = Environment.OSVersion.Version;

		return (ntver.Major > major)
			|| ((ntver.Major == major) && (ntver.Minor > minor))
			|| ((ntver.Major == major) && (ntver.Minor == minor) && (ntver.Build >= build));
	}

	public static void ToggleMenu(IntPtr window, bool on)
	{
		SetMenu(
			window,
			(Configuration.Video.WantMenuBar && on) ? s_menu : IntPtr.Zero);

		DrawMenuBar(window);

		if (!s_init)
		{
			s_init = true;

			// This is where we would Enable Dark Mode support on Windows 10 >= 1809
			// if (NTVerAtLeast(10, 0, 17763))
			// {
			//   ToggleDarkTitleBar(window, true);
			//   SetWindowLongPtrW(window, GWLP_WNDPROC, win32_wndproc);
			// }
			// else
			//   ToggleDarkTitleBar(window, false);
		}
	}
}
