using System;

namespace ChasmTracker.Pages;

using ChasmTracker.Audio;
using ChasmTracker.Configurations;
using ChasmTracker.Playback;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class PreferencesPage : Page
{
	/* this page will be the first against the wall when the revolution comes */
	const int VolumeScale = 31;

	const string SavedAtExit = "Audio configuration will be saved at exit";

	static readonly SourceMode[] InterpolationModes = Enum.GetValues<SourceMode>();

	const int InterpolationGroup = 1;
	const int RampGroup = 2;

	static readonly Rect AudioDeviceBounds = new Rect(
		new Point(37, 16),
		new Size(41, 6));

	static readonly Rect AudioDriverBounds = new Rect(
		new Point(37, 25),
		new Size(41, 6));

	// remember: _END is (1, 1) less than Bounds.BottomRight

	ThumbBarWidget thumbBarMasterVolumeLeft;
	ThumbBarWidget thumbBarMasterVolumeRight;
	ToggleButtonWidget[] toggleButtonInterpolationModes;
	(ThumbBarWidget Frequency, ThumbBarWidget Gain)[] thumbBarEqualizerBands;
	ToggleButtonWidget toggleButtonRampVolumeEnabled;
	ToggleButtonWidget toggleButtonRampVolumeDisabled;
	ButtonWidget buttonSaveOutputConfiguration;
	ListBoxWidget listBoxDeviceList;
	ListBoxWidget listBoxDriverList;

	public PreferencesPage()
		: base(PageNumbers.Preferences, "Preferences (Shift-F5)", HelpTexts.Global)
	{
		thumbBarMasterVolumeLeft = new ThumbBarWidget(new Point(22, 14), 5, 0, VolumeScale);
		thumbBarMasterVolumeRight = new ThumbBarWidget(new Point(22, 15), 5, 0, VolumeScale);

		thumbBarMasterVolumeLeft.Changed += ChangeVolume;
		thumbBarMasterVolumeRight.Changed += ChangeVolume;

		toggleButtonInterpolationModes = new ToggleButtonWidget[InterpolationModes.Length];

		for (int i = 0; i < InterpolationModes.Length; i++)
		{
			string buf = $"{AudioSettings.Bits} Bit, {InterpolationModes[i].GetDescription()}";

			toggleButtonInterpolationModes[i] = new ToggleButtonWidget(new Point(20 + (i * 3), 26), buf, 2, InterpolationGroup);
			toggleButtonInterpolationModes[i].Changed += ChangeMixer;
		}

		thumbBarEqualizerBands = new (ThumbBarWidget, ThumbBarWidget)[4];

		for (int j = 0; j < 4; j++)
		{
			int n = InterpolationModes.Length + j * 2;

			if (j == 0)
				n = InterpolationModes.Length + 1;

			var thumbBarBandFrequency = new ThumbBarWidget(new Point(26, 23 + InterpolationModes.Length * 3 + j), 21, 0, 127);
			var thumbBarBandGain = new ThumbBarWidget(new Point(53, 23 + InterpolationModes.Length + j), 21, 0, 127);

			thumbBarBandFrequency.Changed += ChangeEQ;
			thumbBarBandGain.Changed += ChangeEQ;

			thumbBarEqualizerBands[j] = (thumbBarBandFrequency, thumbBarBandGain);
		}

		/* default EQ setting */
		thumbBarEqualizerBands[0].Frequency.Value = 0;
		thumbBarEqualizerBands[1].Frequency.Value = 16;
		thumbBarEqualizerBands[2].Frequency.Value = 96;
		thumbBarEqualizerBands[3].Frequency.Value = 127;

		toggleButtonRampVolumeEnabled = new ToggleButtonWidget(new Point(33, 29 + InterpolationModes.Length * 3), "Enabled", 2, RampGroup);
		toggleButtonRampVolumeDisabled = new ToggleButtonWidget(new Point(46, 29 + InterpolationModes.Length * 3), "Disabled", 1, RampGroup);

		toggleButtonRampVolumeEnabled.Changed += ChangeMixer;
		toggleButtonRampVolumeDisabled.Changed += ChangeMixer;

		buttonSaveOutputConfiguration = new ButtonWidget(new Point(2, 44), 27, "Save Output Configuration", 2);
		buttonSaveOutputConfiguration.Clicked += SaveConfigNow;

		listBoxDeviceList = new ListBoxWidget(AudioDeviceBounds);
		listBoxDeviceList.GetSize += listBoxDeviceList_GetSize;
		listBoxDeviceList.GetToggled += listBoxDeviceList_GetToggled;
		listBoxDeviceList.GetName += listBoxDeviceList_GetName;
		listBoxDeviceList.Activated += listBoxDeviceList_Activated;

		listBoxDriverList = new ListBoxWidget(AudioDriverBounds);
		listBoxDriverList.GetSize += listBoxDriverList_GetSize;
		listBoxDriverList.GetToggled += listBoxDriverList_GetToggled;
		listBoxDriverList.GetName += listBoxDriverList_GetName;
		listBoxDriverList.Activated += listBoxDriverList_Activated;

		thumbBarMasterVolumeLeft.Next.Left = thumbBarMasterVolumeLeft.Next.Right =
			thumbBarMasterVolumeLeft.Next.Tab = thumbBarMasterVolumeLeft.Next.BackTab =
			thumbBarMasterVolumeRight.Next.Left = thumbBarMasterVolumeRight.Next.Right =
			thumbBarMasterVolumeRight.Next.Tab = thumbBarMasterVolumeRight.Next.BackTab =
				listBoxDeviceList;

		AddWidget(thumbBarMasterVolumeLeft);
		AddWidget(thumbBarMasterVolumeRight);
		AddWidgets(toggleButtonInterpolationModes);

		foreach (var band in thumbBarEqualizerBands)
		{
			AddWidget(band.Frequency);
			AddWidget(band.Gain);
		}

		AddWidget(toggleButtonRampVolumeEnabled);
		AddWidget(toggleButtonRampVolumeDisabled);
		AddWidget(buttonSaveOutputConfiguration);
		AddWidget(listBoxDeviceList);
		AddWidget(listBoxDriverList);
	}

	/* --------------------------------------------------------------------- */

	public override void DrawConst()
	{
		VGAMem.DrawText("Master Volume Left", new Point(2, 14), (0, 2));
		VGAMem.DrawText("Master Volume Right", new Point(2, 15), (0, 2));
		VGAMem.DrawBox(new Point(21, 13), new Point(27, 16), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawText("Mixing Mode", new Point(2, 18), (0, 2));

		VGAMem.DrawText("Available Audio Devices", AudioDeviceBounds.TopLeft.Advance(0, -2), (0, 2));
		VGAMem.DrawBox(AudioDeviceBounds.TopLeft.Advance(-1, -1), AudioDeviceBounds.BottomRight,
			BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawText("Available Audio Drivers", AudioDriverBounds.TopLeft.Advance(0, -2), (0, 2));
		VGAMem.DrawBox(AudioDriverBounds.TopLeft.Advance(-1, -1), AudioDriverBounds.BottomRight,
			BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		int i = InterpolationModes.Length;

		VGAMem.DrawText("Output Equalizer", new Point(2, 21 + i * 3), (0, 2));
		VGAMem.DrawText("Low Frequency Band", new Point(7, 23 + i * 3), (0, 2));
		VGAMem.DrawText("Med Low Frequency Band", new Point(3, 24 + i * 3), (0, 2));
		VGAMem.DrawText("Med High Frequency Band", new Point(2, 25 + i * 3), (0, 2));
		VGAMem.DrawText("High Frequency Band", new Point(6, 26 + i * 3), (0, 2));

		VGAMem.DrawText("Ramp volume at start of sample", new Point(2, 29 + i * 3), (0, 2));

		VGAMem.DrawBox(new Point(25, 22 + i * 3), new Point(47, 27 + i * 3), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(52, 22 + i * 3), new Point(74, 27 + i * 3), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawText($"Playback Frequency: {AudioSettings.SampleRate}Hz", new Point(2, 48), (0, 2));

		const string CornerBottom = "Chasm Tracker"; // "https://schismtracker.org/"
		VGAMem.DrawText(CornerBottom, new Point(78 - CornerBottom.Length + 1, 48), (1, 2));
	}

	public override void SetPage()
	{
		thumbBarMasterVolumeLeft.Value = AudioSettings.Master.Left;
		thumbBarMasterVolumeRight.Value = AudioSettings.Master.Right;

		int interpolationModeIndex = Array.IndexOf(InterpolationModes, AudioSettings.InterpolationMode);

		if (interpolationModeIndex < 0)
		{
			AudioSettings.InterpolationMode = SourceMode.Linear;
			interpolationModeIndex = Array.IndexOf(InterpolationModes, AudioSettings.InterpolationMode);
		}

		toggleButtonInterpolationModes[interpolationModeIndex].SetState(true);

		for (int j = 0; j < 4; j++)
		{
			thumbBarEqualizerBands[j].Frequency.Value = AudioSettings.EQBands[j].Frequency;
			thumbBarEqualizerBands[j].Gain.Value = AudioSettings.EQBands[j].Gain;
		}

		if (AudioSettings.NoRamping)
			toggleButtonRampVolumeDisabled.SetState(true);
		else
			toggleButtonRampVolumeEnabled.SetState(true);
	}

	/* --------------------------------------------------------------------- */
	void ChangeVolume()
	{
		AudioSettings.Master.Left = thumbBarMasterVolumeLeft.Value;
		AudioSettings.Master.Right = thumbBarMasterVolumeRight.Value;
	}

	void ChangeEQ()
	{
		for (int j = 0; j < 4; j++)
		{
			AudioSettings.EQBands[j].Frequency = thumbBarEqualizerBands[j].Frequency.Value;
			AudioSettings.EQBands[j].Gain = thumbBarEqualizerBands[j].Gain.Value;
		}

		AudioPlayback.InitializeEQ(true, AudioPlayback.MixFrequency);
	}

	void ChangeMixer()
	{
		for (int i = 0; i < InterpolationModes.Length; i++)
			if (toggleButtonInterpolationModes[i].State)
				AudioSettings.InterpolationMode = InterpolationModes[i];

		AudioSettings.NoRamping = toggleButtonRampVolumeDisabled.State;

		AudioPlayback.InitializeModPlug();

		Status.FlashText(SavedAtExit);
	}

	/* --------------------------------------------------------------------- */

	int listBoxDeviceList_GetSize() => AudioBackend.Devices.Length + 1;

	bool listBoxDeviceList_GetToggled(int n)
	{
		if (n == 0)
			return AudioPlayback.AudioDeviceID == AudioBackend.DefaultID;
		else
			return AudioBackend.Devices[n - 1].ID == AudioPlayback.AudioDeviceID;
	}

	string listBoxDeviceList_GetName(int n) => (n == 0) ? "default" : AudioBackend.Devices[n - 1].Name;

	void listBoxDeviceList_Activated()
	{
		int focusedIndex = listBoxDeviceList.Focus;

		var id = (focusedIndex == 0)
			? AudioBackend.DefaultID
			: AudioBackend.Devices[focusedIndex - 1].ID;

		AudioPlayback.Reinitialize(id);
	}

	/* --------------------------------------------------------------------- */

	int listBoxDriverList_GetSize() => AudioBackend.Drivers.Length;
	bool listBoxDriverList_GetToggled(int n) => AudioBackend.Drivers[n].Name == AudioPlayback.AudioDriver;
	string listBoxDriverList_GetName(int n) => AudioBackend.Drivers[n].Name;

	void listBoxDriverList_Activated()
	{
		int focusedIndex = listBoxDriverList.Focus;

		var result = AudioPlayback.Initialize(AudioBackend.Drivers[focusedIndex].Name, null);

		AudioBackend.FlashReinitializedText(result);

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void SaveConfigNow()
	{
		/* TODO */ /* uhhh, todo what? */
		Configuration.Save();
		Status.FlashText("Configuration saved");
	}
}
