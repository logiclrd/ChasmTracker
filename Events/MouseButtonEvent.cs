namespace ChasmTracker.Events;

public class MouseButtonEvent : MouseEvent, IButtonPressEvent
{
	public MouseButtonEventType EventType;

	public MouseButton Button;
	public bool State;
	public int Clicks;

	public bool IsPressEvent => (EventType == MouseButtonEventType.Down);
	public bool IsReleaseEvent => (EventType == MouseButtonEventType.Up);

	public MouseButtonEvent()
	{
	}

	public MouseButtonEvent(MouseButtonEventType eventType, MouseButton button, bool state, int clicks, int x, int y)
		: base(x, y)
	{
		EventType = eventType;
		Button = button;
		State = state;
		Clicks = clicks;
	}
}
