using System;

namespace ChasmTracker.Widgets;

using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

/* this was heavily inspired by Qt's "virtual" listbox API,
 * where everything is basically done by virtual functions,
 * hence the name.
 * however, since we aren't C++, we just do everything
 * through function pointers. :) */
public class ListBoxWidget : Widget
{
	/* get the size of the listbox */
	public event Func<int>? GetSize;

	/* get the name of an item */
	public event Func<int, string>? GetName;

	/* get whether an item is toggled or not.
	 * in the scope of the UI, this decides whether
	 * the item is prefixed with a "*" or not.
	 * in most cases there should only be ONE of
	 * these at a time. */
	public event Func<int, bool>? GetToggled;

	/* custom key handler, for extra keybinds. :) */
	public event Func<KeyEvent, bool>? ListBoxHandleKey;

	//struct {
	//	/* left & backtab */
	//	const int *left;
	//	/* right & tab (would tab ever NOT mean right?) */
	//	const int *right;
	//} focus_offsets;

	public int Top;
	public int Focus;

	public ListBoxWidget(Point position, Size size)
		: base(position, size)
	{
		Type = WidgetType.ListBox;
	}

	public ListBoxWidget(Rect bounds)
		: this(bounds.TopLeft, bounds.Size)
	{
	}

	protected override void DrawWidget(bool isSelected, int tfg, int tbg)
	{
		VGAMem.DrawFillCharacters(Position, Position.Advance(Size).Advance(-1, -1), (VGAMem.DefaultForeground, 0));

		if ((GetSize == null) || (GetName == null))
			return;

		int size = GetSize();

		if (Top >= size)
			return; /* wat */

		for (int o = Top, i = 0; o < size && i < Size.Height; i++, o++)
		{
			int fg, bg;

			if (o == Focus)
			{
				if (isSelected)
				{
					fg = 0;
					bg = 3;
				}
				else
				{
					fg = 6;
					bg = 14;
				}
			}
			else
			{
				fg = 6;
				bg = 0;
			}

			bool toggled = GetToggled?.Invoke(o) ?? false;

			VGAMem.DrawTextUnicodeLen(toggled ? "*" : " ", 1, Position.Advance(0, i), (fg, bg));
			VGAMem.DrawTextUnicodeLen(GetName(o), Size.Width - 1, Position.Advance(1, i), (fg, bg));
		}
	}

	public override bool HandleKey(KeyEvent k)
	{
		int newFocus = Focus;
		int size = GetSize?.Invoke() ?? 0;
		bool raiseActivatedEvent = false;

		switch (k.Mouse)
		{
			case MouseState.DoubleClick:
			case MouseState.Click:
				if (k.State != KeyState.Press)
					return false;

				var bounds = new Rect(Position, Size);

				if (!bounds.Contains(k.MousePosition))
					return false;

				newFocus = Top + k.MousePosition.Y - Position.Y;

				if (k.Mouse == MouseState.DoubleClick || newFocus == Focus)
					raiseActivatedEvent = true;
				break;
			case MouseState.ScrollUp:
				newFocus -= Constants.MouseScrollLines;
				break;
			case MouseState.ScrollDown:
				newFocus += Constants.MouseScrollLines;
				break;
			default:
				if (k.State == KeyState.Release)
					return false;
				break;
		}

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (--newFocus < 0)
					return false;
				break;
			case KeySym.Down:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (++newFocus >= (int)size)
					return false;
				break;
			case KeySym.Home:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newFocus = 0;
				break;
			case KeySym.PageUp:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				if (newFocus == 0)
					return true;

				newFocus -= 16;
				break;
			case KeySym.End:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newFocus = size;
				break;
			case KeySym.PageDown:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newFocus += 16;
				break;
			case KeySym.Return:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				raiseActivatedEvent = true;
				break;
			case KeySym.Tab:
			{
				Widget? next = null;

				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					next = WidgetNext.Search(WidgetContext, this, Position.Advance(0, Focus), SearchDirection.Left);
				else if (k.Modifiers == KeyMod.None)
					next = WidgetNext.Search(WidgetContext, this, Position.Advance(Size.Width - 1, Focus), SearchDirection.Right);

				if (next == null)
					return false;

				WidgetContext?.ChangeFocusTo(next);

				return true;
			}
			case KeySym.Left:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				WidgetContext?.ChangeFocusTo(WidgetNext.Search(WidgetContext, this, Position.Advance(0, Focus), SearchDirection.Left));

				return true;
			case KeySym.Right:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				WidgetContext?.ChangeFocusTo(WidgetNext.Search(WidgetContext, this, Position.Advance(Size.Width - 1, Focus), SearchDirection.Right));

				return true;
			default:
				if (ListBoxHandleKey?.Invoke(k) ?? false)
					return true;

				if (k.Mouse == MouseState.None)
					return false;

				break;
		}

		newFocus = newFocus.Clamp(0, size - 1);

		if (newFocus != Focus)
		{
			int top = Top;

			Focus = newFocus;

			Status.Flags |= StatusFlags.NeedUpdate;

			/* these HAVE to be done separately (and not as a CLAMP) because they aren't
			* really guaranteed to be ranges */
			top = Math.Min(top, Focus);
			top = Math.Max(top, Focus - Size.Height + 1);

			top = Math.Min(top, size - Size.Height + 1);
			top = Math.Max(top, 0);

			Top = top;

			OnChanged();
		}

		if (raiseActivatedEvent)
			OnActivated();

		return true;
	}
}
