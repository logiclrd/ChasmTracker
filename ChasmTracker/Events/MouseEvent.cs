namespace ChasmTracker.Events;

using ChasmTracker.Utility;

public abstract class MouseEvent : Event
{
	/* coordinates relative to the window */
	public Point Position;

	public MouseEvent()
	{
	}

	public MouseEvent(int x, int y)
	{
		Position = new Point(x, y);
	}
}
