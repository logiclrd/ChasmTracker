namespace ChasmTracker.Events;

public class NativeOpenEvent : Event
{
	public string FilePath;

	public NativeOpenEvent(string filePath)
	{
		FilePath = filePath;
	}
}
