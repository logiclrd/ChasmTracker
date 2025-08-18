namespace ChasmTracker.Widgets;

using ChasmTracker.Input;
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
		VGAMem.DrawFillCharacters(Position, Position.Advance(Size.Width - 1), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawText(State ? "On" : "Off", Position, (tfg, tbg));
	}

	public override bool? PreHandleKey(KeyEvent k)
	{
		if ((k.Mouse == MouseState.Click) && k.OnTarget)
		{
			if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				return false;
			if (k.State != KeyState.Press)
				return true;

			State = !State;

			OnChanged();

			Status.Flags |= StatusFlags.NeedUpdate;

			return true;
		}

		return default;
	}

	public override bool HandleKey(KeyEvent k)
	{
		switch (k.Sym)
		{
			case KeySym.Space:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				State = !State;

				OnChanged();
				Status.Flags |= StatusFlags.NeedUpdate;

				return true;
		}

		return false;
	}
}
