using System;

namespace ChasmTracker.Utility;

public class DateTimeConversions
{
	public static TimeSpan DOSTimeToTimeSpan(uint dosTime)
	{
		// convert to milliseconds
		return TimeSpan.FromSeconds(dosTime * 1000.0 / 18.2);
	}

	public static uint TimeSpanToDOSTime(TimeSpan tm)
	{
		double dos = tm.TotalSeconds * 18.2;

		// no overflow!
		return (uint)double.Clamp(dos, 0, uint.MaxValue);
	}

	public static DateTime FATDateToDateTime(ushort fatDate, ushort fatTime)
	{
		/* PRESENT DAY */
		int mday = fatDate & 0x1F;
		int mon = (fatDate >> 5) & 0xF;
		int year = (fatDate >> 9) + 80;

		/* PRESENT TIME */
		int sec = (fatTime & 0x1F) << 1;
		int min = (fatTime >> 5) & 0x3F;
		int hour = fatTime >> 11;

		return new DateTime(year, mon, mday, hour, min, sec);
	}

	public static (ushort FATDate, ushort FATTime) DateTimeToFATDate(DateTime tm)
	{
		ushort fatDate = unchecked((ushort)(tm.Day | (tm.Month << 5) | ((tm.Year - 80) << 9)));
		ushort fatTime = unchecked((ushort)((tm.Second >> 1) | (tm.Minute << 5) | (tm.Hour	 << 11)));

		return (fatDate, fatTime);
	}
}
