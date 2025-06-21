namespace ChasmTracker.Widgets;

using System;

using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class ButtonWidget : Widget
{
	public string Text;
	public int Padding;

	public event Action? Clicked;

	public ButtonWidget(Point position, int width, string text, int padding)
		: base(position, width)
	{
		Text = text;
		Padding = padding;

		Changed += () => Clicked?.Invoke();
	}

	public ButtonWidget(Point position, int width, Action clickedAction, string text, int padding)
		: this(position, width, text, padding)
	{
		Clicked += clickedAction;
	}

	protected override void DrawWidget(bool isSelected, int tfg, int tbg)
	{
		VGAMem.DrawBox(
			Position.Advance(-1, -1),
			Position.Advance(Size.Width + 2, 1),
			BoxTypes.Thin | BoxTypes.Inner | (IsDepressed ? BoxTypes.Inset : BoxTypes.Outset));

		VGAMem.DrawText(Text, Position.Advance(Padding), isSelected ? (3, 2) : (0, 2));
	}
}
