namespace ChasmTracker.Configurations;

public static class TimeFormatsExtensions
{
	public static string GetFormatString(this TimeFormats format)
	{
		switch (format)
		{
			case TimeFormats._12hr: return "hh:mm:ss tt";
			case TimeFormats._24hr: return "HH:mm:ss";

			default: return "T";
		}
	}
}
