namespace ChasmTracker;

public class TextInputEvent
{
	public string Text;
	public bool IsHandled;

	public TextInputEvent(string text)
	{
		Text = text;
	}
}
