using System;
using ChasmTracker.VGA;

namespace ChasmTracker.Widgets;

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
		: base(position)
	{
		Size = new Size(width);
		Minimum = min;
		Maximum = max;
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

	protected override void DrawWidget(VGAMem vgaMem, bool isSelected, int tfg, int tbg)
	{
		if ((TextAtMinimum != null) && (Value == Minimum))
			vgaMem.DrawTextLen(TextAtMinimum, Size.Width, Position, isSelected ? 3 : 2, 0);
		else if ((TextAtMaximum != null) && (Value == Maximum))
		{
			int len = TextAtMaximum.Length;
			int offset = Size.Width - len;

			if (offset < 0)
			{
				offset = 0;
				len = Size.Width;
			}

			vgaMem.DrawFillChars(Position, Position.Advance(len - 1), VGAMem.DefaultForeground, 0);
			vgaMem.DrawTextLen(TextAtMaximum, Size.Width, Position.Advance(offset), isSelected ? 3 : 2, 0);
		}
		else
		{
			vgaMem.DrawThumbBar(Position, Size.Width, Minimum, Maximum, Value, isSelected);
		}

		string buf = Value.ToString("d3");

		vgaMem.DrawText(buf, Position.Advance(Size.Width + 1), 1, 2);
	}
}
