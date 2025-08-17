using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.Dialogs.Samples;
using ChasmTracker.Events;
using ChasmTracker.FileSystem;
using ChasmTracker.FileTypes;
using ChasmTracker.FileTypes.SampleConverters;
using ChasmTracker.FM;
using ChasmTracker.Input;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class SampleListPage : Page
{
	/* static in my attic */
	VGAMemOverlay _sampleImage;

	bool _dialogF1Hack = false;

	const int VibratoWaveformsGroup = 1;

	int _topSample = 1;
	int _currentSample = 1;
	int _altSwapLastVisible = 99; // for alt-down sample-swapping

	int _sampleListCursorPos = 25; /* the "play" text */

	/* shared by all the numentry widgets */
	Shared<int> _sampleNumEntryCursorPos = new Shared<int>(0);

	/* for the loops */
	static readonly string[] LoopStates = { "Off", "On Forwards", "On Ping Pong" };

	/* playback */
	static int _lastNote = SpecialNotes.MiddleC;

	public static readonly SampleFileConverter[] SaveFormats = SampleFileConverter.EnumerateImplementations().ToArray();

	OtherWidget otherSampleList;
	ThumbBarWidget thumbBarDefaultVolume;
	ThumbBarWidget thumbBarGlobalVolume;
	ToggleWidget toggleEnableDefaultPan;
	ThumbBarWidget thumbBarDefaultPan;
	ThumbBarWidget thumbBarVibratoSpeed;
	ThumbBarWidget thumbBarVibratoDepth;
	TextEntryWidget textEntryFileName;
	NumberEntryWidget numberEntryC5Speed;
	MenuToggleWidget menuToggleLoopEnable;
	NumberEntryWidget numberEntryLoopStart;
	NumberEntryWidget numberEntryLoopEnd;
	MenuToggleWidget menuToggleSustainLoopEnable;
	NumberEntryWidget numberEntrySustainLoopStart;
	NumberEntryWidget numberEntrySustainLoopEnd;
	ToggleButtonWidget toggleButtonVibratoSine;
	ToggleButtonWidget toggleButtonVibratoRampDown;
	ToggleButtonWidget toggleButtonVibratoSquare;
	ToggleButtonWidget toggleButtonVibratoRandom;
	ThumbBarWidget thumbBarVibratoRate;

	public SampleListPage()
		: base(PageNumbers.SampleList, "Sample List (F3)", HelpTexts.SampleList)
	{
		_sampleImage = VGAMem.AllocateOverlay(
			new Point(55, 26),
			new Point(76, 29));

		/* 0 = sample list */
		otherSampleList = new OtherWidget(new Point(5, 13), new Size(30, 35));
		otherSampleList.OtherHandleKey += otherSampleList_HandleKey;
		otherSampleList.OtherHandleText += otherSampleList_HandleText;
		otherSampleList.OtherRedraw += otherSampleList_Redraw;

		FixAcceptText();

		/* 1 -> 6 = middle column */
		thumbBarDefaultVolume = new ThumbBarWidget(new Point(38, 16), 9, 0, 64);
		thumbBarGlobalVolume = new ThumbBarWidget(new Point(38, 23), 9, 0, 64);
		toggleEnableDefaultPan = new ToggleWidget(new Point(38, 30));
		thumbBarDefaultPan = new ThumbBarWidget(new Point(38, 31), 9, 0, 64);
		thumbBarVibratoSpeed = new ThumbBarWidget(new Point(38, 39), 9, 0, 64);
		thumbBarVibratoDepth = new ThumbBarWidget(new Point(38, 46), 9, 0, 64);

		thumbBarDefaultVolume.Changed += UpdateValuesInSong;
		thumbBarGlobalVolume.Changed += UpdateValuesInSong;
		toggleEnableDefaultPan.Changed += UpdateValuesInSong;
		thumbBarDefaultPan.Changed += UpdatePanning;
		thumbBarVibratoSpeed.Changed += UpdateValuesInSong;
		thumbBarVibratoDepth.Changed += UpdateValuesInSong;

		/* 7 -> 14 = top right box */
		textEntryFileName = new TextEntryWidget(new Point(64, 13), 13, "", 12);
		numberEntryC5Speed = new NumberEntryWidget(new Point(64, 14), 7, 0, 9999999, _sampleNumEntryCursorPos);
		menuToggleLoopEnable = new MenuToggleWidget(new Point(64, 15), LoopStates);
		numberEntryLoopStart = new NumberEntryWidget(new Point(64, 16), 7, 0, 9999999, _sampleNumEntryCursorPos);
		numberEntryLoopEnd = new NumberEntryWidget(new Point(64, 17), 7, 0, 9999999, _sampleNumEntryCursorPos);
		menuToggleSustainLoopEnable = new MenuToggleWidget(new Point(64, 18), LoopStates);
		numberEntrySustainLoopStart = new NumberEntryWidget(new Point(64, 19), 7, 0, 9999999, _sampleNumEntryCursorPos);
		numberEntrySustainLoopEnd = new NumberEntryWidget(new Point(64, 20), 7, 0, 9999999, _sampleNumEntryCursorPos);

		textEntryFileName.Changed += UpdateFilename;
		numberEntryC5Speed.Changed += UpdateSampleSpeed;
		menuToggleLoopEnable.Changed += UpdateSampleLoopFlags;
		numberEntryLoopStart.Changed += UpdateSampleLoopPoints;
		numberEntryLoopEnd.Changed += UpdateSampleLoopPoints;
		menuToggleSustainLoopEnable.Changed += UpdateSampleLoopFlags;
		numberEntrySustainLoopStart.Changed += UpdateSampleLoopPoints;
		numberEntrySustainLoopEnd.Changed += UpdateSampleLoopPoints;

		/* 15 -> 18 = vibrato waveforms */
		toggleButtonVibratoSine = new ToggleButtonWidget(new Point(57, 36), 6, "\xB9\xBA", 3, VibratoWaveformsGroup);
		toggleButtonVibratoRampDown = new ToggleButtonWidget(new Point(67, 36), 6, "\xBD\xBE", 3, VibratoWaveformsGroup);
		toggleButtonVibratoSquare = new ToggleButtonWidget(new Point(57, 39), 6, "\xBB\xBC", 3, VibratoWaveformsGroup);
		toggleButtonVibratoRandom = new ToggleButtonWidget(new Point(67, 39), 6, "Random", 1, VibratoWaveformsGroup);

		toggleButtonVibratoSine.Changed += UpdateValuesInSong;
		toggleButtonVibratoRampDown.Changed += UpdateValuesInSong;
		toggleButtonVibratoSquare.Changed += UpdateValuesInSong;
		toggleButtonVibratoRandom.Changed += UpdateValuesInSong;

		/* 19 = vibrato rate */
		thumbBarVibratoRate = new ThumbBarWidget(new Point(56, 46), 16, 0, 255);

		thumbBarVibratoRate.Changed += UpdateValuesInSong;

		AddWidget(otherSampleList);
		AddWidget(thumbBarDefaultVolume);
		AddWidget(thumbBarGlobalVolume);
		AddWidget(toggleEnableDefaultPan);
		AddWidget(thumbBarDefaultPan);
		AddWidget(thumbBarVibratoSpeed);
		AddWidget(thumbBarVibratoDepth);
		AddWidget(textEntryFileName);
		AddWidget(numberEntryC5Speed);
		AddWidget(menuToggleLoopEnable);
		AddWidget(numberEntryLoopStart);
		AddWidget(numberEntryLoopEnd);
		AddWidget(menuToggleSustainLoopEnable);
		AddWidget(numberEntrySustainLoopStart);
		AddWidget(numberEntrySustainLoopEnd);
		AddWidget(toggleButtonVibratoSine);
		AddWidget(toggleButtonVibratoRampDown);
		AddWidget(toggleButtonVibratoSquare);
		AddWidget(toggleButtonVibratoRandom);
		AddWidget(thumbBarVibratoRate);
	}

	bool otherSampleList_HandleKey(KeyEvent k)
	{
		int newSample = _currentSample;
		int newCursorPos = _sampleListCursorPos;

		if (k.Mouse == MouseState.Click && k.MouseButton == MouseButton.Middle)
		{
			if (k.State == KeyState.Release)
				Status.Flags |= StatusFlags.ClippyPasteSelection;

			return true;
		}
		else if (k.State == KeyState.Press && k.Mouse != MouseState.None && k.MousePosition.X >= 5 && k.MousePosition.Y >= 13 && k.MousePosition.Y <= 47 && k.MousePosition.X <= 34)
		{
			if (k.Mouse == MouseState.ScrollUp)
			{
				_topSample -= Constants.MouseScrollLines;
				if (_topSample < 1) _topSample = 1;
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			}
			else if (k.Mouse == MouseState.ScrollDown)
			{
				_topSample += Constants.MouseScrollLines;
				if (_topSample > (LastVisibleSampleNumber() - 34))
					_topSample = (LastVisibleSampleNumber() - 34);
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			}
			else
			{
				newSample = (k.MousePosition.Y - 13) + _topSample;
				newCursorPos = k.MousePosition.X - 5;
				if (k.MousePosition.X <= 29) /* and button1 */
				{
					if (k.Mouse == MouseState.DoubleClick)
					{
						/* this doesn't appear to work */
						SetPage(PageNumbers.SampleLoad);
						Status.Flags |= StatusFlags.NeedUpdate;
						return true;
					}
					else
					{
					}
				}
#if false
				/* buggy and annoying, could be implemented properly but I don't care enough */
				else if (k.State == KeyState.Release || k.MousePosition.X == k.StartPosition.X)
				{
					if (k.Mouse == MouseState.DoubleClick
					|| (newSample == _currentSample
					&& _sampleListCursorPos == 25))
					{
						Song.KeyDown(_currentSample, KeyJazz.NoInstrument,
							_lastNote, 64, KeyJazz.CurrentChannel);
					}

					newCursorPos = 25;
				}
#endif
			}
		}
		else
		{
			switch (k.Sym)
			{
				case KeySym.Left:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					newCursorPos--;
					break;
				case KeySym.Right:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					newCursorPos++;
					break;
				case KeySym.Home:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					newCursorPos = 0;
					break;
				case KeySym.End:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					newCursorPos = 25;
					break;
				case KeySym.Up:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					{
						if (_currentSample > 1)
						{
							newSample = _currentSample - 1;
							Song.CurrentSong.SwapSamples(_currentSample, newSample);
						}
					}
					else if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					else
						newSample--;
					break;
				case KeySym.Down:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					{
						// restrict position to the "old" value of LastVisibleSampleNumber()
						// (this is entirely for aesthetic reasons)
						if (Status.LastKeySym != KeySym.Down && !k.IsRepeat)
							_altSwapLastVisible = LastVisibleSampleNumber();
						if (_currentSample < _altSwapLastVisible)
						{
							newSample = _currentSample + 1;
							Song.CurrentSong.SwapSamples(_currentSample, newSample);
						}
					}
					else if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					else
						newSample++;
					break;
				case KeySym.PageUp:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Control))
						newSample = 1;
					else
						newSample -= 16;
					break;
				case KeySym.PageDown:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Control))
						newSample = LastVisibleSampleNumber();
					else
						newSample += 16;
					break;
				case KeySym.Return:
					if (k.State == KeyState.Press)
						return false;
					SetPage(PageNumbers.SampleLoad);
					break;
				case KeySym.Backspace:
					if (k.State == KeyState.Release)
						return false;
					if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAlt))
					{
						if (_sampleListCursorPos < 25)
							SampleListDeleteChar();
						return true;
					}
					else if (k.Modifiers.HasAnyFlag(KeyMod.Control))
					{
						/* just for compatibility with every weird thing
						* Impulse Tracker does ^_^ */
						if (_sampleListCursorPos < 25)
							SampleListAddChar((char)127);
						return true;
					}
					return false;
				case KeySym.Delete:
					if (k.State == KeyState.Release)
						return false;
					if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAlt))
					{
						if (_sampleListCursorPos < 25)
							SampleListDeleteNextChar();
						return true;
					}
					return false;
				case KeySym.Escape:
					if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					{
						if (k.State == KeyState.Release)
							return true;
						newCursorPos = 25;
						break;
					}
					return false;
				default:
					if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					{
						if (k.Sym == KeySym.c)
						{
							ClearSampleText();
							return true;
						}
						return false;
					}
					else if (!k.Modifiers.HasAnyFlag(KeyMod.Control) && _sampleListCursorPos < 25)
					{
						if (k.State == KeyState.Release)
							return true;

						if (!string.IsNullOrEmpty(k.Text))
							return SampleListHandleTextInput(k.Text);

						/* ...uhhhhhh */
						return false;
					}
					return false;
			}
		}

		newSample = newSample.Clamp(1, LastVisibleSampleNumber());
		newCursorPos = newCursorPos.Clamp(0, 25);

		if (newSample != _currentSample)
		{
			CurrentSample = newSample;
			Reposition();
		}

		if (newCursorPos != _sampleListCursorPos)
		{
			_sampleListCursorPos = newCursorPos;
			FixAcceptText();
		}

		Status.Flags |= StatusFlags.NeedUpdate;
		return true;
	}

	bool otherSampleList_HandleText(TextInputEvent k)
	{
		return SampleListHandleTextInput(k.Text);
	}

	/* draw the actual list */
	void otherSampleList_Redraw()
	{
		int ss, cl = 0, cr = 0;
		int[] isPlaying = new int[Constants.MaxSamples];

		ss = -1;

		Song.CurrentSong.GetPlayingInstruments(isPlaying);

		/* list */
		for (int pos = 0, n = _topSample; pos < 35; pos++, n++)
		{
			var sample = Song.CurrentSong.GetSample(n);

			if (sample == null)
				continue;

			bool isSelected = (n == _currentSample);

			bool hasData = sample.HasData;

			if (sample.IsPlayed)
				VGAMem.DrawCharacter((isPlaying[n] > 1) ? (char)183 : (char)173, new Point(1, 13 + pos), (isPlaying[n] > 0) ? (3, 2) : (1, 2));

			VGAMem.DrawText(n.ToString99(), new Point(2, 13 + pos), (sample.Flags.HasAllFlags(SampleFlags.Mute)) ? (1, 1) : (0, 2));

			// wow, this is entirely horrible
			int pn = (sample.Name.Length >= 25) ? sample.Name[24] : int.MaxValue;
			int nl;

			if ((pn < 200) && (sample.Name[23] == 0xFF))
			{
				nl = 23;
				VGAMem.DrawText(pn.ToString("d3"), new Point(32, 13 + pos), (0, 2));
				VGAMem.DrawCharacter('P', new Point(28, 13 + pos), (3, 2));
				VGAMem.DrawCharacter('a', new Point(29, 13 + pos), (3, 2));
				VGAMem.DrawCharacter('t', new Point(30, 13 + pos), (3, 2));
				VGAMem.DrawCharacter('.', new Point(31, 13 + pos), (3, 2));
			}
			else
			{
				nl = 25;
				VGAMem.DrawCharacter(168, new Point(30, 13 + pos), (isSelected ? (2, 14) : (2, 0)));
				VGAMem.DrawText("Play", new Point(31, 13 + pos), (hasData ? 6 : 7, isSelected ? 14 : 0));
			}

			VGAMem.DrawTextLen(sample.Name, nl, new Point(5, 13 + pos), isSelected ? (6, 14) : (6, 0));

			if (ss == n)
				VGAMem.DrawTextLen(sample.Name.Substring(cl), (cr - cl) + 1, new Point(5 + cl, 13 + pos), (3, 8));
		}

		/* cursor */
		if (SelectedActiveWidget == otherSampleList)
		{
			int pos = _currentSample - _topSample;

			var sample = Song.CurrentSong.GetSample(_currentSample);

			if (sample != null)
			{
				bool hasData = sample.HasData;

				if (pos < 0 || pos > 34)
				{
					/* err... */
				}
				else if (_sampleListCursorPos == 25)
					VGAMem.DrawText("Play", new Point(31, 13 + pos), hasData ? (0, 3) : (0, 6));
				else
				{
					VGAMem.DrawCharacter((_sampleListCursorPos > sample.Name.Length)
							? (char)0 : sample.Name[_sampleListCursorPos],
							new Point(_sampleListCursorPos + 5, 13 + pos), (0, 3));
				}
			}
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	/* --------------------------------------------------------------------- */
	/* wheesh */

	public override void DrawConst()
	{
		VGAMem.DrawBox(new Point(4, 12), new Point(35, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(63, 12), new Point(77, 24), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawBox(new Point(36, 12), new Point(53, 18), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(37, 15), new Point(47, 17), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(36, 19), new Point(53, 25), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(37, 22), new Point(47, 24), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(36, 26), new Point(53, 33), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(37, 29), new Point(47, 32), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(36, 35), new Point(53, 41), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(37, 38), new Point(47, 40), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(36, 42), new Point(53, 48), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(37, 45), new Point(47, 47), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(54, 25), new Point(77, 30), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(54, 31), new Point(77, 41), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(54, 42), new Point(77, 48), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(55, 45), new Point(72, 47), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawFillCharacters(new Point(41, 30), new Point(46, 30), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawFillCharacters(new Point(64, 13), new Point(76, 23), (VGAMem.DefaultForeground, 0));

		VGAMem.DrawText("Default Volume", new Point(38, 14), (0, 2));
		VGAMem.DrawText("Global Volume", new Point(38, 21), (0, 2));
		VGAMem.DrawText("Default Pan", new Point(39, 28), (0, 2));
		VGAMem.DrawText("Vibrato Speed", new Point(38, 37), (0, 2));
		VGAMem.DrawText("Vibrato Depth", new Point(38, 44), (0, 2));
		VGAMem.DrawText("Filename", new Point(55, 13), (0, 2));
		VGAMem.DrawText("Speed", new Point(58, 14), (0, 2));
		VGAMem.DrawText("Loop", new Point(59, 15), (0, 2));
		VGAMem.DrawText("LoopBeg", new Point(56, 16), (0, 2));
		VGAMem.DrawText("LoopEnd", new Point(56, 17), (0, 2));
		VGAMem.DrawText("SusLoop", new Point(56, 18), (0, 2));
		VGAMem.DrawText("SusLBeg", new Point(56, 19), (0, 2));
		VGAMem.DrawText("SusLEnd", new Point(56, 20), (0, 2));
		VGAMem.DrawText("Quality", new Point(56, 22), (0, 2));
		VGAMem.DrawText("Length", new Point(57, 23), (0, 2));
		VGAMem.DrawText("Vibrato Waveform", new Point(58, 33), (0, 2));
		VGAMem.DrawText("Vibrato Rate", new Point(60, 44), (0, 2));

		for (int n = 0; n < 13; n++)
			VGAMem.DrawCharacter(154, new Point(64 + n, 21), (3, 0));
	}

	public override void PredrawHook()
	{
		var sample = Song.CurrentSong.GetSample(_currentSample);

		if (sample == null)
			return;

		bool hasData = sample.HasData;

		/* set all the values to the current sample */

		/* default volume
		modplug hack here: sample volume has 4x the resolution...
		can't deal with this in song.cc (easily) without changing the actual volume of the sample. */
		thumbBarDefaultVolume.Value = sample.Volume / 4;

		/* global volume */
		thumbBarGlobalVolume.Value = sample.GlobalVolume;
		thumbBarGlobalVolume.TextAtMinimum = sample.Flags.HasAllFlags(SampleFlags.Mute) ? "  Muted  " : null;

		/* default pan (another modplug hack) */
		toggleEnableDefaultPan.State = sample.Flags.HasAllFlags(SampleFlags.Panning);
		thumbBarDefaultPan.Value = sample.Panning / 4;

		thumbBarVibratoSpeed.Value = sample.VibratoSpeed;
		thumbBarVibratoDepth.Value = sample.VibratoDepth;
		textEntryFileName.Text = sample.FileName;
		numberEntryC5Speed.Value = sample.C5Speed;

		menuToggleLoopEnable.State =
			(sample.Flags.HasAllFlags(SampleFlags.Loop) ? (sample.Flags.HasAllFlags(SampleFlags.PingPongLoop) ? 2 : 1) : 0);
		numberEntryLoopStart.Value = sample.LoopStart;
		numberEntryLoopEnd.Value = sample.LoopEnd;
		menuToggleSustainLoopEnable.State =
			(sample.Flags.HasAnyFlag(SampleFlags.SustainLoop) ? (sample.Flags.HasAllFlags(SampleFlags.PingPongSustain) ? 2 : 1) : 0);
		numberEntrySustainLoopStart.Value = sample.SustainStart;
		numberEntrySustainLoopEnd.Value = sample.SustainEnd;

		switch (sample.VibratoType)
		{
			case VibratoType.Sine:
				toggleButtonVibratoSine.SetState(true);
				break;
			case VibratoType.RampDown:
				toggleButtonVibratoRampDown.SetState(true);
				break;
			case VibratoType.Square:
				toggleButtonVibratoSquare.SetState(true);
				break;
			case VibratoType.Random:
				toggleButtonVibratoRandom.SetState(true);
				break;
		}

		thumbBarVibratoRate.Value = sample.VibratoRate;

		string buf;

		if (hasData)
			buf = (sample.Flags.HasAllFlags(SampleFlags._16Bit) ? "16" : "8") + " bit" + (sample.Flags.HasAllFlags(SampleFlags.Stereo) ? " Stereo" : "");
		else
			buf = "No sample";

		VGAMem.DrawTextLen(buf, 13, new Point(64, 22), (2, 0));

		VGAMem.DrawTextLen(sample.Length.ToString(), 13, new Point(64, 23), (2, 0));

		VGAMem.DrawSampleData(_sampleImage, sample);
	}

	public override bool? HandleKey(KeyEvent k)
	{
		SampleListHandleKey(k);
		return true;
	}

	public override void SetPage()
	{
		Reposition();
	}

	public int CurrentSample
	{
		get => _currentSample;
		set
		{
			int newSample = value;

			if (Status.CurrentPage is SampleListPage)
				newSample = Math.Max(1, newSample);
			else
				newSample = Math.Max(0, newSample);

			newSample = Math.Min(LastVisibleSampleNumber(), newSample);

			if (_currentSample == newSample)
				return;

			_currentSample = newSample;
			// TODO: sample_list_reposition() */

			/* update_current_instrument(); */
			if (Status.CurrentPage is SampleListPage)
				Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	/* --------------------------------------------------------------------- */

	/* woo */

	bool IsMagicSample(int no)
	{
		var sample = Song.CurrentSong.GetSample(no);

		if ((sample != null) && (sample.Name.Length >= 25) && (sample.Name[23] == 0xFF))
		{
			int pn = sample.Name[24];
			if (pn < 200) return true;
		}
		return false;
	}

	void FixAcceptText()
	{
		if (IsMagicSample(_currentSample))
			otherSampleList.OtherAcceptsText = (_sampleListCursorPos < 23);
		else
			otherSampleList.OtherAcceptsText = (_sampleListCursorPos < 25);
	}

	int LastVisibleSampleNumber()
	{
		int j = 0;

		/* 65 is first visible sample on last page */
		for (int i = 65; i < Constants.MaxSamples; i++)
			if ((Song.CurrentSong.GetSample(i) is SongSample sample) && !sample.IsEmpty)
				j = i;

		int n = 99;

		while ((j + 34) > n) n += 34;

		if (n >= Constants.MaxSamples) n = Constants.MaxSamples - 1;

		return n;
	}

	/* --------------------------------------------------------------------- */

	void Reposition()
	{
		if (_currentSample < _topSample)
		{
			_topSample = _currentSample;
			if (_topSample < 1)
				_topSample = 1;
		}
		else if (_currentSample > _topSample + 34)
			_topSample = _currentSample - 34;

		if (_dialogF1Hack
				&& Status.CurrentPageNumber == PageNumbers.SampleList
				&& Status.PreviousPageNumber == PageNumbers.Help)
			ShowAdLibConfigDialog();

		_dialogF1Hack = false;
	}

	/* --------------------------------------------------------------------- */

	bool SampleListAddChar(char c)
	{
		if (c < 32)
			return false;

		var smp = Song.CurrentSong.EnsureSample(_currentSample);

		if (smp.Name.Length < 25)
		{
			if (_sampleListCursorPos > smp.Name.Length)
				smp.Name += "                         ";

			smp.Name =
				smp.Name.Substring(0, _sampleListCursorPos) +
				c +
				smp.Name.Substring(_sampleListCursorPos);

			if (smp.Name.Length > 25)
				smp.Name = smp.Name.Substring(0, 25);

			_sampleListCursorPos++;
		}

		if (_sampleListCursorPos == 25)
			_sampleListCursorPos--;

		FixAcceptText();

		Status.Flags |= StatusFlags.NeedUpdate;
		Status.Flags |= StatusFlags.SongNeedsSave;

		return true;
	}

	void SampleListDeleteChar()
	{
		var smp = Song.CurrentSong.GetInstrument(_currentSample);

		if (smp == null)
			return;

		if (_sampleListCursorPos < smp.Name?.Length)
		{
			_sampleListCursorPos--;

			if (IsMagicSample(_currentSample))
				smp.Name = smp.Name.Substring(0, 23).Remove(_sampleListCursorPos, 1) + ' ' + smp.Name.Substring(23);
			else
				smp.Name = smp.Name.Remove(_sampleListCursorPos, 1);
		}

		FixAcceptText();

		Status.Flags |= StatusFlags.NeedUpdate;
		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void SampleListDeleteNextChar()
	{
		var smp = Song.CurrentSong.GetSample(_currentSample);

		if (smp == null)
			return;

		if (_sampleListCursorPos < smp.Name?.Length)
		{
			if (IsMagicSample(_currentSample))
				smp.Name = smp.Name.Substring(0, 23).Remove(_sampleListCursorPos, 1) + ' ' + smp.Name.Substring(23);
			else
				smp.Name = smp.Name.Remove(_sampleListCursorPos, 1);
		}

		FixAcceptText();

		Status.Flags |= StatusFlags.NeedUpdate;
		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void ClearSampleText()
	{
		var smp = Song.CurrentSong.GetSample(_currentSample);

		if (smp == null)
			return;

		smp.FileName = "";

		if (IsMagicSample(_currentSample))
			smp.Name = "                       " + smp.Name.Substring(23);

		_sampleListCursorPos = 0;

		FixAcceptText();

		Status.Flags |= StatusFlags.NeedUpdate;
		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	/* --------------------------------------------------------------------- */

	void DoSwapSample(int n)
	{
		if (n >= 1 && n <= LastVisibleSampleNumber())
			Song.CurrentSong.SwapSamples(_currentSample, n);
	}

	void DoExchangeSample(int n)
	{
		if (n >= 1 && n <= LastVisibleSampleNumber())
			Song.CurrentSong.ExchangeSamples(_currentSample, n);
	}

	void DoCopySample(int n)
	{
		if (n >= 1 && n <= LastVisibleSampleNumber())
		{
			Song.CurrentSong.CopySample(_currentSample, Song.CurrentSong.EnsureSample(n));
			ShowSampleHostDialog(PageNumbers.None);
		}

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void DoReplaceSample(int n)
	{
		if (n >= 1 && n <= LastVisibleSampleNumber())
			Song.CurrentSong.ReplaceSample(_currentSample, n);

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	/* --------------------------------------------------------------------- */

	bool SampleListHandleTextInput(string text)
	{
		bool success = false;

		foreach (char ch in text)
			if ((_sampleListCursorPos < 25) && SampleListAddChar(ch))
				success = true;

		return success;
	}

	void SampleListHandleAltKey(KeyEvent k)
	{
		var sample = Song.CurrentSong.GetSample(_currentSample);

		bool canMod = (sample != null) && sample.HasData && !sample.Flags.HasAllFlags(SampleFlags.AdLib);

		if (k.State == KeyState.Release)
			return;

		switch (k.Sym)
		{
			case KeySym.a:
				if (canMod)
					MessageBox.Show(MessageBoxTypes.OKCancel, "Convert sample?", DoSignConvert);
				return;
			case KeySym.b:
				if (canMod && (sample!.LoopStart > 0
								|| (sample.Flags.HasAllFlags(SampleFlags.SustainLoop) && sample.SustainStart > 0)))
				{
					MessageBox.Show(MessageBoxTypes.OKCancel, "Cut sample?", DoPreLoopCut)
						.SelectedWidgetIndex.Value = 1;
				}
				return;
			case KeySym.d:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift) && !Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
				{
					if (canMod && sample!.Flags.HasAllFlags(SampleFlags.Stereo))
					{
						MessageBox.Show(MessageBoxTypes.OKCancel, "Downmix sample to mono?", DoDownmix);
					}
				}
				else
				{
					MessageBox.Show(MessageBoxTypes.OKCancel, "Delete sample?", DoDeleteSample)
						.SelectedWidgetIndex.Value = 1;
				}
				return;
			case KeySym.e:
				if (canMod)
				{
					if (k.Modifiers.HasAnyFlag(KeyMod.Shift) && !Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
						ShowResampleSampleDialog(true);
					else
						ShowResizeSampleDialog(true);
				}
				break;
			case KeySym.f:
				if (canMod)
				{
					if (k.Modifiers.HasAnyFlag(KeyMod.Shift) && !Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
						ShowResampleSampleDialog(false);
					else
						ShowResizeSampleDialog(false);
				}
				break;
			case KeySym.g:
				if (canMod)
					SampleEditOperations.Reverse(sample!);
				break;
			case KeySym.h:
				if (canMod)
					MessageBox.Show(MessageBoxTypes.YesNo, "Centralise sample?", DoCentralise);
				return;
			case KeySym.i:
				if (canMod)
					SampleEditOperations.Invert(sample!);
				break;
			case KeySym.l:
				if (canMod && (sample!.LoopEnd > 0
								|| (sample.Flags.HasAllFlags(SampleFlags.SustainLoop) && sample.SustainEnd > 0)))
				{
					MessageBox.Show(MessageBoxTypes.OKCancel, "Cut sample?", DoPostLoopCut)
						.SelectedWidgetIndex.Value = 1;
				}
				return;
			case KeySym.m:
				if (canMod)
					ShowSampleAmplifyDialog();
				return;
			case KeySym.n:
				AudioPlayback.ToggleMultichannelMode();
				return;
			case KeySym.o:
				SaveSample(null, new ITS());
				return;
			case KeySym.p:
			{
				var dialog = Dialog.Show(new SamplePromptDialog("Sample", "Copy sample:"));

				dialog.Finish += n => DoCopySample(n);
				return;
			}
			case KeySym.q:
				if (canMod)
					MessageBox.Show(MessageBoxTypes.YesNo, "Convert sample?", DoQualityConvert, DoQualityToggle);
				return;
			case KeySym.r:
			{
				var dialog = Dialog.Show(new SamplePromptDialog("Sample", "Replace sample with:"));

				dialog.Finish += n => DoReplaceSample(n);
				return;
			}
			case KeySym.s:
			{
				var dialog = Dialog.Show(new SamplePromptDialog("Sample", "Swap sample with:"));

				dialog.Finish += n => DoSwapSample(n);
				return;
			}
			case KeySym.t:
				ShowExportSampleDialog();
				return;
			case KeySym.v:
				if (!canMod || (Status.Flags.HasAllFlags(StatusFlags.ClassicMode)))
					return;

				if (!sample!.Flags.HasAnyFlag(SampleFlags.Loop | SampleFlags.SustainLoop))
				{
					MessageBox.Show(MessageBoxTypes.OK, "Crossfade requires a sample loop to work.");
					return;
				}

				if ((sample.LoopStart == 0) && (sample.SustainStart == 0))
				{
					MessageBox.Show(MessageBoxTypes.OK, "Crossfade requires data before the sample loop.");
					return;
				}

				ShowCrossfadeSampleDialog();
				return;
			case KeySym.w:
				SaveSample(null, new RAW());
				return;
			case KeySym.x:
			{
				var dialog = Dialog.Show(new SamplePromptDialog("Sample", "Exchange sample with:"));

				dialog.Finish += n => DoExchangeSample(n);
				return;
			}
			case KeySym.y:
				/* hi virt */
				ShowTextSynthDialog();
				return;
			case KeySym.z:
			{
				// uguu~
				bool doAdLibPatch = k.Modifiers.HasAnyFlag(KeyMod.Shift);

				Action dlg =
					() =>
					{
						if (doAdLibPatch)
							ShowAdLibPatchDialog();
						else
							ShowAdLibConfigDialog();
					};

				if (canMod)
				{
					var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "This will replace the current sample.");

					dialog.SelectedWidgetIndex.Value = 1;
					dialog.ActionYes += dlg;
				}
				else
					dlg();
				return;
			}
			case KeySym.Insert:
				Song.CurrentSong.InsertSampleSlot(_currentSample);
				break;
			case KeySym.Delete:
				Song.CurrentSong.RemoveSampleSlot(_currentSample);
				break;
			case KeySym.F9:
				ToggleMute(_currentSample);
				break;
			case KeySym.F10:
				ToggleSolo(_currentSample);
				break;
			default:
				return;
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void SampleListHandleKey(KeyEvent k)
	{
		int newSample = _currentSample;
		var sample = Song.CurrentSong.GetSample(_currentSample);

		switch (k.Sym)
		{
			case KeySym.Space:
				if (k.State == KeyState.Release)
					return;
				if (SelectedActiveWidget == otherSampleList)
					Status.Flags |= StatusFlags.NeedUpdate;
				return;
			case KeySym.Equals:
				if (!k.Modifiers.HasAnyFlag(KeyMod.Shift))
					return;
				goto case KeySym.Plus;
			case KeySym.Plus:
				if (k.State == KeyState.Release)
					return;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					sample = Song.CurrentSong.EnsureSample(_currentSample);
					sample.C5Speed *= 2;
					Status.Flags |= StatusFlags.SongNeedsSave;
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					sample = Song.CurrentSong.EnsureSample(_currentSample);
					sample.C5Speed = SongNote.CalculateHalfTone(sample.C5Speed, 1);
					Status.Flags |= StatusFlags.SongNeedsSave;
				}
				Status.Flags |= StatusFlags.NeedUpdate;
				return;
			case KeySym.Minus:
				if (k.State == KeyState.Release)
					return;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					sample = Song.CurrentSong.EnsureSample(_currentSample);
					sample.C5Speed /= 2;
					Status.Flags |= StatusFlags.SongNeedsSave;
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					sample = Song.CurrentSong.EnsureSample(_currentSample);
					sample.C5Speed = SongNote.CalculateHalfTone(sample.C5Speed, -1);
					Status.Flags |= StatusFlags.SongNeedsSave;
				}
				Status.Flags |= StatusFlags.NeedUpdate;
				return;

			case KeySym.Comma:
			case KeySym.Less:
				if (k.State == KeyState.Release)
					return;
				AudioPlayback.ChangeCurrentPlayChannel(-1, false);
				return;
			case KeySym.Period:
			case KeySym.Greater:
				if (k.State == KeyState.Release)
					return;
				AudioPlayback.ChangeCurrentPlayChannel(+1, false);
				return;
			case KeySym.PageUp:
				if (k.State == KeyState.Release)
					return;
				newSample--;
				break;
			case KeySym.PageDown:
				if (k.State == KeyState.Release)
					return;
				newSample++;
				break;
			case KeySym.Escape:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					if (k.State == KeyState.Release)
						return;
					_sampleListCursorPos = 25;
					FixAcceptText();
					ChangeFocusTo(otherSampleList);
					Status.Flags |= StatusFlags.NeedUpdate;
					return;
				}
				return;
			default:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Release)
						return;
					SampleListHandleAltKey(k);
				}
				else if (!k.IsRepeat)
				{
					int n, v;

					if (k.MIDINote > -1)
					{
						n = k.MIDINote;

						if (k.MIDIVolume > -1)
							v = k.MIDIVolume / 2;
						else
							v = KeyJazz.DefaultVolume;
					}
					else
					{
						n = (k.Sym == KeySym.Space)
							? _lastNote
							: k.NoteValue;

						if (n <= 0 || n > 120)
							return;

						v = KeyJazz.DefaultVolume;
					}

					if (k.State == KeyState.Release)
						Song.CurrentSong.KeyUp(_currentSample, KeyJazz.NoInstrument, n);
					else
					{
						Song.CurrentSong.KeyDown(_currentSample, KeyJazz.NoInstrument, n, v, KeyJazz.CurrentChannel);
						_lastNote = n;
					}
				}
				return;
		}

		newSample = newSample.Clamp(1, LastVisibleSampleNumber());

		if (newSample != _currentSample)
		{
			CurrentSample = newSample;
			Reposition();
			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	/* --------------------------------------------------------------------- */
	/* alt key dialog callbacks.
	* these don't need to do any actual redrawing, because the screen gets
	* redrawn anyway when the dialog is cleared. */

	void DoSignConvert()
	{
		if (Song.CurrentSong.GetSample(_currentSample) is SongSample sample)
			SampleEditOperations.SignConvert(sample);
	}

	void DoQualityConvert()
	{
		if (Song.CurrentSong.GetSample(_currentSample) is SongSample sample)
			SampleEditOperations.ToggleQuality(sample, true);
	}

	void DoQualityToggle()
	{
		if (Song.CurrentSong.GetSample(_currentSample) is SongSample sample)
		{
			if (sample.Flags.HasAllFlags(SampleFlags.Stereo))
				Status.FlashText("Can't toggle quality for stereo samples");
			else
				SampleEditOperations.ToggleQuality(sample, false);
		}
	}

	void DoDeleteSample()
	{
		Song.CurrentSong.ClearSample(_currentSample);
		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void DoDownmix()
	{
		if (Song.CurrentSong.GetSample(_currentSample) is SongSample sample)
			SampleEditOperations.Downmix(sample);
	}

	void DoPostLoopCut()
	{
		if (Song.CurrentSong.GetSample(_currentSample) is SongSample sample)
		{
			int pos = sample.Flags.HasAllFlags(SampleFlags.SustainLoop)
				? Math.Max(sample.LoopEnd, sample.SustainEnd)
				: sample.LoopEnd;

			if (pos == 0 || pos >= sample.Length)
				return;

			Status.Flags |= StatusFlags.SongNeedsSave;

			lock (AudioPlayback.LockScope())
			{
				Song.CurrentSong.StopSample(sample);

				if (sample.LoopEnd > pos) sample.LoopEnd = pos;
				if (sample.SustainEnd > pos) sample.SustainEnd = pos;

				sample.TakeSubset(0, pos);
				sample.AdjustLoop();
			}
		}
	}

	void DoPreLoopCut()
	{
		if (Song.CurrentSong.GetSample(_currentSample) is SongSample sample)
		{
			int pos = sample.Flags.HasAllFlags(SampleFlags.SustainLoop)
				? Math.Min(sample.LoopStart, sample.SustainStart)
				: sample.LoopStart;

			if (pos <= 0 || pos > sample.Length)
				return;

			Status.Flags |= StatusFlags.SongNeedsSave;

			lock (AudioPlayback.LockScope())
			{
				Song.CurrentSong.StopSample(sample);

				sample.TakeSubset(pos, sample.Length - pos);

				if (sample.LoopStart > pos)
					sample.LoopStart -= pos;
				else
					sample.LoopStart = 0;
				if (sample.SustainStart > pos)
					sample.SustainStart -= pos;
				else
					sample.SustainStart = 0;
				if (sample.LoopEnd > pos)
					sample.LoopEnd -= pos;
				else
					sample.LoopEnd = 0;
				if (sample.SustainEnd > pos)
					sample.SustainEnd -= pos;
				else
					sample.SustainEnd = 0;

				sample.AdjustLoop();
			}
		}
	}

	void DoCentralise()
	{
		if (Song.CurrentSong.GetSample(_currentSample) is SongSample sample)
			SampleEditOperations.Centralise(sample);
	}

	/* --------------------------------------------------------------------- */

	void DoAmplify(int percent)
	{
		if (Song.CurrentSong.GetSample(_currentSample) is SongSample sample)
			SampleEditOperations.Amplify(sample, percent);
	}

	void ShowSampleAmplifyDialog()
	{
		var dialog = Dialog.Show(new AmplifyDialog(_currentSample));

		dialog.ActionYes += () => DoAmplify(dialog.Percent);
	}

	/* --------------------------------------------------------------------- */

	void DoTextSynth(string textSynthEntry)
	{
		if (textSynthEntry.Length == 0)
			return;

		if (Song.CurrentSong.GetSample(_currentSample) is SongSample sample)
		{
			sample.Flags &= ~(SampleFlags._16Bit | SampleFlags.Stereo);
			sample.AllocateData();

			byte[] textSynthEntryBytes = Encoding.ASCII.GetBytes(textSynthEntry);

			textSynthEntryBytes.CopyTo(sample.Data.AsSpan());

			sample.LoopStart = 0;
			sample.LoopEnd = sample.Length;
			sample.SustainStart = sample.SustainEnd = 0;
			sample.Flags |= SampleFlags.Loop;
			sample.Flags &= ~(SampleFlags.PingPongLoop | SampleFlags.SustainLoop | SampleFlags.PingPongSustain
						| SampleFlags._16Bit | SampleFlags.Stereo | SampleFlags.AdLib);

			sample.AdjustLoop();

			ShowSampleHostDialog(PageNumbers.None);
		}

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void ShowTextSynthDialog()
	{
		var dialog = Dialog.Show<TextSynthDialog>();

		dialog.ActionYes = () => DoTextSynth(dialog.Entry);
	}

	/* --------------------------------------------------------------------- */

	void DoAdLibConfig(SongSample sample, byte[] newAdLibBytes)
	{
		//HelpIndex = HelpTexts.SampleList;

		// dumb hackaround that ought to some day be fixed:
		sample.Length = 1;
		sample.AllocateData();

		if (!sample.Flags.HasAllFlags(SampleFlags.AdLib))
		{
			sample.Flags |= SampleFlags.AdLib;
			Status.FlashText("Created adlib sample");
		}

		sample.AdLibBytes = newAdLibBytes;
		sample.Flags &= ~(SampleFlags._16Bit | SampleFlags.Stereo
				| SampleFlags.Loop | SampleFlags.PingPongLoop | SampleFlags.SustainLoop | SampleFlags.PingPongSustain);
		sample.LoopStart = sample.LoopEnd = 0;
		sample.SustainStart = sample.SustainEnd = 0;

		if (sample.C5Speed == 0)
		{
			sample.C5Speed = 8363;
			sample.Volume = 64 * 4;
			sample.GlobalVolume = 64;
		}

		ShowSampleHostDialog(PageNumbers.None);

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void ShowAdLibConfigDialog()
	{
		var dialog = Dialog.Show(new AdLibConfigDialog(_currentSample, _sampleImage));

		dialog.F1Pressed +=
			() =>
			{
				Status.CurrentHelpIndex = HelpTexts.AdLibSample;

				_dialogF1Hack = true;

				Dialog.DestroyAll();

				SetPage(PageNumbers.Help);
			};

		dialog.ActionYes += () => DoAdLibConfig(dialog.Sample, dialog.AdLibBytes);
	}

	void ShowAdLibPatchDialog()
	{
		var dialog = Dialog.Show(new NumberPromptDialog("Enter Patch (1-128)", '\0'));

		dialog.Finish +=
			n =>
			{
				if (n <= 0 || n > 128)
					return;

				var sample = Song.CurrentSong.EnsureSample(_currentSample);
				FMPatches.Apply(sample, n - 1);
				Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave; // redraw the sample

				ShowSampleHostDialog(PageNumbers.None);
			};
	}

	/* --------------------------------------------------------------------- */

	/* filename can be NULL, in which case the sample filename is used (quick save) */
	void SaveSample(string? fileName, SampleFileConverter converter)
	{
		var sample = Song.CurrentSong.EnsureSample(_currentSample);

		fileName ??= sample.FileName;

		if (string.IsNullOrWhiteSpace(fileName))
		{
			Status.FlashText("Sample NOT saved! (No Filename?)");
			return;
		}

		bool samplesDirectoryIsReachable = true;

		try
		{
			if (Directory.Exists(Configuration.Directories.SamplesDirectory)
			 && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				File.GetUnixFileMode(Configuration.Directories.SamplesDirectory);
		}
		catch
		{
			samplesDirectoryIsReachable = false;
		}

		if (!samplesDirectoryIsReachable)
		{
			Status.FlashText(string.Format("Sample directory \"{0}\" unreachable", Configuration.Directories.SamplesDirectory));
			return;
		}

		string effectiveSamplesDirectory = Configuration.Directories.SamplesDirectory;

		if (!Directory.Exists(effectiveSamplesDirectory))
		{
			/* directory browsing */
			effectiveSamplesDirectory = Path.GetDirectoryName(effectiveSamplesDirectory) ?? effectiveSamplesDirectory;
		}

		string ptr = Path.Combine(effectiveSamplesDirectory, fileName ?? sample.FileName);

		void DoSaveSample()
		{
			// I guess this function doesn't need to care about the return value,
			// since Song.SaveSample is handling all the visual feedback...
			Song.CurrentSong.SaveSample(ptr, converter, _currentSample);
		}

		if (Directory.Exists(ptr))
			Status.FlashText(fileName + " is a directory");
		else if (File.Exists(ptr))
		{
			if (Paths.IsRegularFile(ptr))
			{
				var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "Overwrite file?", DoSaveSample);

				dialog.SelectedWidgetIndex.Value = 1;
			}
			else
				Status.FlashText(fileName + " is not a regular file");
		}
		else
			DoSaveSample();
	}

	/* export sample dialog */

	void ShowExportSampleDialog()
	{
		SongSample sample = Song.CurrentSong.EnsureSample(_currentSample);

		var dialog = Dialog.Show(new ExportSampleDialog(sample.FileName));

		dialog.ActionYes += () => SaveSample(dialog.FileName, dialog.SampleConverter);
	}


	/* resize sample dialog */
	void DoResizeSampleAntiAlias(int sampleNumber, int newLength)
	{
		var sample = Song.CurrentSong.EnsureSample(sampleNumber);

		SampleEditOperations.Resize(sample, newLength, true);
	}

	void DoResizeSample(int sampleNumber, int newLength)
	{
		var sample = Song.CurrentSong.EnsureSample(sampleNumber);

		SampleEditOperations.Resize(sample, newLength, false);
	}

	void ShowResizeSampleDialog(bool antiAlias)
	{
		var sample = Song.CurrentSong.EnsureSample(_currentSample);

		var dialog = Dialog.Show(new ResizeSampleDialog(sample.Length));

		if (antiAlias)
			dialog.ActionYes += () => DoResizeSampleAntiAlias(_currentSample, dialog.NewLength);
		else
			dialog.ActionYes += () => DoResizeSample(_currentSample, dialog.NewLength);
	}

	/* resample sample dialog, mostly the same as above */
	void DoResampleSampleAntiAlias(int sampleNumber, int newC5Speed)
	{
		var sample = Song.CurrentSong.EnsureSample(sampleNumber);

		int newLen = (int)((long)sample.Length * newC5Speed / sample.C5Speed);

		SampleEditOperations.Resize(sample, newLen, true);

		sample.C5Speed = newC5Speed;
	}

	void DoResampleSample(int sampleNumber, int newC5Speed)
	{
		var sample = Song.CurrentSong.EnsureSample(sampleNumber);

		int newLen = (int)((long)sample.Length * newC5Speed / sample.C5Speed);

		SampleEditOperations.Resize(sample, newLen, false);

		sample.C5Speed = newC5Speed;
	}

	void ShowResampleSampleDialog(bool antiAlias)
	{
		var sample = Song.CurrentSong.EnsureSample(_currentSample);

		var dialog = Dialog.Show(new ResampleSampleDialog(sample.C5Speed));

		if (antiAlias)
			dialog.ActionYes += () => DoResampleSampleAntiAlias(_currentSample, dialog.NewC5Speed);
		else
			dialog.ActionYes += () => DoResampleSample(_currentSample, dialog.NewC5Speed);
	}

	/* --------------------------------------------------------------------- */

	// TODO openmpt has post loop fade, and we support it
	// internally, but it's never actually used or exposed
	// to the user.

	void ShowCrossfadeSampleDialog()
	{
		var sample = Song.CurrentSong.EnsureSample(_currentSample);

		var dialog = Dialog.Show(new CrossfadeSampleDialog(sample));

		dialog.ActionYes +=
			() =>
			{
				SampleEditOperations.CrossFade(sample, dialog.SamplesToFade, dialog.Priority + 50, false, dialog.SustainLoop);
			};
	}

	/* --------------------------------------------------------------------- */

	public static bool ShowSampleHostDialog(PageNumbers newPage)
	{
		/* Actually IT defaults to No when the sample slot already had a sample in it, rather than checking if
		it was assigned to an instrument. Maybe this is better, though?
		(Not to mention, passing around the extra state that'd be required to do it that way would be kind of
		messy...)

		also the double pointer cast sucks.

		also also, IT says Ok/No here instead of Yes/No... but do I care? */

		if (Song.CurrentSong.IsInstrumentMode)
		{
			int currentSample = AllPages.SampleList.CurrentSample;

			bool isUsed = Song.CurrentSong.SampleIsUsedByInstrument(currentSample);

			var dialog = MessageBox.Show(MessageBoxTypes.YesNo, "Create host instrument?");

			if (isUsed)
				dialog.ChangeFocusTo(1);

			dialog.ActionYes =
				() =>
				{
					Song.CurrentSong.CreateHostInstrument(currentSample);

					if (newPage >= 0)
						SetPage(newPage);
				};

			dialog.ActionNo =
				() =>
				{
					if (newPage >= 0)
						SetPage(newPage);
				};

			return true;
		}

		if (newPage != PageNumbers.None)
			SetPage(newPage);

		return false;
	}

	/* --------------------------------------------------------------------- */

	void SetMute(int n, bool mute)
	{
		var smp = Song.CurrentSong.EnsureSample(n);

		if (mute)
		{
			if (smp.Flags.HasAllFlags(SampleFlags.Mute))
				return;

			smp.SavedGlobalVolume = smp.GlobalVolume;
			smp.GlobalVolume = 0;
			smp.Flags |= SampleFlags.Mute;
		}
		else
		{
			if (!smp.Flags.HasAllFlags(SampleFlags.Mute))
				return;

			smp.GlobalVolume = smp.SavedGlobalVolume;
			smp.Flags &= ~SampleFlags.Mute;
		}
	}

	void ToggleMute(int n)
	{
		var smp = Song.CurrentSong.EnsureSample(n);

		SetMute(n, !smp.Flags.HasAllFlags(SampleFlags.Mute));
	}

	void ToggleSolo(int n)
	{
		bool solo = false;

		if (Song.CurrentSong.EnsureSample(n).Flags.HasAllFlags(SampleFlags.Mute))
		{
			solo = true;
		}
		else
		{
			for (int i = 1; i < Song.CurrentSong.Samples.Count; i++)
			{
				var smp = Song.CurrentSong.GetSample(i);

				if (i != n && (smp != null) && !smp.Flags.HasAllFlags(SampleFlags.Mute))
				{
					solo = true;
					break;
				}
			}
		}

		for (int i = 1; i < Song.CurrentSong.Samples.Count; i++)
			SetMute(i, solo && i != n);
	}

	/* --------------------------------------------------------------------- */
	/* wow. this got ugly. */

	/* callback for the loop menu toggles */
	void UpdateSampleLoopFlags()
	{
		lock (AudioPlayback.LockScope())
		{
			var sample = Song.CurrentSong.EnsureSample(_currentSample);

			/* these switch statements fall through */
			sample.Flags &= ~(SampleFlags.Loop | SampleFlags.PingPongLoop | SampleFlags.SustainLoop | SampleFlags.PingPongSustain);
			switch (menuToggleLoopEnable.State)
			{
				case 2: sample.Flags |= SampleFlags.PingPongLoop; goto case 1;
				case 1: sample.Flags |= SampleFlags.Loop; break;
			}

			switch (menuToggleSustainLoopEnable.State)
			{
				case 2: sample.Flags |= SampleFlags.PingPongSustain; goto case 1;
				case 1: sample.Flags |= SampleFlags.SustainLoop; break;
			}

			if (sample.Flags.HasAllFlags(SampleFlags.Loop))
			{
				if (sample.LoopStart == sample.Length)
					sample.LoopStart = 0;
				if (sample.LoopEnd <= sample.LoopStart)
					sample.LoopEnd = sample.Length;
			}

			if (sample.Flags.HasAllFlags(SampleFlags.SustainLoop))
			{
				if (sample.SustainStart == sample.Length)
					sample.SustainStart = 0;
				if (sample.SustainEnd <= sample.SustainStart)
					sample.SustainEnd = sample.Length;
			}

			sample.AdjustLoop();

			/* update any samples currently playing */
			Song.CurrentSong.UpdatePlayingSample(_currentSample);
		}

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	/* genericized this for both loops */
	void UpdateSampleLoopPointsImpl(ref int loopStart, ref int loopEnd,
		MenuToggleWidget loopToggleEntry, NumberEntryWidget loopStartEntry,
		NumberEntryWidget loopEndEntry, ref bool flagsChanged, int sampleLength)
	{
		if (loopStartEntry.Value > sampleLength - 1)
			loopStartEntry.Value = sampleLength - 1;

		if (loopEndEntry.Value <= loopStartEntry.Value)
		{
			loopToggleEntry.State = 0;
			flagsChanged = true;
		}
		else if (loopEndEntry.Value > sampleLength)
			loopEndEntry.Value = sampleLength;

		if (loopStart != loopStartEntry.Value
			|| loopEnd != loopEndEntry.Value)
			flagsChanged = true;

		loopStart = loopStartEntry.Value;
		loopEnd = loopEndEntry.Value;
	}

	/* callback for the loop numentries */
	void UpdateSampleLoopPoints()
	{
		lock (AudioPlayback.LockScope())
		{
			var sample = Song.CurrentSong.EnsureSample(_currentSample);

			bool flagsChanged = false;

			UpdateSampleLoopPointsImpl(ref sample.LoopStart,
				ref sample.LoopEnd,
				menuToggleLoopEnable,
				numberEntryLoopStart,
				numberEntryLoopEnd,
				ref flagsChanged, sample.Length);
			UpdateSampleLoopPointsImpl(ref sample.SustainStart,
				ref sample.SustainEnd,
				menuToggleSustainLoopEnable,
				numberEntrySustainLoopStart,
				numberEntrySustainLoopEnd,
				ref flagsChanged, sample.Length);

			if (flagsChanged)
				UpdateSampleLoopFlags();

			sample.AdjustLoop();
		}

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	/* --------------------------------------------------------------------- */

	void UpdateValuesInSong()
	{
		lock (AudioPlayback.LockScope())
		{
			var sample = Song.CurrentSong.EnsureSample(_currentSample);

			/* a few more modplug hacks here... */
			sample.Volume = thumbBarDefaultVolume.Value * 4;
			sample.GlobalVolume = thumbBarGlobalVolume.Value;

			if (toggleEnableDefaultPan.State)
				sample.Flags |= SampleFlags.Panning;
			else
				sample.Flags &= ~SampleFlags.Panning;

			sample.VibratoSpeed = thumbBarVibratoSpeed.Value;
			sample.VibratoDepth = thumbBarVibratoDepth.Value;

			if (toggleButtonVibratoSine.State)
				sample.VibratoType = VibratoType.Sine;
			else if (toggleButtonVibratoRampDown.State)
				sample.VibratoType = VibratoType.RampDown;
			else if (toggleButtonVibratoSquare.State)
				sample.VibratoType = VibratoType.Square;
			else
				sample.VibratoType = VibratoType.Random;

			sample.VibratoRate = thumbBarVibratoRate.Value;
		}

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void UpdateSampleSpeed()
	{
		var sample = Song.CurrentSong.EnsureSample(_currentSample);

		lock (AudioPlayback.LockScope())
			sample.C5Speed = numberEntryC5Speed.Value;

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	void UpdatePanning()
	{
		lock (AudioPlayback.LockScope())
		{
			var sample = Song.CurrentSong.EnsureSample(_currentSample);

			sample.Flags |= SampleFlags.Panning;
			sample.Panning = thumbBarDefaultPan.Value * 4;
		}

		toggleEnableDefaultPan.State = true;

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void UpdateFilename()
	{
		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	/* --------------------------------------------------------------------- */

	public void SynchronizeToInstrument()
	{
		int instNum = AllPages.InstrumentList.CurrentInstrument;

		var ins = Song.CurrentSong.GetInstrument(instNum);

		if (ins == null)
			return;

		int first = 0;

		for (int pos = 0; pos < ins.SampleMap.Length; pos++)
		{
			if (first == 0) first = ins.SampleMap[pos];

			if (ins.SampleMap[pos] == instNum)
			{
				CurrentSample = instNum;
				return;
			}
		}

		if (first > 0)
			CurrentSample = first;
		else
			CurrentSample = instNum;
	}

	public override void SynchronizeWith(Page other)
	{
		if (other is InstrumentListPage)
		{
			if (Song.CurrentSong.IsInstrumentMode)
				SynchronizeToInstrument();
		}
	}
}
