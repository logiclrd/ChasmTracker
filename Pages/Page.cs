using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.Dialogs.PatternEditor;
using ChasmTracker.Events;
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
	public Shared<int> SelectedWidgetIndex => _selectedWidgetIndex;

	Shared<int> _selectedWidgetIndex = new Shared<int>();

	protected void LinkSelectedWidgetIndex(Shared<int> commonSelectedWidgetIndex)
	{
		_selectedWidgetIndex = commonSelectedWidgetIndex;
	}

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

	public static bool ChangeFocusTo(Point pt)
	{
		if (ActiveWidgets == null)
			return false;

		for (int i = 0; i < ActiveWidgets.Count; i++)
		{
			if (ActiveWidgets[i].ContainsPoint(pt))
			{
				ChangeFocusTo(i);
				return true;
			}
		}

		return false;
	}

	protected Page(PageNumbers pageNumber, string title, HelpTexts helpText)
	{
		PageNumber = pageNumber;
		Title = title;
		HelpText = helpText;

		if (this is IConfigurable configurable)
			Configuration.RegisterConfigurable(configurable);

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

	bool _drawFullDoesNothing = false;

	void CheckDrawFull()
	{
		var thisType = GetType();

		if (thisType.GetMethod(nameof(DrawFull))?.DeclaringType != thisType)
			_drawFullDoesNothing = true;
	}

	public bool DrawFullDoesNothing => _drawFullDoesNothing;

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

	static int s_lastOrder = -1;
	static int s_lastRow = -1;
	static TimeSpan s_lastTime;

	static (int H, int M, int S) s_currentTime;

	/* return 1 -> the time changed; need to redraw */
	public static bool CheckTime()
	{
		bool isPlaying = AudioPlayback.Mode.HasAnyFlag(AudioPlaybackMode.Playing | AudioPlaybackMode.PatternLoop);

		var td = Status.TimeDisplay;

		switch (td)
		{
			case TrackerTimeDisplay.PlayElapsed:
				td = isPlaying ? TrackerTimeDisplay.Playback : TrackerTimeDisplay.Elapsed;
				break;
			case TrackerTimeDisplay.PlayClock:
				td = isPlaying ? TrackerTimeDisplay.Playback : TrackerTimeDisplay.Clock;
				break;
			case TrackerTimeDisplay.PlayOff:
				td = isPlaying ? TrackerTimeDisplay.Playback : TrackerTimeDisplay.Off;
				break;
		}

		int h, m, s;

		switch (td)
		{
			case TrackerTimeDisplay.Off:
			{
				h = m = s = 0;
				break;
			}
			case TrackerTimeDisplay.Playback:
			{
				var t = AudioPlayback.CurrentTime;
				(h, m, s) = (t.Hours, t.Minutes, t.Seconds);
				break;
			}
			case TrackerTimeDisplay.Elapsed:
			{
				var t = DateTime.UtcNow - Program.StartTimeUTC;
				(h, m, s) = (t.Hours, t.Minutes, t.Seconds);
				break;
			}
			case TrackerTimeDisplay.Absolute:
			{
				/* absolute time shows the time of the current cursor
				position in the pattern editor :) */
				int row, order;

				if (Status.CurrentPageNumber == PageNumbers.PatternEditor)
				{
					row = AllPages.PatternEditor.CurrentRow;
					order = Song.CurrentSong.GetNextOrderForPattern(AllPages.PatternEditor.CurrentPattern);
				}
				else
				{
					order = AllPages.OrderList.CurrentOrder;
					row = 0;
				}

				if (order < 0)
					s = m = h = 0;
				else
				{
					TimeSpan time;

					if (s_lastOrder == order && s_lastRow == row)
						time = s_lastTime;
					else
					{
						s_lastTime = time = Song.CurrentSong.GetLengthTo(order, row);
						s_lastOrder = order;
						s_lastRow = row;
					}

					(h, m, s) = (time.Hours, time.Minutes, time.Seconds);
				}
				break;
			}
			default:
			/* this will never happen */
			case TrackerTimeDisplay.Clock:
			{
				/* Impulse Tracker doesn't have this, but I always wanted it, so here 'tis. */
				var now = Status.Now;
				(h, m, s) = (now.Hour, now.Minute, now.Second);
				break;
			}
		}

		if ((h, m, s) == s_currentTime)
			return false;

		s_currentTime = (h, m, s);

		return true;
	}

	static void DrawTime()
	{
		bool isPlaying = AudioPlayback.Mode.HasAnyFlag(AudioPlaybackMode.Playing | AudioPlaybackMode.PatternLoop);

		if ((Status.TimeDisplay == TrackerTimeDisplay.Off)
		 || (Status.TimeDisplay == TrackerTimeDisplay.PlayOff && !isPlaying))
			return;

		/* this allows for 999 hours... that's like... 41 days...
		* who on earth leaves a tracker running for 41 days? */
		if (Status.TimeDisplay == TrackerTimeDisplay.Clock)
		{
			string str = Status.Now.ToString(Configuration.General.TimeFormat.GetFormatString());

			if (str.Length > 0)
			{
				VGAMem.DrawText(str, new Point(69 + Math.Min(9, 9 - str.Length), 9), (0, 2));
				return;
			}
		}

		string buf = string.Format("{0:##0}:{1:00}:{2:00}", s_currentTime.H % 1000, s_currentTime.M, s_currentTime.S);

		VGAMem.DrawText(buf, new Point(69, 9), (0, 2));
	}

	/* --------------------------------------------------------------------- */

	static void DrawPageTitle()
	{
		int titleLength = Status.CurrentPage.Title.Length;

		if (titleLength > 0)
		{
			int titlePosition = 41 - ((titleLength + 1) / 2);

			for (int x = 1; x < titlePosition - 1; x++)
				VGAMem.DrawCharacter(154, new Point(x, 11), (1, 2));
			VGAMem.DrawCharacter(0, new Point(titlePosition - 1, 11), (1, 2));
			VGAMem.DrawText	(Status.CurrentPage.Title, new Point(titlePosition, 11), (0, 2));
			VGAMem.DrawCharacter(0, new Point(titlePosition + titleLength, 11), (1, 2));
			for (int x = titlePosition + titleLength + 1; x < 79; x++)
				VGAMem.DrawCharacter(154, new Point(x, 11), (1, 2));
		}
		else
		{
			for (int x = 1; x < 79; x++)
				VGAMem.DrawCharacter(154, new Point(x, 11), (1, 2));
		}
	}

	/* --------------------------------------------------------------------- */
	/* Not that happy with the way this function developed, but well, it still
	* works. Maybe someday I'll make it suck less. */

	static void DrawPage()
	{
		var activePage = Status.CurrentPage;

		if (!activePage._drawFullDoesNothing)
			activePage.DrawFull();
		else
		{
			DrawPageTitle();
			activePage.DrawConst();
			activePage.PredrawHook();
		}

		int n = activePage.Widgets.Count;

		/* this doesn't use widgets[] because it needs to draw the page's
		* widgets whether or not a dialog is active */
		while (n-- > 0)
			activePage.Widgets[n].DrawWidget(n == activePage.SelectedWidgetIndex);

		/* redraw the area over the menu if there is one */
		if (Status.DialogType.HasFlag(DialogTypes.Menu))
			Menu.DrawActiveMenu();
		else if (Status.DialogType.HasFlag(DialogTypes.Box))
			Dialog.DrawActiveDialogs();
	}

	/* --------------------------------------------------------------------- */

	static void DrawTopInfoConst()
	{
		int tl, br;

		if (Status.Flags.HasFlag(StatusFlags.InvertedPalette))
			(tl, br) = (3, 1);
		else
			(tl, br) = (1, 3);

		string banner = Version.GetBanner(Status.Flags.HasFlag(StatusFlags.ClassicMode));

		VGAMem.DrawText(banner, new Point((80 - banner.Length) / 2, 1), (0, 2));
		VGAMem.DrawText("Song Name", new Point(2, 3), (0, 2));
		VGAMem.DrawText("File Name", new Point(2, 4), (0, 2));
		VGAMem.DrawText("Order", new Point(6, 5), (0, 2));
		VGAMem.DrawText("Pattern", new Point(4, 6), (0, 2));
		VGAMem.DrawText("Row", new Point(8, 7), (0, 2));

		VGAMem.DrawText("Speed/Tempo", new Point(38, 4), (0, 2));
		VGAMem.DrawText("Octave", new Point(43, 5), (0, 2));

		VGAMem.DrawText("F1...Help       F9.....Load", new Point(21, 6), (0, 2));
		VGAMem.DrawText("ESC..Main Menu  F5/F8..Play / Stop", new Point(21, 7), (0, 2));

		/* the neat-looking (but incredibly ugly to draw) borders */
		VGAMem.DrawCharacter(128, new Point(30, 4), (br, 2));
		VGAMem.DrawCharacter(128, new Point(57, 4), (br, 2));
		VGAMem.DrawCharacter(128, new Point(19, 5), (br, 2));
		VGAMem.DrawCharacter(128, new Point(51, 5), (br, 2));
		VGAMem.DrawCharacter(129, new Point(36, 4), (br, 2));
		VGAMem.DrawCharacter(129, new Point(50, 6), (br, 2));
		VGAMem.DrawCharacter(129, new Point(17, 8), (br, 2));
		VGAMem.DrawCharacter(129, new Point(18, 8), (br, 2));
		VGAMem.DrawCharacter(131, new Point(37, 3), (br, 2));
		VGAMem.DrawCharacter(131, new Point(78, 3), (br, 2));
		VGAMem.DrawCharacter(131, new Point(19, 6), (br, 2));
		VGAMem.DrawCharacter(131, new Point(19, 7), (br, 2));
		VGAMem.DrawCharacter(132, new Point(49, 3), (tl, 2));
		VGAMem.DrawCharacter(132, new Point(49, 4), (tl, 2));
		VGAMem.DrawCharacter(132, new Point(49, 5), (tl, 2));
		VGAMem.DrawCharacter(134, new Point(75, 2), (tl, 2));
		VGAMem.DrawCharacter(134, new Point(76, 2), (tl, 2));
		VGAMem.DrawCharacter(134, new Point(77, 2), (tl, 2));
		VGAMem.DrawCharacter(136, new Point(37, 4), (br, 2));
		VGAMem.DrawCharacter(136, new Point(78, 4), (br, 2));
		VGAMem.DrawCharacter(136, new Point(30, 5), (br, 2));
		VGAMem.DrawCharacter(136, new Point(57, 5), (br, 2));
		VGAMem.DrawCharacter(136, new Point(51, 6), (br, 2));
		VGAMem.DrawCharacter(136, new Point(19, 8), (br, 2));
		VGAMem.DrawCharacter(137, new Point(49, 6), (br, 2));
		VGAMem.DrawCharacter(137, new Point(11, 8), (br, 2));
		VGAMem.DrawCharacter(138, new Point(37, 2), (tl, 2));
		VGAMem.DrawCharacter(138, new Point(78, 2), (tl, 2));
		VGAMem.DrawCharacter(139, new Point(11, 2), (tl, 2));
		VGAMem.DrawCharacter(139, new Point(49, 2), (tl, 2));

		for (int n = 0; n < 5; n++)
		{
			VGAMem.DrawCharacter(132, new Point(11, 3 + n), (tl, 2));
			VGAMem.DrawCharacter(129, new Point(12 + n, 8), (br, 2));
			VGAMem.DrawCharacter(134, new Point(12 + n, 2), (tl, 2));
			VGAMem.DrawCharacter(129, new Point(20 + n, 5), (br, 2));
			VGAMem.DrawCharacter(129, new Point(31 + n, 4), (br, 2));
			VGAMem.DrawCharacter(134, new Point(32 + n, 2), (tl, 2));
			VGAMem.DrawCharacter(134, new Point(50 + n, 2), (tl, 2));
			VGAMem.DrawCharacter(129, new Point(52 + n, 5), (br, 2));
			VGAMem.DrawCharacter(129, new Point(58 + n, 4), (br, 2));
			VGAMem.DrawCharacter(134, new Point(70 + n, 2), (tl, 2));
		}
		for (int n = 5; n < 10; n++)
		{
			VGAMem.DrawCharacter(134, new Point(12 + n, 2), (tl, 2));
			VGAMem.DrawCharacter(129, new Point(20 + n, 5), (br, 2));
			VGAMem.DrawCharacter(134, new Point(50 + n, 2), (tl, 2));
			VGAMem.DrawCharacter(129, new Point(58 + n, 4), (br, 2));
		}
		for (int n = 10; n < 20; n++)
		{
			VGAMem.DrawCharacter(134, new Point(12 + n, 2), (tl, 2));
			VGAMem.DrawCharacter(134, new Point(50 + n, 2), (tl, 2));
			VGAMem.DrawCharacter(129, new Point(58 + n, 4), (br, 2));
		}

		VGAMem.DrawText("Time", new Point(63, 9), (0, 2));
		VGAMem.DrawCharacter('/', new Point(15, 5), (1, 0));
		VGAMem.DrawCharacter('/', new Point(15, 6), (1, 0));
		VGAMem.DrawCharacter('/', new Point(15, 7), (1, 0));
		VGAMem.DrawCharacter('/', new Point(53, 4), (1, 0));
		VGAMem.DrawCharacter(':', new Point(52, 3), (7, 0));
	}

	/* --------------------------------------------------------------------- */

	static void UpdateCurrentInstrument()
	{
		bool instrumentMode;

		if ((Status.CurrentPage is InstrumentListPage)
		|| (Status.CurrentPageNumber == PageNumbers.SampleList)
		|| (Status.CurrentPageNumber == PageNumbers.SampleLoad)
		|| (Status.CurrentPageNumber == PageNumbers.SampleLibrary)
		|| (!Status.Flags.HasFlag(StatusFlags.ClassicMode)
			&& (Status.CurrentPage is OrderListPage)))
			instrumentMode = false;
		else
			instrumentMode = Song.CurrentSong.IsInstrumentMode;

		string name = "";
		int n;

		if (instrumentMode)
		{
			VGAMem.DrawText("Instrument", new Point(39, 3), (0, 2));
			n = AllPages.InstrumentList.CurrentInstrument;
			if (n > 0)
				name = Song.CurrentSong.GetInstrument(n).Name ?? "";
		}
		else
		{
			VGAMem.DrawText("    Sample", new Point(39, 3), (0, 2));
			n = AllPages.SampleList.CurrentSample;
			if (n > 0)
				name = Song.CurrentSong.GetSample(n)?.Name ?? "";
		}

		if (n > 0)
		{
			VGAMem.DrawText(n.ToString99(), new Point(50, 3), (5, 0));
			VGAMem.DrawTextLen(name, 25, new Point(53, 3), (5, 0));
		}
		else
		{
			VGAMem.DrawText("..", new Point(50, 3), (5, 0));
			VGAMem.DrawText(".........................", new Point(53, 3), (5, 0));
		}
	}

	static void RedrawTopInfo()
	{
		UpdateCurrentInstrument();

		VGAMem.DrawTextLen(Song.CurrentSong.BaseName, 18, new Point(12, 4), (5, 0));
		VGAMem.DrawTextLen(Song.CurrentSong.Title, 25, new Point(12, 3), (5, 0));

		if ((Status.Flags & (StatusFlags.ClassicMode | StatusFlags.SongNeedsSave)) == StatusFlags.SongNeedsSave)
			VGAMem.DrawCharacter('+', new Point(29, 4), (4, 0));

		AllPages.OrderList.UpdateCurrentOrder();
		AllPages.PatternEditor.UpdateCurrentPattern();
		AllPages.PatternEditor.UpdateCurrentRow();

		VGAMem.DrawText(Song.CurrentSong.CurrentSpeed.ToString("d3"), new Point(50, 4), (5, 0));
		VGAMem.DrawText(Song.CurrentSong.CurrentTempo.ToString("d3"), new Point(54, 4), (5, 0));
		VGAMem.DrawCharacter((byte)('0' + Keyboard.CurrentOctave), new Point(50, 5), (5, 0));
	}

	static void DrawVisualizationBox()
	{
		VGAMem.DrawBox(new Point(62, 5), new Point(78, 8), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawFillCharacters(new Point(63, 6), new Point(77, 7), (VGAMem.DefaultForeground, 0));
	}

	[MemberNotNull(nameof(s_visualizationOverlay))]
	static void EnsureVisualizationOverlay()
	{
		if (s_visualizationOverlay == null)
			s_visualizationOverlay = VGAMem.AllocateOverlay(new Point(63, 6), new Point(77, 7));
	}

	static VGAMemOverlay? s_visualizationOverlay;

	/*this is the size of s_visualizationOverlay.Width*/
	static byte[] s_outFFT = new byte[120];

	static void VisualizationFFT()
	{
		EnsureVisualizationOverlay();

		DrawVisualizationBox();

		lock (AudioPlayback.LockScope())
		{
			s_visualizationOverlay.Clear(0);

			FFT.GetColumns(s_outFFT, 120);

			for (int i = 0; i < 120; i++)
			{
				int y = s_outFFT[i];
				/* reduce range */
				y = y >> 3;
				if (y > 15) y = 15;
				if (y > 0) s_visualizationOverlay.DrawLine(new Point(i, 15 - y), new Point(i, 15), 5);
			}

			VGAMem.ApplyOverlay(s_visualizationOverlay);
		}
	}

	static void VisualizationOscilloscope()
	{
		EnsureVisualizationOverlay();

		DrawVisualizationBox();

		lock (AudioPlayback.LockScope())
		{
			int outputChannels = (Status.VisualizationStyle == TrackerVisualizationStyle.MonoScope) ? 1 : AudioPlayback.AudioOutputChannels;

			switch (AudioPlayback.AudioOutputBits)
			{
				case 8:
					VGAMem.DrawSampleDataRect8(s_visualizationOverlay, MemoryMarshal.Cast<short, sbyte>(AudioPlayback.AudioBuffer),
						AudioPlayback.AudioOutputChannels, outputChannels);
					break;
				case 16:
					VGAMem.DrawSampleDataRect16(s_visualizationOverlay, AudioPlayback.AudioBuffer,
						AudioPlayback.AudioOutputChannels, outputChannels);
					break;
				case 32:
					VGAMem.DrawSampleDataRect32(s_visualizationOverlay, MemoryMarshal.Cast<short, int>(AudioPlayback.AudioBuffer),
						AudioPlayback.AudioOutputChannels, outputChannels);
					break;
				default:
					break;
			}
		}
	}

	static void VisualizationVUMeter()
	{
		int left, right;

		AudioPlayback.GetVUMeter(out left, out right);

		left >>= 1;
		right >>= 1;

		DrawVisualizationBox();
		VGAMem.DrawVUMeter(new Point(63, 6), 15, left, 5, 4);
		VGAMem.DrawVUMeter(new Point(63, 7), 15, right, 5, 4);
	}

	static void VisualizationFakeMem()
	{
		if (Status.Flags.HasFlag(StatusFlags.ClassicMode))
		{
			int ems = MemoryUsage.EMS;
			if (ems > 67108864) ems = 0;
			else ems = 67108864 - ems;

			int conv = MemoryUsage.LowMemory;
			if (conv > 524288) conv = 0;
			else conv = 524288 - conv;

			conv >>= 10;
			ems >>= 10;

			VGAMem.DrawText($"FreeMem {conv}k", new Point(63, 6), (0, 2));
			VGAMem.DrawText($"FreeEMS {ems}k", new Point(63, 7), (0, 2));
		}
		else
		{
			int sum = MemoryUsage.GetPatternUsage() + MemoryUsage.GetInstrumentUsage() + MemoryUsage.GetSongMessageUsage();

			VGAMem.DrawText($"   Song {sum >> 10}k", new Point(63, 6), (0, 2));
			VGAMem.DrawText($"Samples {MemoryUsage.GetSampleUsage() > 10}k", new Point(63, 7), (0, 2));
		}
	}

	static void DrawVisualization()
	{
		if (Status.Flags.HasFlag(StatusFlags.ClassicMode))
		{
			/* classic mode requires fakemem display */
			VisualizationFakeMem();
			return;
		}

		switch (Status.VisualizationStyle)
		{
			case TrackerVisualizationStyle.FakeMem:
				VisualizationFakeMem();
				break;
			case TrackerVisualizationStyle.Oscilloscope:
			case TrackerVisualizationStyle.MonoScope:
				VisualizationOscilloscope();
				break;
			case TrackerVisualizationStyle.VUMeter:
				VisualizationVUMeter();
				break;
			case TrackerVisualizationStyle.FFT:
				VisualizationFFT();
				break;
			default:
			case TrackerVisualizationStyle.Off:
				break;
		}
	}

	/* this completely redraws everything. */
	public static void RedrawScreen()
	{
		if (Status.CurrentPage.DrawFullDoesNothing)
		{
			VGAMem.DrawFillCharacters(new Point(0, 0), new Point(79, 49), (VGAMem.DefaultForeground, 2));

			/* border around the whole screen */
			VGAMem.DrawCharacter(128, new Point(0, 0), (3, 2));

			int n;

			for (n = 79; n > 49; n--)
				VGAMem.DrawCharacter(129, new Point(n, 0), (3, 2));

			do
			{
				VGAMem.DrawCharacter(129, new Point(n, 0), (3, 2));
				VGAMem.DrawCharacter(131, new Point(0, n), (3, 2));
			} while (--n > 0);

			DrawTopInfoConst();
			RedrawTopInfo();

			DrawVisualization();
			DrawTime();

			Status.RedrawText();
		}

		DrawPage();
	}

	/* important :) */
	public static void MainPlaybackUpdate()
	{
		/* the order here is significant -- check_time has side effects */
		if (CheckTime() || (AudioPlayback.Mode != AudioPlaybackMode.Stopped))
			Status.Flags |= StatusFlags.NeedUpdate;

		Status.CurrentPage.PlaybackUpdate();
	}

	/* called when the song-mode changes */
	public virtual void NotifySongModeChanged() { }

	public static void NotifySongModeChangedGlobal()
	{
		foreach (var page in AllPages.EnumeratePages())
			page.NotifySongModeChanged();
	}

	public static bool DoClipboardPaste(ClipboardPasteEvent cptr)
		=> Status.CurrentPage.ClipboardPaste(cptr);

	/* called by the clipboard manager */
	public virtual bool ClipboardPaste(ClipboardPasteEvent cptr) { return false; }

	public readonly List<Widget> Widgets = new List<Widget>();

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

	public static void SaveCheck(Action ok, Action? cancel = null)
	{
		if (Status.Flags.HasFlag(StatusFlags.SongNeedsSave))
			MessageBox.Show(MessageBoxTypes.OKCancel, "Current module not saved. Proceed?", ok, cancel);
		else
			ok();
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
							MainHandleTextInput(Encoding.Unicode.GetString([unicode, 0]));
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
					Song.CurrentSong.SetCurrentSpeed(Song.CurrentSong.CurrentSpeed - 1);
					Status.FlashText($"Speed set to {Song.CurrentSong.CurrentSpeed} frames per row");
					if (!AudioPlayback.IsPlaying)
						Song.CurrentSong.InitialSpeed = Song.CurrentSong.CurrentSpeed;
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Control) && !Status.Flags.HasFlag(StatusFlags.ClassicMode))
				{
					Song.CurrentSong.SetCurrentTempo(Song.CurrentSong.CurrentTempo - 1);
					Status.FlashText($"Tempo set to {Song.CurrentSong.CurrentTempo} beats per minute");
					if (!AudioPlayback.IsPlaying)
						Song.CurrentSong.InitialTempo = Song.CurrentSong.CurrentTempo;
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					Song.CurrentSong.SetCurrentGlobalVolume(Song.CurrentSong.CurrentGlobalVolume - 1);
					Status.FlashText($"Global volume set to {Song.CurrentSong.CurrentGlobalVolume}");
					if (!AudioPlayback.IsPlaying)
						Song.CurrentSong.InitialGlobalVolume = Song.CurrentSong.CurrentGlobalVolume;
				}
				return;
			case KeySym.RightBracket:
				if (k.State == KeyState.Release) break;
				if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive)) return;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					Song.CurrentSong.SetCurrentSpeed(Song.CurrentSong.CurrentSpeed + 1);
					Status.FlashText($"Speed set to {Song.CurrentSong.CurrentSpeed} frames per row");
					if (!AudioPlayback.IsPlaying)
						Song.CurrentSong.InitialSpeed = Song.CurrentSong.CurrentSpeed;
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Control) && !Status.Flags.HasFlag(StatusFlags.ClassicMode))
				{
					Song.CurrentSong.SetCurrentTempo(Song.CurrentSong.CurrentTempo + 1);
					Status.FlashText($"Tempo set to {Song.CurrentSong.CurrentTempo} beats per minute");
					if (!AudioPlayback.IsPlaying)
						Song.CurrentSong.InitialTempo = Song.CurrentSong.CurrentTempo;
				}
				else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					Song.CurrentSong.SetCurrentGlobalVolume(Song.CurrentSong.CurrentGlobalVolume + 1);
					Status.FlashText($"Global volume set to {Song.CurrentSong.CurrentGlobalVolume}");
					if (!AudioPlayback.IsPlaying)
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
						Song.CurrentSong.SetCurrentSpeed(newValue);
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
						Song.CurrentSong.SetCurrentTempo(newValue);
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
					Video.SetMouseCursor(MouseCursorMode.CycleState);
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
								Dialog.DialogButtonYes();
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
						AllPages.ModuleSave.SaveSongOrSaveAs();
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
						MIDIEngine.Flags ^= MIDIFlags.DisableRecord;
						if (MIDIEngine.Flags.HasFlag(MIDIFlags.DisableRecord))
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
