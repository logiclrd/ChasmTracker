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
	ListBoxWidget? listBoxPaletteList;
	ButtonWidget? buttonCopy;
	ButtonWidget? buttonPaste;

	int SelectedPalette
	{
		get => listBoxPaletteList!.Focus;
		set => listBoxPaletteList!.Focus = value;
	}

	public PaletteEditorPage()
		: base(PageNumbers.PaletteEditor, "Palette Configuration (Ctrl-F12)", HelpTexts.Palettes)
	{
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

		listBoxPaletteList = new ListBoxWidget(new Point(56, 26), new Size(20, 15));

		listBoxPaletteList.GetSize += listBoxPaletteList_GetSize;
		listBoxPaletteList.GetToggled += listBoxPaletteList_GetToggled;
		listBoxPaletteList.GetName += listBoxPaletteList_GetName;
		listBoxPaletteList.Activated += listBoxPaletteList_Activated;

		buttonCopy = new ButtonWidget(new Point(55, 43), 20, "Copy To Clipboard", 3);
		buttonCopy.Clicked += buttonCopy_Clicked;

		buttonPaste = new ButtonWidget(new Point(55, 46), 20, "Paste From Clipboard", 1);
		buttonPaste.Clicked += buttonPaste_Clicked;

		for (int n = 0; n < 16; n++)
		{
			if (n >= 9 && n < 13)
			{
				thumbBarRed[n].Next.Tab = listBoxPaletteList;
				thumbBarGreen[n].Next.Tab = listBoxPaletteList;
				thumbBarBlue[n].Next.Tab = listBoxPaletteList;
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

		listBoxPaletteList.Next.Tab = thumbBarRed[3];
		listBoxPaletteList.Next.BackTab = thumbBarRed[10];

		for (int i = 2; i < 6; i++)
		{
			thumbBarRed[i].Next.BackTab = listBoxPaletteList;
			thumbBarGreen[i].Next.BackTab = listBoxPaletteList;
			thumbBarBlue[i].Next.BackTab = listBoxPaletteList;
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

		AddWidget(listBoxPaletteList);
		AddWidget(buttonCopy);
		AddWidget(buttonPaste);

		SelectedPalette = VGAMem.CurrentPalette.Index;
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

		SelectedPalette = VGAMem.CurrentPalette.Index;

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

		SelectedPalette = 0;

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

	int listBoxPaletteList_GetSize() => Palettes.Presets.Length;
	bool listBoxPaletteList_GetToggled(int n) => n == SelectedPalette;
	string listBoxPaletteList_GetName(int n) => Palettes.Presets[n].Name;

	void listBoxPaletteList_Activated()
	{
		VGAMem.CurrentPalette = Palettes.Presets[SelectedPalette];
		VGAMem.CurrentPalette.Apply();
		UpdateThumbBars();
		Status.Flags |= StatusFlags.NeedUpdate;
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
