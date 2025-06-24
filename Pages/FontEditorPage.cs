using System;
using System.IO;

namespace ChasmTracker;

using ChasmTracker.Configurations;
using ChasmTracker.FileSystem;
using ChasmTracker.Pages;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class FontEditorPage : Page
{
	FontEditorListMode _fontListMode;
	FontEditorItem _selectedItem;
	byte _currentCharacter;
	Point _editPosition;
	int _impulseTrackerFontMapPosition;
	byte[] _clipboard = new byte[8];
	FileList _fontList;
	int _fontListTopFont = 0, _fontListCurFont = 0;

	public PageNumbers ReturnPageNumber;

	public FontEditorPage()
		: base(PageNumbers.FontEditor, "", HelpTexts.Global)
	{
		_fontList = new FileList(); // TODO
	}

	/* --------------------------------------------------------------------- */
	/* statics & local constants
	note: x/y are for the top left corner of the frame, but w/h define the size of its *contents* */

	static readonly Rect EditBoxBounds = new Rect(0, 0, 9, 11);
	static readonly Rect CharacterMapBounds = new Rect(17, 0, 16, 16);
	static readonly Rect ImpulseTrackerFontMapBounds = new Rect(41, 0, 16, 15);
	static readonly Point FontListPosition = new Point(65, 0);

	const int FontListVisibleFonts = 22; /* this should be called FontListHeight... */

	static readonly Point HelpTextPosition = new Point(0, 31);

	/* don't randomly mess with these for obvious reasons */
	int InnerX(Rect bounds) => bounds.TopLeft.X + 3;
	int InnerY(Rect bounds) => bounds.TopLeft.Y + 4;

	static readonly Size FrameBottomRight = new Size(3, 3);

	bool Within(int n, int l, int u) => (n >= l) && (n < u);

	bool PointIn(Point pt, Rect bounds)
		=> Within(pt.X, InnerX(bounds), InnerX(bounds) + bounds.Size.Width)
		&& Within(pt.Y, InnerY(bounds), InnerY(bounds) + bounds.Size.Height);

	bool PointInFrame(Point pt, Rect bounds)
		=> Within(pt.X, bounds.TopLeft.X, InnerX(bounds) + bounds.Size.Width + FrameBottomRight.Width)
		&& Within(pt.Y, bounds.TopLeft.Y, InnerY(bounds) + bounds.Size.Height + FrameBottomRight.Height);

	/* --------------------------------------------------------------------- */

	const byte ___ = (byte)' ';
	const byte _A_ = (byte)'A';
	const byte _B_ = (byte)'B';
	const byte _C_ = (byte)'C';
	const byte _D_ = (byte)'D';
	const byte _E_ = (byte)'E';
	const byte _F_ = (byte)'F';
	const byte _G_ = (byte)'G';
	const byte _0_ = (byte)'0';
	const byte _1_ = (byte)'1';
	const byte _2_ = (byte)'2';
	const byte _3_ = (byte)'3';
	const byte _4_ = (byte)'4';
	const byte _5_ = (byte)'5';
	const byte _6_ = (byte)'6';
	const byte _7_ = (byte)'7';
	const byte _8_ = (byte)'8';
	const byte _9_ = (byte)'9';
	const byte _h_ = (byte)'-'; // hyphen
	const byte _s_ = (byte)'#'; // sharp
	const byte _o_ = (byte)'^'; // note off

	static readonly byte[] ImpulseTrackerFontMapCharacters =
		new byte[]
		{
			128, 129, 130, ___, 128, 129, 141, ___, 142, 143, 144, ___, 168, _C_, _h_, _0_,
			131, ___, 132, ___, 131, ___, 132, ___, 145, ___, 146, ___, 168, _D_, _h_, _1_,
			133, 134, 135, ___, 140, 134, 135, ___, 147, 148, 149, ___, 168, _E_, _h_, _2_,
			___, ___, ___, ___, ___, 139, 134, 138, 153, 148, 152, ___, 168, _F_, _h_, _3_,
			174, ___, ___, ___, 155, 132, ___, 131, 146, ___, 145, ___, 168, _G_, _h_, _4_,
			175, ___, ___, ___, 156, 137, 129, 136, 151, 143, 150, ___, 168, _A_, _h_, _5_,
			176, ___, ___, ___, 157, ___, 184, 184, 191, _6_, _4_, 192, 168, _B_, _h_, _6_,
			176, 177, ___, ___, 158, 163, 250, 250, 250, 250, 250, ___, 168, _C_, _s_, _7_,
			176, 178, ___, ___, 159, 164, ___, ___, ___, 185, 186, ___, 168, _D_, _s_, _8_,
			176, 179, 180, ___, 160, 165, ___, ___, ___, 189, 190, ___, 168, _E_, _s_, _9_,
			176, 179, 181, ___, 161, 166, ___, ___, ___, 187, 188, ___, 168, _F_, _s_, _1_,
			176, 179, 182, ___, 162, 167, 126, 126, 126, ___, ___, ___, 168, _G_, _s_, _2_,
			154, 154, 154, 154, ___, ___, 205, 205, 205, ___, 183, ___, 168, _A_, _s_, _3_,
			169, 170, 171, 172, ___, ___, _o_, _o_, _o_, ___, 173, ___, 168, _B_, _s_, _4_,
			193, 194, 195, 196, 197, 198, 199, 200, 201, ___, ___, ___, ___, ___, ___, ___,
		};

	void Reposition()
	{
		if (_fontListCurFont < 0)
			_fontListCurFont = 0; /* weird! */
		if (_fontListCurFont < _fontListTopFont)
			_fontListCurFont = _fontListTopFont;
		else if (_fontListCurFont > _fontListTopFont + (FontListVisibleFonts - 1))
			_fontListTopFont = _fontListCurFont - (FontListVisibleFonts - 1);
	}

	bool FontGrep(FileReference f)
	{
		if (f.SortOrder == -100)
			return true; /* this is our font.cfg, at the top of the list */
		if (f.Type.HasFlag(FileTypes.BrowsableMask))
			return false; /* we don't care about directories and stuff */

		string ext = Path.GetExtension(f.BaseName);

		return
			ext.Equals(".itf", StringComparison.InvariantCultureIgnoreCase) ||
			ext.Equals(".fnt", StringComparison.InvariantCultureIgnoreCase);
	}

	void LoadFontList()
	{
		_fontListTopFont = _fontListCurFont = 0;

		string fontDir = Path.Combine(Configuration.ConfigurationDirectoryDotSchism, "fonts");

		Directory.CreateDirectory(fontDir);

		string p = Path.Combine(fontDir, "font.cfg");

		_fontList.AddFile(p, -100); /* put it on top */

		try
		{
			DirectoryScanner.Populate(fontDir, _fontList);
		}
		catch (Exception e)
		{
			Log.AppendException(e);
		}

		var filterOperation = _fontList.BeginFilter(FontGrep, _currentFont);

		while (filterOperation.TakeStep())
			;

		Reposition();

		/* p is freed by dmoz_free */

	}

	public override void DrawFull()
	{
		page->draw_full = draw_screen;
		base.DrawFull();
	}

	public override bool PreHandleKey(KeyEvent k)
	{
		switch (k.Sym)
		{
			case KeySym.r:
			case KeySym.l:
			case KeySym.s:
			case KeySym.c:
			case KeySym.p:
			case KeySym.m:
			case KeySym.z:
			case KeySym.v:
			case KeySym.h:
			case KeySym.i:
			case KeySym.q:
			case KeySym.w:
			case KeySym.F1:
			case KeySym.F2:
			case KeySym.F3:
			case KeySym.F4:
			case KeySym.F5:
			case KeySym.F6:
			case KeySym.F7:
			case KeySym.F8:
			case KeySym.F9:
			case KeySym.F10:
			case KeySym.F11:
			case KeySym.F12:
				return HandleKey(k);
			case KeySym.Return:
				if (Status.DialogType.HasFlag(Dialogs.DialogTypes.Menu | Dialogs.DialogTypes.Box))
					return false;
				if (_selectedItem == FontEditorItem.FontList)
				{
					HandleKeyFontList(k);
					return true;
				}
				break;
		}

		return false;
	}

	/* --------------------------------------------------------------------- */

	void HandleMouseEditBox(KeyEvent k)
	{
		int ci = _currentCharacter << 3;

		int xRel = k.MousePosition.X - InnerX(EditBoxPosition.X);
		int yRel = k.MousePosition.Y - InnerY(EditBoxPosition.Y);

		if (xrel > 0 && yrel > 2)
		{
			edit_x = xrel - 1;
			edit_y = yrel - 3;
			switch (k->mouse_button)
			{
				case MOUSE_BUTTON_LEFT: /* set */
					ptr[edit_y] |= (128 >> edit_x);
					break;
				case MOUSE_BUTTON_MIDDLE: /* invert */
					if (k->state == KEY_RELEASE)
						return;
					ptr[edit_y] ^= (128 >> edit_x);
					break;
				case MOUSE_BUTTON_RIGHT: /* clear */
					ptr[edit_y] &= ~(128 >> edit_x);
					break;
			}
		}
		else if (xrel == 0 && yrel == 2)
		{
			/* clicking at the origin modifies the entire character */
			switch (k->mouse_button)
			{
				case MOUSE_BUTTON_LEFT: /* set */
					for (n = 0; n < 8; n++)
						ptr[n] = 255;
					break;
				case MOUSE_BUTTON_MIDDLE: /* invert */
					if (k->state == KEY_RELEASE)
						return;
					for (n = 0; n < 8; n++)
						ptr[n] ^= 255;
					break;
				case MOUSE_BUTTON_RIGHT: /* clear */
					for (n = 0; n < 8; n++)
						ptr[n] = 0;
					break;
			}
		}
		else if (xrel == 0 && yrel > 2)
		{
			edit_y = yrel - 3;
			switch (k->mouse_button)
			{
				case MOUSE_BUTTON_LEFT: /* set */
					ptr[edit_y] = 255;
					break;
				case MOUSE_BUTTON_MIDDLE: /* invert */
					if (k->state == KEY_RELEASE)
						return;
					ptr[edit_y] ^= 255;
					break;
				case MOUSE_BUTTON_RIGHT: /* clear */
					ptr[edit_y] = 0;
					break;
			}
		}
		else if (yrel == 2 && xrel > 0)
		{
			edit_x = xrel - 1;
			switch (k->mouse_button)
			{
				case MOUSE_BUTTON_LEFT: /* set */
					for (n = 0; n < 8; n++)
						ptr[n] |= (128 >> edit_x);
					break;
				case MOUSE_BUTTON_MIDDLE: /* invert */
					if (k->state == KEY_RELEASE)
						return;
					for (n = 0; n < 8; n++)
						ptr[n] ^= (128 >> edit_x);
					break;
				case MOUSE_BUTTON_RIGHT: /* clear */
					for (n = 0; n < 8; n++)
						ptr[n] &= ~(128 >> edit_x);
					break;
			}
		}
	}

	void HandleMouseCharacterMap(KeyEvent k)
	{
		if (k.Mouse == MouseState.None)
			return;

		int xRel = k.MousePosition.X - InnerX(CharacterMapPosition.X);
		int yRel = k.MousePosition.Y - InnerY(CharacterMapPosition.Y);

		_currentCharacter = (byte)(16 * yRel + xRel);
	}

	void HandleMouseImpulseTrackerFontMap(KeyEvent k)
	{
		if (k.Mouse == MouseState.None)
			return;

		int xRel = k.MousePosition.X - InnerX(ImpulseTrackerFontMapPosition.X);
		int yRel = k.MousePosition.Y - InnerY(ImpulseTrackerFontMapPosition.Y);

		_impulseTrackerFontMapPosition = 16 * yRel + xRel;
		_currentCharacter = ImpulseTrackerFontMapCharacters[_impulseTrackerFontMapPosition];
	}

	void HandleMouse(KeyEvent k)
	{
		if (PointInFrame(k.MousePosition, FontEditorItem.EditBox))
		{
			_selectedItem = FontEditorItem.EditBox;
			if (PointIn(k.MousePosition, FontEditorItem.EditBox))
				HandleMouseEditBox(k);
		}
		else if (PointInFrame(k.MousePosition, FontEditorItem.CharacterMap))
		{
			_selectedItem = FontEditorItem.CharacterMap;
			if (PointIn(k.MousePosition, FontEditorItem.CharacterMap))
				HandleMouseCharacterMap(k);
		}
		else if (PointInFrame(k.MousePosition, FontEditorItem.ImpulseTrackerFontMap))
		{
			_selectedItem = FontEditorItem.ImpulseTrackerFontMap;
			if (PointIn(k.MousePosition, FontEditorItem.ImpulseTrackerFontMap))
				HandleMouseImpulseTrackerFontMap(k);
		}
		else
		{
			//Console.WriteLine("stray click\n");
			return;
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public override bool HandleKey(KeyEvent k)
	{
		int ci = _currentCharacter << 3;

		if (k.Mouse == MouseState.ScrollUp || k.Mouse == MouseState.ScrollDown)
			return false; /* err... */

		if (k.Mouse == MouseState.Click)
		{
			HandleMouse(k);
			return true;
		}

		/* kp is special */
		switch (k.Sym)
		{
			case KeySym.KP_0:
				if (k.State == KeyState.Release)
					return true;
				k.Sym += 10;
				/* fall through */
				goto case KeySym.KP_1;
			case KeySym.KP_1:
			case KeySym.KP_2:
			case KeySym.KP_3:
			case KeySym.KP_4:
			case KeySym.KP_5:
			case KeySym.KP_6:
			case KeySym.KP_7:
			case KeySym.KP_8:
			case KeySym.KP_9:
				if (k.State == KeyState.Release)
					return true;
				int n = k.Sym - KeySym.KP_1;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					n += 10;
				Palette.LoadPreset(n);
				Palette.Apply();
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
		}

		switch (k.Sym)
		{
			case KeySym._0:
				if (k.State == KeyState.Release)
					return true;
				k.Sym += 10;
				/* fall through */
				goto case KeySym._1;
			case KeySym._1:
			case KeySym._2:
			case KeySym._3:
			case KeySym._4:
			case KeySym._5:
			case KeySym._6:
			case KeySym._7:
			case KeySym._8:
			case KeySym._9:
				if (k.State == KeyState.Release)
					return true;
				int n = k.Sym - KeySym._1;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					n += 10;
				Palette.LoadPreset(n);
				Palette.Apply();
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.F2:
				if (k.State == KeyState.Release)
					return true;
				_selectedItem = FontEditorItem.EditBox;
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.F3:
				if (k.State == KeyState.Release)
					return true;
				_selectedItem = FontEditorItem.CharacterMap;
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.F4:
				if (k.State == KeyState.Release)
					return true;
				_selectedItem = FontEditorItem.ImpulseTrackerFontMap;
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.Tab:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					if (_selectedItem == 0)
						_selectedItem = _fontListMode == FontEditorListMode.Off ? FontEditorItem.ImpulseTrackerFontMap : FontEditorItem.FontList;
					else
						_selectedItem--;
				}
				else
				{
					_selectedItem = (FontEditorItem)(((int)_selectedItem + 1) % (_fontListMode == FontEditorListMode.Off ? 3 : 4));
				}
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.c:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					Array.Copy(Font.Data, ci, _clipboard, 0, 8);
					return true;
				}
				break;
			case KeySym.p:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					Array.Copy(_clipboard, 0, Font.Data, ci, 8);
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}

				break;
			case KeySym.m:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					Video.SetMouseCursorState((Video.Mouse.Visible != MouseCursorState.Disabled) ? MouseCursorState.Disabled : MouseCursorState.Emulated);
					return true;
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					for (n = 0; n < 8; n++)
						Font.Data[ci + n] |= _clipboard[n];
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}

				break;
			case KeySym.z:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					for (int i = 0; i < 8; i++)
						Font.Data[ci + i] = 0;
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}

				break;
			case KeySym.h:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					for (n = 0; n < 8; n++)
					{
						byte r = Font.Data[ci + n];
						r = unchecked((byte)(((r >> 1) & 0x55) | ((r << 1) & 0xaa)));
						r = unchecked((byte)(((r >> 2) & 0x33) | ((r << 2) & 0xcc)));
						r = unchecked((byte)(((r >> 4) & 0x0f) | ((r << 4) & 0xf0)));
						Font.Data[ci + n] = r;
					}

					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}

				break;
			case KeySym.v:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					for (n = 0; n < 4; n++)
					{
						byte r = Font.Data[ci + n];
						Font.Data[ci + n] = Font.Data[ci + 7 - n];
						Font.Data[ci + 7 - n] = r;
					}
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}

				break;
			case KeySym.i:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					for (n = 0; n < 8; n++)
						Font.Data[ci + n] ^= 255;
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}

				break;

			/* ----------------------------------------------------- */

			case KeySym.l:
			case KeySym.r:
				if (k.State == KeyState.Release)
					return true;
				if (!k.Modifiers.HasAnyFlag(KeyMod.Control)) break;
				/* fall through */
				goto case KeySym.F9;
			case KeySym.F9:
				if (k.State == KeyState.Release)
					return true;
				LoadFontList();
				_fontListMode = FontEditorListMode.Load;
				_selectedItem = FontEditorItem.FontList;
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.s:
				if (k.State == KeyState.Release)
					return true;
				if (!(k.Modifiers.HasAnyFlag(KeyMod.Control))) break;
				/* fall through */
				goto case KeySym.F10;
			case KeySym.F10:
				/* a bit weird, but this ensures that font.cfg
				 * is always the default font to save to, but
				 * without the annoyance of moving the cursor
				 * back to it every time f10 is pressed. */
				if (_fontListMode != FontEditorListMode.Save)
				{
					_fontListCurFont = _fontListTopFont = 0;
					LoadFontList();
					_fontListMode = FontEditorListMode.Save;
				}

				_selectedItem = FontEditorItem.FontList;
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.Backspace:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
					Font.ResetBIOS();
				else if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					Font.ResetCharacter(_currentCharacter);
				else
					Font.ResetUpper();
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.Return:
				return false;
			case KeySym.q:
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
					return false;
				if (k.State == KeyState.Release)
					return true;
				break;
			default:
				if (k.State == KeyState.Release)
					return true;
				break;
		}

		switch (_selectedItem)
		{
			case FontEditorItem.EditBox:
				HandleKeyEditBox(k);
				break;
			case FontEditorItem.CharacterMap:
				HandleKeyCharacterMap(k);
				break;
			case FontEditorItem.ImpulseTrackerFontMap:
				HandleKeyImpulseTrackerMap(k);
				break;
			case FontEditorItem.FontList:
				HandleKeyFontList(k);
				break;
		}

		return true;
	}
}
