namespace ChasmTracker.Events;

using ChasmTracker.Utility;

public class MouseWheelEvent : MouseEvent
{
	public Point WheelDelta;

	public MouseWheelEvent()
	{
	}

	public MouseWheelEvent(int x, int y, int wheelX, int wheelY)
		: base(x, y)
	{
		WheelDelta = new Point(wheelX, wheelY);
	}
}
