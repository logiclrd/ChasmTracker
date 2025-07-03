using System;

namespace ChasmTracker.Widgets;

using ChasmTracker.Events;
using ChasmTracker.Input;
using ChasmTracker.Pages;
using ChasmTracker.Utility;

public abstract class Widget
{
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

	public virtual void HandleText(TextInputEvent textInput) { }

	protected void OnActivated() => _activated?.Invoke();

	/* called whenever the value is changed... duh ;) */
	public virtual void NotifyChanged() { _changed?.Invoke(); }

	/* called when the enter key is pressed */
	public virtual void NotifyActivate() { OnActivated(); }

	/* called by the clipboard manager; really, only "other" widgets
	should "override" this... */
	public virtual bool ClipboardPaste(byte[]? cptr) { return false; }

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

		/*
		if (!Status.Flags.HasFlag(StatusFlags.DiskWriterActive)
				&& (widget is OtherWidget)
				&& widget.HandleKey(k))
			return true;
		*/

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
		if (k.Mouse == MouseState.Click)
		{
			int pad = 0;

			if (widget is ButtonWidget buttonWidget)
				pad = buttonWidget.Padding;
			else if (widget is ToggleButtonWidget toggleButtonWidget)
				pad = toggleButtonWidget.Padding;

			bool onw = (k.MousePosition.X < widget.Position.X
						|| k.MousePosition.X >= widget.Position.X + widget.Size.Width + pad
						|| k.MousePosition.Y != widget.Position.Y) ? false : true;

			bool n = (k.State == KeyState.Press && onw) ? true : false;

			if (widget.IsDepressed != n)
				Status.Flags |= StatusFlags.NeedUpdate;
			else if (k.State == KeyState.Release)
				return true; // swallor

			widget.IsDepressed = n;

			if (!(widget is TextEntryWidget) && !(widget is NumberEntryWidget))
			{
				if (k.State == KeyState.Press || !onw)
					return true;
			}
			else
			{
				if (!onw)
					return true;
			}
		}
		else if (k.Mouse == MouseState.None)
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
		if ((k.Mouse == MouseState.Click) || ((k.Mouse == MouseState.None) && (k.Sym == KeySym.Return)))
		{
			if (k.Mouse != MouseState.None)
			{
				bool activate = true;

				if ((widget is MenuToggleWidget) || (widget is ButtonWidget) || (widget is ToggleButtonWidget))
					activate = k.OnTarget;

				if (activate)
					widget.OnActivated();
			}
			else
			{
				// TODO: if (!(widget is OtherWidget)) {
				widget.OnActivated();
				// }
			}

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

		if (CommonHandleKey(k) is bool commonResult)
			return commonResult;

		widget.HandleKey(k);

		/* if we're here, that mess didn't completely handle the key (gosh...) so now here's another mess. */
		switch (current_type) {
		case WIDGET_MENUTOGGLE:
			if (widget_menutoggle_handle_key(widget, k))
				return 1;
			break;
		case WIDGET_BITSET:
			if (widget_bitset_handle_key(widget, k))
				return 1;
			break;
		case WIDGET_THUMBBAR:
		case WIDGET_PANBAR:
			if (thumbbar_prompt_value(widget, k))
				return 1;
			break;
		case WIDGET_TEXTENTRY:
			if ((k->mod & (SCHISM_KEYMOD_CTRL | SCHISM_KEYMOD_ALT | SCHISM_KEYMOD_GUI)) == 0 &&
				k->text && widget_textentry_add_text(widget, k->text))
				return 1;
			break;
		case WIDGET_NUMENTRY:
			if ((k->mod & (SCHISM_KEYMOD_CTRL | SCHISM_KEYMOD_ALT | SCHISM_KEYMOD_GUI)) == 0 &&
				k->text && widget_numentry_handle_text(widget, k->text))
				return 1;
			break;
		default:
			break;
		}

		/* if we got down here the key wasn't handled */
		return false;
	}

	static bool? CommonHandleKey(Widget widget, KeyEvent k)
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

				Page.ChangeFocusTo(widget.Next.Up);
				return true;
			}
			case KeySym.Down:
			{
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				Page.ChangeFocusTo(widget.Next.Down);
				return true;
			}
			case KeySym.Tab:
			{
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAlt))
					return false;

				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					Page.ChangeFocusTo(widget.Next.BackTab);
				else
					Page.ChangeFocusTo(widget.Next.Tab);

				return true;
			}
			case KeySym.Left:
			{
				if (widget.HandleArrow(k) is var arrowOverrideResult)
					return arrowOverrideResult;

				Page.ChangeFocusTo(widget.Next.Left);

				break;
			}
			case KeySym.Right:
			{
				if (widget.HandleArrow(k) is var arrowOverrideResult)
					return arrowOverrideResult;

				Page.ChangeFocusTo(widget.Next.Right);

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
