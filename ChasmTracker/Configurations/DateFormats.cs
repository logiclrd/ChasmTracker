namespace ChasmTracker.Configurations;

public enum DateFormats
{
	// stolen from Wikipedia:
	MMMMDYYYY,  // January 7, 2025  (United States, default)
	DMMMMYYYY,  // 7 January 2025   (Most of the world)
	YYYYMMMMDD, // 2025 January 07  (Wikipedia has this ?)

	MDYYYY,   // M/D/YYYY (United States, default)
	MMDDYYYY, // M/D/YYYY (United States with leading zeroes)
	DMYYYY,   // D/M/YYYY (United Kingdom with no leading zeroes)
	DDMMYYYY, // DD/MM/YYYY (United Kingdom)
	YYYYMD,   // YYYY/M/D
	YYYYMMDD, // YYYY/MM/DD

	ISO8601, // YYYY-MM-DD

	// special constant.
	Default = -1,
}

