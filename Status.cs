using System;

namespace ChasmTracker;

using ChasmTracker.Configurations;
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

	public static NumLockHandling FixNumLockSetting;

	public static bool ShowDefaultVolumes;

	public static void FlashText(string text)
	{
		StatusText = text;
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
			VGAMem.DrawTextUnicodeLen(StatusText, 60, new Point(2, 9), (0, 2));
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

	static Status()
	{
		Configuration.RegisterConfigurable(new GeneralConfigurationThunk());
		Configuration.RegisterConfigurable(new VideoConfigurationThunk());
	}

	class GeneralConfigurationThunk : IConfigurable<GeneralConfiguration>
	{
		public void SaveConfiguration(GeneralConfiguration config) => Status.SaveConfiguration(config);
		public void LoadConfiguration(GeneralConfiguration config) => Status.LoadConfiguration(config);
	}

	class VideoConfigurationThunk : IConfigurable<VideoConfiguration>
	{
		public void SaveConfiguration(VideoConfiguration config) => Status.SaveConfiguration(config);
		public void LoadConfiguration(VideoConfiguration config) => Status.LoadConfiguration(config);
	}

	static void LoadConfigFlag(bool enabled, StatusFlags flag)
	{
		if (enabled)
			Flags |= flag;
		else
			Flags &= ~flag;
	}

	static void LoadConfiguration(GeneralConfiguration config)
	{
		LoadConfigFlag(!config.StopOnLoad, StatusFlags.PlayAfterLoad);
		LoadConfigFlag(config.ClassicMode, StatusFlags.ClassicMode);
		LoadConfigFlag(config.MakeBackups, StatusFlags.MakeBackups);
		LoadConfigFlag(config.NumberedBackups, StatusFlags.NumberedBackups);

		/* default to play/elapsed for invalid values */
		TimeDisplay = config.TimeDisplay.Clamp(TrackerTimeDisplay.PlayElapsed);
		/* default to oscilloscope for invalid values */
		VisualizationStyle = config.VisualizationStyle;

		LoadConfigFlag(config.MetaIsControl, StatusFlags.MetaIsControl);
		LoadConfigFlag(config.AltGrIsAlt, StatusFlags.AltGrIsAlt);

		LoadConfigFlag(config.MIDILikeTracker, StatusFlags.MIDILikeTracker);
	}

	static void LoadConfiguration(VideoConfiguration config)
	{
		LoadConfigFlag(config.LazyRedraw, StatusFlags.LazyRedraw);
	}

	static void SaveConfiguration(GeneralConfiguration config)
	{
		config.StopOnLoad = !Flags.HasAllFlags(StatusFlags.PlayAfterLoad);
		config.ClassicMode = Flags.HasAllFlags(StatusFlags.ClassicMode);
		config.MakeBackups = Flags.HasAllFlags(StatusFlags.MakeBackups);
		config.NumberedBackups = Flags.HasAllFlags(StatusFlags.NumberedBackups);

		config.TimeDisplay = TimeDisplay;
		config.VisualizationStyle = VisualizationStyle;

		config.MetaIsControl = Flags.HasAllFlags(StatusFlags.MetaIsControl);
		config.AltGrIsAlt = Flags.HasAllFlags(StatusFlags.AltGrIsAlt);

		config.MIDILikeTracker = Flags.HasAllFlags(StatusFlags.MIDILikeTracker);
	}

	static void SaveConfiguration(VideoConfiguration config)
	{
		config.LazyRedraw = Flags.HasAllFlags(StatusFlags.LazyRedraw);
	}

	/* --------------------------------------------------------------------- */

	static int LoopCount(int pos)
	{
		if ((Song.CurrentSong.RepeatCount < 1) || Flags.HasAllFlags(StatusFlags.ClassicMode))
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
