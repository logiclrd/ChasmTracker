using System;

namespace ChasmTracker;

using ChasmTracker.Dialogs;
using ChasmTracker.Pages;

public static class Status
{
	public static Page CurrentPage => AllPages.ByPageNumber(CurrentPageNumber);

	public static PageNumbers CurrentPageNumber;
	public static PageNumbers PreviousPageNumber;
	public static HelpTexts CurrentHelpIndex;
	public static MessageBoxTypes MessageBoxType;
	public static StatusFlags Flags;
	public static TrackerTimeDisplay TimeDisplay;
	public static TrackerVisualizationStyle VisualizationStyle;
	public static KeySym LastKeySym;

	public static KeyMod KeyMod;

	public static long LastMIDITick;
	public static byte[] LastMIDIEvent = new byte[64];
	public static int LastMIDILength;
	public static int LastMIDIRealLength; /* XXX what is this */
	public static MIDIPort? LastMIDIPort;

	public static DateTime Now;

	public static string? StatusText;
	public static DateTime StatusTextTimeout;
	public static bool StatusTextBIOSFont;

	public static NumLockHandling FixNumLockSetting;

	public static bool ShowDefaultVolumes;

	public static void FlashText(string text, bool biosFont = false)
	{
		StatusText = text;
		StatusTextBIOSFont = biosFont;
		StatusTextTimeout = DateTime.UtcNow.AddSeconds(1);

		Flags |= StatusFlags.NeedUpdate;
	}
}
