using System;

namespace ChasmTracker.Widgets;

using ChasmTracker.Events;
using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class NumberEntryWidget : Widget
{
	int _value;

	public int Minimum;
	public int Maximum;
	public SharedInt CursorPosition;
	public event EventHandler<KeyEvent>? HandleUnknownKey;
	public bool Reverse;

	public int Value
	{
		get => _value;
		set
		{
			_value = value.Clamp(Minimum, Maximum);
			OnChanged();
		}
	}

	public NumberEntryWidget(Point position, int width, int min, int max, SharedInt cursorPosition)
		: base(position, width)
	{
		Minimum = min;
		Maximum = max;
		Value = min;
		CursorPosition = cursorPosition;
		Reverse = false;
	}

	public override void HandleText(TextInputEvent textInput)
	{
		int cursorPositionDigitValue = 1;

		for (int i = CursorPosition + 1; i < Size.Width; i++)
			cursorPositionDigitValue *= 10;

		int newValue = _value;

		foreach (char ch in textInput.Text)
		{
			if ((ch < '0') || (ch > '9'))
				break;

			int existingDigit = (newValue / cursorPositionDigitValue) % 10;

			newValue -= existingDigit * cursorPositionDigitValue;
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
		}

		Value = newValue;

		textInput.IsHandled = true;
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
}
