using System;

namespace ChasmTracker.Widgets;

using ChasmTracker.VGA;

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

	public event Action? Changed;
	public event Action? Activated;

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

	protected void OnChanged()
	{
		Changed?.Invoke();
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public virtual void HandleText(TextInputEvent textInput) { }

	protected void OnActivated() => Activated?.Invoke();

	/* called whenever the value is changed... duh ;) */
	public virtual void NotifyChanged() { Changed?.Invoke(); }

	/* called when the enter key is pressed */
	public virtual void NotifyActivate() { Activated?.Invoke(); }

	/* called by the clipboard manager; really, only "other" widgets
	should "override" this... */
	public virtual bool ClipboardPaste(int cb, string cptr) { return false; }

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
}
