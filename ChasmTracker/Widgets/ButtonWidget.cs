using System;

namespace ChasmTracker.Widgets;

using ChasmTracker.Input;
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

	public override bool ContainsPoint(Point pt)
	{
		return new Rect(Position, Size + (2, 1)).Contains(pt);
	}

	protected override void DrawWidget(bool isSelected, int tfg, int tbg)
	{
		VGAMem.DrawBox(
			Position.Advance(-1, -1),
			Position.Advance(Size.Width + 2, 1),
			BoxTypes.Thin | BoxTypes.Inner | (IsDepressed ? BoxTypes.Inset : BoxTypes.Outset));

		VGAMem.DrawText(Text, Position.Advance(Padding), isSelected ? (3, 2) : (0, 2));
	}

	protected override bool ActivateOffTarget => false;

	public override bool? HandleActivate(KeyEvent k)
	{
		base.HandleActivate(k);

		if (k.State == KeyState.Drag)
			IsDepressed = k.OnTarget;
		else
		{
			/* maybe buttons should ignore the changed callback, and use activate instead...
			(but still call the changed callback for togglebuttons if they *actually* changed) */
			if (k.OnTarget && (k.State == KeyState.Release))
			{
				OnChanged();
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			}
		}

		return false;
	}
}
