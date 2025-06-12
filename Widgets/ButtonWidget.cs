namespace ChasmTracker.Widgets;

using System;

using ChasmTracker.VGA;

public class ButtonWidget : Widget
{
	public string Text;
	public int Padding;

	public ButtonWidget(Point position, int width, string text, int padding)
		: base(position)
	{
		Size = new Size(width);
		Text = text;
		Padding = padding;
	}

	public ButtonWidget(Point position, int width, Action changedAction, string text, int padding)
		: this(position, width, text, padding)
	{
		Changed += changedAction;
	}

	protected override void DrawWidget(VGAMem vgaMem, bool isSelected, int tfg, int tbg)
	{
		vgaMem.DrawBox(
			Position.Advance(-1, -1),
			Position.Advance(Size.Width + 2, 1),
			BoxTypes.Thin | BoxTypes.Inner | (IsDepressed ? BoxTypes.Inset : BoxTypes.Outset));

		vgaMem.DrawText(Text, Position.Advance(Padding), isSelected ? 3 : 0, 2);
	}
}
