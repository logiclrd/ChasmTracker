namespace ChasmTracker;

public class LogLine
{
	public byte Colour;
	public string Text;
	public bool Underline;

	public LogLine(byte colour, string text)
	{
		Colour = colour;
		Text = text;
	}
}
