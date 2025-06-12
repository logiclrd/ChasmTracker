/*
using System;

namespace ChasmTracker.Widgets;

public class OtherWidget : Widget
{
	public Action<KeyEvent> HandleKey;
	public Action<TextInputEvent>? HandleTextInput;
	public Action Redraw;

	public OtherWidget(WidgetNext next, Action<KeyEvent> handleKey, Action<TextInputEvent>? handleTextInput, Action redraw)
		: base(new Point(-1, -1), new WidgetNext(-1, -1, -1, -1, next.TabIndex, -1))
	{
		HandleKey = handleKey;
		HandleTextInput = handleTextInput;
		Redraw = redraw;
	}

	public override void HandleText(TextInputEvent textInput)
	{
		HandleTextInput?.Invoke(textInput);
	}
}
*/