namespace ChasmTracker.Widgets;

using ChasmTracker.VGA;

public class ToggleWidget : Widget
{
	public bool State;

	public override bool AcceptsText => false;

	public ToggleWidget(Point position)
		: base(position, width: 3 /* "Off" */)
	{
	}

	protected override void DrawWidget(VGAMem vgaMem, bool isSelected, int tfg, int tbg)
	{
		vgaMem.DrawFillChars(Position, Position.Advance(Size.Width - 1), VGAMem.DefaultForeground, 0);
		vgaMem.DrawText(State ? "On" : "Off", Position, tfg, tbg);
	}
}
