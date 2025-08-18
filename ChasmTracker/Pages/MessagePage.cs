using System;
using System.Text;

namespace ChasmTracker.Pages;

using ChasmTracker.Clipboard;
using ChasmTracker.Dialogs;
using ChasmTracker.Events;
using ChasmTracker.Input;
using ChasmTracker.Memory;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

/* --->> WARNING <<---
 *
 * This is an excellent example of how NOT to write a text editor.
 * IMHO, the best way to add a song message is by writing it in some
 * other program and attaching it to the song with something like
 * ZaStaR's ITTXT utility (hmm, maybe I should rewrite that, too ^_^) so
 * I'm not *really* concerned about the fact that this code completely
 * sucks. Just remember, this ain't Xcode. */

public class MessagePage : Page
{
	OtherWidget otherMessage;

	int _topLine;
	int _cursorLine;
	int _cursorChar;
	/* this is the absolute cursor position from top of message.
	 * (should be updated whenever _cursorLine/_cursorChar change) */
	int _cursorPos = 0;

	bool _editMode = false;

	/* message should use the alternate font */
	bool _useExtFont = true;

	StringBuilder _text = new StringBuilder();

	/* This is a bit weird... Impulse Tracker usually wraps at 74, but if
	 * the line doesn't have any spaces in it (like in a solid line of
	 * dashes or something) it gets wrapped at 75. I'm using 75 for
	 * everything because it's always nice to have a bit extra space :) */
	const int LineWrap = 75;

	public MessagePage()
		: base(PageNumbers.Message, "Message Editor (Shift-F9)", HelpTexts.MessageEditor)
	{
		otherMessage = new OtherWidget(new Point(1, 12), new Size(77, 36));
		otherMessage.OtherHandleKey += HandleKeyViewMode;
		otherMessage.OtherRedraw += Draw;
		otherMessage.OtherAcceptsText = _editMode;
	}


	/* returns the number of characters on the nth line of _text, setting ptr
	 * to the first character on the line. if it there are fewer than n
	 * lines, ptr is set to the \0 at the end of the string, and the
	 * function returns -1. note: if *ptr == text, weird things will
	 * probably happen, so don't do that. */
	int GetNthLine(int start, int n, out int ptr)
	{
		if (_text == null)
		{
			Assert.IsTrue(_text != null, "_text != null", "should never get a NULL pointer here");
			throw new Exception();
		}

		ptr = start;

		while (n > 0)
		{
			n--;
			while ((ptr < _text.Length) && (_text[ptr] != '\r') && (_text[ptr] != '\n'))
				ptr++;

			if (ptr >= _text.Length)
				return -1;

			if ((ptr + 1 < _text.Length) && (_text[ptr] == '\r') && (_text[ptr + 1] == '\n'))
				ptr += 2;
			else
				ptr++;
		}

		int startOfLine = ptr;

		int endOfLine = ptr;

		while ((endOfLine < _text.Length) && (_text[endOfLine] != '\r') && (_text[endOfLine] != '\n'))
			endOfLine++;

		return endOfLine - startOfLine;
	}

	void SetAbsolutePosition(int pos)
	{
		int len;

		_cursorLine = _cursorChar = 0;
		while (pos > 0)
		{
			len = GetNthLine(0, _cursorLine, out _);
			if (len < 0)
			{
				/* end of file */
				_cursorLine--;
				if (_cursorLine < 0) _cursorLine = 0;
				len = GetNthLine(0, _cursorLine, out _);
				_cursorChar = (len < 0) ? 0 : len;
				pos = 0;
			}
			else if (len >= pos)
			{
				_cursorChar = pos;
				pos = 0;
			}
			else
			{
				pos -= (len + 1); /* EOC */
				_cursorLine++;
			}
		}
	}

	int GetAbsolutePosition(int line, int character)
	{
		int len = GetNthLine(0, line, out var ptr);

		if (len < 0)
			return 0;
		else
			return ptr + character; /* hrm... what if _cursorChar > len? */
	}

	/* --------------------------------------------------------------------- */

	void Reposition()
	{
		if (_cursorLine < _topLine)
			_topLine = _cursorLine;
		else if (_cursorLine > _topLine + 34)
			_topLine = _cursorLine - 34;
	}

	/* --------------------------------------------------------------------- */

	/* returns true if a character was actually added */
	bool AddChar(char newChar, int position)
	{
		if (_text.Length == Constants.MaxMessage)
		{
			MessageBox.Show(MessageBoxTypes.OK, "  Song message too long!  ");
			return false;
		}

		if ((position < 0) || (position > _text.Length))
		{
			Log.Append(4, "MessageAddChar: position={0}, len={1} - shouldn't happen!", position, _text.Length);
			return false;
		}

		_text.Insert(position, newChar);

		return true;
	}

	/* this returns the new length of the line */
	int WrapLine(int bolPtr)
	{
		int tmp = bolPtr;

		if (bolPtr < 0)
			/* shouldn't happen, but... */
			return 0;

		int eolPtr = bolPtr;

		while ((eolPtr < _text.Length) && (_text[eolPtr] != '\r') && (_text[eolPtr] != '\n'))
			eolPtr++;

		int lastSpace = -1;

		for (; ; )
		{
			while ((tmp < eolPtr) && (_text[tmp] != ' ') && (_text[tmp] != '\t'))
				tmp++;

			if ((tmp >= eolPtr) || (tmp - bolPtr > LineWrap))
				break;

			lastSpace = tmp;
		}

		if (lastSpace >= 0)
		{
			_text[lastSpace] = '\r';
			return lastSpace - bolPtr;
		}
		else
		{
			/* what, no spaces to cut at? chop it mercilessly. */
			if (!AddChar('\r', bolPtr + LineWrap))
			{
				/* ack, the message is too long to wrap the line!
				* gonna have to resort to something ugly. */
				_text[bolPtr + LineWrap] = '\r';
			}

			return LineWrap;
		}
	}

	/* --------------------------------------------------------------------- */
	void Text(int line, int len, int n)
	{
		int fg = _useExtFont ? 12 : 6;

		var position = new Point(2, 13 + n);

		for (int i = 0; line + i < _text.Length && i < len; i++)
		{
			var ch = _text[line + i];

			if (ch == ' ')
				VGAMem.DrawCharacter(' ', position.Advance(i), (3, 0));
			else if (_useExtFont)
				VGAMem.DrawCharacterBIOS(ch, position.Advance(i), (fg, 0));
			else
				VGAMem.DrawCharacter(ch, position.Advance(i), (fg, 0));
		}
	}

	void Draw()
	{
		int prevLine = 0;
		int len = GetNthLine(0, _topLine, out var line);

		VGAMem.DrawFillCharacters(new Point(2, 13), new Point(77, 47), (VGAMem.DefaultForeground, 0));

		int clipl, clipr;

		if (Clippy.Owner(ClippySource.Select) == otherMessage)
		{
			clipl = otherMessage.ClipStart;
			clipr = otherMessage.ClipEnd;

			if (clipl > clipr)
				(clipl, clipr) = (clipr, clipl);
		}
		else
			clipl = clipr = -1;

		int n;

		for (n = 0; n < 35; n++)
		{
			if (len < 0)
				break;

			if (len > 0)
			{
				/* FIXME | shouldn't need this check here,
				* FIXME | because the line should already be
				* FIXME | short enough to fit */
				if (len > LineWrap)
					len = LineWrap;

				Text(line, len, n);

				if (clipl > -1)
				{
					int skipc = clipl - line;
					int cutc = clipr - clipl;

					if (skipc < 0)
					{
						cutc += skipc; /* ... -skipc */
						skipc = 0;
					}

					if (cutc < 0) cutc = 0;
					if (cutc > (len - skipc)) cutc = (len - skipc);

					if (cutc > 0 && skipc < len)
					{
						if (_useExtFont)
							VGAMem.DrawTextBIOSLen(_text, line + skipc, cutc, new Point(2 + skipc, 13 + n), (6, 8));
						else
							VGAMem.DrawTextLen(_text, line + skipc, cutc, new Point(2 + skipc, 13 + n), (6, 8));
					}
				}
			}

			if (_editMode)
				VGAMem.DrawCharacter(20, new Point(2 + len, 13 + n), (1, 0));

			prevLine = line;
			len = GetNthLine(prevLine, 1, out line);
		}

		if (_editMode && len < 0)
		{
			/* end of the message */
			len = GetNthLine(prevLine, 0, out line);
			/* FIXME: see above */
			if (len > LineWrap)
				len = LineWrap;
			VGAMem.DrawCharacter(20, new Point(2 + len, 13 + n - 1), (2, 0));
		}

		if (_editMode)
		{
			/* draw the cursor */
			len = GetNthLine(0, _cursorLine, out line);

			/* FIXME: ... ugh */
			if (len > LineWrap)
				len = LineWrap;
			if (_cursorChar > LineWrap + 1)
				_cursorChar = LineWrap + 1;

			if (_cursorChar >= len)
			{
				if (_useExtFont)
					VGAMem.DrawCharacterBIOS(20, new Point(2 + _cursorChar, 13 + (_cursorLine - _topLine)), (0, 3));
				else
					VGAMem.DrawCharacter(20, new Point(2 + _cursorChar, 13 + (_cursorLine - _topLine)), (0, 3));
			}
			else
			{
				if (_useExtFont)
					VGAMem.DrawCharacterBIOS(_text[line + _cursorChar], new Point(2 + _cursorChar, 13 + (_cursorLine - _topLine)), (8, 3));
				else
					VGAMem.DrawCharacter(_text[line + _cursorChar], new Point(2 + _cursorChar, 13 + (_cursorLine - _topLine)), (8, 3));
			}
		}
	}

	/* --------------------------------------------------------------------- */

	public void SetEditMode()
	{
		_editMode = true;
		otherMessage.OtherAcceptsText = true;

		_topLine = _cursorLine = _cursorChar = _cursorPos = 0;

		otherMessage.OtherHandleKey -= HandleKeyViewMode;
		otherMessage.OtherHandleKey += HandleKeyEditMode;
		otherMessage.OtherHandleText += HandleTextInputEditMode;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public void SetViewMode()
	{
		_editMode = false;
		otherMessage.OtherAcceptsText = false;

		otherMessage.OtherHandleKey -= HandleKeyEditMode;
		otherMessage.OtherHandleKey += HandleKeyViewMode;
		otherMessage.OtherHandleText -= HandleTextInputEditMode;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	/* --------------------------------------------------------------------- */

	void InsertChar(char c)
	{
		if (!_editMode)
			return;

		MemoryUsage.NotifySongChanged();

		if (c == '\t')
		{
			/* Find the number of characters until the next tab stop.
			* (This is new behaviour; Impulse Tracker just inserts
			* eight characters regardless of the cursor position.) */
			int n = 8 - _cursorChar % 8;
			if (_cursorChar + n > LineWrap)
				InsertChar('\r');
			else
			{
				do
				{
					if (!AddChar(' ', _cursorPos))
						break;
					_cursorChar++;
					_cursorPos++;
					n--;
				} while (n > 0);
			}
		}
		else if (c < 32 && c != '\r' && c != '\n')
			return;
		else
		{
			if (!AddChar(c, _cursorPos))
				return;
			_cursorPos++;
			if (c == '\r' || c == '\n')
			{
				_cursorChar = 0;
				_cursorLine++;
			}
			else
			{
				_cursorChar++;
			}
		}
		if (GetNthLine(0, _cursorLine, out var ptr) >= LineWrap)
			WrapLine(ptr);

		if (_cursorChar >= LineWrap)
		{
			_cursorChar = GetNthLine(0, ++_cursorLine, out _);
			_cursorPos = GetAbsolutePosition(_cursorLine, _cursorChar);
		}

		Reposition();

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	void DeleteChar()
	{
		if (_cursorPos <= 0)
			return;
		_text.Remove(_cursorPos - 1, 1);
		_cursorPos--;

		if (_cursorChar == 0)
		{
			_cursorLine--;
			_cursorChar = GetNthLine(0, _cursorLine, out _);
		}
		else
		{
			_cursorChar--;
		}

		Reposition();

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	void DeleteNextChar()
	{
		if (_cursorPos >= _text.Length)
			return;

		_text.Remove(_cursorPos, 1);

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	void DeleteLine()
	{
		int len = GetNthLine(0, _cursorLine, out var ptr);

		if (len < 0)
			return;
		if (_text[ptr + len] == 13 && _text[ptr + len + 1] == 10)
			len++;

		_text.Remove(ptr, len + 1);

		len = GetNthLine(0, _cursorLine, out _);

		if (_cursorChar > len)
		{
			_cursorChar = len;
			_cursorPos = GetAbsolutePosition(_cursorLine, _cursorChar);
		}

		Reposition();

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	void Clear()
	{
		_text.Clear();

		MemoryUsage.NotifySongChanged();

		SetViewMode();

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	/* --------------------------------------------------------------------- */

	void PromptClear()
	{
		var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "Clear song message?");

		dialog.ChangeFocusTo(1);
		dialog.ActionYes = () => Clear();
	}

	/* --------------------------------------------------------------------- */

	bool HandleKeyViewMode(KeyEvent k)
	{
		if (k.State == KeyState.Press)
		{
			if (k.Mouse == MouseState.ScrollUp)
				_topLine -= Constants.MouseScrollLines;
			else if (k.Mouse == MouseState.ScrollDown)
				_topLine += Constants.MouseScrollLines;
			else if (k.Mouse == MouseState.Click)
			{
				SetEditMode();
				return HandleKeyEditMode(k);
			}
		}

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.State == KeyState.Release)
					return false;
				_topLine--;
				break;
			case KeySym.Down:
				if (k.State == KeyState.Release)
					return false;
				_topLine++;
				break;
			case KeySym.PageUp:
				if (k.State == KeyState.Release)
					return false;
				_topLine -= 35;
				break;
			case KeySym.PageDown:
				if (k.State == KeyState.Release)
					return false;
				_topLine += 35;
				break;
			case KeySym.Home:
				if (k.State == KeyState.Release)
					return false;
				_topLine = 0;
				break;
			case KeySym.End:
				if (k.State == KeyState.Release)
					return false;
				_topLine = _text.GetNumLines() - 34;
				break;
			case KeySym.t:
				if (k.State == KeyState.Release)
					return false;

				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					_useExtFont = !_useExtFont;
					break;
				}

				return true;
			case KeySym.Return:
				if (k.State == KeyState.Press)
					return false;
				SetEditMode();
				return true;
			default:
				return false;
		}

		if (_topLine < 0)
			_topLine = 0;

		Status.Flags |= StatusFlags.NeedUpdate;

		return true;
	}

	void DeleteSelection()
	{
		int len = _text.Length;
		int eat;

		_cursorPos = otherMessage.ClipStart;
		if (_cursorPos > otherMessage.ClipEnd)
		{
			_cursorPos = otherMessage.ClipEnd;
			eat = otherMessage.ClipStart - _cursorPos;
		}
		else
			eat = otherMessage.ClipEnd - _cursorPos;

		Clippy.Select(null, null, 0);

		if (_cursorPos == len)
			return;

		_text.Remove(_cursorPos, eat + 1);

		SetAbsolutePosition(_cursorPos);

		Reposition();

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	bool HandleTextInputEditMode(TextInputEvent text)
	{
		if (Clippy.Owner(ClippySource.Select) == otherMessage)
			DeleteSelection();

		foreach (char ch in text.Text)
			InsertChar(ch);

		return true;
	}

	bool HandleKeyEditMode(KeyEvent k)
	{
		int newCursorLine = _cursorLine;
		int newCursorChar = _cursorChar;
		bool doingDrag = false;

		if (k.Mouse == MouseState.ScrollUp)
		{
			if (k.State == KeyState.Release)
				return false;
			newCursorLine -= Constants.MouseScrollLines;
		}
		else if (k.Mouse == MouseState.ScrollDown)
		{
			if (k.State == KeyState.Release)
				return false;
			newCursorLine += Constants.MouseScrollLines;
		}
		else if (k.Mouse == MouseState.Click && k.MouseButton == MouseButton.Middle)
		{
			if (k.State == KeyState.Release)
				Status.Flags |= StatusFlags.ClippyPasteSelection;
			return true;
		}
		else if (k.Mouse == MouseState.Click)
		{
			if (k.MousePosition.X >= 2 && k.MousePosition.X <= 77 && k.MousePosition.Y >= 13 && k.MousePosition.Y <= 47)
			{
				newCursorLine = (k.MousePosition.Y - 13) + _topLine;
				newCursorChar = (k.MousePosition.X - 2);
				if (k.StartPosition.X != k.MousePosition.X || k.StartPosition.Y != k.MousePosition.Y)
				{
					/* yay drag operation */
					int cp = GetAbsolutePosition((k.StartPosition.Y - 13) + _topLine,
								(k.StartPosition.X - 2));
					otherMessage.ClipStart = cp;
					doingDrag = true;
				}
			}
		}

		int lineLen = GetNthLine(0, _cursorLine, out var ptr);
		int numLines = -1;

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newCursorLine--;
				break;
			case KeySym.Down:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newCursorLine++;
				break;
			case KeySym.Left:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newCursorChar--;
				break;
			case KeySym.Right:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newCursorChar++;
				break;
			case KeySym.PageUp:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newCursorLine -= 35;
				break;
			case KeySym.PageDown:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newCursorLine += 35;
				break;
			case KeySym.Home:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
					newCursorLine = 0;
				else
					newCursorChar = 0;
				break;
			case KeySym.End:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					numLines = _text.GetNumLines();
					newCursorLine = numLines;
				}
				else
					newCursorChar = lineLen;
				break;
			case KeySym.Escape:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				SetViewMode();
				MemoryUsage.NotifySongChanged();
				return true;
			case KeySym.Backspace:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				if ((k.Sym != KeySym.None) && (Clippy.Owner(ClippySource.Select) == otherMessage))
					DeleteSelection();
				else
					DeleteChar();
				return true;
			case KeySym.Delete:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;

				if (Clippy.Owner(ClippySource.Select) == otherMessage)
					DeleteSelection();
				else
					DeleteNextChar();

				return true;
			case KeySym.Return:
				if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				{
					if (k.State == KeyState.Release)
						return true;

					if (Clippy.Owner(ClippySource.Select) == otherMessage)
						DeleteSelection();

					InsertChar('\r');

					return true;
				}
				return false;
			default:
				/* keybinds... */
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					if (k.State == KeyState.Release)
						return true;

					if (k.Sym == KeySym.t)
					{
						_useExtFont = !_useExtFont;
						break;
					}
					else if (k.Sym == KeySym.y)
					{
						Clippy.Select(null, null, 0);
						DeleteLine();
						break;
					}
				}
				else if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Release)
						return true;

					if (k.Sym == KeySym.c)
					{
						PromptClear();
						return true;
					}
				}
				else if (k.Mouse == MouseState.None)
				{
					if (!string.IsNullOrEmpty(k.Text))
						return HandleTextInputEditMode(k.ToTextInputEvent());
					else if (k.Sym == KeySym.Tab)
					{
						if (k.State == KeyState.Press)
							InsertChar('\t');

						return true;
					}

					return false;
				}

				if (k.Mouse != MouseState.Click)
					return false;

				if (k.State == KeyState.Release)
					return true;
				if (!doingDrag)
					Clippy.Select(null, null, 0);

				break;
		}

		if (newCursorLine != _cursorLine)
		{
			if (numLines == -1)
				numLines = _text.GetNumLines();

			if (newCursorLine < 0)
				newCursorLine = 0;
			else if (newCursorLine > numLines)
				newCursorLine = numLines;

			/* make sure the cursor doesn't go past the new eol */
			lineLen = GetNthLine(0, newCursorLine, out ptr);
			if (newCursorChar > lineLen)
				newCursorChar = lineLen;

			_cursorChar = newCursorChar;
			_cursorLine = newCursorLine;
		}
		else if (newCursorChar != _cursorChar)
		{
			/* we say "else" here ESPECIALLY because the mouse can only come
			in the top section - not because it's some clever optimization */
			if (newCursorChar < 0)
			{
				if (_cursorLine == 0)
				{
					newCursorChar = _cursorChar;
				}
				else
				{
					_cursorLine--;
					newCursorChar =
						GetNthLine(0, _cursorLine, out ptr);
				}

			}
			else if (newCursorChar >
					GetNthLine(0, _cursorLine, out ptr))
			{
				if (_cursorLine == _text.GetNumLines())
					newCursorChar = _cursorChar;
				else
				{
					_cursorLine++;
					newCursorChar = 0;
				}
			}

			_cursorChar = newCursorChar;
		}

		Reposition();

		_cursorPos = GetAbsolutePosition(_cursorLine, _cursorChar);

		if (doingDrag)
		{
			otherMessage.ClipEnd = _cursorPos;

			int clipl = otherMessage.ClipStart;
			int clipr = otherMessage.ClipEnd;

			if (clipl > clipr)
				(clipl, clipr) = (clipr, clipl);

			Clippy.Select(otherMessage, _text.ToString(clipl, clipr - clipl), clipr - clipl);
		}

		Status.Flags |= StatusFlags.NeedUpdate;

		return true;
	}

	/* --------------------------------------------------------------------- */

	public override void SetPage()
	{
		_text = new StringBuilder(Song.CurrentSong.Message);
	}

	public override void UnsetPage()
	{
		Song.CurrentSong.Message = _text.ToString();
	}

	public override void DrawConst()
	{
		VGAMem.DrawBox(new Point(1, 12), new Point(78, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}

	public override void NotifySongChanged()
	{
		_editMode = false;

		otherMessage.OtherAcceptsText = false;
		otherMessage.OtherHandleKey -= HandleKeyEditMode;
		otherMessage.OtherHandleKey += HandleKeyViewMode;

		_topLine = 0;

		int len = GetNthLine(0, 0, out var line);

		int prevLine;

		while (len >= 0)
		{
			if (len > LineWrap)
				WrapLine(line);

			prevLine = line;

			len = GetNthLine(prevLine, 1, out line);
		}

		if (Status.CurrentPageNumber == PageNumbers.Message)
			Status.Flags |= StatusFlags.NeedUpdate;
	}

	public void ResetSelection()
	{
		otherMessage.ClipStart = otherMessage.ClipEnd = 0;
	}
}
