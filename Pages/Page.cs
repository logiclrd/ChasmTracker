using System;
using System.Collections.Generic;
using System.Linq;

//using Microsoft.Maui.Graphics;

namespace ChasmTracker;

using ChasmTracker.Dialogs;
using ChasmTracker.Memory;
using ChasmTracker.Pages;
using ChasmTracker.Songs;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public abstract class Page
{
	public readonly PageNumbers PageNumber;
	public readonly string Title;
	public readonly HelpTexts HelpText;
	public readonly SharedInt SelectedWidget = new SharedInt();

	public static List<Widget>? ActiveWidgets;
	public static SharedInt? SelectedActiveWidget;

	public static MiniPopState MiniPopActive;

	protected Page(PageNumbers pageNumber, string title, HelpTexts helpText)
	{
		PageNumber = pageNumber;
		Title = title;
		HelpText = helpText;
	}

	public static void SetPage(PageNumbers newPageNumber)
	{
		if (Status.CurrentPageNumber != newPageNumber)
			Status.PreviousPageNumber = Status.CurrentPageNumber;

		var newPage = AllPages.ByPageNumber(newPageNumber);

		newPage.SynchronizeWith(Status.CurrentPage);

		ActiveWidgets = newPage.Widgets;
		SelectedActiveWidget = newPage.SelectedWidget;

		Status.CurrentPageNumber = newPageNumber;
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
	public virtual bool PreHandleKey(KeyEvent k) { return false; }
	/* this catches any keys that the main handler doesn't deal with */
	public virtual bool HandleKey(KeyEvent k) { return false; }
	/* handle any text input events from SDL */
	public virtual void HandleTextInput(string textInput) { }
	/* called when the page is set. this is for reloading the
	 * directory in the file browsers. */
	public virtual void SetPage() { }

	/* called when the song-mode changes */
	public virtual void NotifySongModeChanged() { }

	/* called by the clipboard manager */
	public virtual bool ClipboardPaste(int cb, byte[] cptr) { return false; }

	public readonly List<Widget> Widgets = new List<Widget>();

	public void SetFocus(Widget widget)
	{
		SelectedWidget.Value = Widgets.IndexOf(widget);
	}

	/* HelpTexts.Global if no page-specific help */
	public HelpTexts HelpIndex;

	public PageMiniPopSlideDialog ShowMiniPop(int currentValue, string name, int min, int max, Point mid)
	{
		var miniPopDialog = Dialog.Show(new PageMiniPopSlideDialog(currentValue, name, min, max, mid));

		miniPopDialog.MiniPopUsed +=
			() => { MiniPopActive = MiniPopState.ActiveUsed; };

		MiniPopActive = MiniPopState.Active;

		return miniPopDialog;
	}

	public void FinishMiniPop()
	{
		if (MiniPopActive != MiniPopState.Inactive)
		{
			Dialog.DestroyAll();
			MiniPopActive = MiniPopState.Inactive;
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

	static PageNumbers s_fontEditReturnPage;

	public static void SaveCheck(Action<object?> ok, Action<object?>? cancel = null, object? data = null)
	{
		if (Status.Flags.HasFlag(StatusFlags.SongNeedsSave))
			MessageBox.Show(MessageBoxTypes.OKCancel, "Current module not saved. Proceed?", ok, cancel, data);
		else
			ok(data);
	}

	public static void ShowExitPrompt()
	{
		/* This used to kill all open dialogs, but that doesn't seem to be necessary.
		Do keep in mind though, a dialog *might* exist when this function is called
		(for example, if the WM sends a close request). */

		if (Status.CurrentPage is AboutPage)
		{
			/* haven't even started up yet; don't bother confirming */
			Program.Exit(0);
		}
		else if (Status.CurrentPage is FontEditorPage)
		{
			if (Status.Flags.HasFlag(StatusFlags.StartupFontEdit))
			{
				MessageBox.Show(MessageBoxTypes.OKCancel, "Exit Font Editor?", _ => Program.Exit(0));
			}
			else
			{
				/* don't ask, just go away */
				Dialog.DestroyAll();
				SetPage(s_fontEditReturnPage);
			}
		}
		else if (Status.MessageBoxType != MessageBoxTypes.OKCancel)
		{
			/* don't draw an exit prompt on top of an existing one */
			MessageBox.Show(
				MessageBoxTypes.OKCancel,
				Status.Flags.HasFlag(StatusFlags.ClassicMode)
				? "Exit Impulse Tracker?"
				: "Exit Schism Tracker?",
				_ => SaveCheck(_ => Program.Exit(0)));
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

		/* returns true if the key was handled */
	public bool HandleKeyGlobal(KeyEvent k)
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
				var minipop = ShowMiniPop(Song.CurrentSpeed, "Speed", 1, 255, new Point(51, 4));

				minipop.SetValue +=
					newValue =>
					{
						Song.CurrentSpeed = newValue;
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
				var minipop = ShowMiniPop(Song.CurrentTempo, "Tempo", 32, 255, new Point(55, 4));

				minipop.SetValue +=
					newValue =>
					{
						Song.CurrentTempo = newValue;
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
						ShowExitPrompt();
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
						NewSongDialog();
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
						ShowSongTimeJump();
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
						if (k.State == KeyState.Press && Status.MessageBoxType == MessageBoxTypes.None)
							PatternEditorLengthEdit();
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
							if (Status.MessageBoxType.HasFlag(MessageBoxTypes.Menu))
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
								PatternEditorDisplayOptions();
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
						Song.StartAtArder(AllPages.OrderList.CurrentOrder, 0);
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						Song.LoopPattern(AllPages.PatternEditor.CurrentPattern);
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
						Song.PlayFromMark();
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
						Song.Pause();
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					FinishMiniPop();
					if (k.State == KeyState.Press)
						Song.Stop();
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
						SetPage(PageNumbers.ExportModule);
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
						{
							ShowAbout();
						}
						else
						{
							SetPage(PageNumbers.Log);
						}
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
}
