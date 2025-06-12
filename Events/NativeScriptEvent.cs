namespace ChasmTracker.Events;

public class NativeScriptEvent : Event
{
	public string Which;

	public NativeScriptEvent(string which)
	{
		Which = which;
	}
}
