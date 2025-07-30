using System;

namespace ChasmTracker;

using ChasmTracker.Dialogs;
using ChasmTracker.Input;
using ChasmTracker.MIDI;
using ChasmTracker.Pages;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public static class Status
{
	public static Page CurrentPage => AllPages.ByPageNumber(CurrentPageNumber);

	public static PageNumbers CurrentPageNumber = PageNumbers.Blank;
	public static PageNumbers PreviousPageNumber = PageNumbers.Blank;
	public static HelpTexts CurrentHelpIndex = HelpTexts.Global;
	public static DialogTypes DialogType = DialogTypes.None;
	public static StatusFlags Flags = 0;
	public static TrackerTimeDisplay TimeDisplay = TrackerTimeDisplay.PlayElapsed;
	public static TrackerVisualizationStyle VisualizationStyle = TrackerVisualizationStyle.VUMeter;
	public static KeySym LastKeySym;

	public static MessageBoxTypes MessageBoxType
	{
		get
		{
			if (DialogType.HasAnyFlag(DialogTypes.MessageBoxTypeMask))
				return (MessageBoxTypes)(DialogType & DialogTypes.MessageBoxTypeMask);
			else
				return MessageBoxTypes.None;
		}
	}

	public static KeyMod KeyMod;

	public static DateTime LastMIDITick;
	public static byte[] LastMIDIEvent = new byte[64];
	public static int LastMIDILength;
	public static int LastMIDIRealLength; /* XXX what is this */
	public static MIDIPort? LastMIDIPort;

	public static DateTime Now;

	public static string? StatusText;
	public static DateTime StatusTextTimeoutUTC;
	public static bool StatusTextBIOSFont;

	public static NumLockHandling FixNumLockSetting;

	public static bool ShowDefaultVolumes;

	public static void FlashText(string text, bool biosFont = false)
	{
		StatusText = text;
		StatusTextBIOSFont = biosFont;
		StatusTextTimeoutUTC = DateTime.UtcNow.AddSeconds(1);

		Flags |= StatusFlags.NeedUpdate;
	}

	public static void RedrawText()
	{
		var now = DateTime.UtcNow;

		/* if there's a message set, and it's expired, clear it */
		if ((StatusText != null) && (now > StatusTextTimeoutUTC))
			StatusText = null;

		if (StatusText != null)
		{
			if (StatusTextBIOSFont)
				VGAMem.DrawTextBIOSLen(StatusText, 60, new Point(2, 9), (0, 2));
			else
				VGAMem.DrawTextLen(StatusText, 60, new Point(2, 9), (0, 2));
		}
		else
		{
			switch (AudioPlayback.Mode)
			{
				case AudioPlaybackMode.Playing:
					DrawSongPlayingStatus();
					break;
				case AudioPlaybackMode.PatternLoop:
					DrawPatternPlayingStatus();
					break;
				case AudioPlaybackMode.SingleStep:
					if (AudioPlayback.PlayingChannels > 1)
						DrawPlayingChannels();
					break;
			}
		}
	}

	/* --------------------------------------------------------------------- */

	static int LoopCount(int pos)
	{
		if ((Song.CurrentSong.RepeatCount < 1) || Flags.HasFlag(StatusFlags.ClassicMode))
			pos += VGAMem.DrawText("Playing", new Point(pos, 9), (0, 2));
		else
		{
			pos += VGAMem.DrawText("Loop: ", new Point(pos, 9), (0, 2)); ;
			pos += VGAMem.DrawText(Song.CurrentSong.RepeatCount.ToString(), new Point(pos, 9), (3, 2));
		}

		return pos;
	}

	static void DrawSongPlayingStatus()
	{
		int pattern = AudioPlayback.PlayingPattern;

		int pos = 2;

		pos = LoopCount(pos);
		pos += VGAMem.DrawText(", Order: ", new Point(pos, 9), (0, 2));
		pos += VGAMem.DrawText(AudioPlayback.CurrentOrder.ToString(), new Point(pos, 9), (3, 2));
		VGAMem.DrawCharacter('/', new Point(pos, 9), (0, 2));
		pos++;
		pos += VGAMem.DrawText(Song.CurrentSong.GetLastOrder().ToString(), new Point(pos, 9), (3, 2));
		pos += VGAMem.DrawText(", Pattern: ", new Point(pos, 9), (0, 2));
		pos += VGAMem.DrawText(pattern.ToString(), new Point(pos, 9), (3, 2));
		pos += VGAMem.DrawText(", Row: ", new Point(pos, 9), (0, 2));
		pos += VGAMem.DrawText(AudioPlayback.CurrentRow.ToString(), new Point(pos, 9), (3, 2));
		VGAMem.DrawCharacter('/', new Point(pos, 9), (0, 2));
		pos++;
		pos += VGAMem.DrawText(Song.CurrentSong.GetPatternLength(pattern).ToString(), new Point(pos, 9), (3, 2));
		VGAMem.DrawCharacter(',', new Point(pos, 9), (0, 2));
		pos++;
		VGAMem.DrawCharacter(0, new Point(pos, 9), (0, 2));
		pos++;
		pos += VGAMem.DrawText(AudioPlayback.PlayingChannels.ToString(), new Point(pos, 9), (3, 2));

		if (VGAMem.DrawTextLen(" Channels", 62 - pos, new Point(pos, 9), (0, 2)) < 9)
			VGAMem.DrawCharacter((char)16, new Point(61, 9), (1, 2));
	}

	static void DrawPatternPlayingStatus()
	{
		int pattern = AudioPlayback.PlayingPattern;

		int pos = 2;

		pos = LoopCount(pos);
		pos += VGAMem.DrawText(", Pattern: ", new Point(pos, 9), (0, 2));
		pos += VGAMem.DrawText(pattern.ToString(), new Point(pos, 9), (3, 2));
		pos += VGAMem.DrawText(", Row: ", new Point(pos, 9), (0, 2));
		pos += VGAMem.DrawText(AudioPlayback.CurrentRow.ToString(), new Point(pos, 9), (3, 2));
		VGAMem.DrawCharacter('/', new Point(pos, 9), (0, 2));
		pos++;
		pos += VGAMem.DrawText(Song.CurrentSong.GetPatternLength(pattern).ToString(), new Point(pos, 9), (3, 2));
		VGAMem.DrawCharacter(',', new Point(pos, 9), (0, 2));
		pos++;
		VGAMem.DrawCharacter(0, new Point(pos, 9), (0, 2));
		pos++;
		pos += VGAMem.DrawText(AudioPlayback.PlayingChannels.ToString(), new Point(pos, 9), (3, 2));

		if (VGAMem.DrawTextLen(" Channels", 62 - pos, new Point(pos, 9), (0, 2)) < 9)
			VGAMem.DrawCharacter((char)16, new Point(61, 9), (1, 2));
	}

	static void DrawPlayingChannels()
	{
		int pos = 2;

		pos += VGAMem.DrawText("Playing, ", new Point(2, 9), (0, 2));
		pos += VGAMem.DrawText(AudioPlayback.PlayingChannels.ToString(), new Point(pos, 9), (3, 2));
		VGAMem.DrawText(" Channels", new Point(pos, 9), (0, 2));
	}
}
