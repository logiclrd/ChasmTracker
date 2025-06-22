namespace ChasmTracker.Events;

public class TextInputEvent : Event
{
	public string Text;
	public bool IsHandled;

	public TextInputEvent(string text)
	{
		Text = text;
	}
}
