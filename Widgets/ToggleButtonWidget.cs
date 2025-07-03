using System.Collections.Generic;
using System.Linq;

namespace ChasmTracker.Widgets;

using ChasmTracker.Input;
using ChasmTracker.Utility;
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
			if (Group != null)
				foreach (var toggleButton in Group.Widgets.OfType<ToggleButtonWidget>())
					toggleButton._state = false;

			_state = true;
		}
		else
			_state = false;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public ToggleButtonWidget(Point position, string text, int padding, int groupNumber)
		: base(position, width: 3 /* "Off" */)
	{
		Text = text;
		Padding = padding;
		GroupNumber = groupNumber;
	}

	public ToggleButtonWidget(Point position, int width, string text, int padding, int groupNumber)
		: base(position, width)
	{
		Text = text;
		Padding = padding;
		GroupNumber = groupNumber;
	}

	public static void BuildGroups(IEnumerable<Widget> widgets)
	{
		var toggleButtonGroups = widgets
			.Select((widget, index) => (Widget: widget, Index: index))
			.Where(p => p.Widget is ToggleButtonWidget)
			.Select(p => (ToggleButton: (ToggleButtonWidget)p.Widget, p.Index))
			.GroupBy(p => p.ToggleButton.GroupNumber);

		foreach (var toggleButtonGroup in toggleButtonGroups)
		{
			var group = new WidgetGroup(
				toggleButtonGroup.Select(p => p.Index).ToArray(),
				toggleButtonGroup.Select(p => p.ToggleButton).ToArray());

			foreach (var pair in toggleButtonGroup)
				pair.ToggleButton.Group = group;
		}
	}

	protected override void DrawWidget(bool isSelected, int tfg, int tbg)
	{
		VGAMem.DrawBox(
			Position.Advance(-1, -1),
			Position.Advance(Size.Width + 2, 1),
			BoxTypes.Thin | BoxTypes.Inner |
			(State || IsDepressed ? BoxTypes.Inset : BoxTypes.Outset));

		VGAMem.DrawText(Text, Position.Advance(Padding), isSelected ? (3, 2) : (0, 2));
	}

	public override bool? HandleActivate(KeyEvent k)
	{
		if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive))
			return false;

		if (GroupNumber != 0)
		{
			/* this also runs the changed callback and redraws the button(s) */
			SetState(true);
			return true;
		}

		/* else... */
		_state = !_state;
		/* maybe buttons should ignore the changed callback, and use activate instead...
		(but still call the changed callback for togglebuttons if they *actually* changed) */
		OnChanged();

		Status.Flags |= StatusFlags.NeedUpdate;

		return true;
	}
}
