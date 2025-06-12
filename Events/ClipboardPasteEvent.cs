namespace ChasmTracker.Events;

public class ClipboardPasteEvent : Event
{
	public string Clipboard;

	public ClipboardPasteEvent(string clipboard)
	{
		Clipboard = clipboard;
	}
}
