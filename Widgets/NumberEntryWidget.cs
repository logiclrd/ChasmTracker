using System;
using ChasmTracker.VGA;

namespace ChasmTracker.Widgets;

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

	public NumberEntryWidget(Point position, WidgetNext next, int width, int min, int max, SharedInt cursorPosition)
		: base(position, next)
	{
		Size = new Size(width);
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

	protected override void DrawWidget(VGAMem vgaMem, bool isSelected, int tfg, int tbg)
	{
		if (Reverse)
		{
			string str = Value.ToString();

			if (str.Length > Size.Width)
				str = str.Substring(str.Length - Size.Width);

			vgaMem.DrawTextLen("", Size.Width, Position, 2, 0);

			vgaMem.DrawText(str, Position.Advance(Size.Width - str.Length), 2, 0);
			
			if (isSelected)
			{
				if (str == "")
					str = " ";

				vgaMem.DrawCharacter(str[str.Length - 1], Position.Advance(Size.Width - 1), 0, 3);
			}
		}
		else
		{
			string buf = Value.ToString("d" + Size.Width);

			vgaMem.DrawTextLen(buf, Size.Width, Position, 2, 0);

			if (isSelected)
				vgaMem.DrawCharacter(buf[CursorPosition], Position.Advance(CursorPosition), 0, 3);
		}
	}
}
