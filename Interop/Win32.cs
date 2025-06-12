using System;
using System.Runtime.InteropServices;

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

	[DllImport("shell32", CharSet = CharSet.Unicode)]
	public static extern int DragQueryFileW(IntPtr hDrop, int iFile, char[]? lpszFile, int cch);

	[DllImport("user32", CharSet = CharSet.Unicode)]
	public static extern short GetKeyState(int nVirtKey);
}
