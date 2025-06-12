namespace ChasmTracker.Events;

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
