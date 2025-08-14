using System;
using System.Text;

namespace ChasmTracker.Pages;

using ChasmTracker.Clipboard;
using ChasmTracker.Configurations;
using ChasmTracker.Events;
using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class PaletteEditorPage : Page
{
	ThumbBarWidget[] thumbBarRed = new ThumbBarWidget[16];
	ThumbBarWidget[] thumbBarGreen = new ThumbBarWidget[16];
	ThumbBarWidget[] thumbBarBlue = new ThumbBarWidget[16];
	OtherWidget? otherPaletteList;
	ButtonWidget? buttonCopy;
	ButtonWidget? buttonPaste;

	int _selectedPalette;

	public PaletteEditorPage()
		: base(PageNumbers.PaletteEditor, "Palette Configuration (Ctrl-F12)", HelpTexts.Palettes)
	{
		_selectedPalette = VGAMem.CurrentPalette.Index;

		for (int n = 0; n < 16; n++)
		{
			thumbBarRed[n] = new ThumbBarWidget(
				new Point(10 + 27 * (n / 7), 5 * (n % 7) + 14), 9,
				0, 63);
			thumbBarGreen[n] = new ThumbBarWidget(
				new Point(10 + 27 * (n / 7), 5 * (n % 7) + 15), 9,
				0, 63);
			thumbBarBlue[n] = new ThumbBarWidget(
				new Point(10 + 27 * (n / 7), 5 * (n % 7) + 16), 9,
				0, 63);

			thumbBarRed[n].Changed += UpdatePalette;
			thumbBarGreen[n].Changed += UpdatePalette;
			thumbBarBlue[n].Changed += UpdatePalette;
		}

		UpdateThumbBars();

		otherPaletteList = new OtherWidget(new Point(56, 26), new Size(20, 15));

		otherPaletteList.OtherHandleKey += otherPaletteList_HandleKey;
		otherPaletteList.OtherRedraw += otherPaletteList_Redraw;

		buttonCopy = new ButtonWidget(new Point(55, 43), 20, "Copy To Clipboard", 3);
		buttonCopy.Clicked += buttonCopy_Clicked;

		buttonPaste = new ButtonWidget(new Point(55, 46), 20, "Paste From Clipboard", 1);
		buttonPaste.Clicked += buttonPaste_Clicked;

		for (int n = 0; n < 16; n++)
		{
			if (n >= 9 && n < 13)
			{
				thumbBarRed[n].Next.Tab = otherPaletteList;
				thumbBarGreen[n].Next.Tab = otherPaletteList;
				thumbBarBlue[n].Next.Tab = otherPaletteList;
			}
			else if (n == 13)
			{
				thumbBarRed[n].Next.Tab = buttonCopy;
				thumbBarGreen[n].Next.Tab = buttonCopy;
				thumbBarBlue[n].Next.Tab = buttonCopy;
			}
			else if (n > 13)
			{
				thumbBarRed[n].Next.Tab = thumbBarRed[n - 14];
				thumbBarGreen[n].Next.Tab = thumbBarGreen[n - 14];
				thumbBarBlue[n].Next.Tab = thumbBarBlue[n - 14];

				thumbBarRed[n - 14].Next.BackTab = thumbBarRed[n];
				thumbBarGreen[n - 14].Next.BackTab = thumbBarGreen[n];
				thumbBarBlue[n - 14].Next.BackTab = thumbBarBlue[n];
			}
			else
			{
				thumbBarRed[n].Next.Tab = thumbBarGreen[n + 7];
				thumbBarGreen[n].Next.Tab = thumbBarBlue[n + 7];
				thumbBarBlue[n].Next.Tab = thumbBarRed[n + 7];

				thumbBarRed[n + 7].Next.BackTab = thumbBarRed[n];
				thumbBarGreen[n + 7].Next.BackTab = thumbBarGreen[n];
				thumbBarBlue[n + 7].Next.BackTab = thumbBarBlue[n];
			}
		}

		otherPaletteList.Next.Tab = thumbBarRed[3];
		otherPaletteList.Next.BackTab = thumbBarRed[10];

		for (int i = 2; i < 6; i++)
		{
			thumbBarRed[i].Next.BackTab = otherPaletteList;
			thumbBarGreen[i].Next.BackTab = otherPaletteList;
			thumbBarBlue[i].Next.BackTab = otherPaletteList;
		}

		thumbBarRed[6].Next.BackTab = buttonCopy;
		thumbBarGreen[6].Next.BackTab = buttonCopy;
		thumbBarBlue[6].Next.BackTab = buttonCopy;

		buttonCopy.Next.Tab = thumbBarRed[6];
		buttonCopy.Next.BackTab = thumbBarRed[13];

		buttonPaste.Next.Tab = thumbBarBlue[6];
		buttonPaste.Next.BackTab = thumbBarBlue[13];

		for (int i = 0; i < 16; i++)
		{
			AddWidget(thumbBarRed[i]);
			AddWidget(thumbBarGreen[i]);
			AddWidget(thumbBarBlue[i]);
		}

		AddWidget(otherPaletteList);
		AddWidget(buttonCopy);
		AddWidget(buttonPaste);
	}

	/* --------------------------------------------------------------------- */
	/*
	This is actually wrong. For some reason the boxes around the little color swatches are drawn with the top
	right and bottom left corners in color 3 instead of color 1 like all the other thick boxes have. I'm going
	to leave it this way, though -- it's far more likely that someone will comment on, say, my completely
	changing the preset switcher than about the corners having different colors :)

	(Another discrepancy: seems that Impulse Tracker draws the thumbbars with a "fake" range of 0-64, because
	it never gets drawn at the far right. Oh well.) */

	public override void DrawConst()
	{
		int n;

		VGAMem.DrawText("Predefined Palettes", new Point(56, 24), (0, 2));

		for (n = 0; n < 7; n++)
		{
			VGAMem.DrawBox(new Point(2, 13 + (5 * n)), new Point(8, 17 + (5 * n)), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
			VGAMem.DrawBox(new Point(9, 13 + (5 * n)), new Point(19, 17 + (5 * n)), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
			VGAMem.DrawBox(new Point(29, 13 + (5 * n)), new Point(35, 17 + (5 * n)), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
			VGAMem.DrawBox(new Point(36, 13 + (5 * n)), new Point(46, 17 + (5 * n)), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
			VGAMem.DrawFillCharacters(new Point(3, 14 + (5 * n)), new Point(7, 16 + (5 * n)), (VGAMem.DefaultForeground, n));
			VGAMem.DrawFillCharacters(new Point(30, 14 + (5 * n)), new Point(34, 16 + (5 * n)), (VGAMem.DefaultForeground, n + 7));
		}

		VGAMem.DrawBox(new Point(56, 13), new Point(62, 17), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(63, 13), new Point(73, 17), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(56, 18), new Point(62, 22), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(63, 18), new Point(73, 22), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(54, 25), new Point(77, 41), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawFillCharacters(new Point(57, 14), new Point(61, 16), (VGAMem.DefaultForeground, 14));
		VGAMem.DrawFillCharacters(new Point(57, 19), new Point(61, 21), (VGAMem.DefaultForeground, 15));
	}

	/* --------------------------------------------------------------------- */

	public override bool? HandleKey(KeyEvent k)
	{
		int n = SelectedWidgetIndex;

		if (k.State == KeyState.Release)
			return null;

		switch (k.Sym)
		{
			case KeySym.PageUp:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					n -= 3;
				break;
			case KeySym.PageDown:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					n += 3;
				break;
			case KeySym.c:
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					CopyCurrentToClipboard();
					return true;
				}
				break;
			case KeySym.v:
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					PasteFromClipboard();
					return true;
				}
				break;
			default:
				return null;
		}

		if (Status.Flags.HasFlag(StatusFlags.ClassicMode))
		{
			if (n < 0)
				return false;
			if (n > 48)
				n = 48;
		}
		else
			n = n.Clamp(0, 48);

		if (n != SelectedWidgetIndex)
			ChangeFocusTo(Widgets[n]);

		return null;
	}

	public override bool ClipboardPaste(ClipboardPasteEvent cptr)
	{
		var data = cptr.Clipboard;

		if (data == null)
			return false;

		bool result = Palettes.UserDefined.SetFromString(Encoding.ASCII.GetString(data));

		if (!result)
		{
			Status.FlashText("Bad character or wrong length");
			return false;
		}

		VGAMem.CurrentPalette = Palettes.UserDefined;
		VGAMem.CurrentPalette.Apply();

		_selectedPalette = VGAMem.CurrentPalette.Index;

		UpdateThumbBars();

		Status.Flags |= StatusFlags.NeedUpdate;

		Status.FlashText("Palette pasted");

		return true;
	}

	/* --------------------------------------------------------------------- */

	void UpdateThumbBars()
	{
		for (int n = 0; n < 16; n++)
		{
			/* palettes[current_palette_index].colors[n] ?
			 * or current_palette[n] ? */

			var colour = VGAMem.CurrentPalette[n];

			(thumbBarRed[n].Value, thumbBarGreen[n].Value, thumbBarBlue[n].Value)
			 = (colour.Red, colour.Green, colour.Blue);
		}
	}

	/* --------------------------------------------------------------------- */

	void CopyPaletteToClipboard(Palette palette)
	{
		Clippy.Select(buttonCopy, palette.ToString());
		Clippy.Yank();
	}

	void CopyCurrentToClipboard()
	{
		CopyPaletteToClipboard(VGAMem.CurrentPalette);
	}

	void PasteFromClipboard()
	{
		Clippy.Paste(ClippySource.Buffer);
	}

	/* --------------------------------------------------------------------- */

	/* TODO | update_palette should only change the palette index for the color that's being changed, not all
		TODO | of them. also, it should call ccache_destroy_color(n) instead of wiping out the whole character
		TODO | cache whenever a color value is changed. */

	void UpdatePalette()
	{
		int n;

		_selectedPalette = 0;

		VGAMem.CurrentPalette = Palettes.UserDefined;

		for (n = 0; n < 16; n++)
		{
			VGAMem.CurrentPalette[n, 0] = unchecked((byte)thumbBarRed[n].Value);
			VGAMem.CurrentPalette[n, 1] = unchecked((byte)thumbBarRed[n].Value);
			VGAMem.CurrentPalette[n, 2] = unchecked((byte)thumbBarRed[n].Value);
		}

		VGAMem.CurrentPalette.Apply();

		Configuration.Save();

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	/* --------------------------------------------------------------------- */

	void otherPaletteList_Redraw()
	{
		bool focused = (SelectedActiveWidget == otherPaletteList);

		VGAMem.DrawFillCharacters(new Point(55, 26), new Point(76, 40), (VGAMem.DefaultForeground, 0));

		for (int n = 0; n < Palettes.Presets.Length; n++)
		{
			int fg = 6;
			int bg = 0;

			if (focused && n == _selectedPalette)
			{
				fg = 0;
				bg = 3;
			}
			else if (n == _selectedPalette)
				bg = 14;

			if (n == VGAMem.CurrentPalette.Index)
				VGAMem.DrawTextLen("*", 1, new Point(55, 26 + n), (fg, bg));
			else
				VGAMem.DrawTextLen(" ", 1, new Point(55, 26 + n), (fg, bg));

			VGAMem.DrawTextLen(Palettes.Presets[n].Name, 21, new Point(56, 26 + n), (fg, bg));
		}
	}

	static readonly int[] FocusOffsets = { 0, 1, 1, 2, 3, 3, 4, 4, 5, 6, 6, 7, 7, 8, 9, 9, 10, 10, 11, 12 };
	static Widget[]? FocusTabTargets = null;
	static Widget[]? FocusBackTabTargets = null;

	bool otherPaletteList_HandleKey(KeyEvent k)
	{
		if (FocusTabTargets == null)
		{
			FocusTabTargets =
				new Widget[]
				{
					thumbBarRed[3],
					thumbBarRed[3],
					thumbBarGreen[3],
					thumbBarGreen[3],
					thumbBarGreen[3],
					thumbBarBlue[3],
					thumbBarBlue[3],
					thumbBarRed[4],
					thumbBarGreen[4],
					thumbBarGreen[4],
					thumbBarBlue[4],
					thumbBarBlue[4],
					thumbBarRed[5],
					thumbBarGreen[5],
					thumbBarGreen[5],
					thumbBarBlue[5],
					thumbBarBlue[5],
					thumbBarRed[6],
					thumbBarGreen[6],
				};
		}

		if (FocusBackTabTargets == null)
		{
			FocusBackTabTargets =
				new Widget[]
				{
					thumbBarRed[10],
					thumbBarRed[10],
					thumbBarGreen[10],
					thumbBarGreen[10],
					thumbBarGreen[10],
					thumbBarBlue[10],
					thumbBarBlue[10],
					thumbBarRed[11],
					thumbBarGreen[11],
					thumbBarGreen[11],
					thumbBarBlue[11],
					thumbBarBlue[11],
					thumbBarRed[12],
					thumbBarGreen[12],
					thumbBarGreen[12],
					thumbBarBlue[12],
					thumbBarBlue[12],
					thumbBarRed[13],
					thumbBarGreen[13],
				};
		}

		int newPalette = _selectedPalette;

		bool loadSelectedPalette = false;

		if (k.Mouse == MouseState.DoubleClick)
		{
			if (k.State == KeyState.Press)
				return false;
			if (k.MousePosition.X < 55 || k.MousePosition.Y < 26 || k.MousePosition.Y > 40 || k.MousePosition.X > 76) return false;
			newPalette = k.MousePosition.Y - 26;
			loadSelectedPalette = true;
		}
		else if (k.Mouse == MouseState.Click)
		{
			if (k.State == KeyState.Press)
				return false;
			if (k.MousePosition.X < 55 || k.MousePosition.Y < 26 || k.MousePosition.Y > 40 || k.MousePosition.X > 76) return false;
			newPalette = k.MousePosition.Y - 26;
			if (newPalette == _selectedPalette)
				loadSelectedPalette = true;
		}
		else
		{
			if (k.State == KeyState.Release)
				return false;
			if (k.Mouse == MouseState.ScrollUp)
				newPalette -= Constants.MouseScrollLines;
			else if (k.Mouse == MouseState.ScrollDown)
				newPalette += Constants.MouseScrollLines;
		}

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (--newPalette < 0)
				{
					ChangeFocusTo(thumbBarBlue[15]);
					return true;
				}
				break;
			case KeySym.Down:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				// newPalette++;
				if (++newPalette >= Palettes.Presets.Length)
				{
					ChangeFocusTo(buttonCopy!);
					return true;
				}
				break;
			case KeySym.Home:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newPalette = 0;
				break;
			case KeySym.PageUp:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (newPalette == 0)
				{
					ChangeFocusTo(thumbBarRed[15]);
					return true;
				}
				newPalette -= 16;
				break;
			case KeySym.End:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newPalette = Palettes.Presets.Length - 1;
				break;
			case KeySym.PageDown:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newPalette += 16;
				break;
			case KeySym.Return:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				// if (_selectedPalette == -1) return true;
				Palettes.Presets[_selectedPalette].Apply();
				UpdateThumbBars();
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.Right:
			case KeySym.Tab:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					ChangeFocusTo(FocusBackTabTargets[_selectedPalette + 1]);
					return true;
				}
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				ChangeFocusTo(FocusTabTargets[_selectedPalette + 1]);
				return true;
			case KeySym.Left:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				ChangeFocusTo(FocusBackTabTargets[_selectedPalette + 1]);
				return true;
			case KeySym.c:
				/* pasting is handled by the page */
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					CopyPaletteToClipboard(Palettes.Presets[_selectedPalette]);
					return true;
				}
				return false;
			default:
				if (k.Mouse == MouseState.None)
					return false;
				break;
		}

		newPalette = newPalette.Clamp(0, Palettes.Presets.Length - 1);

		if (newPalette != _selectedPalette || loadSelectedPalette)
		{
			_selectedPalette = newPalette;

			if (loadSelectedPalette)
			{
				VGAMem.CurrentPalette = Palettes.Presets[_selectedPalette];
				VGAMem.CurrentPalette.Apply();
				UpdateThumbBars();
			}

			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return true;
	}

	void buttonCopy_Clicked()
	{
		CopyCurrentToClipboard();
	}

	void buttonPaste_Clicked()
	{
		PasteFromClipboard();
	}
}
