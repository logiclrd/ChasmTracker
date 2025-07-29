using System;

namespace ChasmTracker.Widgets;

using ChasmTracker.Events;
using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class TextEntryWidget : Widget
{
	public char[] TextCharacters;
	public int MaxLength;
	public int FirstCharacter; /* first visible character (generally 0) */
	public int CursorPosition; /* 0 = first character */

	public string Text
	{
		get => new string(TextCharacters, 0, TextLength);
		set
		{
			Array.Clear(TextCharacters);
			for (int i = 0; (i < value.Length) && (i < TextCharacters.Length); i++)
				TextCharacters[i] = value[i];
		}
	}

	public int TextLength
	{
		get
		{
			int terminatorIndex = Array.IndexOf(TextCharacters, 0);

			if (terminatorIndex < 0)
				terminatorIndex = TextCharacters.Length;

			return terminatorIndex;
		}
	}

	public override bool AcceptsText => true;

	public TextEntryWidget(Point position, int width, string text, int maxLength)
		: base(position, width)
	{
		TextCharacters = new char[maxLength + 1];
		MaxLength = maxLength;
		FirstCharacter = 0;
		CursorPosition = 0;

		text.CopyTo(0, TextCharacters, 0, Math.Min(text.Length, TextCharacters.Length));
	}

	public void MoveCursor(int delta)
	{
		int n = CursorPosition + delta;

		n = n.Clamp(0, MaxLength);

		if (CursorPosition != n)
		{
			CursorPosition = n;
			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	public void Reposition()
	{
		if (CursorPosition < FirstCharacter)
			FirstCharacter = CursorPosition;
		else if (CursorPosition > Text.Length)
			CursorPosition = Text.Length;
		else if (CursorPosition > FirstCharacter + Size.Width - 1)
			FirstCharacter = Math.Max(0, CursorPosition - Size.Width + 1);
	}

	public void AddCharacter(char c)
	{
		int len = TextLength;

		if (CursorPosition >= MaxLength)
			CursorPosition = MaxLength - 1;

		while (len < CursorPosition)
			TextCharacters[len++] = ' ';

		if (CursorPosition < len)
			Array.Copy(TextCharacters, CursorPosition, TextCharacters, CursorPosition + 1, MaxLength - CursorPosition - 1);

		TextCharacters[CursorPosition++] = c;

		OnChanged();
	}

	public void AddText(string text)
	{
		foreach (char ch in text)
			AddCharacter(ch);
	}

	public void DeleteCharacter()
	{
		// Backspace
		if (CursorPosition == 0)
			return;

		CursorPosition--;

		Array.Copy(TextCharacters, CursorPosition + 1, TextCharacters, CursorPosition, MaxLength - CursorPosition);
	}

	public void DeleteNextCharacter()
	{
		Array.Copy(TextCharacters, CursorPosition + 1, TextCharacters, CursorPosition, MaxLength - CursorPosition);
	}

	public override void HandleText(TextInputEvent textInput)
	{
		AddText(textInput.Text);
		textInput.IsHandled = true;
	}

	protected override void DrawWidget(bool isSelected, int tfg, int tbg)
	{
		int len = TextLength;

		Reposition();

		VGAMem.DrawTextLen(Text, FirstCharacter, Size.Width, Position, (2, 0));

		if (isSelected)
		{
			int cursorOffset = CursorPosition - FirstCharacter;

			VGAMem.DrawCharacter(
				(cursorOffset < TextLength) ? TextCharacters[cursorOffset] : ' ',
				Position.Advance(cursorOffset),
				(0, 3));
		}
	}

	public override bool? HandleActivate(KeyEvent k)
	{
		if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive))
			return false;

		/* LOL WOW THIS SUCKS */
		if (k.Mouse == MouseState.Click && k.OnTarget)
		{
			/* position cursor */
			int n = k.MousePosition.X - Position.X;
			n = n.Clamp(0, Size.Width - 1);

			int wx = k.StartPosition.X - Position.X;
			wx = wx.Clamp(0, Size.Width - 1);

			CursorPosition = n + FirstCharacter;

			wx = wx + FirstCharacter;

			if (CursorPosition >= Text.Length)
				CursorPosition = Text.Length;
			if (wx >= Text.Length)
				wx = Text.Length;

			Status.Flags |= StatusFlags.NeedUpdate;
		}

		/* for a text entry, the only thing enter does is run the activate callback.
		thus, if no activate callback is defined, the key wasn't handled */
		return HasActivatedHandler;
	}

	public override bool? HandleArrow(KeyEvent k)
	{
		if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
			return false;

		if (k.Sym == KeySym.Left)
			MoveCursor(-1);
		else if (k.Sym == KeySym.Right)
			MoveCursor(+1);

		return true;
	}

	public override bool HandleKey(KeyEvent k)
	{
		if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
			return false;

		switch (k.Sym)
		{
			case KeySym.Home:
				MoveCursor(int.MinValue);
				return true;
			case KeySym.End:
				MoveCursor(int.MaxValue);
				return true;
			case KeySym.Backspace:
				if (TextCharacters[0] == 0)
				{
					/* nothing to do */
					return true;
				}

				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
				{
					/* clear the whole field */
					TextCharacters[0] = '\0';
					CursorPosition = 0;
				}
				else
				{
					if (CursorPosition == 0)
					{
						/* act like ST3 */
						DeleteNextCharacter();
					}
					else
						DeleteCharacter();
				}

				OnChanged();
				Status.Flags |= StatusFlags.NeedUpdate;

				return true;
			case KeySym.Delete:
				if (TextCharacters[0] == 0)
				{
					/* nothing to do */
					return true;
				}

				DeleteNextCharacter();

				OnChanged();
				Status.Flags |= StatusFlags.NeedUpdate;

				return true;
		}

		return false;
	}
}
