using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ChasmTracker.Pages;

using ChasmTracker.Dialogs;
using ChasmTracker.Dialogs.PatternEditor;
using ChasmTracker.Input;
using ChasmTracker.Memory;
using ChasmTracker.Menus;
using ChasmTracker.MIDI;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public abstract class Page
{
	public readonly PageNumbers PageNumber;
	public readonly string Title;
	public readonly HelpTexts HelpText;
	public readonly Shared<int> SelectedWidgetIndex = new Shared<int>();

	public static List<Widget>? ActiveWidgets;
	public static Shared<int>? SelectedActiveWidgetIndex;

	public Widget? SelectedWidget
	{
		get
		{
			if ((SelectedWidgetIndex < 0) || (SelectedWidgetIndex >= Widgets.Count))
				return null;

			return Widgets[SelectedWidgetIndex];
		}
	}

	public static Widget? SelectedActiveWidget
	{
		get
		{
			if ((ActiveWidgets == null) || (SelectedActiveWidgetIndex == null))
				return null;

			if ((SelectedActiveWidgetIndex < 0) || (SelectedActiveWidgetIndex >= ActiveWidgets.Count))
				return null;

			return ActiveWidgets[SelectedActiveWidgetIndex];
		}
	}

	public static MiniPopState MiniPopActive;

	public static void ChangeFocusTo(int newWidgetIndex)
	{
		if ((ActiveWidgets == null) || (SelectedActiveWidgetIndex == null))
			return;

		if ((newWidgetIndex == SelectedActiveWidgetIndex)
		 || (newWidgetIndex < 0)
		 || (newWidgetIndex >= ActiveWidgets.Count))
			return;

		if (SelectedActiveWidget != null)
			SelectedActiveWidget.IsDepressed = false;

		SelectedActiveWidgetIndex.Value = newWidgetIndex;

		if (SelectedActiveWidget != null)
		{
			SelectedActiveWidget.IsDepressed = false;

			if (SelectedActiveWidget is TextEntryWidget textEntry)
				textEntry.CursorPosition = textEntry.Text.Length;
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public static void ChangeFocusTo(Widget? newWidget)
	{
		if (newWidget != null)
		{
			if (ActiveWidgets == null)
				return;

			ChangeFocusTo(ActiveWidgets.IndexOf(newWidget));
		}
		else
			ChangeFocusTo(-1);
	}

	protected Page(PageNumbers pageNumber, string title, HelpTexts helpText)
	{
		PageNumber = pageNumber;
		Title = title;
		HelpText = helpText;

		CheckDrawFull();
	}

	public static void SetPage(PageNumbers newPageNumber)
	{
		if (Status.CurrentPageNumber != newPageNumber)
		{
			Status.PreviousPageNumber = Status.CurrentPageNumber;
			Status.CurrentPage.UnsetPage();
		}

		var newPage = AllPages.ByPageNumber(newPageNumber);

		newPage.SynchronizeWith(Status.CurrentPage);

		ActiveWidgets = newPage.Widgets;
		SelectedActiveWidgetIndex = newPage.SelectedWidgetIndex;

		Status.CurrentPageNumber = newPageNumber;
		Status.CurrentPage.SetPage();
	}

	public static void NotifySongChangedGlobal()
	{
		AllPages.OrderListPanning.CurrentOrder = 0;

		int n = Song.CurrentSong?.OrderList?.FirstOrDefault() ?? 0;

		if (n > 199)
			n = 0;

		AllPages.PatternEditor.CurrentPattern = n;
		AllPages.PatternEditor.CurrentRow = 0;

		Song.CurrentSong?.SaveChannelMuteStates();

		foreach (var page in AllPages.EnumeratePages())
			page.NotifySongChanged();

		/* TODO | print some message like "new song created" if there's
		 * TODO | no filename, and thus no file. (but DON'T print it the
		 * TODO | very first time this is called) */

		Status.Flags |= StatusFlags.NeedUpdate;
		MemoryUsage.NotifySongChanged();
	}

	public virtual void SynchronizeWith(Page other) { }

	// TODO: test once during startup with reflection whether it is overridden instead?
	bool _drawFullDoesNothing = false;

	void CheckDrawFull()
	{
		var thisType = GetType();

		var drawFull = thisType.GetMethod(nameof(DrawFull), BindingFlags.DeclaredOnly);

		if (drawFull == null)
			_drawFullDoesNothing = true;
	}

	/* font editor takes over full screen */
	public virtual void DrawFull() { }

	/* draw the labels, etc. that don't change */
	public virtual void DrawConst() { }
	/* redraw the page */
	public virtual void Redraw() { }
	/* called after the song is changed. this is to copy the new
	 * values from the song to the widgets on the page. */
	public virtual void NotifySongChanged() { }
	/* called before widgets are drawn, mostly to fix the values
	 * (for example, on the sample page this sets everything to
	 * whatever values the current sample has) - this is a lousy
	 * hack. sorry. :P */
	public virtual void PredrawHook() { }
	/* draw the parts of the page that change when the song is playing
	 * (this is called *very* frequently) */
	public virtual void PlaybackUpdate() { }
	/* this gets first shot at keys (to do unnatural overrides) */
	public virtual bool? PreHandleKey(KeyEvent k) { return false; }
	/* this catches any keys that the main handler doesn't deal with */
	public virtual bool? HandleKey(KeyEvent k) { return false; }
	/* handle any text input events from SDL */
	public virtual bool HandleTextInput(string textInput) { return false; }
	/* called when the page is set. this is for reloading the
	 * directory in the file browsers. */
	public virtual void SetPage() { }
	/* called when the page is losing focus to another page */
	public virtual void UnsetPage() { }

	/* called when the song-mode changes */
	public virtual void NotifySongModeChanged() { }

	public static void NotifySongModeChangedGlobal()
	{
		foreach (var page in AllPages.EnumeratePages())
			page.NotifySongModeChanged();
	}

	/* called by the clipboard manager */
	public virtual bool ClipboardPaste(byte[]? cptr) { return false; }

	public readonly List<Widget> Widgets = new List<Widget>();

	public void SetFocus(Widget widget)
	{
		SelectedWidgetIndex.Value = Widgets.IndexOf(widget);
	}

	/* HelpTexts.Global if no page-specific help */
	public HelpTexts HelpIndex;

	/* --------------------------------------------------------------------------------------------------------- */

	public static MiniPopSlideDialog ShowMiniPop(int currentValue, string name, int min, int max, Point mid)
	{
		var miniPopDialog = Dialog.Show(new MiniPopSlideDialog(currentValue, name, min, max, mid));

		miniPopDialog.MiniPopUsed +=
			() => { MiniPopActive = MiniPopState.ActiveUsed; };

		MiniPopActive = MiniPopState.Active;

		return miniPopDialog;
	}

	public static void FinishMiniPop()
	{
		if (MiniPopActive != MiniPopState.Inactive)
		{
			Dialog.DestroyAll();
			MiniPopActive = MiniPopState.Inactive;
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

	public static void SaveCheck(Action<object?> ok, Action<object?>? cancel = null, object? data = null)
	{
		if (Status.Flags.HasFlag(StatusFlags.SongNeedsSave))
			MessageBox.Show(MessageBoxTypes.OKCancel, "Current module not saved. Proceed?", ok, cancel, data);
		else
			ok(data);
	}

	/* --------------------------------------------------------------------------------------------------------- */

	static int s_altNumPad = 0;
	static int s_altNumPadC = 0;
	static int s_digraphN = 0;
	static char s_digraphC = '\0';
	static int s_csUnicode = 0;
	static int s_csUnicodeC = 0;

	static bool HandleIME(KeyEvent k)
	{
		char c;

		if (Status.CurrentPage.SelectedWidgetIndex > -1 && Status.CurrentPage.SelectedWidgetIndex < Status.CurrentPage.Widgets.Count
				&& Status.CurrentPage.Widgets[Status.CurrentPage.SelectedWidgetIndex].AcceptsText)
		{
			if (s_digraphN == -1 && k.State == KeyState.Release) {
				s_digraphN = 0;

			}
			else if (!Status.Flags.HasFlag(StatusFlags.ClassicMode) && (k.Sym == KeySym.LeftControl || k.Sym == KeySym.RightControl))
			{
				if (k.State == KeyState.Release && s_digraphN >= 0)
				{
					s_digraphN++;
					if (s_digraphN >= 2)
						Status.FlashText("Enter digraph:", biosFont: true);
				}
			}
			else if (k.Sym == KeySym.LeftShift || k.Sym == KeySym.RightShift)
			{
				/* do nothing */
			}
			else if (k.Modifiers.HasAnyFlag(KeyMod.ControlAlt) || (c = (k.Text != null) ? k.Text[0] : (char)k.Sym) == 0 || s_digraphN < 2)
			{
				if (k.State == KeyState.Press && k.Mouse == MouseState.None)
				{
					if (s_digraphN > 0)
						Status.FlashText(" ");
					s_digraphN = -1;
				}
			}
			else if (s_digraphN >= 2)
			{
				if (k.State == KeyState.Release)
					return true;

				if (s_digraphC == 0)
				{
					s_digraphC = c;
					Status.FlashText("Enter digraph: " + c, biosFont: true);
				}
				else
				{
					char digraphInput = Digraphs.Digraph(s_digraphC, c);

					if (digraphInput != '\0')
						Status.FlashText($"Enter digraph: {s_digraphC}{c} -> {digraphInput}", biosFont: true);
					else
						Status.FlashText($"Enter digraph: {s_digraphC}{c} -> INVALID", biosFont: true);

					s_digraphN = 0;
					s_digraphC = '\0';

					if (digraphInput != '\0')
						MainHandleTextInput(digraphInput.ToString());
				}

				return true;
			}
			else
			{
				if (s_digraphN > 0)
					Status.FlashText(" ");
				s_digraphN = 0;
			}

			/* ctrl+shift -> unicode character */
			if (k.Sym == KeySym.LeftControl || k.Sym == KeySym.RightControl || k.Sym == KeySym.LeftShift || k.Sym == KeySym.RightShift)
			{
				if (k.State == KeyState.Release)
				{
					if (s_csUnicodeC > 0)
					{
						byte unicode = ((char)s_csUnicode).ToCP437();

						if (unicode >= 32)
						{
							Status.FlashText($"Enter Unicode: U+{s_csUnicode:X4} -> {unicode}");
							MainHandleTextInput(Encoding.ASCII.GetString(new[] { unicode }));
						}
						else
							Status.FlashText($"Enter Unicode: U+{s_csUnicode:X4} -> INVALID");

						s_csUnicode = 0;
						s_csUnicodeC = 0;

						s_altNumPad = 0;
						s_altNumPadC = 0;

						s_digraphN = 0;
						s_digraphC = '\0';
					}

					return true;
				}
			}
			else if (!Status.Flags.HasFlag(StatusFlags.ClassicMode) && k.Modifiers.HasAnyFlag(KeyMod.Control) && k.Modifiers.HasAnyFlag(KeyMod.Shift))
			{
				if (s_csUnicodeC >= 0)
				{
					/* bleh... */
					var m = k.Modifiers;
					k.Modifiers = KeyMod.None;
					var v = k.HexValue;
					k.Modifiers = m;

					if (v == -1)
						s_csUnicode = s_csUnicodeC = -1;
					else
					{
						if (k.State == KeyState.Press)
							return true;

						s_csUnicode *= 16;
						s_csUnicode += v;
						s_csUnicodeC++;

						s_digraphN = 0;
						s_digraphC = '\0';

						Status.FlashText($"Enter Unicode: U+{s_csUnicode:X4}", biosFont: true);

						return true;
					}
				}
			}
			else
			{
				if (k.Sym == KeySym.LeftControl || k.Sym == KeySym.RightControl || k.Sym == KeySym.LeftShift || k.Sym == KeySym.RightShift)
					return true;

				s_csUnicode = 0;
				s_csUnicodeC = 0;
			}

			/* alt+numpad -> char number */
			if (k.Sym == KeySym.LeftAlt || k.Sym == KeySym.RightAlt
				|| k.Sym == KeySym.LeftGUI || k.Sym == KeySym.RightGUI)
			{
				if (k.State == KeyState.Release && s_altNumPadC > 0 && (s_altNumPad & 255) > 0)
				{
					if (s_altNumPad < 32)
						return false;

					char unicode = (char)(s_altNumPad & 255);

					if (!Status.Flags.HasFlag(StatusFlags.ClassicMode))
						Status.FlashText($"Enter DOS/ASCII: {(int)unicode} -> {unicode}");

					MainHandleTextInput(unicode.ToString());

					s_altNumPad = 0;
					s_altNumPadC = 0;

					s_digraphN = 0;
					s_digraphC = '\0';

					s_csUnicode = 0;
					s_csUnicodeC = 0;

					return true;
				}
			}
			else if (k.Modifiers.HasAnyFlag(KeyMod.Alt) && !k.Modifiers.HasAnyFlag(KeyMod.Control | KeyMod.Shift))
			{
				if (s_altNumPadC >= 0)
				{
					var m = k.Modifiers;
					k.Modifiers = KeyMod.None;
					int v = k.NumericValue(kpOnly: true);
					k.Modifiers = m;

					if (v == -1 || v > 9)
					{
						s_altNumPad = -1;
						s_altNumPadC = -1;
					}
					else
					{
						if (k.State == KeyState.Press)
							return true;

						s_altNumPad *= 10;
						s_altNumPad += v;
						s_altNumPadC++;

						if (!Status.Flags.HasFlag(StatusFlags.ClassicMode))
							Status.FlashText($"Enter DOS/ASCII: {s_altNumPad}", biosFont: true);

						return true;
					}
				}
			}
			else
			{
				s_altNumPad = 0;
				s_altNumPadC = 0;
			}
		}
		else
		{
			s_altNumPad = 0;
			s_altNumPadC = 0;

			s_digraphN = 0;
			s_digraphC = '\0';

			s_csUnicode = 0;
			s_csUnicodeC = 0;
		}

		return false;
	}

	/* whenever there's a keypress ;) */
	/* this is the important one */
	public static void MainHandleKey(KeyEvent k)
	{
		if (HandleIME(k))
			return;

		/* okay... */
		if (!Status.Flags.HasFlag(StatusFlags.DiskWriterActive))
		{
			if (Status.CurrentPage.PreHandleKey(k) ?? true)
				return;
		}

		if (HandleKeyGlobal(k))
			return;
		if (!Status.Flags.HasFlag(StatusFlags.DiskWriterActive) && Menu.HandleKey(k))
			return;
		if (Widget.MainHandleKey(k))
			return;

		/* now check a couple other keys. */
		switch (k.Sym)
		{
			case KeySym.Left:
				if (k.State == KeyState.Release) return;
				if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive)) return;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control) && Status.CurrentPageNumber != PageNumbers.PatternEditor)
				{
					FinishMiniPop();
					if (AudioPlayback.Mode == AudioPlaybackMode.Playing)
						AudioPlayback.CurrentOrder--;
					return;
				}
				break;
			case KeySym.Right:
				if (k.State == KeyState.Release) return;
				if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive)) return;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control) && Status.CurrentPageNumber != PageNumbers.PatternEditor)
				{
					FinishMiniPop();
					if (AudioPlayback.Mode == AudioPlaybackMode.Playing)
						AudioPlayback.CurrentOrder++;
					return;
				}
				break;
			case KeySym.Escape:
				/* TODO | Page key handlers should return true/false depending on if the key was handled
					TODO | (same as with other handlers), and the escape key check should go *after* the
					TODO | page gets a chance to grab it. This way, the load sample page can switch back
					TODO | to the sample list on escape like it's supposed to. (The Status.CurrentPageNumber
					TODO | checks above won't be necessary, either.) */
				if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift) && Status.DialogType == DialogTypes.None
						&& Status.CurrentPageNumber != PageNumbers.SampleLibrary
						&& Status.CurrentPageNumber != PageNumbers.InstrumentLoad)
				{
					if (k.State == KeyState.Release) return;
					if (MiniPopActive != MiniPopState.Inactive)
					{
						FinishMiniPop();
						return;
					}
					Menu.Show();
					return;
				}
				break;
			case KeySym.Slash:
				if (k.State == KeyState.Release) return;
				if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive)) return;
				Keyboard.CurrentOctave--;
				break;
			case KeySym.Asterisk:
				if (k.State == KeyState.Release) return;
				if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive)) return;
				Keyboard.CurrentOctave++;
				break;
			case KeySym.LeftBracket:
				if (k.State == KeyState.Release) break;
				if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive)) return;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					Song.CurrentSong.CurrentSpeed--;
					Status.FlashText($"Speed set to {Song.CurrentSong.CurrentSpeed} frames per row");
					if (!AudioPlayback.Mode.HasAnyFlag(AudioPlaybackMode.Playing | AudioPlaybackMode.PatternLoop))
						Song.CurrentSong.InitialSpeed = Song.CurrentSong.CurrentSpeed;
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Control) && !Status.Flags.HasFlag(StatusFlags.ClassicMode))
				{
					Song.CurrentSong.CurrentTempo--;
					Status.FlashText($"Tempo set to {Song.CurrentSong.CurrentTempo} beats per minute");
					if (!AudioPlayback.Mode.HasAnyFlag(AudioPlaybackMode.Playing | AudioPlaybackMode.PatternLoop))
						Song.CurrentSong.InitialTempo = Song.CurrentSong.CurrentTempo;
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					Song.CurrentSong.CurrentGlobalVolume--;
					Status.FlashText($"Global volume set to {Song.CurrentSong.CurrentGlobalVolume}");
					if (!AudioPlayback.Mode.HasAnyFlag(AudioPlaybackMode.Playing | AudioPlaybackMode.PatternLoop))
						Song.CurrentSong.InitialGlobalVolume = Song.CurrentSong.CurrentGlobalVolume;
				}
				return;
			case KeySym.RightBracket:
				if (k.State == KeyState.Release) break;
				if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive)) return;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					Song.CurrentSong.CurrentSpeed++;
					Status.FlashText($"Speed set to {Song.CurrentSong.CurrentSpeed} frames per row");
					if (!AudioPlayback.Mode.HasAnyFlag(AudioPlaybackMode.Playing | AudioPlaybackMode.PatternLoop))
						Song.CurrentSong.InitialSpeed = Song.CurrentSong.CurrentSpeed;
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Control) && !Status.Flags.HasFlag(StatusFlags.ClassicMode))
				{
					Song.CurrentSong.CurrentTempo++;
					Status.FlashText($"Tempo set to {Song.CurrentSong.CurrentTempo} beats per minute");
					if (!AudioPlayback.Mode.HasAnyFlag(AudioPlaybackMode.Playing | AudioPlaybackMode.PatternLoop))
						Song.CurrentSong.InitialTempo = Song.CurrentSong.CurrentTempo;
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					Song.CurrentSong.CurrentGlobalVolume++;
					Status.FlashText($"Global volume set to {Song.CurrentSong.CurrentGlobalVolume}");
					if (!AudioPlayback.Mode.HasAnyFlag(AudioPlaybackMode.Playing | AudioPlaybackMode.PatternLoop))
						Song.CurrentSong.InitialGlobalVolume = Song.CurrentSong.CurrentGlobalVolume;
				}

				return;
		}

		/* and if we STILL didn't handle the key, pass it to the page.
		* (or dialog, if one's active) */
		if (Dialog.HasCurrentDialog)
			Dialog.HandleKeyForCurrentDialog(k);
		else
		{
			if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive))
				return;

			Status.CurrentPage.HandleKey(k);
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* text input handler */

	public static void MainHandleTextInput(string textInput)
	{
		if (Widget.HandleTextInput(textInput))
			return;

		if (!Status.DialogType.HasFlag(DialogTypes.Box))
			Status.CurrentPage.HandleTextInput(textInput);
	}

	/* returns true if the key was handled */
	public static bool HandleKeyGlobal(KeyEvent k)
	{
		if ((MiniPopActive == MiniPopState.ActiveUsed) && (k.Mouse == MouseState.Click) && (k.State == KeyState.Release))
		{
			Status.Flags |= StatusFlags.NeedUpdate;
			Dialog.DestroyAll();
			MiniPopActive = MiniPopState.Inactive;
			// eat it...
			return true;
		}
		if ((MiniPopActive == MiniPopState.Inactive) && (k.State == KeyState.Press) && (k.Mouse == MouseState.Click))
		{
			if (k.MousePosition.X >= 63 && k.MousePosition.X <= 77 && k.MousePosition.Y >= 6 && k.MousePosition.Y <= 7)
			{
				Status.VisualizationStyle++;
				if (!Enum.IsDefined<TrackerVisualizationStyle>(Status.VisualizationStyle))
					Status.VisualizationStyle = TrackerVisualizationStyle.Off;

				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			}
			else if (k.MousePosition.Y == 5 && k.MousePosition.X == 50)
			{
				var minipop = ShowMiniPop(Keyboard.CurrentOctave, "Octave", 0, 8, new Point(50, 5));

				minipop.SetValue +=
					newValue =>
					{
						Keyboard.CurrentOctave = newValue;
					};

				return true;
			}
			else if (k.MousePosition.Y == 4 && k.MousePosition.X >= 50 && k.MousePosition.X <= 52)
			{
				var minipop = ShowMiniPop(Song.CurrentSong.CurrentSpeed, "Speed", 1, 255, new Point(51, 4));

				minipop.SetValue +=
					newValue =>
					{
						Song.CurrentSong.CurrentSpeed = newValue;
					};

				minipop.SetValueNoPlay +=
					newValue =>
					{
						if (Song.CurrentSong is Song currentSong)
							currentSong.InitialSpeed = newValue;
					};

				return true;
			}
			else if (k.MousePosition.Y == 4 && k.MousePosition.X >= 54 && k.MousePosition.X <= 56)
			{
				var minipop = ShowMiniPop(Song.CurrentSong.CurrentTempo, "Tempo", 32, 255, new Point(55, 4));

				minipop.SetValue +=
					newValue =>
					{
						Song.CurrentSong.CurrentTempo = newValue;
					};

				minipop.SetValueNoPlay +=
					newValue =>
					{
						if (Song.CurrentSong is Song currentSong)
							currentSong.InitialTempo = newValue;
					};

				return true;
			}
			else if (k.MousePosition.Y == 3 && k.MousePosition.X >= 50 && k.MousePosition.X <= 77)
			{
				bool instrumentMode;

				if ((Status.CurrentPage is InstrumentListPage)
				 || (Status.CurrentPage is SampleListPage)
				 || (!Status.Flags.HasFlag(StatusFlags.ClassicMode)
					&& (Status.CurrentPage is OrderListPage)))
					instrumentMode = false;
				else
					instrumentMode = Song.CurrentSong.IsInstrumentMode;

				if (instrumentMode)
				{
					var minipop = ShowMiniPop(AllPages.InstrumentList.CurrentInstrument, "Instrument", (Status.CurrentPage is InstrumentListPage) ? 1 : 0, 99 /* FIXME */, new Point(58, 3));

					minipop.SetValue +=
						newValue =>
						{
							AllPages.InstrumentList.CurrentInstrument = newValue;
						};
				}
				else
				{
					var minipop = ShowMiniPop(AllPages.SampleList.CurrentSample, "Sample", (Status.CurrentPage is SampleListPage) ? 1 : 0, 99 /* FIXME */, new Point(58, 3));

					minipop.SetValue +=
						newValue =>
						{
							AllPages.SampleList.CurrentSample = newValue;
						};
				}
			}
			else if (k.MousePosition.X >= 12 && k.MousePosition.X <= 18)
			{
				if (k.MousePosition.Y == 7)
				{
					var minipop = ShowMiniPop(AllPages.PatternEditor.CurrentRow, "Row", 0, Song.CurrentSong?.GetPattern(AllPages.PatternEditor.CurrentPattern)?.Rows?.Count ?? 64, new Point(14, 7));

					minipop.SetValue +=
						newValue =>
						{
							AllPages.PatternEditor.CurrentRow = newValue;
						};

					return true;
				}
				else if (k.MousePosition.Y == 6)
				{
					var minipop = ShowMiniPop(AllPages.PatternEditor.CurrentPattern, "Pattern", 0, Song.CurrentSong?.Patterns.Count ?? 9, new Point(14, 6));

					minipop.SetValue +=
						newValue =>
						{
							AllPages.PatternEditor.CurrentPattern = newValue;
						};

					return true;
				}
				else if (k.MousePosition.Y == 5)
				{
					var minipop = ShowMiniPop(AllPages.OrderList.CurrentOrder, "Order", 0, Song.CurrentSong?.Patterns.Count ?? 0, new Point(14, 5));

					minipop.SetValue +=
						newValue =>
						{
							AllPages.OrderList.CurrentOrder = newValue;
						};

					return true;
				}
			}
		}
		else if ((MiniPopActive == MiniPopState.Inactive) && (k.Mouse == MouseState.DoubleClick))
		{
			if (k.MousePosition.Y == 4 && k.MousePosition.X >= 11 && k.MousePosition.X <= 28)
			{
				SetPage(PageNumbers.ModuleSave);
				return true;
			}
			else if (k.MousePosition.Y == 3 && k.MousePosition.X >= 11 && k.MousePosition.X <= 35)
			{
				SetPage(PageNumbers.SongVariables);
				return true;
			}
		}

		/* shortcut */
		if (k.Mouse != MouseState.None)
			return false;

		/* first, check the truly global keys (the ones that still work if
		* a dialog's open) */
		switch (k.Sym)
		{
			case KeySym.Return:
				if (k.Modifiers.HasAnyFlag(KeyMod.Control) && k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Press)
						return true;
					Video.ToggleDisplayFullscreen();
					return true;
				}
				break;
			case KeySym.m:
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					if (k.State == KeyState.Release)
						return true;
					Video.SetMouseCursorState(MouseCursorState.CycleState);
					return true;
				}
				break;

			case KeySym.d:
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					if (k.State == KeyState.Release)
						return true; /* argh */
					bool grabbed = !Video.IsInputGrabbed();
					Video.SetInputGrabbed(grabbed);
					Status.FlashText(grabbed
						? "Mouse and keyboard grabbed, press Ctrl+D to release"
						: "Mouse and keyboard released");
					return true;
				}
				break;

			case KeySym.i:
				/* reset audio stuff? */
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					if (k.State == KeyState.Release)
						return true;
					AudioPlayback.Reinitialize(null);
					return true;
				}
				break;
			case KeySym.e:
				/* This should reset everything display-related. */
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					if (k.State == KeyState.Release)
						return true;
					Font.Initialize();
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
				break;
			case KeySym.Home:
				if (!(k.Modifiers.HasAnyFlag(KeyMod.Alt))) break;
				if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive)) break;
				if (k.State == KeyState.Release)
					return false;
				Keyboard.CurrentOctave--;
				return true;
			case KeySym.End:
				if (!(k.Modifiers.HasAnyFlag(KeyMod.Alt))) break;
				if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive)) break;
				if (k.State == KeyState.Release)
					return false;
				Keyboard.CurrentOctave++;
				return true;
			default:
				break;
		}

		/* next, if there's no dialog, check the rest of the keys */
		if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive))
			return false;

		switch (k.Sym)
		{
			case KeySym.q:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
					{
						if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
							Program.Exit(0);
						Program.ShowExitPrompt();
					}
					return true;
				}
				break;
			case KeySym.n:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						Dialog.Show<NewSongDialog>();
					return true;
				}
				break;
			case KeySym.g:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						Dialog.Show<TimeJumpDialog>();
					return true;
				}
				break;
			case KeySym.p:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						ShowSongLength();
					return true;
				}
				break;
			case KeySym.F1:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.Config);
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(Status.CurrentPageNumber == PageNumbers.MIDI ? PageNumbers.MIDIOutput : PageNumbers.MIDI);
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.Help);
				}
				else
				{
					break;
				}
				return true;
			case KeySym.F2:
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					if (Status.CurrentPageNumber == PageNumbers.PatternEditor)
					{
						FinishMiniPop();
						if (k.State == KeyState.Press && Status.DialogType == DialogTypes.None)
						{
							Dialog.Show(new LengthDialog(
								Song.CurrentSong.GetPatternLength(AllPages.PatternEditor.CurrentPattern),
								AllPages.PatternEditor.CurrentPattern));
						}
						return true;
					}
					if (Status.MessageBoxType != MessageBoxTypes.None)
						return false;
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					if (Status.CurrentPageNumber == PageNumbers.PatternEditor)
					{
						if (k.State == KeyState.Press)
						{
							if (Status.DialogType.HasFlag(DialogTypes.Menu))
							{
								return false;
							}
							else if (Status.MessageBoxType != MessageBoxTypes.None)
							{
								Dialog.DialogButtonYes(null);
								Status.Flags |= StatusFlags.NeedUpdate;
							}
							else
							{
								FinishMiniPop();

								var optionsDialog = Dialog.Show<OptionsDialog>();

								optionsDialog.ApplyOptions +=
									() =>
									{
										var pattern = Song.CurrentSong.GetPattern(AllPages.PatternEditor.CurrentPattern);

										if (pattern != null)
										{
											int oldSize = pattern.Rows.Count;
											int newSize = optionsDialog.PatternLength;

											if (oldSize != newSize)
											{
												pattern.Resize(newSize);
												if (AllPages.PatternEditor.CurrentRow >= newSize)
													AllPages.PatternEditor.CurrentRow = newSize - 1;
												AllPages.PatternEditor.Reposition();
											}
										}
									};

								optionsDialog.RevertOptions +=
									() =>
									{
										Keyboard.CurrentOctave = optionsDialog.LastOctave;
									};
							}
						}
					}
					else
					{
						if (Status.MessageBoxType != MessageBoxTypes.None)
							return false;
						FinishMiniPop();
						if (k.State == KeyState.Press)
							SetPage(PageNumbers.PatternEditor);
					}
					return true;
				}
				break;
			case KeySym.F3:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.SampleList);
				}
				else
				{
					FinishMiniPop();
					if (k.Modifiers.HasAnyFlag(KeyMod.Control)) SetPage(PageNumbers.SampleLibrary);
					break;
				}
				return true;
			case KeySym.F4:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					if (Status.CurrentPageNumber == PageNumbers.InstrumentList) return false;
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.InstrumentList);
				}
				else
				{
					if (k.Modifiers.HasAnyFlag(KeyMod.Shift)) return false;
					FinishMiniPop();
					if (k.Modifiers.HasAnyFlag(KeyMod.Control)) SetPage(PageNumbers.InstrumentLibrary);
					break;
				}
				return true;
			case KeySym.F5:
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						AudioPlayback.Start();
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					if (Status.MessageBoxType != MessageBoxTypes.None)
						return false;
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.Preferences);
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					if ((AudioPlayback.Mode == AudioPlaybackMode.Stopped)
					 || (AudioPlayback.Mode == AudioPlaybackMode.SingleStep && Status.CurrentPageNumber == PageNumbers.Info))
					{
						FinishMiniPop();
						if (k.State == KeyState.Press)
							AudioPlayback.Start();
					}
					if (k.State == KeyState.Press)
					{
						if (Status.MessageBoxType != MessageBoxTypes.None)
							return false;
						FinishMiniPop();
						SetPage(PageNumbers.Info);
					}
				}
				else
				{
					break;
				}
				return true;
			case KeySym.F6:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						AudioPlayback.StartAtOrder(AllPages.OrderList.CurrentOrder, 0);
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						AudioPlayback.LoopPattern(AllPages.PatternEditor.CurrentPattern, 0);
				}
				else
				{
					break;
				}
				return true;
			case KeySym.F7:
				if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						AllPages.PatternEditor.PlaySongFromMark();
				}
				else
				{
					break;
				}
				return true;
			case KeySym.F8:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					if (k.State == KeyState.Press)
						AudioPlayback.Pause();
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						AudioPlayback.Stop();
					Status.Flags |= StatusFlags.NeedUpdate;
				}
				else
				{
					break;
				}
				return true;
			case KeySym.F9:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.Message);
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.ModuleLoad);
				}
				else
				{
					break;
				}
				return true;
			case KeySym.l:
			case KeySym.r:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					FinishMiniPop();
					if (k.State == KeyState.Release)
						SetPage(PageNumbers.ModuleLoad);
				}
				else
				{
					break;
				}
				return true;
			case KeySym.s:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					FinishMiniPop();
					if (k.State == KeyState.Release)
						SaveSongOrSaveAs();
				}
				else
				{
					break;
				}
				return true;
			case KeySym.w:
				/* Ctrl-W _IS_ in IT, and hands don't leave home row :) */
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					FinishMiniPop();
					if (k.State == KeyState.Release)
						SetPage(PageNumbers.ModuleSave);
				}
				else
				{
					break;
				}
				return true;
			case KeySym.F10:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt)) break;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control)) break;

				FinishMiniPop();
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.ModuleExport);
				}
				else
				{
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.ModuleSave);
				}
				return true;
			case KeySym.F11:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					FinishMiniPop();
					if (Status.CurrentPageNumber == PageNumbers.OrderListPanning)
					{
						if (k.State == KeyState.Press)
							SetPage(PageNumbers.OrderListVolumes);
					}
					else
					{
						if (k.State == KeyState.Press)
							SetPage(PageNumbers.OrderListPanning);
					}
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					if (k.State == KeyState.Press)
					{
						FinishMiniPop();

						if (Status.CurrentPageNumber == PageNumbers.Log)
							SetPage(PageNumbers.About);
						else
							SetPage(PageNumbers.Log);
					}
				}
				else if (k.State == KeyState.Press && (k.Modifiers.HasAnyFlag(KeyMod.Alt)))
				{
					FinishMiniPop();
					if (Song.CurrentSong != null)
					{
						Song.CurrentSong.Flags ^= SongFlags.OrderListLocked;

						if (Song.CurrentSong.Flags.HasFlag(SongFlags.OrderListLocked))
							Status.FlashText("Order list locked");
						else
							Status.FlashText("Order list unlocked");
					}
				}
				else
				{
					break;
				}
				return true;
			case KeySym.F12:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				if ((k.Modifiers.HasAnyFlag(KeyMod.Alt)) && Status.CurrentPageNumber == PageNumbers.Info)
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.Waterfall);
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.PaletteEditor);
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
					{
						AllPages.FontEditor.ReturnPageNumber = Status.CurrentPageNumber;
						SetPage(PageNumbers.FontEditor);
					}
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.SongVariables);
				}
				else
				{
					break;
				}
				return true;
			/* hack alert */
			case KeySym.f:
				if (!(k.Modifiers.HasAnyFlag(KeyMod.Control)))
					return false;
				/* fall through */
				goto case KeySym.ScrollLock;
			case KeySym.ScrollLock:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				FinishMiniPop();
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Press)
					{
						AllPages.MIDI.Flags ^= MIDIFlags.DisableRecord;
						if (AllPages.MIDI.Flags.HasFlag(MIDIFlags.DisableRecord))
							Status.FlashText("MIDI Input Enabled");
						else
							Status.FlashText("MIDI Input Disabled");
					}

					return true;
				}
				else
				{
					/* os x steals plain scroll lock for brightness,
					* so catch ctrl+scroll lock here as well */
					if (k.State == KeyState.Press)
					{
						AllPages.PatternEditor.MIDIPlaybackTracing ^= true;
						if (AllPages.PatternEditor.MIDIPlaybackTracing)
							Status.FlashText("Playback tracing enabled");
						else
							Status.FlashText("Playback tracing disabled");
					}

					return true;
				}
			case KeySym.Pause:
				if (k.Modifiers.HasFlag(KeyMod.LeftShift) && k.Modifiers.HasFlag(KeyMod.LeftAlt) && k.Modifiers.HasFlag(KeyMod.RightAlt) && k.Modifiers.HasFlag(KeyMod.RightControl))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.TimeInformation);

					return true;
				}
				return false;
			case KeySym.t:
				if (k.Modifiers.HasAnyFlag(KeyMod.Control) && k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						SetPage(PageNumbers.TimeInformation);

					return true;
				}
				return false;
			default:
				if (Status.MessageBoxType != MessageBoxTypes.None)
					return false;
				break;
		}

		/* got a bit ugly here, sorry */
		if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
		{
			int i;

			switch (k.Sym)
			{
				case KeySym.F1: i = 0; break;
				case KeySym.F2: i = 1; break;
				case KeySym.F3: i = 2; break;
				case KeySym.F4: i = 3; break;
				case KeySym.F5: i = 4; break;
				case KeySym.F6: i = 5; break;
				case KeySym.F7: i = 6; break;
				case KeySym.F8: i = 7; break;
				default:
					return false;
			}

			if (k.State == KeyState.Release)
				return true;

			Song.CurrentSong.ToggleChannelMute(i);
			Status.Flags |= StatusFlags.NeedUpdate;
			return true;
		}

		/* oh well */
		return false;
	}

	protected static void ShowLengthDialog(string label, TimeSpan length)
	{
		MessageBox.Show(
			MessageBoxTypes.OK,
			$"{label}: {length.Hours,3}:{length.Minutes:d2}:{length.Seconds:d2}");
	}

	public static void ShowSongLength()
	{
		ShowLengthDialog("Total song time", Song.CurrentSong.GetLength());
	}
}
