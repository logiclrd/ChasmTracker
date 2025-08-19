using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.Dialogs.Instruments;
using ChasmTracker.Events;
using ChasmTracker.FileSystem;
using ChasmTracker.FileTypes;
using ChasmTracker.FileTypes.InstrumentConverters;
using ChasmTracker.Input;
using ChasmTracker.Memory;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

/* This is getting almost as disturbing as the pattern editor. */
public abstract class InstrumentListPage : Page
{
	public static InstrumentListPage CurrentSubpage = AllPages.InstrumentListGeneral;

	// Widgets shared across all 4 subpages. They literally use the same actual widget.
	protected static OtherWidget otherInstrumentList;
	protected static ToggleButtonWidget toggleButtonSubpageGeneral;
	protected static ToggleButtonWidget toggleButtonSubpageVolume;
	protected static ToggleButtonWidget toggleButtonSubpagePanning;
	protected static ToggleButtonWidget toggleButtonSubpagePitch;

	static Shared<int> s_commonSelectedWidgetIndex = new Shared<int>();

	static List<Widget> s_commonWidgets = new List<Widget>();

	protected const int SubpageSwitchesGroup = 1;
	protected const int NNAGroup = 2;
	protected const int DCTGroup = 3;
	protected const int DCAGroup = 4;

	static readonly string[] PitchEnvelopeStates = { "Off", "On Pitch", "On Filter" };

	static int s_topInstrument = 1;
	static int s_altSwap_lastVisibleInstrumentNumber = 99; // for alt-down instrument-swapping

	protected static int s_instrumentCursorPos = 25; /* "play" mode */

	protected static bool s_envelopeEditMode = false;
	protected static bool s_envelopeMouseEdit = false;
	protected static int s_envelopeTickLimit = 0;

	/* playback */
	protected static int s_lastNote = 61;      /* C-5 */

	/* strange saved envelopes */
	Envelope[] _savedEnv = new Envelope[10];
	InstrumentFlags[] _savedEnvFlags = new InstrumentFlags[10];

	/* rastops for envelope */
	protected static VGAMemOverlay s_envOverlay;

	static InstrumentListPage()
	{
		s_envOverlay = VGAMem.AllocateOverlay(new Point(32, 18), new Point(65, 25));

		/* the first five widgets are the same for all four pages. */

		/* instrument list */
		otherInstrumentList = new OtherWidget(new Point(5, 13), new Size(24, 34));
		otherInstrumentList.OtherAcceptsText = (s_instrumentCursorPos < 25);
		otherInstrumentList.OtherRedraw += otherInstrumentList_Redraw;
		otherInstrumentList.OtherHandleText += otherInstrumentList_HandleText;
		otherInstrumentList.OtherHandleKey += otherInstrumentList_HandleKey;

		/* subpage switches */
		toggleButtonSubpageGeneral = new ToggleButtonWidget(new Point(32, 13), 7, "General", 1, SubpageSwitchesGroup);
		toggleButtonSubpageVolume = new ToggleButtonWidget(new Point(44, 13), 7, "Volume", 1, SubpageSwitchesGroup);
		toggleButtonSubpagePanning = new ToggleButtonWidget(new Point(56, 13), 7, "Panning", 1, SubpageSwitchesGroup);
		toggleButtonSubpagePitch = new ToggleButtonWidget(new Point(68, 13), 7, "Pitch", 2, SubpageSwitchesGroup);

		toggleButtonSubpageGeneral.Changed += ChangeSubpage;
		toggleButtonSubpageVolume.Changed += ChangeSubpage;
		toggleButtonSubpagePanning.Changed += ChangeSubpage;
		toggleButtonSubpagePitch.Changed += ChangeSubpage;

		s_commonWidgets.Add(otherInstrumentList);
		s_commonWidgets.Add(toggleButtonSubpageGeneral);
		s_commonWidgets.Add(toggleButtonSubpageVolume);
		s_commonWidgets.Add(toggleButtonSubpagePanning);
		s_commonWidgets.Add(toggleButtonSubpagePitch);
	}

	public InstrumentListPage(PageNumbers number)
		: base(number, "Instrument List (F4)", HelpTexts.InstrumentList)
	{
		for (int i = 0; i < _savedEnv.Length; i++)
			_savedEnv[i] = new Envelope(32);

		AddSharedWidgets(s_commonWidgets);

		SelectedWidgetIndex = s_commonSelectedWidgetIndex;
	}

	public override void SetPage()
	{
		CurrentSubpage = this;

		InstrumentListReposition();
	}

	public override void SynchronizeWith(Page other)
	{
		base.SynchronizeWith(other);

		AllPages.InstrumentList = this;

		if (other is SampleListPage sampleList)
		{
			if (Song.CurrentSong.IsInstrumentMode)
				SynchronizeToSample();
			else
				CurrentInstrument = sampleList.CurrentSample;
		}
	}

	static int s_currentInstrument = 1;

	public int CurrentInstrument
	{
		get => s_currentInstrument;
		set => SetCurrentInstrument(value);
	}

	static void SetCurrentInstrument(int value)
	{
		int newInstrument = value;

		if (Status.CurrentPage is InstrumentListPage)
			newInstrument = Math.Max(1, newInstrument);
		else
			newInstrument = Math.Max(0, newInstrument);

		newInstrument = Math.Min(LastVisibleInstrumentNumber(), newInstrument);

		if (s_currentInstrument == newInstrument)
			return;

		s_envelopeEditMode = false;

		s_currentInstrument = newInstrument;

		InstrumentListReposition();

		var ins = Song.CurrentSong.GetInstrument(s_currentInstrument);

		AllPages.InstrumentListVolume.ResetCurrentNode(ins);
		AllPages.InstrumentListPanning.ResetCurrentNode(ins);
		AllPages.InstrumentListPitch.ResetCurrentNode(ins);

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	protected void SaveEnvelope(int slot, Envelope e, InstrumentFlags sec)
	{
		slot = (int)(unchecked((uint)slot) % 10);

		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		_savedEnv[slot] = e.Clone();

		switch (sec)
		{
			case InstrumentFlags.VolumeEnvelope:
				_savedEnvFlags[slot] = ins.Flags & (InstrumentFlags.VolumeEnvelope | InstrumentFlags.VolumeEnvelopeSustain | InstrumentFlags.VolumeEnvelopeLoop | InstrumentFlags.VolumeEnvelopeCarry);
				break;
			case InstrumentFlags.PanningEnvelope:
				_savedEnvFlags[slot] =
					(ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelope) ? InstrumentFlags.VolumeEnvelope : 0) |
					(ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeSustain) ? InstrumentFlags.VolumeEnvelopeSustain : 0) |
					(ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeLoop) ? InstrumentFlags.VolumeEnvelopeLoop : 0) |
					(ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeCarry) ? InstrumentFlags.VolumeEnvelopeCarry : 0);
				break;
			case InstrumentFlags.PitchEnvelope:
				_savedEnvFlags[slot] =
					(ins.Flags.HasAllFlags(InstrumentFlags.PitchEnvelope) ? InstrumentFlags.VolumeEnvelope : 0) |
					(ins.Flags.HasAllFlags(InstrumentFlags.PitchEnvelopeSustain) ? InstrumentFlags.VolumeEnvelopeSustain : 0) |
					(ins.Flags.HasAllFlags(InstrumentFlags.PitchEnvelopeLoop) ? InstrumentFlags.VolumeEnvelopeLoop : 0) |
					(ins.Flags.HasAllFlags(InstrumentFlags.PitchEnvelopeCarry) ? InstrumentFlags.VolumeEnvelopeCarry : 0);
				break;
		}
	}

	protected void RestoreEnvelope(int slot, Envelope e, InstrumentFlags sec)
	{
		using (AudioPlayback.LockScope())
		{
			slot = (int)(unchecked((uint)slot) % 10);

			var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

			e.CopyFrom(_savedEnv[slot]);

			switch (sec)
			{
				case InstrumentFlags.VolumeEnvelope:
					ins.Flags &= ~(InstrumentFlags.VolumeEnvelope | InstrumentFlags.VolumeEnvelopeSustain | InstrumentFlags.VolumeEnvelopeLoop | InstrumentFlags.VolumeEnvelopeCarry);
					ins.Flags |= (_savedEnvFlags[slot] & (InstrumentFlags.VolumeEnvelope | InstrumentFlags.VolumeEnvelopeSustain | InstrumentFlags.VolumeEnvelopeLoop | InstrumentFlags.VolumeEnvelopeCarry));
					break;

				case InstrumentFlags.PanningEnvelope:
					ins.Flags &= ~(InstrumentFlags.PanningEnvelope | InstrumentFlags.PanningEnvelopeSustain | InstrumentFlags.PanningEnvelopeLoop | InstrumentFlags.PanningEnvelopeCarry);
					if (_savedEnvFlags[slot].HasAllFlags(InstrumentFlags.VolumeEnvelope)) ins.Flags |= InstrumentFlags.PanningEnvelope;
					if (_savedEnvFlags[slot].HasAllFlags(InstrumentFlags.VolumeEnvelopeSustain)) ins.Flags |= InstrumentFlags.PanningEnvelopeSustain;
					if (_savedEnvFlags[slot].HasAllFlags(InstrumentFlags.VolumeEnvelopeLoop)) ins.Flags |= InstrumentFlags.PanningEnvelopeLoop;
					if (_savedEnvFlags[slot].HasAllFlags(InstrumentFlags.VolumeEnvelopeCarry)) ins.Flags |= InstrumentFlags.PanningEnvelopeCarry;
					ins.Flags |= (_savedEnvFlags[slot] & InstrumentFlags.SetPanning);
					break;

				case InstrumentFlags.PitchEnvelope:
					ins.Flags &= ~(InstrumentFlags.PitchEnvelope | InstrumentFlags.PitchEnvelopeSustain | InstrumentFlags.PitchEnvelopeLoop | InstrumentFlags.PitchEnvelopeCarry);
					if (_savedEnvFlags[slot].HasAllFlags(InstrumentFlags.VolumeEnvelope)) ins.Flags |= InstrumentFlags.PitchEnvelope;
					if (_savedEnvFlags[slot].HasAllFlags(InstrumentFlags.VolumeEnvelopeSustain)) ins.Flags |= InstrumentFlags.PitchEnvelopeSustain;
					if (_savedEnvFlags[slot].HasAllFlags(InstrumentFlags.VolumeEnvelopeLoop)) ins.Flags |= InstrumentFlags.PitchEnvelopeLoop;
					if (_savedEnvFlags[slot].HasAllFlags(InstrumentFlags.VolumeEnvelopeCarry)) ins.Flags |= InstrumentFlags.PitchEnvelopeCarry;
					ins.Flags |= (_savedEnvFlags[slot] & InstrumentFlags.Filter);
					break;
			}
		}

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* the actual list */
	static int LastVisibleInstrumentNumber()
	{
		if (Song.CurrentSong.Instruments.Count == 0)
			return 0;

		int n = 99;
		int j = 0;

		/* 65 is first visible instrument on last page */
		for (int i = 65; i < Song.CurrentSong.Instruments.Count; i++)
			if (!Song.CurrentSong.GetInstrument(i).IsEmpty)
				j = i;

		while ((j + 34) > n)
			n += 34;

		return Math.Min(n, Song.CurrentSong.Instruments.Count - 1);
	}

	static void InstrumentListReposition()
	{
		if (s_currentInstrument < s_topInstrument)
		{
			s_topInstrument = s_currentInstrument;
			if (s_topInstrument < 1)
				s_topInstrument = 1;
		}
		else if (s_currentInstrument > s_topInstrument + 34)
			s_topInstrument = s_currentInstrument - 34;
	}

	public void SynchronizeToSample()
	{
		byte sample = (byte)AllPages.SampleList.CurrentSample;

		/* 1. if the instrument with the same number as the current sample
		* has the sample in its sample_map, change to that instrument. */
		var ins = Song.CurrentSong.GetInstrument(sample);

		if (ins.SampleMap.Contains(sample))
		{
			CurrentInstrument = sample;
			return;
		}

		/* 2. look through the instrument list for the first instrument
		* that uses the selected sample. */
		for (int n = 1; n < Song.CurrentSong.Instruments.Count; n++)
		{
			if (n == sample)
				continue;

			ins = Song.CurrentSong.GetInstrument(n);

			if (ins.SampleMap.Contains(sample))
			{
				CurrentInstrument = n;
				return;
			}
		}

		/* 3. if no instruments are using the sample, just change to the
		* same-numbered instrument. */
		CurrentInstrument = sample;
	}

	/* --------------------------------------------------------------------- */

	static bool InstrumentListAddChar(char c)
	{
		if (c < 32)
			return false;

		var ins = Song.CurrentSong.GetInstrument(s_currentInstrument);

		if (ins.Name?.Length < 25)
		{
			ins.Name =
				ins.Name.Substring(0, s_instrumentCursorPos) +
				c +
				ins.Name.Substring(s_instrumentCursorPos);

			s_instrumentCursorPos++;
		}

		if (s_instrumentCursorPos == 25)
			s_instrumentCursorPos--;

		otherInstrumentList.OtherAcceptsText = (s_instrumentCursorPos < 25);

		Status.Flags |= StatusFlags.NeedUpdate;
		Status.Flags |= StatusFlags.SongNeedsSave;

		return true;
	}

	static void InstrumentListDeleteChar()
	{
		var ins = Song.CurrentSong.GetInstrument(s_currentInstrument);

		if (s_instrumentCursorPos < ins.Name?.Length)
		{
			s_instrumentCursorPos--;
			ins.Name = ins.Name.Remove(s_instrumentCursorPos, 1);
		}

		otherInstrumentList.OtherAcceptsText = (s_instrumentCursorPos < 25);

		Status.Flags |= StatusFlags.NeedUpdate;
		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	static void InstrumentListDeleteNextChar()
	{
		var ins = Song.CurrentSong.GetInstrument(s_currentInstrument);

		if (s_instrumentCursorPos < ins.Name?.Length)
			ins.Name = ins.Name.Remove(s_instrumentCursorPos, 1);

		otherInstrumentList.OtherAcceptsText = (s_instrumentCursorPos < 25);

		Status.Flags |= StatusFlags.NeedUpdate;
		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	static void ClearInstrumentText()
	{
		var ins = Song.CurrentSong.GetInstrument(s_currentInstrument);

		ins.FileName = "";
		ins.Name = "";

		if (s_instrumentCursorPos != 25)
			s_instrumentCursorPos = 0;

		otherInstrumentList.OtherAcceptsText = (s_instrumentCursorPos < 25);

		Status.Flags |= StatusFlags.NeedUpdate;
		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	/* --------------------------------------------------------------------- */

	static void DoSwapInstrument(int n)
	{
		if (n >= 1 && n <= LastVisibleInstrumentNumber())
			Song.CurrentSong.SwapInstruments(s_currentInstrument, n);
	}

	static void DoExchangeInstrument(int n)
	{
		if (n >= 1 && n <= LastVisibleInstrumentNumber())
			Song.CurrentSong.ExchangeInstruments(s_currentInstrument, n);
	}

	static void DoCopyInstrument(int n)
	{
		if (n >= 1 && n <= LastVisibleInstrumentNumber())
			Song.CurrentSong.CopyInstrument(s_currentInstrument, n);
	}

	static void DoReplaceInstrument(int n)
	{
		if (n >= 1 && n <= LastVisibleInstrumentNumber())
			Song.CurrentSong.ReplaceInstrument(s_currentInstrument, n);
	}

	/* --------------------------------------------------------------------- */

	static void otherInstrumentList_Redraw()
	{
		bool isSelected = (SelectedActiveWidget == otherInstrumentList);
		int cl = 0, cr = 0;

		int ss = -1;

		var instrumentPlayingState = new int[Song.CurrentSong.Instruments.Count];

		Song.CurrentSong.GetPlayingInstruments(instrumentPlayingState);

		for (int pos = 0, n = s_topInstrument; pos < 35; pos++, n++)
		{
			var ins = Song.CurrentSong.GetInstrument(n);

			bool isCurrent = (n == s_currentInstrument);
			int isPlaying = (n < instrumentPlayingState.Length) ? instrumentPlayingState[n] : 0;

			if (ins.IsPlayed)
				VGAMem.DrawCharacter(isPlaying > 1 ? (char)183 : (char)173, new Point(1, 13 + pos), (isPlaying > 0) ? (3, 2) : (1, 2));

			VGAMem.DrawText(n.ToString99(), new Point(2, 13 + pos), (0, 2));

			if (s_instrumentCursorPos < 25)
			{
				/* it's in edit mode */
				if (isCurrent)
				{
					VGAMem.DrawTextLen(ins.Name ?? "", 25, new Point(5, 13 + pos), (6, 14));

					if (isSelected)
					{
						char ch = '\0';

						if ((ins.Name != null) && (s_instrumentCursorPos < ins.Name.Length))
							ch = ins.Name[s_instrumentCursorPos];

						VGAMem.DrawCharacter(ch,
								new Point(5 + s_instrumentCursorPos, 13 + pos), (0, 3));
					}
				}
				else
				{
					VGAMem.DrawTextLen(ins.Name ?? "", 25, new Point(5, 13 + pos), (6, 0));
				}
			}
			else
			{
				int fg = (isCurrent && isSelected) ? 0 : 6;
				int bg = isCurrent ? (isSelected ? 3 : 14) : 0;

				VGAMem.DrawTextLen(ins.Name ?? "", 25, new Point(5, 13 + pos), (fg, bg));
			}

			if ((ss == n) && (ins.Name != null) && (ins.Name.Length >= cl))
			{
				VGAMem.DrawTextLen(ins.Name.Substring(cl), (cr - cl) + 1,
						new Point(5 + cl, 13 + pos),
						isCurrent ? (3, 8) : (11, 8));
			}
		}
	}

	static bool otherInstrumentList_HandleText(TextInputEvent evt)
	{
		bool success = false;

		foreach (char ch in evt.Text)
		{
			if (s_instrumentCursorPos >= 25)
				break;

			success |= InstrumentListAddChar(ch);
		}

		return success;
	}

	static bool otherInstrumentList_HandleKey(KeyEvent k)
	{
		int newInstrument = s_currentInstrument;

		if (k.State == KeyState.Press && k.Mouse != MouseState.None && k.MousePosition.Y >= 13 && k.MousePosition.Y <= 47 && k.MousePosition.X >= 5 && k.MousePosition.X <= 30)
		{
			if (k.Mouse == MouseState.Click)
			{
				newInstrument = (k.MousePosition.Y - 13) + s_topInstrument;
				if (s_instrumentCursorPos < 25)
					s_instrumentCursorPos = Math.Min(k.MousePosition.X - 5, 24);
				Status.Flags |= StatusFlags.NeedUpdate;
			}
			else if (k.Mouse == MouseState.DoubleClick)
			{
				/* this doesn't seem to work, but I think it'd be
				more useful if double click switched to edit mode */
				if (s_instrumentCursorPos < 25)
				{
					s_instrumentCursorPos = 25;
					otherInstrumentList.OtherAcceptsText = false;
				}
				else
					SetPage(PageNumbers.InstrumentLoad);

				Status.Flags |= StatusFlags.NeedUpdate;
				return true;

			}
			else if (k.Mouse == MouseState.ScrollUp)
			{
				s_topInstrument -= Constants.MouseScrollLines;
				if (s_topInstrument < 1) s_topInstrument = 1;
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			}
			else if (k.Mouse == MouseState.ScrollDown)
			{
				s_topInstrument += Constants.MouseScrollLines;
				if (s_topInstrument > (LastVisibleInstrumentNumber() - 34)) s_topInstrument = LastVisibleInstrumentNumber() - 34;
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			}
		}
		else
		{
			switch (k.Sym)
			{
				case KeySym.Up:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					{
						if (s_currentInstrument > 1)
						{
							newInstrument = s_currentInstrument - 1;
							Song.CurrentSong.SwapInstruments(s_currentInstrument, newInstrument);
						}
					}
					else if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					{
						return false;
					}
					else
					{
						newInstrument--;
					}
					break;
				case KeySym.Down:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					{
						// restrict position to the "old" value of _last_vis_inst()
						// (this is entirely for aesthetic reasons)
						if (Status.LastKeyIs(KeySym.Down, KeyMod.Alt) && !k.IsRepeat)
							s_altSwap_lastVisibleInstrumentNumber = LastVisibleInstrumentNumber();
						if (s_currentInstrument < s_altSwap_lastVisibleInstrumentNumber)
						{
							newInstrument = s_currentInstrument + 1;
							Song.CurrentSong.SwapInstruments(s_currentInstrument, newInstrument);
						}
					}
					else if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					{
						return false;
					}
					else
					{
						newInstrument++;
					}
					break;
				case KeySym.PageUp:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Control))
						newInstrument = 1;
					else
						newInstrument -= 16;
					break;
				case KeySym.PageDown:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Control))
						newInstrument = LastVisibleInstrumentNumber();
					else
						newInstrument += 16;
					break;
				case KeySym.Home:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					if (s_instrumentCursorPos < 25)
					{
						s_instrumentCursorPos = 0;
						otherInstrumentList.OtherAcceptsText = true;
						Status.Flags |= StatusFlags.NeedUpdate;
					}
					return true;
				case KeySym.End:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					if (s_instrumentCursorPos < 24)
					{
						s_instrumentCursorPos = 24;
						otherInstrumentList.OtherAcceptsText = true;
						Status.Flags |= StatusFlags.NeedUpdate;
					}
					return true;
				case KeySym.Left:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					if (s_instrumentCursorPos < 25 && s_instrumentCursorPos > 0)
					{
						s_instrumentCursorPos--;
						otherInstrumentList.OtherAcceptsText = true;
						Status.Flags |= StatusFlags.NeedUpdate;
					}
					return true;
				case KeySym.Right:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					if (s_instrumentCursorPos == 25)
					{
						otherInstrumentList.OtherAcceptsText = false;
						AllPages.InstrumentList.ChangeFocusTo(1);
					}
					else if (s_instrumentCursorPos < 24)
					{
						otherInstrumentList.OtherAcceptsText = true;
						s_instrumentCursorPos++;
						Status.Flags |= StatusFlags.NeedUpdate;
					}
					return true;
				case KeySym.Return:
					if (k.State == KeyState.Press)
						return false;
					if (s_instrumentCursorPos < 25)
					{
						s_instrumentCursorPos = 25;
						otherInstrumentList.OtherAcceptsText = false;
						Status.Flags |= StatusFlags.NeedUpdate;
					}
					else
					{
						otherInstrumentList.OtherAcceptsText = true;
						SetPage(PageNumbers.InstrumentLoad);
					}
					return true;
				case KeySym.Escape:
					if (k.Modifiers.HasAnyFlag(KeyMod.Shift) || s_instrumentCursorPos < 25)
					{
						if (k.State == KeyState.Release)
							return true;
						s_instrumentCursorPos = 25;
						otherInstrumentList.OtherAcceptsText = false;
						Status.Flags |= StatusFlags.NeedUpdate;
						return true;
					}
					return false;
				case KeySym.Backspace:
					if (k.State == KeyState.Release)
						return false;
					if (s_instrumentCursorPos == 25)
						return false;
					if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAlt))
						InstrumentListDeleteChar();
					else if (k.Modifiers.HasAnyFlag(KeyMod.Control))
						InstrumentListAddChar((char)127);
					return true;
				case KeySym.Insert:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					{
						Song.CurrentSong.InsertInstrumentSlot(s_currentInstrument);
						Status.Flags |= StatusFlags.NeedUpdate;
						return true;
					}
					return false;
				case KeySym.Delete:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					{
						Song.CurrentSong.RemoveInstrumentSlot(s_currentInstrument);
						Status.Flags |= StatusFlags.NeedUpdate;
						return true;
					}
					else if (!k.Modifiers.HasAnyFlag(KeyMod.Control))
					{
						if (s_instrumentCursorPos == 25)
							return false;
						InstrumentListDeleteNextChar();
						return true;
					}
					return false;
				default:
					if (k.State == KeyState.Release)
						return false;

					if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					{
						if (k.Sym == KeySym.c)
						{
							ClearInstrumentText();
							return true;
						}
					}
					else if (!k.Modifiers.HasAnyFlag(KeyMod.Control))
					{
						if (s_instrumentCursorPos < 25)
						{
							if (!string.IsNullOrEmpty(k.Text))
							{
								otherInstrumentList_HandleText(k.ToTextInputEvent());
								return true;
							}
						}
						else if (k.Sym == KeySym.Space)
						{
							s_instrumentCursorPos = 0;
							otherInstrumentList.OtherAcceptsText = false;
							Status.Flags |= StatusFlags.NeedUpdate;
							MemoryUsage.NotifySongChanged();
							return true;
						}
					}

					return false;
			}
		}

		newInstrument = newInstrument.Clamp(1, LastVisibleInstrumentNumber());

		if (newInstrument != s_currentInstrument)
		{
			SetCurrentInstrument(newInstrument);
			Status.Flags |= StatusFlags.NeedUpdate;
			MemoryUsage.NotifySongChanged();
		}

		return true;
	}

	/* ----------------------------------------------------------------------------- */
	/* generic ITI saving routines */

	class InstrumentSaveData
	{
		public string Path;
		/* string Options? */
		public InstrumentFileConverter Converter;

		public InstrumentSaveData(string path, InstrumentFileConverter converter)
		{
			Path = path;
			Converter = converter;
		}
	}

	void DoSaveInstrument(InstrumentSaveData data)
	{
		Song.CurrentSong.SaveInstrument(data.Path, data.Converter, CurrentInstrument);
	}

	/* filename can be NULL, in which case the instrument filename is used (quick save) */
	void InstrumentSave(string? filename, InstrumentFileConverter converter)
	{
		var pEnv = Song.CurrentSong.GetInstrument(CurrentInstrument);

		if (pEnv == null)
			return;

		string ptr;

		if (filename != null)
			ptr = Path.Combine(Configuration.Directories.InstrumentsDirectory, filename);
		else if (!string.IsNullOrEmpty(pEnv.FileName))
			ptr = Path.Combine(Configuration.Directories.InstrumentsDirectory, pEnv.FileName);
		else
		{
			Status.FlashText($"Error: Instrument {CurrentInstrument} NOT saved! (No Filename?)");
			return;
		}

		var data = new InstrumentSaveData(ptr, converter);

		if (Directory.Exists(ptr))
		{
			Status.FlashText(filename + " is a directory");
			return;
		}

		if (File.Exists(ptr))
		{
			if (Paths.IsRegularFile(ptr))
				MessageBox.Show(MessageBoxTypes.OKCancel, "Overwrite file?", accept: () => DoSaveInstrument(data));
			else
				Status.FlashText(filename + " is not a regular file");
		}
		else
			DoSaveInstrument(data);
	}

	void ShowExportInstrumentDialog()
	{
		var instrument = Song.CurrentSong.GetInstrument(CurrentInstrument);

		if (instrument == null)
			return;

		var dialog = Dialog.Show(new ExportInstrumentDialog(instrument.FileName ?? "", InstrumentFileConverter.EnumerateImplementations().ToArray()));

		dialog.ActionYes += () => InstrumentSave(dialog.FileName, dialog.InstrumentConverter);
	}

	void HandleAltKey(KeyEvent k)
	{
		/* var ins = Song.CurrentSong.GetInstrument(CurrentInstrument); */

		if (k.State == KeyState.Release)
			return;
		switch (k.Sym)
		{
			case KeySym.n:
				AudioPlayback.ToggleMultichannelMode();
				return;
			case KeySym.o:
				InstrumentSave(null, new ITI());
				return;
			case KeySym.r:
			{
				var dialog = Dialog.Show(new SamplePromptDialog("Instrument", "Replace instrument with:"));
				dialog.Finish += DoReplaceInstrument;
				return;
			}
			case KeySym.s:
			{
				// extra space to align the text like IT
				var dialog = Dialog.Show(new SamplePromptDialog("Instrument", "Swap instrument with: "));
				dialog.Finish += DoSwapInstrument;
				return;
			}
			case KeySym.x:
			{
				var dialog = Dialog.Show(new SamplePromptDialog("Instrument", "Exchange instrument with: "));
				dialog.Finish += DoExchangeInstrument;
				return;
			}
			case KeySym.p:
			{
				var dialog = Dialog.Show(new SamplePromptDialog("Instrument", "Copy instrument:"));
				dialog.Finish += DoCopyInstrument;
				return;
			}
			case KeySym.w:
				Song.CurrentSong.WipeInstrument(CurrentInstrument);
				break;
			case KeySym.d:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "Delete Instrument? (preserve shared samples)");

					dialog.SelectedWidgetIndex.Value = 1;
					dialog.ActionYes = () => Song.CurrentSong.DeleteInstrument(CurrentInstrument, preserveSharedSamples: true);
				}
				else
				{
					var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "Delete Instrument?");

					dialog.SelectedWidgetIndex.Value = 1;
					dialog.ActionYes = () => Song.CurrentSong.DeleteInstrument(CurrentInstrument, preserveSharedSamples: false);
				}
				return;
			case KeySym.t:
				ShowExportInstrumentDialog();
				break;
			default:
				return;
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public override bool? PreHandleKey(KeyEvent k)
	{
		// Only handle plain F4 key when no dialog is active.
		if (Status.DialogType != DialogTypes.None || k.Sym != KeySym.F4 || k.Modifiers.HasAnyFlag(KeyMod.ControlAlt))
			return false;
		if (k.State == KeyState.Release)
			return true;

		if (Song.CurrentSong.IsInstrumentMode)
		{
			int csamp = AllPages.SampleList.CurrentSample;

			AllPages.SampleList.SynchronizeToInstrument();

			if (csamp != AllPages.SampleList.CurrentSample)
			{
				Status.Flags |= StatusFlags.NeedUpdate;
				return false;
			}
		}

		if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
		{
			switch (Status.CurrentPage)
			{
				default:
				case InstrumentListVolumeSubpage: SetSubpage(PageNumbers.InstrumentListGeneral); break;
				case InstrumentListPanningSubpage: SetSubpage(PageNumbers.InstrumentListVolume); break;
				case InstrumentListPitchSubpage: SetSubpage(PageNumbers.InstrumentListPanning); break;
				case InstrumentListGeneralSubpage: SetSubpage(PageNumbers.InstrumentListPitch); break;
			}
		}
		else
		{
			switch (Status.CurrentPage)
			{
				default:
				case InstrumentListPitchSubpage: SetSubpage(PageNumbers.InstrumentListGeneral); break;
				case InstrumentListGeneralSubpage: SetSubpage(PageNumbers.InstrumentListVolume); break;
				case InstrumentListVolumeSubpage: SetSubpage(PageNumbers.InstrumentListPanning); break;
				case InstrumentListPanningSubpage: SetSubpage(PageNumbers.InstrumentListPitch); break;
			}
		}

		return true;
	}

	public override bool? HandleKey(KeyEvent k)
	{
		switch (k.Sym)
		{
			case KeySym.Comma:
				if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					if (!Status.Flags.HasAllFlags(StatusFlags.ClassicMode)
					&& SelectedWidgetIndex == 5)
						return false;
				}
				goto case KeySym.Less;
			case KeySym.Less:
				if (k.State == KeyState.Release)
					return false;
				AudioPlayback.ChangeCurrentPlayChannel(-1, wraparound: false);
				return true;
			case KeySym.Period:
				if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					if (!Status.Flags.HasAllFlags(StatusFlags.ClassicMode)
					&& SelectedWidgetIndex == 5)
						return false;
				}
				goto case KeySym.Greater;
			case KeySym.Greater:
				if (k.State == KeyState.Release)
					return false;
				AudioPlayback.ChangeCurrentPlayChannel(+1, wraparound: false);
				return true;
			case KeySym.PageUp:
				if (k.State == KeyState.Release)
					return false;
				CurrentInstrument--;
				break;
			case KeySym.PageDown:
				if (k.State == KeyState.Release)
					return false;
				CurrentInstrument++;
				break;
			case KeySym.Escape:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift) || s_instrumentCursorPos < 25)
				{
					if (k.State == KeyState.Release)
						return false;
					s_instrumentCursorPos = 25;
					otherInstrumentList.OtherAcceptsText = false;
					ChangeFocusTo(0);
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
				return false;
			default:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					HandleAltKey(k);
				else
				{
					int n, v;

					if (k.MIDINote > -1)
					{
						n = k.MIDINote;

						if (k.MIDIVolume > -1)
							v = k.MIDIVolume / 2;
						else
							v = 64;
					}
					else
					{
						v = 64;
						n = k.NoteValue;
						if (n <= 0 || n > 120)
							return false;
					}

					if (k.State == KeyState.Release)
					{
						Song.CurrentSong.KeyUp(KeyJazz.NoInstrument, CurrentInstrument, n);
						Status.LastKeySym = KeySym.None;
					}
					else if (!k.IsRepeat)
						Song.CurrentSong.KeyDown(KeyJazz.NoInstrument, CurrentInstrument, n, v, KeyJazz.AutomaticChannel);

					s_lastNote = n;
				}

				return true;
		}

		return true;
	}

	/* --------------------------------------------------------------------- */

	static void SetSubpage(PageNumbers page)
	{
		switch (page)
		{
			case PageNumbers.InstrumentListGeneral: toggleButtonSubpageGeneral.SetState(true); break;
			case PageNumbers.InstrumentListVolume: toggleButtonSubpageVolume.SetState(true); break;
			case PageNumbers.InstrumentListPanning: toggleButtonSubpagePanning.SetState(true); break;
			case PageNumbers.InstrumentListPitch: toggleButtonSubpagePitch.SetState(true); break;
			default: return;
		}

		SetPage(page);

		if ((ActiveWidgetContext != null)
		 && (ActiveWidgetContext.SelectedWidgetIndex >= ActiveWidgetContext.Widgets.Count))
			ActiveWidgetContext.SelectedWidgetIndex.Value = ActiveWidgetContext.Widgets.Count - 1;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	static void ChangeSubpage()
	{
		if (toggleButtonSubpageGeneral.State)
			SetSubpage(PageNumbers.InstrumentListGeneral);
		else if (toggleButtonSubpageVolume.State)
			SetSubpage(PageNumbers.InstrumentListVolume);
		else if (toggleButtonSubpagePanning.State)
			SetSubpage(PageNumbers.InstrumentListPanning);
		else if (toggleButtonSubpagePitch.State)
			SetSubpage(PageNumbers.InstrumentListPitch);
	}

	/* --------------------------------------------------------------------- */

	public override void DrawConst()
	{
		VGAMem.DrawBox(new Point(4, 12), new Point(30, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}
}
