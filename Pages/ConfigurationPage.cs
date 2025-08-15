namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.MIDI;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class ConfigurationPage : Page
{
	ThumbBarWidget thumbBarChannelLimit;
	NumberEntryWidget numberEntryMixingRate;
	MenuToggleWidget menuToggleSampleSize;
	MenuToggleWidget menuToggleOutputChannels;
	MenuToggleWidget menuToggleVisualizationStyle;
	ToggleWidget toggleClassicMode;
	MenuToggleWidget menuToggleAccidentalsMode;
	MenuToggleWidget menuToggleTimeDisplay;
	MenuToggleWidget menuToggleMIDIMode;
	ToggleButtonWidget toggleButtonVideoNearest;
	ToggleButtonWidget toggleButtonVideoLinear;
	ToggleButtonWidget toggleButtonVideoBest;
	ToggleButtonWidget toggleButtonRenderingHardware;
	ToggleButtonWidget toggleButtonRenderingSoftware;
	ToggleButtonWidget toggleButtonFullScreenYes;
	ToggleButtonWidget toggleButtonFullScreenNo;
	ToggleButtonWidget? toggleButtonMenuBarYes;
	ToggleButtonWidget? toggleButtonMenuBarNo;

	public ConfigurationPage()
		: base(PageNumbers.Config, "System Configuration (Ctrl-F1)", HelpTexts.Global)
	{
		thumbBarChannelLimit = new ThumbBarWidget(new Point(18, 15), 17, 4, 256);
		numberEntryMixingRate = new NumberEntryWidget(new Point(18, 16), 7, 4000, 192000, new Shared<int>());
		menuToggleSampleSize = new MenuToggleWidget(new Point(18, 17), SampleSizes);
		menuToggleOutputChannels = new MenuToggleWidget(new Point(18, 18), OutputChannels);
		menuToggleVisualizationStyle = new MenuToggleWidget(new Point(18, 20), VisualizationStyles);
		toggleClassicMode = new ToggleWidget(new Point(18, 21));
		menuToggleAccidentalsMode = new MenuToggleWidget(new Point(18, 22), AccidentalsModes);
		menuToggleTimeDisplay = new MenuToggleWidget(new Point(18, 23), TimeDisplays);
		menuToggleMIDIMode = new MenuToggleWidget(new Point(18, 25), MIDIModes);
		toggleButtonFullScreenYes = new ToggleButtonWidget(new Point(44, 30), 5, "Yes", 2, FullScreenGroup);
		toggleButtonFullScreenNo = new ToggleButtonWidget(new Point(54, 30), 5, "No", 2, FullScreenGroup);
		toggleButtonVideoNearest = new ToggleButtonWidget(new Point(6, 30), 26, "Nearest", 2, VideoGroup);
		toggleButtonVideoLinear = new ToggleButtonWidget(new Point(6, 33), 26, "Linear", 2, VideoGroup);
		toggleButtonVideoBest = new ToggleButtonWidget(new Point(6, 36), 26, "Best", 2, VideoGroup);
		toggleButtonRenderingHardware = new ToggleButtonWidget(new Point(6, 42), 26, "Hardware", 2, RendererGroup);
		toggleButtonRenderingSoftware = new ToggleButtonWidget(new Point(6, 45), 26, "Software", 2, RendererGroup);

		if (Video.HaveMenu)
		{
			toggleButtonMenuBarYes = new ToggleButtonWidget(new Point(44, 34), 5, "Yes", 2, MenuBarGroup);
			toggleButtonMenuBarNo = new ToggleButtonWidget(new Point(54, 34), 5, "No", 2, MenuBarGroup);
		}

		thumbBarChannelLimit.Changed += ChangeMixerLimits;
		numberEntryMixingRate.Changed += ChangeMixerLimits;
		menuToggleSampleSize.Changed += ChangeMixerLimits;
		menuToggleOutputChannels.Changed += ChangeMixerLimits;

		menuToggleVisualizationStyle.Changed += ChangeUISettings;
		toggleClassicMode.Changed += ChangeUISettings;
		menuToggleAccidentalsMode.Changed += ChangeUISettings;
		menuToggleTimeDisplay.Changed += ChangeUISettings;
		menuToggleMIDIMode.Changed += ChangeUISettings;

		toggleButtonFullScreenYes.Changed += ChangeVideoSettings;
		toggleButtonFullScreenNo.Changed += ChangeVideoSettings;

		if (Video.HaveMenu)
		{
			toggleButtonMenuBarYes!.Changed += ChangeMenuBarSettings;
			toggleButtonMenuBarNo!.Changed += ChangeMenuBarSettings;
		}

		AddWidget(thumbBarChannelLimit);
		AddWidget(numberEntryMixingRate);
		AddWidget(menuToggleSampleSize);
		AddWidget(menuToggleOutputChannels);
		AddWidget(menuToggleVisualizationStyle);
		AddWidget(toggleClassicMode);
		AddWidget(menuToggleAccidentalsMode);
		AddWidget(menuToggleTimeDisplay);
		AddWidget(menuToggleMIDIMode);
		AddWidget(toggleButtonFullScreenYes);
		AddWidget(toggleButtonFullScreenNo);
		AddWidget(toggleButtonVideoNearest);
		AddWidget(toggleButtonVideoLinear);
		AddWidget(toggleButtonVideoBest);
		AddWidget(toggleButtonRenderingHardware);
		AddWidget(toggleButtonRenderingSoftware);

		if (Video.HaveMenu)
		{
			AddWidget(toggleButtonMenuBarYes!);
			AddWidget(toggleButtonMenuBarNo!);
		}
	}

	const string SavedAtExit = "System configuration will be saved at exit";

	// TrackerTimeDisplay
	static readonly string[] TimeDisplays =
		{
			"Off", "Play / Elapsed", "Play / Clock", "Play / Off", "Elapsed", "Clock", "Absolute",
		};

	// TrackerVisualizationStyle
	static readonly string[] VisualizationStyles =
		{
			"Off", "Memory Stats", "Oscilloscope", "VU Meter", "Monoscope", "Spectrum",
		};

	static readonly string[] AccidentalsModes =
		{
			"Sharps (#)", "Flats (b)",
		};

	static readonly string[] OutputChannels =
		{
			"Mono", "Stereo",
		};

	static readonly string[] SampleSizes =
		{
			"8 Bit", "16 Bit", /*"24 Bit", */"32 Bit",
		};

	static readonly string[] MIDIModes =
		{
			"IT semantics", "Tracker semantics",
		};

	const int FullScreenGroup = 1;
	const int MenuBarGroup = 2;
	const int VideoGroup = 3;
	const int RendererGroup = 4;

	public override void DrawConst()
	{
		VGAMem.DrawText("Channel Limit", new Point(4, 15), (0, 2));
		VGAMem.DrawText("Mixing Rate", new Point(6, 16), (0, 2));
		VGAMem.DrawText("Sample Size", new Point(6, 17), (0, 2));
		VGAMem.DrawText("Output Channels", new Point(2, 18), (0, 2));

		VGAMem.DrawText("Visualization", new Point(4, 20), (0, 2));
		VGAMem.DrawText("Classic Mode", new Point(5, 21), (0, 2));
		VGAMem.DrawText("Accidentals", new Point(6, 22), (0, 2));
		VGAMem.DrawText("Time Display", new Point(5, 23), (0, 2));

		VGAMem.DrawText("MIDI mode", new Point(8, 25), (0, 2));

		VGAMem.DrawText("Video Scaling:", new Point(2, 28), (0, 2));
		VGAMem.DrawText("Video Rendering:", new Point(2, 40), (0, 2));
		VGAMem.DrawText("Full Screen:", new Point(38, 28), (0, 2));
		if (Video.HaveMenu)
			VGAMem.DrawText("Menu Bar:", new Point(38, 32), (0, 2));

		VGAMem.DrawFillCharacters(new Point(18, 15), new Point(34, 25), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(17, 14), new Point(35, 26), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		for (int n = 18; n < 35; n++)
		{
			VGAMem.DrawCharacter(154, new Point(n, 19), (3, 0));
			VGAMem.DrawCharacter(154, new Point(n, 24), (3, 0));
		}
	}

	public override void SetPage()
	{
		thumbBarChannelLimit.Value = AudioSettings.ChannelLimit;
		numberEntryMixingRate.Value = AudioSettings.SampleRate;

		switch (AudioSettings.Bits)
		{
			case 8: menuToggleSampleSize.State = 0; break;
			default:
			case 16: menuToggleSampleSize.State = 1; break;
			case 32: menuToggleSampleSize.State = 2; break;
		}

		menuToggleOutputChannels.State = AudioSettings.Channels - 1;

		menuToggleVisualizationStyle.State = (int)Status.VisualizationStyle;
		toggleClassicMode.State = Status.Flags.HasAllFlags(StatusFlags.ClassicMode);
		menuToggleAccidentalsMode.State = (SongNote.AccidentalsMode == AccidentalsMode.Flats) ? 1 : 0;
		menuToggleTimeDisplay.State = (int)Status.TimeDisplay;

		menuToggleMIDIMode.State = Status.Flags.HasAllFlags(StatusFlags.MIDILikeTracker) ? 1 : 0;

		toggleButtonFullScreenYes.SetState(Video.IsFullScreen);
		toggleButtonFullScreenNo.SetState(!Video.IsFullScreen);

		switch (Configuration.Video.Interpolation)
		{
			case VideoInterpolationMode.NearestNeighbour: toggleButtonVideoNearest.SetState(true); break;
			case VideoInterpolationMode.Linear: toggleButtonVideoLinear.SetState(true); break;
			case VideoInterpolationMode.Best: toggleButtonVideoBest.SetState(true); break;
		}

		toggleButtonRenderingHardware.SetState(Video.IsHardware);
		toggleButtonRenderingSoftware.SetState(!Video.IsHardware);

		if (Video.HaveMenu)
		{
			toggleButtonMenuBarYes!.SetState(Configuration.Video.WantMenuBar);
			toggleButtonMenuBarNo!.SetState(!Configuration.Video.WantMenuBar);
		}
	}

	void ChangeMixerLimits()
	{
		AudioSettings.ChannelLimit = thumbBarChannelLimit.Value;

		AudioSettings.SampleRate = numberEntryMixingRate.Value;

		switch (menuToggleSampleSize.State)
		{
			case 0: AudioSettings.Bits = 8; break;
			default:
			case 1: AudioSettings.Bits = 16; break;
			case 2: AudioSettings.Bits = 32; break;
		}

		AudioSettings.Channels = menuToggleOutputChannels.State + 1;

		AudioPlayback.InitializeModPlug();

		Status.FlashText(SavedAtExit);
	}

	void ChangeUISettings()
	{
		Status.VisualizationStyle = (TrackerVisualizationStyle)menuToggleVisualizationStyle.State;
		Status.TimeDisplay = (TrackerTimeDisplay)menuToggleTimeDisplay.State;

		if (toggleClassicMode.State)
			Status.Flags |= StatusFlags.ClassicMode;
		else
			Status.Flags &= ~StatusFlags.ClassicMode;

		SongNote.ToggleAccidentalsMode((menuToggleAccidentalsMode.State != 0) ? AccidentalsMode.Flats : AccidentalsMode.Sharps);

		GeneralMIDI.Reset(Song.CurrentSong, false);

		if (menuToggleMIDIMode.State != 0)
			Status.Flags |= StatusFlags.MIDILikeTracker;
		else
			Status.Flags &= ~StatusFlags.MIDILikeTracker;

		Status.Flags |= StatusFlags.NeedUpdate;

		Status.FlashText(SavedAtExit);
	}

	VideoInterpolationMode _videoRevertInterpolation;
	bool _videoRevertFullScreen;
	bool _videoRevertHardware;

	void VideoModeKeep()
	{
		Status.FlashText(SavedAtExit);
		SetPage();
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void VideoModeCancel()
	{
		if (Configuration.Video.Interpolation != _videoRevertInterpolation)
			Video.SetUp(_videoRevertInterpolation);

		if (Video.IsFullScreen != _videoRevertFullScreen)
			Video.Fullscreen(_videoRevertFullScreen);

		if (Video.IsHardware != _videoRevertHardware)
			Video.SetHardware(_videoRevertHardware);

		VGAMem.CurrentPalette.Apply();

		Font.Initialize();

		SetPage();

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void ShowVideoChangeDialog()
	{
		_videoRevertInterpolation = Configuration.Video.Interpolation;
		_videoRevertFullScreen = Video.IsFullScreen;
		_videoRevertHardware = Video.IsHardware;

		var dialog = Dialog.Show<VideoChangeDialog>();

		dialog.ActionYes += () => VideoModeKeep();
		dialog.ActionNo += () => VideoModeCancel();
		dialog.ActionCancel += () => VideoModeCancel();
	}

	void ChangeVideoSettings()
	{
		var newVideoInterpolation =
			toggleButtonVideoNearest.State ? VideoInterpolationMode.NearestNeighbour :
			toggleButtonVideoLinear.State ? VideoInterpolationMode.Linear :
			toggleButtonVideoBest.State ? VideoInterpolationMode.Best :
			VideoInterpolationMode.NearestNeighbour;

		bool newFSFlag = toggleButtonFullScreenYes.State;

		bool newHWFlag = toggleButtonRenderingHardware.State;

		bool interpolationChanged = (newVideoInterpolation != Configuration.Video.Interpolation);
		bool fsChanged = (newFSFlag != Video.IsFullScreen);
		bool hwChanged = (newHWFlag != Video.IsHardware);

		if (!interpolationChanged && !fsChanged && !hwChanged)
			return;

		ShowVideoChangeDialog();

		if (interpolationChanged)
			Video.SetUp(newVideoInterpolation);
		if (fsChanged)
			Video.Fullscreen(newFSFlag);
		if (hwChanged)
			Video.SetHardware(newHWFlag);

		VGAMem.CurrentPalette.Apply();

		Font.Initialize();
	}

	void ChangeMenuBarSettings()
	{
		if (toggleButtonMenuBarYes != null)
		{
			Configuration.Video.WantMenuBar = toggleButtonMenuBarYes.State;

			Video.ToggleMenu(!Video.IsFullScreen);
		}
	}
}
