namespace ChasmTracker.Widgets;

using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class ToggleWidget : Widget
{
	public bool State;

	public override bool AcceptsText => false;

	public ToggleWidget(Point position)
		: base(position, width: 3 /* "Off" */)
	{
	}

	protected override void DrawWidget(bool isSelected, int tfg, int tbg)
	{
		VGAMem.DrawFillCharacters(Position, Position.Advance(Size.Width - 1), VGAMem.DefaultForeground, 0);
		VGAMem.DrawText(State ? "On" : "Off", Position, tfg, tbg);
	}
}
