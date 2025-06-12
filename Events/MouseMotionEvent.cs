namespace ChasmTracker.Events;

public class MouseMotionEvent : MouseEvent
{
	public MouseMotionEvent()
	{
	}

	public MouseMotionEvent(int x, int y)
		: base(x, y)
	{
	}
}
