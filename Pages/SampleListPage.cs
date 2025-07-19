using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.Dialogs.Samples;
using ChasmTracker.Events;
using ChasmTracker.FileSystem;
using ChasmTracker.FileTypes.Converters;
using ChasmTracker.Input;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;
using Mono.Posix;
using Mono.Unix.Native;

namespace ChasmTracker.Pages;

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

	static readonly SaveFormat[] SaveFormats =
		new SaveFormat[]
		{
			new SaveFormat("ITS", "Impulse Tracker", ".its") { SampleConverter = new ITS() },
			new SaveFormat("S3I", "Scream Tracker", ".s3i") { SampleConverter = new S3I() },
			new SaveFormat("AIFF", "Audio IFF", ".aiff") { SampleConverter = new AIFF() },
			new SaveFormat("AU", "Sun/NeXT", ".au") { SampleConverter = new AU() },
			new SaveFormat("WAV", "WAV", ".wav") { SampleConverter = new WAV() },
			new SaveFormat("RAW", "Raw", ".raw") { SampleConverter = new RAW() },
		};

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

		thumbBarDefaultVolume.Changed += update_values_in_song;
		thumbBarGlobalVolume.Changed += update_values_in_song;
		toggleEnableDefaultPan.Changed += update_values_in_song;
		thumbBarDefaultPan.Changed += update_panning;
		thumbBarVibratoSpeed.Changed += update_values_in_song;
		thumbBarVibratoDepth.Changed += update_values_in_song;

		/* 7 -> 14 = top right box */
		textEntryFileName = new TextEntryWidget(new Point(64, 13), 13, "", 12);
		numberEntryC5Speed = new NumberEntryWidget(new Point(64, 14), 7, 0, 9999999, _sampleNumEntryCursorPos);
		menuToggleLoopEnable = new MenuToggleWidget(new Point(64, 15), LoopStates);
		numberEntryLoopStart = new NumberEntryWidget(new Point(64, 16), 7, 0, 9999999, _sampleNumEntryCursorPos);
		numberEntryLoopEnd = new NumberEntryWidget(new Point(64, 17), 7, 0, 9999999, _sampleNumEntryCursorPos);
		menuToggleSustainLoopEnable = new MenuToggleWidget(new Point(64, 18), LoopStates);
		numberEntrySustainLoopStart = new NumberEntryWidget(new Point(64, 19), 7, 0, 9999999, _sampleNumEntryCursorPos);
		numberEntrySustainLoopEnd = new NumberEntryWidget(new Point(64, 20), 7, 0, 9999999, _sampleNumEntryCursorPos);

		textEntryFileName.Changed += update_filename;
		numberEntryC5Speed.Changed += update_sample_loop_flags;
		menuToggleLoopEnable.Changed += update_sample_loop_flags;
		numberEntryLoopStart.Changed += update_sample_loop_points;
		numberEntryLoopEnd.Changed += update_sample_loop_points;
		menuToggleSustainLoopEnable.Changed += update_sample_loop_flags;
		numberEntrySustainLoopStart.Changed += update_sample_loop_points;
		numberEntrySustainLoopEnd.Changed += update_sample_loop_points;

		/* 15 -> 18 = vibrato waveforms */
		toggleButtonVibratoSine = new ToggleButtonWidget(new Point(57, 36), "\xB9\xBA", 3, VibratoWaveformsGroup);
		toggleButtonVibratoRampDown = new ToggleButtonWidget(new Point(67, 36), "\xBD\xBE", 3, VibratoWaveformsGroup);
		toggleButtonVibratoSquare = new ToggleButtonWidget(new Point(57, 39), "\xBB\xBC", 3, VibratoWaveformsGroup);
		toggleButtonVibratoRandom = new ToggleButtonWidget(new Point(67, 39), "Random", 1, VibratoWaveformsGroup);

		toggleButtonVibratoSine.Changed += update_values_in_song;
		toggleButtonVibratoRampDown.Changed += update_values_in_song;
		toggleButtonVibratoSquare.Changed += update_values_in_song;
		toggleButtonVibratoRandom.Changed += update_values_in_song;

		/* 19 = vibrato rate */
		thumbBarVibratoRate = new ThumbBarWidget(new Point(56, 46), 16, 0, 255);

		thumbBarVibratoRate.Changed += update_values_in_song;

		Widgets.Add(otherSampleList);
		Widgets.Add(thumbBarDefaultVolume);
		Widgets.Add(thumbBarGlobalVolume);
		Widgets.Add(toggleEnableDefaultPan);
		Widgets.Add(thumbBarDefaultPan);
		Widgets.Add(thumbBarVibratoSpeed);
		Widgets.Add(thumbBarVibratoDepth);
		Widgets.Add(textEntryFileName);
		Widgets.Add(numberEntryC5Speed);
		Widgets.Add(menuToggleLoopEnable);
		Widgets.Add(numberEntryLoopStart);
		Widgets.Add(numberEntryLoopEnd);
		Widgets.Add(menuToggleSustainLoopEnable);
		Widgets.Add(numberEntrySustainLoopStart);
		Widgets.Add(numberEntrySustainLoopEnd);
		Widgets.Add(toggleButtonVibratoSine);
		Widgets.Add(toggleButtonVibratoRampDown);
		Widgets.Add(toggleButtonVibratoSquare);
		Widgets.Add(toggleButtonVibratoRandom);
		Widgets.Add(thumbBarVibratoRate);
	}

	bool otherSampleList_HandleKey(KeyEvent k)
	{
		int newSample = _currentSample;
		int newCursorPos = _sampleListCursorPos;

		if (k.Mouse == MouseState.Click && k.MouseButton == MouseButton.Middle)
		{
			if (k.State == KeyState.Release)
				Status.Flags |= StatusFlags.ClippyPasteSelection;
			return 1;
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
						SetPage(PageNumbers.LoadSample);
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
				case KeySym.Pagedown:
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
					SetPage(PageNumbers.LoadSample);
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
					if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAlt)) {
						if (_sampleListCursorPos < 25) {
							sample_list_delete_next_char();
						}
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
					else if ((k.Modifiers.HasAnyFlag(KeyMod.Control)) == 0 && _sampleListCursorPos < 25)
					{
						if (k.State == KeyState.Release)
							return true;

						if (k->text)
							return SampleListHandleTextInput(k->text);

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

	void otherSampleList_HandleText(TextInputEvent k)
	{
		SampleListHandleTextInput(k.Text);
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
				VGAMem.DrawCharacter(isPlaying[n] > 1 ? 183 : 173, new Point(1, 13 + pos), isPlaying[n] ? (3, 2) : (1, 2));

			VGAMem.DrawText(n.ToString99(), new Point(2, 13 + pos), (sample.FlagsFlags.HasFlag(SampleFlags.Mute)) ? (1, 1) : (0, 2));

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

	public override void DrawConst()
	{
	//page->draw_const = sample_list_draw_const;
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
		thumbBarGlobalVolume.TextAtMinimum = sample.Flags.HasFlag(SampleFlags.Mute) ? "  Muted  " : "";

		/* default pan (another modplug hack) */
		toggleEnableDefaultPan.State = sample.Flags.HasFlag(SampleFlags.Panning);
		thumbBarDefaultPan.Value = sample.Panning / 4;

		thumbBarVibratoSpeed.Value = sample.VibratoSpeed;
		thumbBarVibratoDepth.Value = sample.VibratoDepth;
		textEntryFileName.Text = sample.FileName;
		numberEntryC5Speed.Value = sample.C5Speed;

		menuToggleLoopEnable.State =
			(sample.Flags.HasFlag(SampleFlags.Loop) ? (sample.Flags.HasFlag(SampleFlags.PingPongLoop) ? 2 : 1) : 0);
		numberEntryLoopStart.Value = sample.LoopStart;
		numberEntryLoopEnd.Value = sample.LoopEnd;
		menuToggleSustainLoopEnable.State =
			(sample.Flags.HasAnyFlag(SampleFlags.SustainLoop) ? (sample.Flags.HasFlag(SampleFlags.PingPongSustain) ? 2 : 1) : 0);
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
			buf = (sample.Flags.HasFlag(SampleFlags._16Bit) ? "16" : "8") + " bit" + (sample.Flags.HasFlag(SampleFlags.Stereo) ? " Stereo" : "");
		else
			buf = "No sample";

		VGAMem.DrawTextLen(buf, 13, new Point(64, 22), (2, 0));

		VGAMem.DrawTextLen(sample.Length.ToString(), 13, new Point(64, 23), (2, 0));

		VGAMem.DrawSampleData(_sampleImage, sample);
	}

	public override bool? HandleKey(KeyEvent k)
	{
		//page->handle_key = sample_list_handle_key;
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
			ShowAdlibConfigDialog();

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

		if (_sampleListCursorPos < smp.Name.Length)
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
			ShowSampleHostDialog(-1);
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
			if (sample.Flags.HasFlag(SampleFlags.Stereo))
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
			int pos = sample.Flags.HasFlag(SampleFlags.SustainLoop)
				? Math.Max(sample.LoopEnd, sample.SustainEnd)
				: sample.LoopEnd;

			if (pos == 0 || pos >= sample.length)
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
			int pos = sample.Flags.HasFlag(SampleFlags.SustainLoop)
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
		var dialog = Dialog.Show<AmplifyDialog>();

		dialog.ActionYes += _ => DoAmplify(dialog.Percent);
	}

	/* --------------------------------------------------------------------- */

	void DoTextSynth(string textSynthEntry)
	{
		if (textSynthEntry.Length == 0)
			return;

		if (Song.CurrentSong.GetSample(_currentSample) is SongSample sample)
		{
			sample.Flags &= ~(SampleFlags._16Bit | SampleFlags.Stereo);
			sample.Length = (textSynthEntry.Length + bytesPerFrame - 1) / bytesPerFrame;
			sample.AllocateData();

			byte[] textSynthEntryBytes = Encoding.ASCII.GetBytes(textSynthEntry);

			Buffer.BlockCopy(textSynthEntryBytes, 0, sample.Buffer, SongSample.AllocatePrepend, textSynthEntryBytes.Length);

			sample.LoopStart = 0;
			sample.LoopEnd = sample.Length;
			sample.SustainStart = sample.SustainEnd = 0;
			sample.Flags |= SampleFlags.Loop;
			sample.Flags &= ~(SampleFlags.PingPongLoop | SampleFlags.SustainLoop | SampleFlags.PingPongSustain
						| SampleFlags._16Bit | SampleFlags.Stereo | SampleFlags.Adlib);

			sample.AdjustLoop();

			ShowSampleHostDialog(-1);
		}

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void ShowTextSynthDialog()
	{
		var dialog = Dialog.Show<TextSynthDialog>();

		dialog.ActionYes = _ => DoTextSynth(dialog.Entry);
	}

	/* --------------------------------------------------------------------- */

	void DoAdlibConfig(SongSample sample, byte[] newAdlibBytes)
	{
		//HelpIndex = HelpTexts.SampleList;

		// dumb hackaround that ought to some day be fixed:
		sample.Length = 1;
		sample.AllocateData();

		if (!sample.Flags.HasFlag(SampleFlags.Adlib))
		{
			sample.Flags |= SampleFlags.Adlib;
			Status.FlashText("Created adlib sample");
		}

		sample.Flags &= ~(SampleFlags._16Bit | SampleFlags.Stereo
				| SampleFlags.Loop | SampleFlags.PingPongLoop | SampleFlags.SustainLoop | SampleFlags.PingPongSustain);
		sample.LoopStart = sample.LoopEnd = 0;
		sample.SustainStart = sample.SustainEnd = 0;
		if (sample.C5Speed == 0) {
			sample.C5Speed = 8363;
			sample.Volume = 64 * 4;
			sample.GlobalVolume = 64;
		}

		ShowSampleHostDialog(-1);

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void ShowAdlibConfigDialog()
	{
		var dialog = Dialog.Show(new AdlibConfigDialog(_currentSample, _sampleImage));

		dialog.F1Pressed +=
			() =>
			{
				Status.CurrentHelpIndex = HelpTexts.AdlibSample;

				_dialogF1Hack = true;

				Dialog.DestroyAll();

				SetPage(PageNumbers.Help);
			};

		dialog.ActionYes += _ => DoAdlibConfig(dialog.Sample, dialog.AdlibBytes);
	}

	void ShowAdlibPatchDialog()
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

				ShowSampleHostDialog(-1);
			};
	}

	/* --------------------------------------------------------------------- */

	/* filename can be NULL, in which case the sample filename is used (quick save) */
	void SaveSample(string? fileName, string format)
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
			Song.CurrentSong.SaveSample(ptr, format, _currentSample);
		}

		if (Directory.Exists(ptr))
			Status.FlashText(fileName + " is a directory");
		else if (File.Exists(ptr))
		{
			if (Paths.IsRegularFile(ptr))
			{
				var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "Overwrite file?", _ => DoSaveSample());

				dialog.SelectedWidgetIndex = 1;
			}
			else
				Status.FlashText(fileName + " is not a regular file");
		}
		else
			DoSaveSample();
	}

	/* export sample dialog */

	struct widget export_sample_widgets[4];
	char export_sample_filename[SCHISM_NAME_MAX + 1] = "";
	int export_sample_format = 0;

	void do_export_sample()
	{
		int exp = export_sample_format;
		int i, c;

		for (i = 0, c = 0; sample_save_formats[i].label; i++) {
			if (sample_save_formats[i].enabled && !sample_save_formats[i].enabled())
				continue;

			if (c == exp)
				break;

			c++;
		}

		if (!sample_save_formats[i].label) return; /* ??? */

		sample_save(export_sample_filename, sample_save_formats[i].label);
	}

	void export_sample_list_draw(void)
	{
		int n, focused = (*selected_widget == 3), c;

		draw_fill_chars(53, 24, 56, 31, DEFAULT_FG, 0);
		for (c = 0, n = 0; sample_save_formats[n].label; n++) {
			if (sample_save_formats[n].enabled && !sample_save_formats[n].enabled())
				continue;

			int fg = 6, bg = 0;
			if (focused && c == export_sample_format) {
				fg = 0;
				bg = 3;
			} else if (c == export_sample_format) {
				bg = 14;
			}
			draw_text_len(sample_save_formats[n].label, 4, 53, 24 + c, fg, bg);
			c++;
		}
	}

	int export_sample_list_handle_key(struct key_event * k)
	{
		int new_format = export_sample_format;

		if (k.State == KeyState.Release)
			return 0;
		switch (k.Sym) {
		case KeySym.Up:
			if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				return 0;
			new_format--;
			break;
		case KeySym.Down:
			if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				return 0;
			new_format++;
			break;
		case KeySym.Pageup:
		case KeySym.Home:
			if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				return 0;
			new_format = 0;
			break;
		case KeySym.Pagedown:
		case KeySym.End:
			if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				return 0;
			new_format = num_save_formats - 1;
			break;
		case KeySym.Tab:
			if (k.Modifiers.HasAnyFlag(KeyMod.Shift)) {
				widget_change_focus_to(0);
				return 1;
			}
			/* fall through */
		case KeySym.Left:
		case KeySym.Right:
			if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				return 0;
			widget_change_focus_to(0); /* should focus 0/1/2 depending on what's closest */
			return 1;
		default:
			return 0;
		}

		new_format = new_format.Clamp(0, num_save_formats - 1);
		if (new_format != export_sample_format) {
			/* update the option string */
			export_sample_format = new_format;
			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return 1;
	}

	void export_sample_draw_const(void)
	{
		draw_text("Export Sample", 34, 21, 0, 2);

		draw_text("Filename", 24, 24, 0, 2);
		draw_box(32, 23, 51, 25, BOX_THICK | BOX_INNER | BOX_INSET);

		draw_box(52, 23, 57, 32, BOX_THICK | BOX_INNER | BOX_INSET);
	}

	void export_sample_dialog(void)
	{
		song_sample_t *sample = song_get_sample(_currentSample);
		struct dialog *dialog;

		widget_create_textentry(export_sample_widgets + 0, 33, 24, 18, 0, 1, 3, NULL,
				export_sample_filename, ARRAY_SIZE(export_sample_filename) - 1);
		widget_create_button(export_sample_widgets + 1, 31, 35, 6, 0, 1, 2, 2, 2, dialog_yes_NULL, "OK", 3);
		widget_create_button(export_sample_widgets + 2, 42, 35, 6, 3, 2, 1, 1, 1, dialog_cancel_NULL, "Cancel", 1);
		widget_create_other(export_sample_widgets + 3, 0, export_sample_list_handle_key, NULL, export_sample_list_draw);

		strncpy(export_sample_filename, sample.filename, ARRAY_SIZE(export_sample_filename) - 1);
		export_sample_filename[ARRAY_SIZE(export_sample_filename) - 1] = 0;

		dialog = dialog_create_custom(21, 20, 39, 18, export_sample_widgets, 4, 0,
								export_sample_draw_const, NULL);
		dialog->action_yes = do_export_sample;
	}


	/* resize sample dialog */
	struct widget resize_sample_widgets[2];
	int resize_sample_cursor;

	void do_resize_sample_aa()
	{
		song_sample_t *sample = song_get_sample(_currentSample);
		uint32_t newlen = resize_sample_widgets[0].d.numentry.value;
		sample_resize(sample, newlen, 1);
	}

	void do_resize_sample()
	{
		song_sample_t *sample = song_get_sample(_currentSample);
		uint32_t newlen = resize_sample_widgets[0].d.numentry.value;
		sample_resize(sample, newlen, 0);
	}

	void resize_sample_draw_const(void)
	{
		draw_text("Resize Sample", 34, 24, 3, 2);
		draw_text("New Length", 31, 27, 0, 2);
		draw_box(41, 26, 49, 28, BOX_THICK | BOX_INNER | BOX_INSET);
	}

	void resize_sample_dialog(int aa)
	{
		song_sample_t *sample = song_get_sample(_currentSample);
		struct dialog *dialog;

		resize_sample_cursor = 0;
		widget_create_numentry(resize_sample_widgets + 0, 42, 27, 7, 0, 1, 1, NULL, 0, 9999999, &resize_sample_cursor);
		resize_sample_widgets[0].d.numentry.value = sample.length;
		widget_create_button(resize_sample_widgets + 1, 36, 30, 6, 0, 1, 1, 1, 1,
			dialog_cancel_NULL, "Cancel", 1);
		dialog = dialog_create_custom(26, 22, 29, 11, resize_sample_widgets, 2, 0,
			resize_sample_draw_const, NULL);
		dialog->action_yes = aa ? do_resize_sample_aa : do_resize_sample;
	}

	/* resample sample dialog, mostly the same as above */
	struct widget resample_sample_widgets[2];
	int resample_sample_cursor;

	void do_resample_sample_aa()
	{
		song_sample_t *sample = song_get_sample(_currentSample);
		uint32_t newlen = _muldiv(sample.length, resample_sample_widgets[0].d.numentry.value, sample.c5speed);
		sample_resize(sample, newlen, 1);
	}

	void do_resample_sample()
	{
		song_sample_t *sample = song_get_sample(_currentSample);
		uint32_t newlen = _muldiv(sample.length, resample_sample_widgets[0].d.numentry.value, sample.c5speed);
		sample_resize(sample, newlen, 0);
	}

	void resample_sample_draw_const(void)
	{
		draw_text("Resample Sample", 33, 24, 3, 2);
		draw_text("New Sample Rate", 28, 27, 0, 2);
		draw_box(43, 26, 51, 28, BOX_THICK | BOX_INNER | BOX_INSET);
	}

	void resample_sample_dialog(int aa)
	{
		song_sample_t *sample = song_get_sample(_currentSample);
		struct dialog *dialog;

		resample_sample_cursor = 0;
		widget_create_numentry(resample_sample_widgets + 0, 44, 27, 7, 0, 1, 1, NULL, 0, 9999999, &resample_sample_cursor);
		resample_sample_widgets[0].d.numentry.value = sample.c5speed;
		widget_create_button(resample_sample_widgets + 1, 37, 30, 6, 0, 1, 1, 1, 1,
			dialog_cancel_NULL, "Cancel", 1);
		dialog = dialog_create_custom(26, 22, 28, 11, resample_sample_widgets, 2, 0,
			resample_sample_draw_const, NULL);
		dialog->action_yes = aa ? do_resample_sample_aa : do_resample_sample;
	}

	/* --------------------------------------------------------------------- */

	// TODO openmpt has post loop fade, and we support it
	// internally, but it's never actually used or exposed
	// to the user.

	struct widget crossfade_sample_widgets[6];
	int crossfade_sample_length_cursor;
	const int crossfade_sample_loop_group[] = { 0, 1, -1 };

	void do_crossfade_sample()
	{
		song_sample_t *smp = song_get_sample(_currentSample);

		sample_crossfade(smp, crossfade_sample_widgets[2].d.numentry.value, crossfade_sample_widgets[3].d.thumbbar.value + 50, 0, crossfade_sample_widgets[1].d.togglebutton.state);
	}

	void crossfade_sample_draw_const(void)
	{
		draw_text("Crossfade Sample", 32, 22, 3, 2);
		draw_text("Samples To Fade", 28, 27, 0, 2);
		draw_text("Volume", 28, 29, 0, 2);
		draw_text("Power", 47, 29, 0, 2);
		draw_box(27, 30, 48, 32, BOX_THIN | BOX_INNER | BOX_INSET);

		draw_box(44, 26, 52, 28, BOX_THICK | BOX_INNER | BOX_INSET);
	}

	// regenerate the sample loop widget based on loop/susloop data
	void crossfade_sample_loop_changed(void)
	{
		song_sample_t *smp = song_get_sample(_currentSample);

		const int sustain = crossfade_sample_widgets[1].d.togglebutton.state;

		const uint32_t loop_start = (sustain) ? smp->sustain_start : smp->loop_start;
		const uint32_t loop_end = (sustain) ? smp->sustain_end : smp->loop_end;

		const uint32_t max = MIN(loop_end - loop_start, loop_start);

		widget_create_numentry(crossfade_sample_widgets + 2, 45, 27, 7, 0, 3, 3, NULL, 0, max, &crossfade_sample_length_cursor);
		crossfade_sample_widgets[2].d.numentry.value = max;
	}

	void crossfade_sample_dialog(void)
	{
		song_sample_t *smp = song_get_sample(_currentSample);
		struct dialog *dialog;

		// Sample Loop/Sustain Loop
		// FIXME the buttons for loop/sustain ought to be disabled when their respective loops are not valid
		widget_create_togglebutton(crossfade_sample_widgets + 0, 31, 24, 6, 0, 2, 1, 1, 1, crossfade_sample_loop_changed, "Loop",    2, crossfade_sample_loop_group);
		widget_create_togglebutton(crossfade_sample_widgets + 1, 41, 24, 7, 2, 2, 0, 0, 2, crossfade_sample_loop_changed, "Sustain", 1, crossfade_sample_loop_group);

		// Default to sustain loop if there is a sustain loop but no regular loop, or the regular loop is not valid
		crossfade_sample_widgets[((smp->flags & CHN_SUSTAINLOOP) && !((smp->flags & CHN_LOOP) && smp->loop_start && smp->loop_end)) ? 1 : 0].d.togglebutton.state = 1;

		// Samples To Fade; handled in other function to account for differences between
		// sample loop and sustain loop
		crossfade_sample_loop_changed();

		// Priority
		widget_create_thumbbar(crossfade_sample_widgets + 3, 28, 31, 20, 2, 4, 4, NULL, -50, 50);
		crossfade_sample_widgets[3].d.thumbbar.value = 0;

		// Cancel/OK
		widget_create_button(crossfade_sample_widgets + 4, 31, 34, 6, 3, 4, 5, 5, 5, dialog_cancel_NULL, "Cancel", 1);
		widget_create_button(crossfade_sample_widgets + 5, 41, 34, 6, 3, 5, 4, 4, 0, dialog_yes_NULL, "OK", 3);

		dialog = dialog_create_custom(26, 20, 28, 17, crossfade_sample_widgets, 6, 0, crossfade_sample_draw_const, NULL);
		dialog->action_yes = do_crossfade_sample;
	}

	/* --------------------------------------------------------------------- */

	void sample_set_mute(int n, int mute)
	{
		song_sample_t *smp = song_get_sample(n);

		if (mute) {
			if (smp->flags & CHN_MUTE)
				return;
			smp->globalvol_saved = smp->global_volume;
			smp->global_volume = 0;
			smp->flags |= CHN_MUTE;
		} else {
			if (!(smp->flags & CHN_MUTE))
				return;
			smp->global_volume = smp->globalvol_saved;
			smp->flags &= ~CHN_MUTE;
		}
	}

	void sample_toggle_mute(int n)
	{
		song_sample_t *smp = song_get_sample(n);
		sample_set_mute(n, !(smp->flags & CHN_MUTE));
	}

	void sample_toggle_solo(int n)
	{
		int i, solo = 0;

		if (song_get_sample(n)->flags & CHN_MUTE) {
			solo = 1;
		} else {
			for (i = 1; i < MAX_SAMPLES; i++) {
				if (i != n && !(song_get_sample(i)->flags & CHN_MUTE)) {
					solo = 1;
					break;
				}
			}
		}
		for (i = 1; i < MAX_SAMPLES; i++)
			sample_set_mute(i, solo && i != n);
	}

	/* --------------------------------------------------------------------- */

	void dialog_noop(void *x)
	{
		(void)x;
	}

	void sample_list_handle_alt_key(struct key_event * k)
	{
		song_sample_t *sample = song_get_sample(_currentSample);
		int canmod = (sample.data != NULL && !(sample.Flags & CHN_ADLIB));

		if (k.State == KeyState.Release)
			return;

		switch (k.Sym) {
		case KeySym.a:
			if (canmod)
				dialog_create(DIALOG_OK_CANCEL, "Convert sample?", do_sign_convert, NULL, 0, NULL);
			return;
		case KeySym.b:
			if (canmod && (sample.LoopStart > 0
							|| ((sample.Flags & CHN_SUSTAINLOOP) && sample.SustainStart > 0))) {
				dialog_create(DIALOG_OK_CANCEL, "Cut sample?", do_pre_loop_cut, NULL, 1, NULL);
			}
			return;
		case KeySym.d:
			Sf F(k.Modifiers.HasAnyFlag(KeyMod.Shift)) && !(status.flags & CLASSIC_MODE)) {
				if (canmod && sample.Flags & CHN_STEREO) {
					dialog_create(DIALOG_OK_CANCEL, "Downmix sample to mono?",
						do_downmix, NULL, 0, NULL);
				}
			} else {
				dialog_create(DIALOG_OK_CANCEL, "Delete sample?", do_delete_sample,
					NULL, 1, NULL);
			}
			return;
		case KeySym.e:
			if (canmod) {
				Sf F(k.Modifiers.HasAnyFlag(KeyMod.Shift)) && !(status.flags & CLASSIC_MODE))
					resample_sample_dialog(1);
				else
					resize_sample_dialog(1);
			}
			break;
		case KeySym.f:
			if (canmod) {
				Sf F(k.Modifiers.HasAnyFlag(KeyMod.Shift)) && !(status.flags & CLASSIC_MODE))
					resample_sample_dialog(0);
				else
					resize_sample_dialog(0);
			}
			break;
		case KeySym.g:
			if (canmod)
				sample_reverse(sample);
			break;
		case KeySym.h:
			if (canmod)
				dialog_create(DIALOG_YES_NO, "Centralise sample?", do_centralise, NULL, 0, NULL);
			return;
		case KeySym.i:
			if (canmod)
				sample_invert(sample);
			break;
		case KeySym.l:
			if (canmod && (sample.LoopEnd > 0
							|| ((sample.Flags & CHN_SUSTAINLOOP) && sample.SustainEnd > 0))) {
				dialog_create(DIALOG_OK_CANCEL, "Cut sample?", do_post_loop_cut, NULL, 1, NULL);
			}
			return;
		case KeySym.m:
			if (canmod)
				sample_amplify_dialog();
			return;
		case KeySym.n:
			song_toggle_multichannel_mode();
			return;
		case KeySym.o:
			sample_save(NULL, "ITS");
			return;
		case KeySym.p:
			smpprompt_create("Copy sample:", "Sample", do_copy_sample);
			return;
		case KeySym.q:
			if (canmod) {
				dialog_create(DIALOG_YES_NO, "Convert sample?",
							do_quality_convert, do_quality_toggle, 0, NULL);
			}
			return;
		case KeySym.r:
			smpprompt_create("Replace sample with:", "Sample", do_replace_sample);
			return;
		case KeySym.s:
			smpprompt_create("Swap sample with:", "Sample", do_swap_sample);
			return;
		case KeySym.t:
			export_sample_dialog();
			return;
		case KeySym.v:
			Sf F!canmod || (status.flags & CLASSIC_MODE))
				return;

			if (!(sample.Flags & (CHN_LOOP|CHN_SUSTAINLOOP))) {
				dialog_create(DIALOG_OK, "Crossfade requires a sample loop to work.", dialog_noop, NULL, 0, NULL);
				return;
			}

			if (!sample.LoopStart && !sample.SustainStart) {
				dialog_create(DIALOG_OK, "Crossfade requires data before the sample loop.", dialog_noop, NULL, 0, NULL);
				return;
			}

			crossfade_sample_dialog();
			return;
		case KeySym.w:
			sample_save(NULL, "RAW");
			return;
		case KeySym.x:
			smpprompt_create("Exchange sample with:", "Sample", do_exchange_sample);
			return;
		case KeySym.y:
			/* hi virt */
			txtsynth_dialog();
			return;
		case KeySym.z:
			{ // uguu~
				void (*dlg)(void *) = (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					? sample_adlibpatch_dialog
					: sample_adlibconfig_dialog;
				if (canmod) {
					dialog_create(DIALOG_OK_CANCEL, "This will replace the current sample.",
									dlg, NULL, 1, NULL);
				} else {
					dlg(NULL);
				}
			}
			return;
		case KeySym.Insert:
			song_insert_sample_slot(_currentSample);
			break;
		case KeySym.Delete:
			song_remove_sample_slot(_currentSample);
			break;
		case KeySym.F9:
			sample_toggle_mute(_currentSample);
			break;
		case KeySym.F10:
			sample_toggle_solo(_currentSample);
			break;
		default:
			return;
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void sample_list_handle_key(struct key_event * k)
	{
		int new_sample = _currentSample;
		song_sample_t *sample = song_get_sample(_currentSample);

		switch (k.Sym) {
		case KeySym.Space:
			if (k.State == KeyState.Release)
				return;
			if (selected_widget && *selected_widget == 0) {
				Status.Flags |= StatusFlags.NeedUpdate;
			}
			return;
		case KeySym.Equals:
			if (!(k.Modifiers.HasAnyFlag(KeyMod.Shift)))
				return;
			SCHISM_FALLTHROUGH;
		case KeySym.Plus:
			if (k.State == KeyState.Release)
				return;
			if (k.Modifiers.HasAnyFlag(KeyMod.Alt)) {
				sample.c5speed *= 2;
				Status.Flags |= StatusFlags.SongNeedsSave;
			} else if (k.Modifiers.HasAnyFlag(KeyMod.Control)) {
				sample.c5speed = calc_halftone(sample.c5speed, 1);
				Status.Flags |= StatusFlags.SongNeedsSave;
			}
			Status.Flags |= StatusFlags.NeedUpdate;
			return;
		case KeySym.Minus:
			if (k.State == KeyState.Release)
				return;
			if (k.Modifiers.HasAnyFlag(KeyMod.Alt)) {
				sample.c5speed /= 2;
				Status.Flags |= StatusFlags.SongNeedsSave;
			} else if (k.Modifiers.HasAnyFlag(KeyMod.Control)) {
				sample.c5speed = calc_halftone(sample.c5speed, -1);
				Status.Flags |= StatusFlags.SongNeedsSave;
			}
			Status.Flags |= StatusFlags.NeedUpdate;
			return;

		case KeySym.Comma:
		case KeySym.Less:
			if (k.State == KeyState.Release)
				return;
			song_change_current_play_channel(-1, 0);
			return;
		case KeySym.Period:
		case KeySym.Greater:
			if (k.State == KeyState.Release)
				return;
			song_change_current_play_channel(1, 0);
			return;
		case KeySym.Pageup:
			if (k.State == KeyState.Release)
				return;
			new_sample--;
			break;
		case KeySym.Pagedown:
			if (k.State == KeyState.Release)
				return;
			new_sample++;
			break;
		case KeySym.Escape:
			if (k.Modifiers.HasAnyFlag(KeyMod.Shift)) {
				if (k.State == KeyState.Release)
					return;
				_sampleListCursorPos = 25;
				_fix_accept_text();
				widget_change_focus_to(0);
				Status.Flags |= StatusFlags.NeedUpdate;
				return;
			}
			return;
		default:
			if (k.Modifiers.HasAnyFlag(KeyMod.Alt)) {
				if (k.State == KeyState.Release)
					return;
				sample_list_handle_alt_key(k);
			} else if (!k.IsRepeat) {
				int n, v;
				if (k->midi_note > -1) {
					n = k->midi_note;
					if (k->midi_volume > -1) {
						v = k->midi_volume / 2;
					} else {
						v = KEYJAZZ_DEFAULTVOL;
					}
				} else {
					n = (k.Sym == KeySym.Space)
						? last_note
						: kbd_get_note(k);
					if (n <= 0 || n > 120)
						return;
					v = KEYJAZZ_DEFAULTVOL;
				}
				if (k.State == KeyState.Release) {
					song_keyup(_currentSample, KEYJAZZ_NOINST, n);
				} else {
					song_keydown(_currentSample, KEYJAZZ_NOINST, n, v, KEYJAZZ_CHAN_CURRENT);
					last_note = n;
				}
			}
			return;
		}

		new_sample = new_sample.Clamp(1, LastVisibleSampleNumber());

		if (new_sample != _currentSample) {
			sample_set(new_sample);
			sample_list_reposition();
			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	/* --------------------------------------------------------------------- */
	/* wheesh */

	void sample_list_draw_const(void)
	{
		int n;

		draw_box(4, 12, 35, 48, BOX_THICK | BOX_INNER | BOX_INSET);
		draw_box(63, 12, 77, 24, BOX_THICK | BOX_INNER | BOX_INSET);

		draw_box(36, 12, 53, 18, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(37, 15, 47, 17, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(36, 19, 53, 25, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(37, 22, 47, 24, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(36, 26, 53, 33, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(37, 29, 47, 32, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(36, 35, 53, 41, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(37, 38, 47, 40, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(36, 42, 53, 48, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(37, 45, 47, 47, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(54, 25, 77, 30, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(54, 31, 77, 41, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(54, 42, 77, 48, BOX_THIN | BOX_INNER | BOX_INSET);
		draw_box(55, 45, 72, 47, BOX_THIN | BOX_INNER | BOX_INSET);

		draw_fill_chars(41, 30, 46, 30, DEFAULT_FG, 0);
		draw_fill_chars(64, 13, 76, 23, DEFAULT_FG, 0);

		draw_text("Default Volume", 38, 14, 0, 2);
		draw_text("Global Volume", 38, 21, 0, 2);
		draw_text("Default Pan", 39, 28, 0, 2);
		draw_text("Vibrato Speed", 38, 37, 0, 2);
		draw_text("Vibrato Depth", 38, 44, 0, 2);
		draw_text("Filename", 55, 13, 0, 2);
		draw_text("Speed", 58, 14, 0, 2);
		draw_text("Loop", 59, 15, 0, 2);
		draw_text("LoopBeg", 56, 16, 0, 2);
		draw_text("LoopEnd", 56, 17, 0, 2);
		draw_text("SusLoop", 56, 18, 0, 2);
		draw_text("SusLBeg", 56, 19, 0, 2);
		draw_text("SusLEnd", 56, 20, 0, 2);
		draw_text("Quality", 56, 22, 0, 2);
		draw_text("Length", 57, 23, 0, 2);
		draw_text("Vibrato Waveform", 58, 33, 0, 2);
		draw_text("Vibrato Rate", 60, 44, 0, 2);

		for (n = 0; n < 13; n++)
			draw_char(154, 64 + n, 21, 3, 0);
	}

	/* --------------------------------------------------------------------- */
	/* wow. this got ugly. */

	/* callback for the loop menu toggles */
	void update_sample_loop_flags(void)
	{
		song_sample_t *sample = song_get_sample(_currentSample);

		/* these switch statements fall through */
		sample.Flags &= ~(CHN_LOOP | CHN_PINGPONGLOOP | CHN_SUSTAINLOOP | CHN_PINGPONGSUSTAIN);
		switch (widgets_samplelist[9].d.menutoggle.state) {
		case 2: sample.Flags |= CHN_PINGPONGLOOP; SCHISM_FALLTHROUGH;
		case 1: sample.Flags |= CHN_LOOP;
		}

		switch (widgets_samplelist[12].d.menutoggle.state) {
		case 2: sample.Flags |= CHN_PINGPONGSUSTAIN; SCHISM_FALLTHROUGH;
		case 1: sample.Flags |= CHN_SUSTAINLOOP;
		}

		if (sample.Flags & CHN_LOOP) {
			if (sample.LoopStart == sample.length)
				sample.LoopStart = 0;
			if (sample.LoopEnd <= sample.LoopStart)
				sample.LoopEnd = sample.length;
		}

		if (sample.Flags & CHN_SUSTAINLOOP) {
			if (sample.SustainStart == sample.length)
				sample.SustainStart = 0;
			if (sample.SustainEnd <= sample.SustainStart)
				sample.SustainEnd = sample.length;
		}

		csf_adjust_sample_loop(sample);

		/* update any samples currently playing */
		song_update_playing_sample(_currentSample);

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}
ShowSampleHostDialog/* callback for the loop numentries */
	void update_sample_loop_points(void)
	{
		song_sample_t *sample = song_get_sample(_currentSample);
		int flags_changed = 0;

		/* 9 = loop toggle, 10 = loop start, 11 = loop end */
		if ((unsigned long) widgets_samplelist[10].d.numentry.value > sample.length - 1)
			widgets_samplelist[10].d.numentry.value = sample.length - 1;
		if (widgets_samplelist[11].d.numentry.value <= widgets_samplelist[10].d.numentry.value) {
			widgets_samplelist[9].d.menutoggle.state = 0;
			flags_changed = 1;
		} else if ((unsigned long) widgets_samplelist[11].d.numentry.value > sample.length) {
			widgets_samplelist[11].d.numentry.value = sample.length;
		}
		if (sample.LoopStart != (unsigned long) widgets_samplelist[10].d.numentry.value
		|| sample.LoopEnd != (unsigned long) widgets_samplelist[11].d.numentry.value) {
			flags_changed = 1;
		}
		sample.LoopStart = widgets_samplelist[10].d.numentry.value;
		sample.LoopEnd = widgets_samplelist[11].d.numentry.value;

		/* 12 = sus toggle, 13 = sus start, 14 = sus end */
		if ((unsigned long) widgets_samplelist[13].d.numentry.value > sample.length - 1)
			widgets_samplelist[13].d.numentry.value = sample.length - 1;
		if (widgets_samplelist[14].d.numentry.value <= widgets_samplelist[13].d.numentry.value) {
			widgets_samplelist[12].d.menutoggle.state = 0;
			flags_changed = 1;
		} else if ((unsigned long) widgets_samplelist[14].d.numentry.value > sample.length) {
			widgets_samplelist[14].d.numentry.value = sample.length;
		}
		if (sample.SustainStart != (unsigned long) widgets_samplelist[13].d.numentry.value
		|| sample.SustainEnd != (unsigned long) widgets_samplelist[14].d.numentry.value) {
			flags_changed = 1;
		}
		sample.SustainStart = widgets_samplelist[13].d.numentry.value;
		sample.SustainEnd = widgets_samplelist[14].d.numentry.value;

		if (flags_changed) {
			update_sample_loop_flags();
		}

		csf_adjust_sample_loop(sample);

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}
ShowSampleHostDialog/* --------------------------------------------------------------------- */

	void update_values_in_song(void)
	{
		song_sample_t *sample = song_get_sample(_currentSample);

		/* a few more modplug hacks here... */
		sample.volume = widgets_samplelist[1].d.thumbbar.value * 4;
		sample.GlobalVolume = widgets_samplelist[2].d.thumbbar.value;

		if (widgets_samplelist[3].d.toggle.state)
			sample.Flags |= CHN_PANNING;
		else
			sample.Flags &= ~CHN_PANNING;
		sample.vib_speed = widgets_samplelist[5].d.thumbbar.value;
		sample.vib_depth = widgets_samplelist[6].d.thumbbar.value;

		if (widgets_samplelist[15].d.togglebutton.state)
			sample.VibratoType = VIB_SINE;
		else if (widgets_samplelist[16].d.togglebutton.state)
			sample.VibratoType = VIB_RAMP_DOWN;
		else if (widgets_samplelist[17].d.togglebutton.state)
			sample.VibratoType = VIB_SQUARE;
		else
			sample.VibratoType = VIB_RANDOM;
		sample.VibratoRate = widgets_samplelist[19].d.thumbbar.value;

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void update_sample_speed(void)
	{
		song_sample_t *sample = song_get_sample(_currentSample);

		sample.c5speed = widgets_samplelist[8].d.numentry.value;

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}
ShowSampleHostDialogvoid update_panning(void)
	{
		song_sample_t *sample = song_get_sample(_currentSample);

		sample.Flags |= CHN_PANNING;
		sample.panning = widgets_samplelist[4].d.thumbbar.value * 4;

		widgets_samplelist[3].d.toggle.state = 1;

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	void update_filename(void)
	{
		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	/* --------------------------------------------------------------------- */

	void sample_synchronize_to_instrument(void)
	{
		song_instrument_t *ins;
		int instnum = instrument_get_current();
		int pos, first;

		ins = song_get_instrument(instnum);
		first = 0;
		for (pos = 0; pos < 120; pos++) {
			if (first == 0) first = ins->sample_map[pos];
			if (ins->sample_map[pos] == (unsigned int)instnum) {
				sample_set(instnum);
				return;
			}
		}
		if (first > 0) {
			sample_set(first);
		} else {
			sample_set(instnum);
		}
	}
}
