using System;

namespace ChasmTracker.Widgets;

using ChasmTracker.Events;
using ChasmTracker.Input;
using ChasmTracker.Utility;

public class OtherWidget : Widget
{
	public bool OtherAcceptsText;

	public event Func<KeyEvent, bool>? OtherHandleKey;
	public event Action<TextInputEvent>? OtherHandleText;
	public event Action? OtherRedraw;

	public OtherWidget()
		: base(new Point(-1, -1), 0)
	{
	}

	// Position and size for navigation discovery
	public OtherWidget(Point position, Size size)
		: base(position, size)
	{
	}

	public OtherWidget(Rect bounds)
		: this(bounds.TopLeft, bounds.Size)
	{
	}

	public override bool AcceptsText => OtherAcceptsText;

	protected override void DrawWidget(bool isSelected, int tfg, int tbg)
		=> OtherRedraw?.Invoke();

	public override bool HandleKey(KeyEvent k)
		=> OtherHandleKey?.Invoke(k) ?? false;

	public override void HandleText(TextInputEvent textInput)
		=> OtherHandleText?.Invoke(textInput);
}