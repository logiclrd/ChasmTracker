namespace ChasmTracker.Configurations;

public static class DateFormatsExtensions
{
	public static string GetFormatString(this DateFormats format)
	{
		switch (format)
		{
			case DateFormats.MMMMDYYYY: return "MMM d, yyyy";
			case DateFormats.DMMMMYYYY: return "d MMM yyyy";
			case DateFormats.YYYYMMMMDD: return "yyyy MMM dd";
			case DateFormats.MDYYYY: return "m/d/yyyy";
			case DateFormats.DMYYYY: return "d/m/yyyy";
			case DateFormats.DDMMYYYY: return "dd/mm/yyyy";
			case DateFormats.YYYYMD: return "yyyy/m/d";
			case DateFormats.YYYYMMDD: return "yyyy/mm/dd";
			case DateFormats.ISO8601: return "yyyy-mm-dd";

			default: return "d";
		}
	}
}
