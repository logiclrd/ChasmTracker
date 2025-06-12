namespace ChasmTracker.Events;

public class FileDropEvent : Event
{
	public string FilePath;

	public FileDropEvent(string filePath)
	{
		FilePath = filePath;
	}
}
