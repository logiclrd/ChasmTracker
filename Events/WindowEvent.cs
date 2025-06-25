namespace ChasmTracker.Events;

using ChasmTracker.Utility;

public class WindowEvent : Event
{
	public WindowEventType EventType;
	public Size NewSize;

	public WindowEvent()
	{
	}

	public WindowEvent(WindowEventType eventType)
	{
		EventType = eventType;
	}
}
