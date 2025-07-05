using System;

namespace ChasmTracker.Pages;

using System.Linq;
using ChasmTracker.Audio;
using ChasmTracker.Configurations;
using ChasmTracker.Input;
using ChasmTracker.MIDI.Drivers;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
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

	static int _selectedAudioDevice = 0;
	static int _topAudioDevice = 0;

	static int _selectedAudioDriver = 0;
	static int _topAudioDriver = 0;

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
	OtherWidget otherDeviceList;
	OtherWidget otherDriverList;

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

		otherDeviceList = new OtherWidget(AudioDeviceBounds);
		otherDeviceList.OtherHandleKey += AudioDeviceListHandleKeyOnList;
		otherDeviceList.OtherRedraw += AudioDeviceListDraw;

		otherDriverList = new OtherWidget(AudioDriverBounds);
		otherDriverList.OtherHandleKey += AudioDriverListHandleKeyOnList;
		otherDriverList.OtherRedraw += AudioDriverListDraw;

		thumbBarMasterVolumeLeft.Next.Left = thumbBarMasterVolumeLeft.Next.Right =
			thumbBarMasterVolumeLeft.Next.Tab = thumbBarMasterVolumeLeft.Next.BackTab =
			thumbBarMasterVolumeRight.Next.Left = thumbBarMasterVolumeRight.Next.Right =
			thumbBarMasterVolumeRight.Next.Tab = thumbBarMasterVolumeRight.Next.BackTab =
				otherDeviceList;

		Widgets.Add(thumbBarMasterVolumeLeft);
		Widgets.Add(thumbBarMasterVolumeRight);
		Widgets.AddRange(toggleButtonInterpolationModes);

		foreach (var band in thumbBarEqualizerBands)
		{
			Widgets.Add(band.Frequency);
			Widgets.Add(band.Gain);
		}

		Widgets.Add(toggleButtonRampVolumeEnabled);
		Widgets.Add(toggleButtonRampVolumeDisabled);
		Widgets.Add(buttonSaveOutputConfiguration);
		Widgets.Add(otherDeviceList);
		Widgets.Add(otherDriverList);
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

		AudioPlayback.InitializeEQ(true, Song.CurrentSong.MixFrequency);
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

	void AudioDeviceListDraw()
	{
		bool focused = (SelectedActiveWidget == otherDeviceList);

		VGAMem.DrawFillCharacters(AudioDeviceBounds.TopLeft, AudioDeviceBounds.BottomRight.Advance(-1, -1), (VGAMem.DefaultForeground, 0));

		int o = 0;

		void DrawDevice(int id, string name)
		{
			int fg, bg;

			if (o + _topAudioDevice == _selectedAudioDevice)
			{
				if (focused)
					(fg, bg) = (0, 3);
				else
					(fg, bg) = (6, 14);
			}
			else
				(fg, bg) = (6, 0);

			VGAMem.DrawTextLen((id == AudioPlayback.AudioDeviceID) ? "*" : " ", 1, AudioDeviceBounds.TopLeft.Advance(0, o), (fg, bg));
			VGAMem.DrawTextLen(name, AudioDeviceBounds.Size.Width - 1, AudioDeviceBounds.TopLeft.Advance(1, o), (fg, bg));

			o++;
		}

		if (_topAudioDevice < 1)
			DrawDevice(AudioBackend.DefaultID, "default");

		foreach (var device in AudioBackend.Devices.Skip(_topAudioDevice - 1))
		{
			DrawDevice(device.ID, device.Name);

			if (o >= AudioDeviceBounds.Size.Height)
				break;
		}
	}

	bool AudioDeviceListHandleKeyOnList(KeyEvent k)
	{
		int newDevice = _selectedAudioDevice;
		bool loadSelectedDevice = false;


		switch (k.Mouse)
		{
			case MouseState.DoubleClick:
			case MouseState.Click:
				if (k.State == KeyState.Press)
					return false;
				if (!AudioDeviceBounds.Contains(k.MousePosition)) return false;
				newDevice = _topAudioDevice + (k.MousePosition.Y - AudioDeviceBounds.TopLeft.Y);
				if (k.Mouse == MouseState.DoubleClick || newDevice == _selectedAudioDevice)
					loadSelectedDevice = true;
				break;
			case MouseState.ScrollUp:
				newDevice -= Constants.MouseScrollLines;
				break;
			case MouseState.ScrollDown:
				newDevice += Constants.MouseScrollLines;
				break;
			default:
				if (k.State == KeyState.Release)
					return false;
				break;
		}

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (--newDevice < 0)
				{
					ChangeFocusTo(thumbBarMasterVolumeLeft);
					return true;
				}
				break;
			case KeySym.Down:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (++newDevice >= AudioBackend.Devices.Length + 1)
				{
					ChangeFocusTo(otherDriverList);
					return true;
				}
				break;
			case KeySym.Home:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newDevice = 0;
				break;
			case KeySym.PageUp:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				if (newDevice == 0)
					return true;

				newDevice -= 16;
				break;
			case KeySym.End:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newDevice = AudioBackend.Devices.Length;
				break;
			case KeySym.PageDown:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newDevice += 16;
				break;
			case KeySym.Return:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				loadSelectedDevice = true;
				break;
			case KeySym.Left:
			case KeySym.Right:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				if (_selectedAudioDevice < 2)
					ChangeFocusTo(thumbBarMasterVolumeRight);
				else
					ChangeFocusTo(toggleButtonInterpolationModes[0]);

				return true;
			default:
				if (k.Mouse == MouseState.None)
					return false;

				break;
		}

		newDevice = newDevice.Clamp(0, AudioBackend.Devices.Length);

		if (newDevice != _selectedAudioDevice)
		{
			_selectedAudioDevice = newDevice;
			Status.Flags |= StatusFlags.NeedUpdate;

			/* these HAVE to be done separately (and not as a CLAMP) because they aren't
			* really guaranteed to be ranges */
			_topAudioDevice = Math.Min(_topAudioDevice, _selectedAudioDevice);
			_topAudioDevice = Math.Max(_topAudioDevice, _selectedAudioDevice - AudioDeviceBounds.Size.Height + 1);

			_topAudioDevice = Math.Min(_topAudioDevice, AudioBackend.Devices.Length - AudioDeviceBounds.Size.Height + 1);
			_topAudioDevice = Math.Max(_topAudioDevice, 0);
		}

		if (loadSelectedDevice)
		{
			var id =
				(_selectedAudioDevice == 0)
				? AudioBackend.DefaultID
				: AudioBackend.Devices[_selectedAudioDevice - 1].ID;

			AudioBackend.Reinitialize(id);
		}

		return true;
	}

	/* --------------------------------------------------------------------- */

	void AudioDriverListDraw()
	{
		int numDrivers = AudioBackend.Drivers.Length;

		bool focused = (SelectedActiveWidget == otherDriverList);

		int fg, bg;

		string currentAudioDriver = AudioPlayback.AudioDriver;

		VGAMem.DrawFillCharacters(AudioDriverBounds, (VGAMem.DefaultForeground, 0));

		int o = 0;

		foreach (var driver in AudioBackend.Drivers.Skip(_topAudioDriver))
		{
			if ((o + _topAudioDriver) == _selectedAudioDriver)
			{
				if (focused)
					(fg, bg) = (0, 3);
				else
					(fg, bg) = (6, 14);
			}
			else
				(fg, bg) = (6, 0);

			VGAMem.DrawTextLen((currentAudioDriver == driver.Name) ? "*" : " ", 1, AudioDriverBounds.TopLeft.Advance(0, o), (fg, bg));
			VGAMem.DrawTextLen(driver.Name, AudioDriverBounds.Size.Width - 1, AudioDriverBounds.TopLeft.Advance(1, o), (fg, bg));

			o++;
		}
	}

	bool AudioDriverListHandleKeyOnList(KeyEvent k)
	{
		int newDriver = _selectedAudioDriver;
		bool loadSelectedDriver = false;

		switch (k.Mouse)
		{
			case MouseState.DoubleClick:
			case MouseState.Click:
				if (k.State == KeyState.Press)
					return false;
				if (AudioDriverBounds.Contains(k.MousePosition)) return false;
				newDriver = _topAudioDriver + (k.MousePosition.Y - AudioDriverBounds.TopLeft.Y);
				if (k.Mouse == MouseState.DoubleClick || newDriver == _selectedAudioDriver)
					loadSelectedDriver = true;
				break;
			case MouseState.ScrollUp:
				newDriver -= Constants.MouseScrollLines;
				break;
			case MouseState.ScrollDown:
				newDriver += Constants.MouseScrollLines;
				break;
			default:
				if (k.State == KeyState.Release)
					return false;
				break;
		}

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				if (--newDriver < 0)
				{
					ChangeFocusTo(otherDeviceList);
					return true;
				}
				break;
			case KeySym.Down:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (++newDriver >= AudioBackend.Drivers.Length)
				{
					ChangeFocusTo(thumbBarEqualizerBands[0].Gain);
					return true;
				}
				break;
			case KeySym.Home:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newDriver = 0;
				break;
			case KeySym.PageUp:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				if (newDriver == 0)
					return true;

				newDriver -= 16;
				break;
			case KeySym.End:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newDriver = AudioBackend.Drivers.Length;
				break;
			case KeySym.PageDown:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newDriver += 16;
				break;
			case KeySym.Return:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				loadSelectedDriver = true;
				break;
			case KeySym.Tab:
				if (!(k.Modifiers.HasAnyFlag(KeyMod.Shift) || !k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift)))
					return false;

				if (_selectedAudioDriver - _topAudioDriver < 3)
					ChangeFocusTo(toggleButtonInterpolationModes[2]);
				else
					ChangeFocusTo(toggleButtonInterpolationModes[3]);

				return true;
			case KeySym.Left:
			case KeySym.Right:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				if (_selectedAudioDriver - _topAudioDriver < 3)
					ChangeFocusTo(toggleButtonInterpolationModes[2]);
				else
					ChangeFocusTo(toggleButtonInterpolationModes[3]);

				return true;
			default:
				if (k.Mouse == MouseState.None)
					return false;
				break;
		}

		newDriver = newDriver.Clamp(0, AudioBackend.Drivers.Length - 1);

		if (newDriver != _selectedAudioDriver)
		{
			_selectedAudioDriver = newDriver;
			Status.Flags |= StatusFlags.NeedUpdate;

			/* these HAVE to be done separately (and not as a CLAMP) because they aren't
			* really guaranteed to be ranges */
			_topAudioDriver = Math.Min(_topAudioDriver, _selectedAudioDriver);
			_topAudioDriver = Math.Max(_topAudioDriver, _selectedAudioDriver - AudioDriverBounds.Size.Height + 1);

			_topAudioDriver = Math.Min(_topAudioDriver, AudioBackend.Drivers.Length - AudioDriverBounds.Size.Height + 1);
			_topAudioDriver = Math.Max(_topAudioDriver, 0);
		}

		if (loadSelectedDriver)
		{
			var result = AudioBackend.Initialize(AudioBackend.Drivers[_selectedAudioDriver].Name, null);

			AudioBackend.FlashReinitializedText(result);

			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return true;
	}

	void SaveConfigNow()
	{
		/* TODO */ /* uhhh, todo what? */
		Configuration.Save();
		Status.FlashText("Configuration saved");
	}

}
