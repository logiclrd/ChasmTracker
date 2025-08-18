using System;
using System.Linq;

namespace ChasmTracker.Widgets;

using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class MenuToggleWidget : Widget
{
	public int State;
	public MenuToggleWidgetChoice[] Choices;

	public MenuToggleWidget(Point position, string[] choices)
		: base(position, width: choices.Max(choice => choice.Length))
	{
		Choices = choices.Select(choice => new MenuToggleWidgetChoice(choice)).ToArray();
	}

	protected override void DrawWidget(bool isSelected, int tfg, int tbg)
	{
		VGAMem.DrawFillCharacters(Position, Position.Advance(Size.Width - 1), (VGAMem.DefaultForeground, 0));

		string choice = Choices[State].Label;

		int n = choice.IndexOf(' ');

		if (n >= 0)
		{
			VGAMem.DrawTextLen(choice, n, Position, (tfg, tbg));
			VGAMem.DrawText(choice, n, Position.Advance(n), (2, 0));
		}
		else
			VGAMem.DrawText(choice, Position, (tfg, tbg));
	}

	public override bool? PreHandleKey(KeyEvent k)
	{
		if ((k.Mouse == MouseState.Click) && k.OnTarget)
		{
			if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				return false;

			if (k.State != KeyState.Press)
				return true;

			State = (State + 1)
				% Choices.Length;

			OnChanged();

			Status.Flags |= StatusFlags.NeedUpdate;

			return true;
		}

		return default;
	}

	public override bool HandleKey(KeyEvent k)
	{
		if (k.Mouse == MouseState.Click)
		{
			if (k.OnTarget)
				OnActivated();
		}
		else
		{
			if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				return false;

			int newSelectionIndex;

			if (k.Sym == KeySym.Space)
				newSelectionIndex = (State + 1) % Choices.Length;
			else
			{
				char ch = (char)k.Sym;

				newSelectionIndex = Array.FindIndex(Choices, c => c.ActivationKey == ch);
			}

			if (newSelectionIndex >= 0)
			{
				State = newSelectionIndex;

				OnChanged();
				Status.Flags |= StatusFlags.NeedUpdate;

				return true;
			}
		}

		return false;
	}
}
