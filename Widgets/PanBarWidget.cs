using ChasmTracker.VGA;

namespace ChasmTracker.Widgets;

public class PanBarWidget : Widget
{
	public int Minimum;
	public int Maximum;
	public int Value;
	public int Channel;
	public bool IsReversed;
	public bool IsMuted;
	public bool IsSurround;

	public PanBarWidget(Point position, WidgetNext next, int channel)
		: base(position, width: 24)
	{
		Minimum = 0;
		Maximum = 64;
		Channel = channel;
	}

	protected override void DrawWidget(VGAMem vgaMem, bool isSelected, int tfg, int tbg)
	{
		string buf = "        " + Channel.ToString("d2");

		vgaMem.DrawText(buf, Position, isSelected ? 3 : 0, 2);

		if (IsMuted)
			vgaMem.DrawText("  Muted  ", Position.Advance(11), isSelected ? 3 : 5, 0);
		else if (IsSurround)
			vgaMem.DrawText("Surround ", Position.Advance(11), isSelected ? 3 : 5, 0);
		else
		{
			vgaMem.DrawThumbBar(Position.Advance(11), 9, 0, 64, Value, isSelected);
			vgaMem.DrawText(Value.ToString("d3"), Position.Advance(21), 1, 2);
		}
	}
}
