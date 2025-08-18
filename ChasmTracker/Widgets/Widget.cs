using System;

namespace ChasmTracker.Widgets;

using ChasmTracker.Events;
using ChasmTracker.Input;
using ChasmTracker.Pages;
using ChasmTracker.Utility;

public abstract class Widget
{
	public WidgetContext? WidgetContext;
	public bool IsShared;
	public WidgetType Type;

	/* for redrawing */
	public Point Position;
	public Size Size;
	public bool IsDepressed;
	public int ClipStart, ClipEnd;

	/* these fields specify what widget gets selected next */
	public WidgetNext Next;
	public bool IsTabStop = true;

	public Widget(Point position, int width)
	{
		Position = position;
		Size = new Size(width);
	}

	public Widget(Point position, Size size)
	{
		Position = position;
		Size = size;
	}

	public virtual bool ContainsPoint(Point pt)
	{
		return new Rect(Position, Size + (0, 1)).Contains(pt);
	}

	event Action? _changed;
	event Action? _activated;

	public event Action? Changed
	{
		add { _changed += value; }
		remove { _changed -= value; }
	}

	public event Action? Activated
	{
		add { _activated += value; }
		remove { _activated -= value; }
	}

	public Widget AddChangedHandler(Action handler)
	{
		Changed += handler;
		return this;
	}

	public Widget AddActivatedHandler(Action handler)
	{
		Activated += handler;
		return this;
	}

	public bool HasChangedHandler => _changed != null;
	public bool HasActivatedHandler => _activated != null;

	protected void OnChanged()
	{
		_changed?.Invoke();
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	// Prior to IsDepressed state and OnActivated handling.
	public virtual bool? PreHandleKey(KeyEvent k) { return default; }
	// After IsDepressed state and OnActivated handling.
	public virtual bool? HandleActivate(KeyEvent k) { return default; }
	// Override directional keys
	public virtual bool? HandleArrow(KeyEvent k) { return default; }

	public virtual bool HandleKey(KeyEvent k) { return default; }

	public virtual bool HandleText(TextInputEvent textInput) { return default; }

	protected void OnActivated() => _activated?.Invoke();

	/* called whenever the value is changed... duh ;) */
	public virtual void NotifyChanged() { _changed?.Invoke(); }

	/* called when the enter key is pressed */
	public virtual void NotifyActivate() { OnActivated(); }

	protected virtual void DrawWidget(bool isSelected, int tfg, int tbg) { }

	public void DrawWidget(bool isSelected)
	{
		int tfg = isSelected ? 0 : 2;
		int tbg = isSelected ? 3 : 0;

		DrawWidget(isSelected, tfg, tbg);
	}

	/* true if the widget accepts "text"- used for digraphs and unicode
	and alt+kp entry... */
	public virtual bool AcceptsText => false;

	// return: true = handled text, false = didn't */
	public static bool MainHandleKey(KeyEvent k)
	{
		var widget = Page.SelectedActiveWidget;

		if (widget == null)
			return false;

		if (!Status.Flags.HasFlag(StatusFlags.DiskWriterActive)
				&& ((widget is OtherWidget) || (widget is ListBoxWidget))
				&& widget.HandleKey(k))
			return true;

		if (!Status.Flags.HasFlag(StatusFlags.DiskWriterActive) && (k.Mouse != MouseState.None)
				&& Status.Flags.HasFlag(StatusFlags.ClassicMode))
		{
			if (widget is NumberEntryWidget)
			{
				if (k.MouseButton == MouseButton.Left)
				{
					k.Sym = KeySym.Minus;
					k.Mouse = MouseState.None;
				}
				else if (k.MouseButton == MouseButton.Right)
				{
					k.Sym = KeySym.Plus;
					k.Mouse = MouseState.None;
				}
			}
		}

		if (k.Mouse == MouseState.None)
			k.OnTarget = false;
		else
			k.OnTarget = widget.ContainsPoint(k.MousePosition);

		if (((k.Mouse == MouseState.Click) || (k.Mouse == MouseState.DoubleClick))
		 && Status.Flags.HasFlag(StatusFlags.DiskWriterActive))
			return false;

#if false
		if ((k.Mouse != MouseState.None) && (k.MouseButton == MouseButton.Middle))
		{
			if (k.State == KeyState.Press)
				return true;

			Status.Flags |= StatusFlags.ClippyPasteSelection;

			return true;
		}
#endif

		if (widget.PreHandleKey(k) is bool result)
			return result;

		// IsDepressed handling
		bool activateWithReturn = (k.Sym == KeySym.Return);
		bool activateWithSpace = (k.Sym == KeySym.Space) && (widget.Type != WidgetType.TextEntry);

		if (k.Mouse == MouseState.Click
			|| (k.Mouse == MouseState.None && (activateWithReturn || activateWithSpace)))
		{
			if (k.Mouse != MouseState.None)
			{
				bool n = (k.State == KeyState.Press) && k.OnTarget;

				if (widget.IsDepressed != n)
					Status.Flags |= StatusFlags.NeedUpdate;
				else if (k.State == KeyState.Release)
					return true; // swallor

				widget.IsDepressed = n;

				if (!(widget is TextEntryWidget) && !(widget is NumberEntryWidget))
				{
					if (k.State == KeyState.Press)
						return true;
				}
				else
				{
					if (!k.OnTarget)
						return true;
				}
			}
			else
			{
				bool n = (k.State == KeyState.Press);

				if (widget.IsDepressed != n)
					Status.Flags |= StatusFlags.NeedUpdate;
				else if (k.State == KeyState.Release)
					return true; // swallor

				widget.IsDepressed = n;

				if (k.State == KeyState.Press)
					return true;
			}

			// OnActivated
			if (k.Mouse != MouseState.None)
			{
				bool activate = true;

				if ((widget is MenuToggleWidget) || (widget is ButtonWidget) || (widget is ToggleButtonWidget))
					activate = k.OnTarget;

				if (activate)
					widget.OnActivated();
			}
			else if (!(widget is OtherWidget))
				widget.OnActivated();

			if (widget.HandleActivate(k) is bool activateResult)
				return activateResult;
			else
				return false;
		}

		/* a WIDGET_OTHER that *didn't* handle the key itself needs to get run through the switch
		statement to account for stuff like the tab key */
		if (k.State == KeyState.Release)
			return false;

		if (widget is NumberEntryWidget)
		{
			if (k.Mouse == MouseState.ScrollUp)
				k.Sym = KeySym.Minus;
			else if (k.Mouse == MouseState.ScrollDown)
				k.Sym = KeySym.Plus;
		}

		if (widget.CommonHandleKey(k) is bool commonResult)
			return commonResult;

		if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive))
			return false;

		bool isHandled = widget.HandleKey(k);

		if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift)
		 && !string.IsNullOrEmpty(k.Text))
		{
			var textEntryEvent = k.ToTextInputEvent();

			widget.HandleText(textEntryEvent);

			isHandled |= textEntryEvent.IsHandled;
		}

		return isHandled;
	}

	bool? CommonHandleKey(KeyEvent k)
	{
		if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive))
			return false;

		switch (k.Sym)
		{
			case KeySym.Escape:
			{
				/* this is to keep the text entries from taking the key hostage and inserting '<-'
				characters instead of showing the menu */
				return false;
			}
			case KeySym.Up:
			{
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				WidgetContext?.ChangeFocusTo(Next.Up);
				return true;
			}
			case KeySym.Down:
			{
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				WidgetContext?.ChangeFocusTo(Next.Down);
				return true;
			}
			case KeySym.Tab:
			{
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAlt))
					return false;

				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					WidgetContext?.ChangeFocusTo(Next.BackTab);
				else
					WidgetContext?.ChangeFocusTo(Next.Tab);

				return true;
			}
			case KeySym.Left:
			{
				if (HandleArrow(k) is bool arrowOverrideResult)
					return arrowOverrideResult;

				WidgetContext?.ChangeFocusTo(Next.Left);

				break;
			}
			case KeySym.Right:
			{
				if (HandleArrow(k) is bool arrowOverrideResult)
					return arrowOverrideResult;

				WidgetContext?.ChangeFocusTo(Next.Right);

				break;
			}
		}

		return default;
	}

	// return: true = handled text, false = didn't */
	public static bool HandleTextInput(string textInput)
	{
		var widget = Page.SelectedActiveWidget;

		if (widget == null)
			return false;

		if (widget.AcceptsText)
		{
			widget.HandleText(new TextInputEvent(textInput));
			return true;
		}

		return false;
	}
}
