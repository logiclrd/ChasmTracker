using System;

namespace ChasmTracker.Widgets;

using ChasmTracker.Events;
using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class NumberEntryWidget : Widget
{
	public int Value;
	public int Minimum;
	public int Maximum;
	public Shared<int> CursorPosition;
	public bool Reverse;

	public event Func<KeyEvent, bool>? HandleUnknownKey;

	public void ChangeValue(int value)
	{
		Value = value.Clamp(Minimum, Maximum);
		OnChanged();
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public NumberEntryWidget(Point position, int width, int min, int max, Shared<int> cursorPosition)
		: base(position, width)
	{
		Minimum = min;
		Maximum = max;
		Value = min;
		CursorPosition = cursorPosition;
		Reverse = false;
	}

	public void MoveCursor(int delta)
	{
		if (Reverse)
			return;

		int n = CursorPosition + delta;

		n = n.Clamp(0, Size.Width - 1);

		if (CursorPosition != n)
		{
			CursorPosition.Value = n;
			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	public override bool HandleText(TextInputEvent textInput)
	{
		int cursorPositionDigitValue = 1;

		for (int i = CursorPosition + 1; i < Size.Width; i++)
			cursorPositionDigitValue *= 10;

		int newValue = Value;

		bool success = false;

		foreach (char ch in textInput.Text)
		{
			if ((ch < '0') || (ch > '9'))
				break;

			int existingDigit = (newValue / cursorPositionDigitValue) % 10;

			/* isolate our digit and subtract it */
			newValue -= existingDigit * cursorPositionDigitValue;
			/* add our digit in its place */
			newValue += (ch - '0') * cursorPositionDigitValue;

			if (Reverse)
			{
				cursorPositionDigitValue *= 10;
				CursorPosition.Value--;
			}
			else
			{
				cursorPositionDigitValue /= 10;
				CursorPosition.Value++;
			}

			success = true;
		}

		CursorPosition.Value = CursorPosition.Value.Clamp(0, Size.Width - 1);

		Value = newValue;

		textInput.IsHandled = true;

		return success;
	}

	protected override void DrawWidget(bool isSelected, int tfg, int tbg)
	{
		if (Reverse)
		{
			string str = Value.ToString();

			if (str.Length > Size.Width)
				str = str.Substring(str.Length - Size.Width);

			VGAMem.DrawTextLen("", Size.Width, Position, (2, 0));

			VGAMem.DrawText(str, Position.Advance(Size.Width - str.Length), (2, 0));

			if (isSelected)
			{
				if (str == "")
					str = " ";

				VGAMem.DrawCharacter(str[str.Length - 1], Position.Advance(Size.Width - 1), (0, 3));
			}
		}
		else
		{
			string buf = Value.ToString("d" + Size.Width);

			VGAMem.DrawTextLen(buf, Size.Width, Position, (2, 0));

			if (isSelected)
				VGAMem.DrawCharacter(buf[CursorPosition], Position.Advance(CursorPosition), (0, 3));
		}
	}

	public override bool? HandleActivate(KeyEvent k)
	{
		if (Status.Flags.HasAllFlags(StatusFlags.DiskWriterActive))
			return false;

		if (/*k.State == KeyState.Press && */k.Mouse == MouseState.Click && k.OnTarget)
		{
			/* position cursor */
			int n = k.MousePosition.X - Position.X;
			n = n.Clamp(0, Size.Width - 1);

			int wx = k.StartPosition.X - Position.X;
			wx = wx.Clamp(0, Size.Width - 1);

			if (n >= Size.Width)
				n = Size.Width - 1;

			CursorPosition.Value = n;

			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return default;
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
		if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
		{
			switch (k.Sym)
			{
				case KeySym.Home:
					MoveCursor(int.MinValue);
					return true;
				case KeySym.End:
					MoveCursor(int.MaxValue);
					return true;
				case KeySym.Backspace:
					if (Reverse)
					{
						/* woot! */
						ChangeValue(Value / 10);

						OnChanged();
						Status.Flags |= StatusFlags.NeedUpdate;

						return true;
					}

					break;
				case KeySym.Plus:
					ChangeValue(Value + 1);
					return true;
				case KeySym.Minus:
					ChangeValue(Value - 1);
					return true;
			}
		}

		/* weird hack? */
		return HandleUnknownKey?.Invoke(k) ?? false;
	}
}
