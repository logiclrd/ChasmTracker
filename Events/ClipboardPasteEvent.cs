namespace ChasmTracker.Events;

public class ClipboardPasteEvent : Event
{
	public byte[]? Clipboard;

	public ClipboardPasteEvent(byte[]? clipboard)
	{
		Clipboard = clipboard;
	}
}
