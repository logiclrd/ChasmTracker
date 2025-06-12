using System.Linq;

namespace ChasmTracker.Widgets;

using ChasmTracker.VGA;

public class ToggleButtonWidget : Widget
{
	bool _state;

	public string Text;
	public int Padding;
	public int GroupNumber;

	public WidgetGroup? Group;

	public bool State
	{
		get => _state;
		set
		{
			SetState(value);
			OnChanged();
		}
	}

	public void SetState(bool state)
	{
		if (state)
		{
			foreach (var toggleButton in Group.Widgets.OfType<ToggleButtonWidget>())
				toggleButton._state = false;

			_state = true;
		}
		else
			_state = false;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public ToggleButtonWidget(Point position, int width, string text, int padding, int groupNumber)
		: base(position)
	{
		Size = new Size(width);
		Text = text;
		Padding = padding;
		GroupNumber = groupNumber;
	}

	protected override void DrawWidget(VGAMem vgaMem, bool isSelected, int tfg, int tbg)
	{
		vgaMem.DrawBox(
			Position.Advance(-1, -1),
			Position.Advance(Size.Width + 2, 1),
			BoxTypes.Thin | BoxTypes.Inner |
			(State || IsDepressed ? BoxTypes.Inset : BoxTypes.Outset));

		vgaMem.DrawText(Text, Position.Advance(Padding), isSelected ? 3 : 0, 2);
	}
}
