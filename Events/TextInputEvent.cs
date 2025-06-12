namespace ChasmTracker.Events;

public class TextInputEvent : Event
{
	public string? Text;

	public TextInputEvent()
	{
	}

	public TextInputEvent(string? text)
	{
		Text = text;
	}
}
