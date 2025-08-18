namespace ChasmTracker.Widgets;

using ChasmTracker.Dialogs;
using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class ThumbBarWidget : Widget
{
	/* pretty much the same as the numentry, just without the cursor
	 * position field... */
	public int Minimum;
	public int Maximum;
	public int Value;
	/* this is currently only used with the midi thumbbars on the ins. list + pitch page. if
	 * this is non-NULL, and value == {min,max}, the text is drawn instead of the thumbbar. */
	public string? TextAtMinimum;
	public string? TextAtMaximum;

	public ThumbBarWidget(Point position, int width, int min, int max)
		: base(position, width)
	{
		Minimum = min;
		Maximum = max;
	}

	public void ChangeValue(int newValue)
	{
		Value = newValue.Clamp(Minimum, Maximum);
		OnChanged();
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public ThumbBarWidget(Point position, int width, int min, int max, string textAtMin, string textAtMax)
		: this(position, width, min, max)
	{
		if (textAtMin.Length > width)
			textAtMin = textAtMin.Substring(0, width);
		if (textAtMax.Length > width)
			textAtMax = textAtMax.Substring(0, width);

		TextAtMinimum = textAtMin;
		TextAtMaximum = textAtMax;
	}

	protected override void DrawWidget(bool isSelected, int tfg, int tbg)
	{
		if ((TextAtMinimum != null) && (Value == Minimum))
			VGAMem.DrawTextLen(TextAtMinimum, Size.Width, Position, isSelected ? (3, 0) : (2, 0));
		else if ((TextAtMaximum != null) && (Value == Maximum))
		{
			int len = TextAtMaximum.Length;
			int offset = Size.Width - len;

			if (offset < 0)
			{
				offset = 0;
				len = Size.Width;
			}

			VGAMem.DrawFillCharacters(Position, Position.Advance(len - 1), (VGAMem.DefaultForeground, 0));
			VGAMem.DrawTextLen(TextAtMaximum, Size.Width, Position.Advance(offset), isSelected ? (3, 0) : (2, 0));
		}
		else
		{
			VGAMem.DrawThumbBar(Position, Size.Width, Minimum, Maximum, Value, isSelected);
		}

		string buf = Value.ToString("d3");

		VGAMem.DrawText(buf, Position.Advance(Size.Width + 1), (1, 2));
	}

	public override bool? PreHandleKey(KeyEvent k)
	{
		if (k.Mouse == MouseState.Click)
		{
			if (Status.Flags.HasAllFlags(StatusFlags.DiskWriterActive))
				return false;

			/* swallow it */
			if (!k.OnTarget && (k.State != KeyState.Drag))
				return false;

			int fMin = Minimum;
			int fMax = Maximum;

			int n = k.MousePositionFine.X - Position.X * k.CharacterResolution.X;
			int wx = (Size.Width - 1) * k.CharacterResolution.X;

			n = n.Clamp(0, wx);
			n = fMin + n * (fMax - fMin) / wx;
			n = n.Clamp(fMin, fMax);

			ChangeValue(n);

			return true;
		}

		return default;
	}

	public override bool? HandleArrow(KeyEvent k)
	{
		/* I'm handling the key modifiers differently than Impulse Tracker, but only
		because I think this is much more useful. :) */
		int n = 1;

		if (k.Modifiers.HasAnyFlag(KeyMod.Alt | KeyMod.GUI))
			n *= 8;
		if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
			n *= 4;
		if (k.Modifiers.HasAnyFlag(KeyMod.Control))
			n *= 2;

		if (k.Sym == KeySym.Left)
			n = Value - n;
		else if (k.Sym == KeySym.Right)
			n = Value + n;
		else
			return default;

		ChangeValue(n);

		return true;
	}


	public override bool HandleKey(KeyEvent k)
	{
		switch (k.Sym)
		{
			case KeySym.Home:
				ChangeValue(Minimum);
				return true;
			case KeySym.End:
				ChangeValue(Maximum);
				return true;
		}

		if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
		{
			/* annoying */
			return false;
		}

		char c;

		if (k.Sym == KeySym.Minus)
		{
			if (Minimum >= 0)
				return false;

			c = '-';
		}
		else
		{
			int n = k.NumericValue(false);

			if (n < 0)
				return false;

			c = (char)('0' + n);
		}

		var dialog = Dialog.Show(new NumberPromptDialog("Enter Value", c));

		dialog.Finish +=
			n =>
			{
				if ((n >= Minimum) && (n <= Maximum))
				{
					Value = n;
					OnChanged();
				}
			};

		return false;
	}
}
