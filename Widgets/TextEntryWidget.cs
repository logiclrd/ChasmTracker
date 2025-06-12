using System;
using ChasmTracker.VGA;

namespace ChasmTracker.Widgets;

public class TextEntryWidget : Widget
{
	public char[] TextCharacters;
	public int MaxLength;
	public int FirstCharacter; /* first visible character (generally 0) */
	public int CursorPosition; /* 0 = first character */

	public string Text => new string(TextCharacters, 0, TextLength);

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

	public TextEntryWidget(Point position, WidgetNext next, int width, string text, int maxLength)
		: base(position, next)
	{
		Size = new Size(width);
		TextCharacters = new char[maxLength];
		MaxLength = maxLength;
		FirstCharacter = 0;
		CursorPosition = 0;

		text.CopyTo(0, TextCharacters, 0, Math.Min(text.Length, TextCharacters.Length));
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

	public override void HandleText(TextInputEvent textInput)
	{
		AddText(textInput.Text);
		textInput.IsHandled = true;
	}

	protected override void DrawWidget(VGAMem vgaMem, bool isSelected, int tfg, int tbg)
	{
		int len = TextLength;

		if (CursorPosition < FirstCharacter)
			FirstCharacter = CursorPosition;
		else if (CursorPosition > len)
			CursorPosition = len;
		else if (CursorPosition > FirstCharacter + Size.Width - 1)
			FirstCharacter = Math.Max(0, CursorPosition - Size.Width + 1);

		vgaMem.DrawTextLen(Text, FirstCharacter, Size.Width, Position, 2, 0);

		if (isSelected)
		{
			int cursorOffset = CursorPosition - FirstCharacter;

			vgaMem.DrawCharacter(
				(cursorOffset < TextLength) ? TextCharacters[cursorOffset] : ' ',
				Position.Advance(cursorOffset),
				0, 3);
		}
	}
}
