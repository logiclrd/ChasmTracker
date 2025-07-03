using System;
using System.IO;

namespace ChasmTracker;

using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.FileSystem;
using ChasmTracker.Input;
using ChasmTracker.Pages;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class FontEditorPage : Page
{
	OtherWidget otherSink;

	FontEditorListMode _fontListMode;
	FontEditorItem _selectedItem;
	byte _currentCharacter;
	Point _editPosition;
	int _impulseTrackerFontMapPosition;
	byte[] _clipboard = new byte[8];
	FileList _fontList;
	int _fontListTopFont = 0;
	Shared<int> _fontListCurFont = new Shared<int>();

	public PageNumbers ReturnPageNumber;

	public FontEditorPage()
		: base(PageNumbers.FontEditor, "", HelpTexts.Global)
	{
		_fontList = new FileList(); // TODO

		otherSink = new OtherWidget();


	}

	/* --------------------------------------------------------------------- */
	/* statics & local constants
	note: x/y are for the top left corner of the frame, but w/h define the size of its *contents* */

	static readonly Point FontListPosition = new Point(65, 0);

	const int FontListVisibleFonts = 22; /* this should be called FontListHeight... */

	static readonly Rect EditBoxBounds = new Rect(0, 0, 9, 11);
	static readonly Rect CharacterMapBounds = new Rect(17, 0, 16, 16);
	static readonly Rect ImpulseTrackerFontMapBounds = new Rect(41, 0, 16, 15);
	static readonly Rect FontListBounds = new Rect(FontListPosition, new Size(9, FontListVisibleFonts));

	static readonly Point HelpTextPosition = new Point(0, 31);

	static readonly Rect HelpTextBounds = new Rect(HelpTextPosition, new Size(74, 12));

	/* don't randomly mess with these for obvious reasons */
	static int InnerX(Point pt) => pt.X + 3;
	static int InnerY(Point pt) => pt.Y + 4;

	static int InnerX(Rect bounds) => InnerX(bounds.TopLeft);
	static int InnerY(Rect bounds) => InnerX(bounds.TopLeft);

	static Point Inner(Point p) => p.Advance(3, 4);
	static Point Inner(Rect bounds) => Inner(bounds.TopLeft);

	static readonly Size FrameBottomRight = new Size(3, 3);

	static bool Within(int n, int l, int u) => (n >= l) && (n < u);

	static bool PointIn(Point pt, Rect bounds)
		=> Within(pt.X, InnerX(bounds), InnerX(bounds) + bounds.Size.Width)
		&& Within(pt.Y, InnerY(bounds), InnerY(bounds) + bounds.Size.Height);

	static bool PointInFrame(Point pt, Rect bounds)
		=> Within(pt.X, bounds.TopLeft.X, InnerX(bounds) + bounds.Size.Width + FrameBottomRight.Width)
		&& Within(pt.Y, bounds.TopLeft.Y, InnerY(bounds) + bounds.Size.Height + FrameBottomRight.Height);

	static bool PointIn(Point pt, FontEditorItem item)
	{
		return PointIn(pt, GetItemBounds(item));
	}

	static bool PointInFrame(Point pt, FontEditorItem item)
	{
		return PointInFrame(pt, GetItemBounds(item));
	}

	static ref readonly Rect GetItemBounds(FontEditorItem item)
	{
		switch (item)
		{
			case FontEditorItem.EditBox: return ref EditBoxBounds;
			case FontEditorItem.CharacterMap: return ref CharacterMapBounds;
			case FontEditorItem.ImpulseTrackerFontMap: return ref ImpulseTrackerFontMapBounds;
			case FontEditorItem.FontList: return ref FontListBounds;
			default:
				throw new Exception("Internal error: Unrecognized font editor item " + item);
		}
	}

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

	const char A8 = '\xA8';

	static readonly string HelpTextGeneral =
$@"Tab         Next box   {A8} Alt-C  Copy
Shift-Tab   Prev. box  {A8} Alt-P  Paste
F2-F4       Switch box {A8} Alt-M  Mix paste
{"\x18\x19\x1a\x1b"}        Dump core  {A8} Alt-Z  Clear
Ctrl-S/F10  Save font  {A8} Alt-H  Flip horiz
Ctrl-R/F9   Load font  {A8} Alt-V  Flip vert
Backspace   Reset font {A8} Alt-I  Invert
Ctrl-Bksp   BIOS font  {A8} Alt-Bk Reset text
                       {A8} 0-9    Palette
Ctrl-Q      Exit       {A8}  (+10 with shift)
";

	static readonly string HelpTextEditBox =
$@"Space       Plot/clear point
Ins/Del     Fill/clear horiz.
...w/Shift  Fill/clear vert.

+/-         Next/prev. char.
PgUp/PgDn   Next/previous row
Home/End    Top/bottom corner

Shift-{"\x18\x19\x1a\x1b"}  Shift character
[/]         Rotate 90{'\xF8'}
";

	static readonly string HelpTextCharacterMap =
$@"Home/End    First/last char.
";

	static readonly string HelpTextFontList =
$@"Home/End    First/last font
Enter       Load/save file
Escape      Hide font list

{"\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a"}

Remember to save as font.cfg
to change the default font!
";

	void FontListReposition()
	{
		if (_fontListCurFont < 0)
			_fontListCurFont.Value = 0; /* weird! */
		if (_fontListCurFont < _fontListTopFont)
			_fontListCurFont.Value = _fontListTopFont;
		else if (_fontListCurFont > _fontListTopFont + (FontListVisibleFonts - 1))
			_fontListTopFont = _fontListCurFont - (FontListVisibleFonts - 1);
	}

	bool FontGrep(FileReference f)
	{
		if (f.SortOrder == -100)
			return true; /* this is our font.cfg, at the top of the list */
		if (f.Type.HasFlag(FileSystem.FileTypes.BrowsableMask))
			return false; /* we don't care about directories and stuff */

		string ext = Path.GetExtension(f.BaseName);

		return
			ext.Equals(".itf", StringComparison.InvariantCultureIgnoreCase) ||
			ext.Equals(".fnt", StringComparison.InvariantCultureIgnoreCase);
	}

	void LoadFontList()
	{
		_fontListTopFont = 0;
		_fontListCurFont.Value = 0;

		string fontDir = Path.Combine(Configuration.Directories.DotSchism, "fonts");

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

		var filterOperation = _fontList.BeginFilter(FontGrep, _fontListCurFont);

		filterOperation.RunToCompletion();

		FontListReposition();
	}

	public override void DrawFull()
	{
		VGAMem.DrawFillCharacters(new Point(0,0), new Point(79,49), (VGAMem.DefaultForeground,0));
		DrawFrame("Edit Box", EditBoxBounds, _selectedItem == FontEditorItem.EditBox);
		DrawEditBox();

		DrawFrame("Current Font", CharacterMapBounds, _selectedItem == FontEditorItem.CharacterMap);
		DrawCharacterMap();

		DrawFrame("Preview", ImpulseTrackerFontMapBounds, _selectedItem == FontEditorItem.ImpulseTrackerFontMap);
		DrawImpulseTrackerFontMap();

		switch (_fontListMode)
		{
			case FontEditorListMode.Load:
			case FontEditorListMode.Save:
				DrawFrame(
					(_fontListMode == FontEditorListMode.Load) ? "Load/Browse" : "Save As...",
					FontListBounds, _selectedItem == FontEditorItem.FontList);
				DrawFontList();
				break;
			default: /* Off? (I sure hope so!) */
				break;
		}

		DrawFrame("Quick Help", HelpTextBounds, ActiveState.Disabled);
		DrawHelpText();

		DrawTime();
	}

	public override bool? PreHandleKey(KeyEvent k)
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

	void HandleKeyEditBox(KeyEvent k)
	{
		int ci = _currentCharacter << 3;
		var ptr = Font.Data.Slice(ci, 8);

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					var s = ptr[0];
					for (int n = 0; n < 7; n++)
						ptr[n] = ptr[n + 1];
					ptr[7] = s;
				}
				else
				{
					if (--_editPosition.Y < 0)
						_editPosition.Y = 7;
				}
				break;
			case KeySym.Down:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					var s = ptr[7];
					for (int n = 7; n > 0; n--)
						ptr[n] = ptr[n - 1];
					ptr[0] = s;
				}
				else
					_editPosition.Y = (_editPosition.Y + 1) % 8;
				break;
			case KeySym.Left:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					for (int n = 0; n < 8; n++)
						ptr[n] = unchecked((byte)((ptr[n] >> 7) | (ptr[n] << 1)));
				}
				else
				{
					if (--_editPosition.X < 0)
						_editPosition.X = 7;
				}
				break;
			case KeySym.Right:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					for (int n = 0; n < 8; n++)
						ptr[n] = unchecked((byte)((ptr[n] << 7) | (ptr[n] >> 1)));
				} else {
					_editPosition.X = (_editPosition.X + 1) % 8;
				}
				break;
			case KeySym.Home:
				_editPosition.X = _editPosition.Y = 0;
				break;
			case KeySym.End:
				_editPosition.X = _editPosition.Y = 7;
				break;
			case KeySym.Space:
				ptr[_editPosition.Y] = unchecked((byte)(ptr[_editPosition.Y] ^ (128 >> _editPosition.X)));
				break;
			case KeySym.Insert:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					for (int n = 0; n < 8; n++)
						ptr[n] = unchecked((byte)(ptr[n] | (128 >> _editPosition.X)));
				}
				else
					ptr[_editPosition.Y] = 255;
				break;
			case KeySym.Delete:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					for (int n = 0; n < 8; n++)
						ptr[n] = unchecked((byte)(ptr[n] & ~(128 >> _editPosition.X)));
				}
				else
					ptr[_editPosition.Y] = 0;
				break;
			case KeySym.LeftBracket:
			case KeySym.RightBracket:
			{
				var tmp = new byte[8];

				switch (k.Sym)
				{
					case KeySym.LeftBracket:
						for (int n = 0; n < 8; n++)
							for (int bit = 0; bit < 8; bit++)
								if ((ptr[n] & (1 << bit)) != 0)
									tmp[bit] = unchecked((byte)(tmp[bit] | 1 << (7 - n)));
						break;
					case KeySym.RightBracket:
						for (int n = 0; n < 8; n++)
							for (int bit = 0; bit < 8; bit++)
								if ((ptr[n] & (1 << bit)) != 0)
									tmp[7 - bit] = unchecked((byte)(tmp[7 - bit] | 1 << n));
						break;
				}

				tmp.CopyTo(ptr);
				break;
			}
			case KeySym.Plus:
			case KeySym.Equals:
				_currentCharacter++;
				break;
			case KeySym.Minus:
			case KeySym.Underscore:
				_currentCharacter--;
				break;
			case KeySym.PageUp:
				_currentCharacter -= 16;
				break;
			case KeySym.PageDown:
				_currentCharacter += 16;
				break;
			default:
				return;
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void HandleKeyCharacterMap(KeyEvent k)
	{
		switch (k.Sym)
		{
			case KeySym.Up:
				_currentCharacter -= 16;
				break;
			case KeySym.Down:
				_currentCharacter += 16;
				break;
			case KeySym.Left:
				_currentCharacter = DecrWrapped(_currentCharacter);
				break;
			case KeySym.Right:
				_currentCharacter = IncrWrapped(_currentCharacter);
				break;
			case KeySym.Home:
				_currentCharacter = 0;
				break;
			case KeySym.End:
				_currentCharacter = 255;
				break;
			default:
				return;
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void HandleKeyImpulseTrackerMap(KeyEvent k)
	{
		switch (k.Sym)
		{
			case KeySym.Up:
				if (_impulseTrackerFontMapPosition < 0)
					_impulseTrackerFontMapPosition = 224;
				else
				{
					_impulseTrackerFontMapPosition -= 16;
					if (_impulseTrackerFontMapPosition < 0)
						_impulseTrackerFontMapPosition += 240;
				}
				_currentCharacter = ImpulseTrackerFontMapCharacters[_impulseTrackerFontMapPosition];
				break;
			case KeySym.Down:
				if (_impulseTrackerFontMapPosition < 0)
					_impulseTrackerFontMapPosition = 16;
				else
					_impulseTrackerFontMapPosition = (_impulseTrackerFontMapPosition + 16) % 240;
				_currentCharacter = ImpulseTrackerFontMapCharacters[_impulseTrackerFontMapPosition];
				break;
			case KeySym.Left:
				if (_impulseTrackerFontMapPosition < 0)
					_impulseTrackerFontMapPosition = 15;
				else
					_impulseTrackerFontMapPosition = DecrWrapped(_impulseTrackerFontMapPosition);
				_currentCharacter = ImpulseTrackerFontMapCharacters[_impulseTrackerFontMapPosition];
				break;
			case KeySym.Right:
				if (_impulseTrackerFontMapPosition < 0)
					_impulseTrackerFontMapPosition = 0;
				else
					_impulseTrackerFontMapPosition = IncrWrapped(_impulseTrackerFontMapPosition);
				_currentCharacter = ImpulseTrackerFontMapCharacters[_impulseTrackerFontMapPosition];
				break;
			case KeySym.Home:
				_currentCharacter = ImpulseTrackerFontMapCharacters[0];
				_impulseTrackerFontMapPosition = 0;
				break;
			case KeySym.End:
				_currentCharacter = ImpulseTrackerFontMapCharacters[239];
				_impulseTrackerFontMapPosition = 239;
				break;
			default:
				return;
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void HandleKeyFontList(KeyEvent k)
	{
		int newFont = _fontListCurFont;

		switch (k.Sym)
		{
			case KeySym.Home:
				newFont = 0;
				break;
			case KeySym.End:
				newFont = _fontList.NumFiles - 1;
				break;
			case KeySym.Up:
				newFont--;
				break;
			case KeySym.Down:
				newFont++;
				break;
			case KeySym.PageUp:
				newFont -= FontListVisibleFonts;
				break;
			case KeySym.PageDown:
				newFont += FontListVisibleFonts;
				break;
			case KeySym.Escape:
				_selectedItem = FontEditorItem.EditBox;
				_fontListMode = FontEditorListMode.Off;
				break;
			case KeySym.Return:
				if (k.State == KeyState.Press)
					return;

				switch (_fontListMode)
				{
					case FontEditorListMode.Load:
						if (_fontListCurFont < _fontList.NumFiles
						&& !Font.Load(_fontList[_fontListCurFont].BaseName))
							Font.Reset();
						break;
					case FontEditorListMode.Save:
						if (_fontListCurFont < _fontList.NumFiles)
						{
							string fontFileName = _fontList[_fontListCurFont].BaseName;

							if (!fontFileName.Equals("font.cfg", StringComparison.InvariantCultureIgnoreCase))
							{
								var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "Overwrite font file?");

								dialog.ActionYes =
									_ => DoSave();

								return;
							}

							DoSave();

							void DoSave()
							{
								if (Font.Save(fontFileName))
									_selectedItem = FontEditorItem.EditBox;
							}
						}

						_selectedItem = FontEditorItem.EditBox;
						/* _fontListMode = FontEditorListMode.Off; */
						break;
					default:
						/* should never happen */
						return;
				}

				break;
			default:
				return;
		}

		if (newFont != _fontListCurFont) {
			newFont = newFont.Clamp(0, _fontList.NumFiles - 1);

			if (newFont == _fontListCurFont)
				return;

			_fontListCurFont.Value = newFont;

			FontListReposition();
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	static byte IncrWrapped(int n)
	{
		return unchecked((byte)((n & 0xF0) | ((n + 1) & 0x0F)));
	}

	static byte DecrWrapped(int n)
	{
		return unchecked((byte)((n & 0xF0) | ((n - 1) & 0x0F)));
	}

	/* --------------------------------------------------------------------- */

	void HandleMouseEditBox(KeyEvent k)
	{
		int ci = _currentCharacter << 3;

		int xRel = k.MousePosition.X - InnerX(EditBoxBounds);
		int yRel = k.MousePosition.Y - InnerY(EditBoxBounds);

		var ptr = new Span<byte>(Font.Data, ci, 8);

		if (xRel > 0 && yRel > 2)
		{
			int editX = xRel - 1;
			int editY = yRel - 3;
			switch (k.MouseButton)
			{
				case MouseButton.Left: /* set */
					ptr[editY] |= (byte)(128 >> editX);
					break;
				case MouseButton.Middle: /* invert */
					if (k.State == KeyState.Release)
						return;
					ptr[editY] ^= (byte)(128 >> editX);
					break;
				case MouseButton.Right: /* clear */
					ptr[editY] &= (byte)~(128 >> editX);
					break;
			}
		}
		else if (xRel == 0 && yRel == 2)
		{
			/* clicking at the origin modifies the entire character */
			switch (k.MouseButton)
			{
				case MouseButton.Left: /* set */
					for (int n = 0; n < 8; n++)
						ptr[n] = 255;
					break;
				case MouseButton.Middle: /* invert */
					if (k.State == KeyState.Release)
						return;
					for (int n = 0; n < 8; n++)
						ptr[n] ^= 255;
					break;
				case MouseButton.Right: /* clear */
					for (int n = 0; n < 8; n++)
						ptr[n] = 0;
					break;
			}
		}
		else if (xRel == 0 && yRel > 2)
		{
			int editY = yRel - 3;
			switch (k.MouseButton)
			{
				case MouseButton.Left: /* set */
					ptr[editY] = 255;
					break;
				case MouseButton.Middle: /* invert */
					if (k.State == KeyState.Release)
						return;
					ptr[editY] ^= 255;
					break;
				case MouseButton.Right: /* clear */
					ptr[editY] = 0;
					break;
			}
		}
		else if (yRel == 2 && xRel > 0)
		{
			int editX = xRel - 1;
			switch (k.MouseButton)
			{
				case MouseButton.Left: /* set */
					for (int n = 0; n < 8; n++)
						ptr[n] |= (byte)(128 >> editX);
					break;
				case MouseButton.Middle: /* invert */
					if (k.State == KeyState.Release)
						return;
					for (int n = 0; n < 8; n++)
						ptr[n] ^= (byte)(128 >> editX);
					break;
				case MouseButton.Right: /* clear */
					for (int n = 0; n < 8; n++)
						ptr[n] &= (byte)~(128 >> editX);
					break;
			}
		}
	}

	void HandleMouseCharacterMap(KeyEvent k)
	{
		if (k.Mouse == MouseState.None)
			return;

		int xRel = k.MousePosition.X - InnerX(CharacterMapBounds);
		int yRel = k.MousePosition.Y - InnerY(CharacterMapBounds);

		_currentCharacter = (byte)(16 * yRel + xRel);
	}

	void HandleMouseImpulseTrackerFontMap(KeyEvent k)
	{
		if (k.Mouse == MouseState.None)
			return;

		int xRel = k.MousePosition.X - InnerX(ImpulseTrackerFontMapBounds);
		int yRel = k.MousePosition.Y - InnerY(ImpulseTrackerFontMapBounds);

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

	public override bool? HandleKey(KeyEvent k)
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
				VGAMem.CurrentPalette = Palettes.Presets[n];
				VGAMem.CurrentPalette.Apply();
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
				VGAMem.CurrentPalette = Palettes.Presets[n];
				VGAMem.CurrentPalette.Apply();
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
					_fontListCurFont.Value = _fontListTopFont = 0;
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

	enum ActiveState
	{
		Disabled = -1,
		Inactive = 0,
		Active = 1,
	}

	void DrawFrame(string name, Rect bounds, ActiveState active)
		=> DrawFrame(name, bounds.TopLeft, bounds.Size, active);

	void DrawFrame(string name, Rect bounds, bool active)
		=> DrawFrame(name, bounds.TopLeft, bounds.Size, active ? ActiveState.Active : ActiveState.Inactive);

	/* if this is nonzero, the screen will be redrawn. none of the functions
	 * except main should call draw_anything -- set this instead. */
	void DrawFrame(string name, Point position, Size innerSize, ActiveState active)
	{
		int len = name.Length;

		if (len > innerSize.Width + 2)
			len = innerSize.Width + 2;

		int c = Status.Flags.HasFlag(StatusFlags.InvertedPalette) ? 1 : 3;

		VGAMem.DrawBox(position.Advance(0, 1), position.Advance(innerSize).Advance(5, 6),
			BoxTypes.Thick | BoxTypes.Corner | BoxTypes.Outset);
		VGAMem.DrawBox(position.Advance(1, 2), position.Advance(innerSize).Advance(4, 5),
			BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawCharacter(128, position, (c, 2));
		for (int n = 0; n < len + 1; n++)
			VGAMem.DrawCharacter(129, position.Advance(n + 1), (c, 2));
		VGAMem.DrawCharacter(130, position.Advance(len + 1), (c, 2));
		VGAMem.DrawCharacter(131, position.Advance(0, 1), (c, 2));
		VGAMem.DrawCharacter(137, position.Advance(len + 1, 1), (c, 2));

		switch (active)
		{
			case ActiveState.Inactive:
				c = 0;
				break;
			case ActiveState.Disabled:
				c = 1;
				break;
			default: /* Active */
				c = 3;
				break;
		}

		VGAMem.DrawTextLen(name, len, position.Advance(1, 1), (c, 2));
	}

	/* --------------------------------------------------------------------- */

	void DrawEditBox()
	{
		int ci = _currentCharacter << 3;

		for (int i = 0; i < 8; i++)
		{
			VGAMem.DrawCharacter((char)('1' + i), Inner(EditBoxBounds).Advance(i + 1, 2),
						i == _editPosition.X ? (3, 0) : (1, 0));
			VGAMem.DrawCharacter((char)('1' + i), Inner(EditBoxBounds).Advance(0, i + 3),
						i == _editPosition.Y ? (3, 0) : (1, 0));

			for (int j = 0; j < 8; j++)
			{
				byte c;
				int fg;

				if ((Font.Data[ci + j] & (128 >> i)) != 0)
				{
					c = 15;
					fg = 6;
				}
				else
				{
					c = 173;
					fg = 1;
				}

				if (_selectedItem == FontEditorItem.EditBox && (i, j) == _editPosition)
					VGAMem.DrawCharacter(c, Inner(EditBoxBounds).Advance(1 + i, 3 + j), (0, 3));
				else
					VGAMem.DrawCharacter(c, Inner(EditBoxBounds).Advance(1 + i, 3 + j), (fg, 0));
			}
		}

		string buf = ((int)_currentCharacter) + " $" + ((int)_currentCharacter).ToString("X2");

		VGAMem.DrawCharacter(_currentCharacter, Inner(EditBoxBounds), (5, 0));
		VGAMem.DrawText(buf, Inner(EditBoxBounds).Advance(2), (5, 0));
	}

	void DrawCharacterMap()
	{
		int n = 256;

		if (_selectedItem == FontEditorItem.CharacterMap)
		{
			while (n > 0)
			{
				n--;
				VGAMem.DrawCharacter((byte)n, Inner(CharacterMapBounds).Advance(n % 16, n / 16),
					n == _currentCharacter ? (0, 3) : (1, 0));
			}
		}
		else
		{
			while (n > 0)
			{
				n--;
				VGAMem.DrawCharacter((char)n, Inner(CharacterMapBounds).Advance(n % 16, n / 16),
					n == _currentCharacter ? (3, 0) : (1, 0));
			}
		}
	}

	void DrawImpulseTrackerFontMap()
	{
		if (_impulseTrackerFontMapPosition < 0
		 || ImpulseTrackerFontMapCharacters[_impulseTrackerFontMapPosition] != _currentCharacter)
			_impulseTrackerFontMapPosition = Array.IndexOf(ImpulseTrackerFontMapCharacters, _currentCharacter);

		for (int n = 0; n < 240; n++)
		{
			int fg = 1;
			int bg = 0;

			if (n == _impulseTrackerFontMapPosition)
			{
				if (_selectedItem == FontEditorItem.ImpulseTrackerFontMap)
				{
					fg = 0;
					bg = 3;
				}
				else
					fg = 3;
			}

			VGAMem.DrawCharacter(ImpulseTrackerFontMapCharacters[n],
				Inner(ImpulseTrackerFontMapBounds).Advance(n % 16, n / 16), (fg, bg));
		}
	}

	void DrawFontList()
	{
		int pos = 0;

		int cfg, cbg;

		if (_selectedItem == FontEditorItem.FontList)
		{
			cfg = 0;
			cbg = 3;
		}
		else
		{
			cfg = 3;
			cbg = 0;
		}

		if (_fontListTopFont < 0) _fontListTopFont = 0;

		int n = _fontListTopFont;

		while (n < _fontList.NumFiles && pos < FontListVisibleFonts)
		{
			int x = 1;
			var f = _fontList[n];

			if (f == null)
				break;

			int ptr = 0;

			if (n == _fontListCurFont)
			{
				VGAMem.DrawCharacter(183, Inner(FontListBounds).Advance(0, pos), (cfg, cbg));

				while (x < 9 && (ptr < f.BaseName.Length) && (n == 0 || f.BaseName[ptr] != '.'))
				{
					VGAMem.DrawCharacter(f.BaseName[ptr],
						Inner(FontListBounds).Advance(x, pos),
						(cfg, cbg));
					x++;
					ptr++;
				}

				while (x < 9)
				{
					VGAMem.DrawCharacter(0,
						Inner(FontListBounds).Advance(x, pos),
						(cfg, cbg));
					x++;
				}
			}
			else
			{
				VGAMem.DrawCharacter(173, Inner(FontListBounds).Advance(0, pos), (2, 0));
				while (x < 9 && (ptr < f.BaseName.Length) && (n == 0 || f.BaseName[ptr] != '.'))
				{
					VGAMem.DrawCharacter(f.BaseName[ptr],
						Inner(FontListBounds).Advance(x, pos), (5, 0));
					x++;
					ptr++;
				}
				while (x < 9)
				{
					VGAMem.DrawCharacter(0, Inner(FontListBounds).Advance(x, pos), (5, 0));
					x++;
				}
			}

			n++;
			pos++;
		}
	}

	void DrawHelpText()
	{
		var ptr = HelpTextGeneral.AsSpan();

		for (int line = InnerY(HelpTextBounds); ptr.Length > 0; line++)
		{
			int eol = ptr.IndexOf('\n');

			if (eol < 0)
				eol = ptr.IndexOf('\0');

			if (eol < 0)
				eol = ptr.Length;

			for (int column = InnerX(HelpTextBounds); eol > 0; ptr = ptr.Slice(1), eol--, column++)
				VGAMem.DrawCharacter(ptr[0], new Point(column, line), (12, 0));

			ptr = ptr.Slice(1);
		}

		for (int line = 0; line < 10; line++)
			VGAMem.DrawCharacter(168, Inner(HelpTextBounds).Advance(43, line), (12, 0));

		/* context sensitive stuff... oooh :) */
		switch (_selectedItem)
		{
			case FontEditorItem.EditBox:
				ptr = HelpTextEditBox.AsSpan();
				break;
			case FontEditorItem.CharacterMap:
			case FontEditorItem.ImpulseTrackerFontMap:
				ptr = HelpTextCharacterMap.AsSpan();
				break;
			case FontEditorItem.FontList:
				ptr = HelpTextFontList.AsSpan();
				break;
		}

		for (int line = InnerY(HelpTextBounds); ptr.Length > 0; line++)
		{
			int eol = ptr.IndexOf('\n');

			if (eol < 0)
				eol = ptr.IndexOf('\0');

			if (eol < 0)
				eol = ptr.Length;

			VGAMem.DrawCharacter(168, new Point(InnerX(HelpTextBounds) + 43, line), (12, 0));

			for (int column = InnerX(HelpTextBounds) + 45; eol > 0; ptr = ptr.Slice(1), eol--, column++)
				VGAMem.DrawCharacter(ptr[0], new Point(column, line), (12, 0));

			ptr = ptr.Slice(1);
		}

		VGAMem.DrawText(Copyright.ShortCopyright, new Point(77 - Copyright.ShortCopyright.Length, 46), (1, 0));
	}

	void DrawTime()
	{
		string buf = DateTime.Now.ToString("HH:mm:ss");
		VGAMem.DrawText(buf, new Point(3, 46), (1, 0));
	}
}
