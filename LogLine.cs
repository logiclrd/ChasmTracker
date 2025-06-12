namespace ChasmTracker;

public class LogLine
{
	public int Colour;
	public string Text;
	public bool BIOSFont;

	public LogLine(int colour, string text, bool biosFont)
	{
		Colour = colour;
		Text = text;
		BIOSFont = biosFont;
	}
}
