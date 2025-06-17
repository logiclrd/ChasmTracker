using System.Linq;

namespace ChasmTracker.Widgets;

using ChasmTracker.VGA;

public class MenuToggleWidget : Widget
{
	public int State;
	public MenuToggleWidgetChoice[] Choices;

	public MenuToggleWidget(Point position, WidgetNext next, string[] choices)
		: base(position, width: choices.Max(choice => choice.Length))
	{
		Choices = choices.Select(choice => new MenuToggleWidgetChoice(choice)).ToArray();
	}

	protected override void DrawWidget(VGAMem vgaMem, bool isSelected, int tfg, int tbg)
	{
		vgaMem.DrawFillChars(Position, Position.Advance(Size.Width - 1), VGAMem.DefaultForeground, 0);

		string choice = Choices[State].Label;

		int n = choice.IndexOf(' ');

		if (n >= 0)
		{
			vgaMem.DrawTextLen(choice, n, Position, tfg, tbg);
			vgaMem.DrawText(choice, n, Position.Advance(n), 2, 0);
		}
		else
			vgaMem.DrawText(choice, Position, tfg, tbg);
	}
}
