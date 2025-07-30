using System;
using System.Diagnostics;

namespace ChasmTracker.Menus;

using ChasmTracker.Dialogs;
using ChasmTracker.Input;
using ChasmTracker.Pages;
using ChasmTracker.Playback;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class Menu
{
	/* EnsureMenu()
	 * will emit a warning and cause the function to return
	 * if a menu is not active.
	 *
	 * usage: if (!EnsureMenu()) return;
	 */
	static bool EnsureMenu()
	{
#if DEBUG
		if (!Status.DialogType.HasFlag(Dialogs.DialogTypes.Menu))
		{
			Console.Error.WriteLine("{0} called with no menu", new StackFrame(1, true).GetMethod()?.Name ?? "<unknown>");
			return false;
		}
#endif

		return true;
	}

	/* --------------------------------------------------------------------- */

	public Point Position;
	public int Width;
	public string Title;
	public string[] Items;
	public int SelectedItem; /* the highlighted item */
	public int ActiveItem; /* "pressed" menu item, for submenus */
	public Action SelectedCallback; /* triggered by return key */

	Menu(Point position, int width, string title, string[] items, Action selectedCallback)
	{
		Position = position;
		Width = width;
		Title = title;
		Items = items;
		SelectedCallback = selectedCallback;
	}

	static Menu MainMenu = new Menu(
		position: new Point(6, 11),
		width: 25,
		title: " Main Menu",
		items:
		[
			"File Menu...",
			"Playback Menu...",
			"View Patterns        (F2)",
			"Sample Menu...",
			"Instrument Menu...",
			"View Orders/Panning (F11)",
			"View Variables      (F12)",
			"Message Editor (Shift-F9)",
			"Settings Menu...",
			"Help!                (F1)",
		],
		selectedCallback: MainMenuSelected);

	static Menu FileMenu = new Menu(
		position: new Point(25, 13),
		width: 22,
		title: "File Menu",
		items:
		[
			"Load...           (F9)",
			"New...        (Ctrl-N)",
			"Save Current  (Ctrl-S)",
			"Save As...       (F10)",
			"Export...  (Shift-F10)",
			"Message Log (Ctrl-F11)",
			"Quit          (Ctrl-Q)",
		],
		selectedCallback: FileMenuSelected);

	static Menu PlaybackMenu = new Menu(
		position: new Point(25, 13),
		width: 27,
		title: " Playback Menu",
		items:
		[
			"Show Infopage          (F5)",
			"Play Song         (Ctrl-F5)",
			"Play Pattern           (F6)",
			"Play from Order  (Shift-F6)",
			"Play from Mark/Cursor  (F7)",
			"Stop                   (F8)",
			"Reinit Soundcard   (Ctrl-I)",
			"Driver Screen    (Shift-F5)",
			"Calculate Length   (Ctrl-P)",
		],
		selectedCallback: PlaybackMenuSelected);

	static Menu SampleMenu = new Menu(
		position: new Point(25, 20),
		width: 25,
		title: "Sample Menu",
		items:
		[
			"Sample List          (F3)",
			"Sample Library  (Ctrl-F3)",
		],
		selectedCallback: SampleMenuSelected);

	static Menu InstrumentMenu = new Menu(
		position: new Point(20, 23),
		width: 29,
		title: "Instrument Menu",
		items:
		[
			"Instrument List          (F4)",
			"Instrument Library  (Ctrl-F4)",
		],
		selectedCallback: InstrumentMenuSelected);

	static Menu SettingsMenu = new Menu(
		position: new Point(22, 25),
		width: 34,
		title: "Settings Menu",
		/* num_items is fiddled with when the menu is loaded (if there's no window manager,
		the toggle fullscreen item doesn't appear) */
		items:
		[
			"Preferences             (Shift-F5)",
			"MIDI Configuration      (Shift-F1)",
			"System Configuration     (Ctrl-F1)",
			"Palette Editor          (Ctrl-F12)",
			"Font Editor            (Shift-F12)",
			"Toggle Fullscreen (Ctrl-Alt-Enter)",
		],
		selectedCallback: SettingsMenuSelected);

	/* *INDENT-ON* */

	/* updated to whatever menu is currently active.
	* this generalises the key handling.
	* if Status.DialogType == DialogTypes.SubMenu, use s_currentMenu[1]
	* else, use s_currentMenu[0] */
	static Menu?[] s_currentMenu = new Menu[2];

	/* --------------------------------------------------------------------- */

	void Draw()
	{
		int h = 6, n = Items.Length;

		while (n-- > 0)
		{
			VGAMem.DrawBox(new Point(2 + Position.X, 4 + Position.Y + 3 * n),
				new Point(5 + Position.X + Width, 6 + Position.Y + 3 * n),
				BoxTypes.Thin | BoxTypes.Corner | (n == ActiveItem ? BoxTypes.Inset : BoxTypes.Outset));

			VGAMem.DrawTextLen(Items[n], Width, new Point(4 + Position.X, 5 + Position.Y + 3 * n),
							n == SelectedItem ? (3, 2) : (0, 2));

			VGAMem.DrawCharacter(0, new Point(3 + Position.X, 5 + Position.Y + 3 * n), (0, 2));
			VGAMem.DrawCharacter(0, new Point(4 + Position.X + Width, 5 + Position.Y + 3 * n), (0, 2));

			h += 3;
		}

		VGAMem.DrawBox(new Point(Position.X, Position.Y), new Point(Position.X + Width + 7, Position.Y + h - 1),
			BoxTypes.Thick | BoxTypes.Outer | BoxTypes.FlatLight);
		VGAMem.DrawBox(new Point(Position.X + 1, Position.Y + 1), new Point(Position.X + Width + 6,
			Position.Y + h - 2), BoxTypes.Thin | BoxTypes.Outer | BoxTypes.FlatDark);
		VGAMem.DrawFillCharacters(new Point(Position.X + 2, Position.Y + 2), new Point(Position.X + Width + 5, Position.Y + 3), (VGAMem.DefaultForeground, 2));
		VGAMem.DrawText(Title, new Point(Position.X + 6, Position.Y + 2), (3, 2));
	}

	public static void DrawActiveMenu()
	{
		if (!EnsureMenu())
			return;

		s_currentMenu[0]?.Draw();
		s_currentMenu[1]?.Draw();
	}

	/* --------------------------------------------------------------------- */

	public static void Show()
	{
		Dialog.DestroyAll();

		Status.DialogType = DialogTypes.MainMenu;

		s_currentMenu[0] = MainMenu;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public static void Hide()
	{
		if (!EnsureMenu())
			return;

		Status.DialogType = DialogTypes.None;

		/* "unpress" the menu items */
		for (int i = 0; i < s_currentMenu.Length; i++)
			if (s_currentMenu[i] is Menu menu)
				menu.ActiveItem = -1;

		Array.Clear(s_currentMenu);

		/* note! this does NOT redraw the screen; that's up to the caller.
		* the reason for this is that so many of the menu items cause a
		* page switch, and redrawing the current page followed by
		* redrawing a new page is redundant. */
	}

	/* --------------------------------------------------------------------- */

	static void SetSubmenu(Menu menu)
	{
		if (!EnsureMenu())
			return;

		Status.DialogType = DialogTypes.SubMenu;
		MainMenu.ActiveItem = MainMenu.SelectedItem;
		s_currentMenu[1] = menu;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	/* --------------------------------------------------------------------- */
	/* callbacks */

	static void MainMenuSelected()
	{
		switch (MainMenu.SelectedItem)
		{
			case 0: /* file menu... */
				SetSubmenu(FileMenu);
				break;
			case 1: /* playback menu... */
				SetSubmenu(PlaybackMenu);
				break;
			case 2: /* view patterns */
				Page.SetPage(PageNumbers.PatternEditor);
				break;
			case 3: /* sample menu... */
				SetSubmenu(SampleMenu);
				break;
			case 4: /* instrument menu... */
				SetSubmenu(InstrumentMenu);
				break;
			case 5: /* view orders/panning */
				Page.SetPage(PageNumbers.OrderListPanning);
				break;
			case 6: /* view variables */
				Page.SetPage(PageNumbers.SongVariables);
				break;
			case 7: /* message editor */
				Page.SetPage(PageNumbers.Message);
				break;
			case 8: /* settings menu */
				SetSubmenu(SettingsMenu);
				break;
			case 9: /* help! */
				Page.SetPage(PageNumbers.Help);
				break;
		}
	}

	static void FileMenuSelected()
	{
		switch (FileMenu.SelectedItem)
		{
			case 0: /* load... */
				Page.SetPage(PageNumbers.ModuleLoad);
				break;
			case 1: /* new... */
				Dialog.Show<NewSongDialog>();
				break;
			case 2: /* save current */
				AllPages.ModuleSave.SaveSongOrSaveAs();
				break;
			case 3: /* save as... */
				Page.SetPage(PageNumbers.ModuleSave);
				break;
			case 4:
				/* export ... */
				Page.SetPage(PageNumbers.ModuleExport);
				break;
			case 5: /* message log */
				Page.SetPage(PageNumbers.Log);
				break;
			case 6: /* quit */
				Program.ShowExitPrompt();
				break;
		}
	}

	static void PlaybackMenuSelected()
	{
		switch (PlaybackMenu.SelectedItem)
		{
			case 0: /* show infopage */
				if (AudioPlayback.Mode == AudioPlaybackMode.Stopped
						|| (AudioPlayback.Mode == AudioPlaybackMode.SingleStep && Status.CurrentPageNumber == PageNumbers.Info))
					AudioPlayback.Start();
				Page.SetPage(PageNumbers.Info);
				return;
			case 1: /* play song */
				AudioPlayback.Start();
				break;
			case 2: /* play pattern */
				AudioPlayback.LoopPattern(AllPages.PatternEditor.CurrentPattern, 0);
				break;
			case 3: /* play from order */
				AudioPlayback.StartAtOrder(AllPages.OrderList.CurrentOrder, 0);
				break;
			case 4: /* play from mark/cursor */
				AllPages.PatternEditor.PlaySongFromMark();
				break;
			case 5: /* stop */
				AudioPlayback.Stop();
				break;
			case 6: /* reinit soundcard */
				AudioPlayback.Reinitialize(null);
				break;
			case 7: /* driver screen */
				Page.SetPage(PageNumbers.Preferences);
				return;
			case 8: /* calculate length */
				Page.ShowSongLength();
				return;
		}

		Hide();
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	static void SampleMenuSelected()
	{
		switch (SampleMenu.SelectedItem)
		{
			case 0: /* sample list */
				Page.SetPage(PageNumbers.SampleList);
				break;
			case 1: /* sample library */
				Page.SetPage(PageNumbers.SampleLibrary);
				break;
		}
	}

	static void InstrumentMenuSelected()
	{
		switch (InstrumentMenu.SelectedItem)
		{
			case 0: /* instrument list */
				Page.SetPage(PageNumbers.InstrumentList);
				break;
			case 1: /* instrument library */
				Page.SetPage(PageNumbers.InstrumentLibrary);
				break;
		}
	}

	static void SettingsMenuSelected()
	{
		switch (SettingsMenu.SelectedItem)
		{
			case 0: /* preferences page */
				Page.SetPage(PageNumbers.Preferences);
				return;
			case 1: /* midi configuration */
				Page.SetPage(PageNumbers.MIDI);
				return;
			case 2: /* config */
				Page.SetPage(PageNumbers.Config);
				return;
			case 3: /* palette configuration */
				Page.SetPage(PageNumbers.PaletteEditor);
				return;
			case 4: /* font editor */
				Page.SetPage(PageNumbers.FontEditor);
				return;
			case 5: /* toggle fullscreen */
				Video.ToggleDisplayFullscreen();
				break;
		}

		Hide();
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	/* --------------------------------------------------------------------- */

	/* As long as there's a menu active, this function will return true. */
	public static bool HandleKey(KeyEvent k)
	{
		if (!Status.DialogType.HasFlag(DialogTypes.Menu))
			return false;

		var menu = (Status.DialogType == DialogTypes.SubMenu) ? s_currentMenu[1] : s_currentMenu[0];

		if (menu == null)
			return false;

		if (k.Mouse != MouseState.None)
		{
			if (k.Mouse == MouseState.Click || k.Mouse == MouseState.DoubleClick)
			{
				int h = menu.Items.Length * 3;

				var bounds = new Rect(
					menu.Position,
					new Size(menu.Width + 7, h + 4) + (1, 1));

				var itemBounds = bounds.Advance(2, 4, -4, -4);

				if (itemBounds.Contains(k.MousePosition))
				{
					int n = (k.MousePosition.Y - itemBounds.TopLeft.Y) / 3;

					if (n >= 0 && n < menu.Items.Length)
					{
						menu.SelectedItem = n;
						if (k.State == KeyState.Release)
						{
							menu.ActiveItem = -1;
							menu.SelectedCallback();
						}
						else
						{
							Status.Flags |= StatusFlags.NeedUpdate;
							menu.ActiveItem = n;
						}
					}
				}
				else if (k.State == KeyState.Release && !bounds.Contains(k.MousePosition))
				{
					/* get rid of the menu */
					s_currentMenu[1] = null;

					if (Status.DialogType == DialogTypes.SubMenu)
					{
						Status.DialogType = DialogTypes.MainMenu;
						MainMenu.ActiveItem = -1;
					}
					else
						Hide();

					Status.Flags |= StatusFlags.NeedUpdate;
				}
			}

			return true;
		}

		switch (k.Sym)
		{
			case KeySym.Escape:
				if (k.State == KeyState.Release)
					return true;
				s_currentMenu[1] = null;
				if (Status.DialogType == DialogTypes.SubMenu)
				{
					Status.DialogType = DialogTypes.MainMenu;
					MainMenu.ActiveItem = -1;
				}
				else
					Hide();
				break;
			case KeySym.Up:
				if (k.State == KeyState.Release)
					return true;
				if (menu.SelectedItem > 0)
				{
					menu.SelectedItem--;
					break;
				}
				return true;
			case KeySym.Down:
				if (k.State == KeyState.Release)
					return true;
				if (menu.SelectedItem < menu.Items.Length - 1)
				{
					menu.SelectedItem++;
					break;
				}
				return true;
			/* home/end are new here :) */
			case KeySym.Home:
				if (k.State == KeyState.Release)
					return true;
				menu.SelectedItem = 0;
				break;
			case KeySym.End:
				if (k.State == KeyState.Release)
					return true;
				menu.SelectedItem = menu.Items.Length - 1;
				break;
			case KeySym.Return:
				if (k.State == KeyState.Press)
				{
					menu.ActiveItem = menu.SelectedItem;
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
				menu.SelectedCallback();
				return true;
			default:
				return true;
		}

		Status.Flags |= StatusFlags.NeedUpdate;

		return true;
	}
}