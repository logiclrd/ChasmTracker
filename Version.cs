using System;
using System.Globalization;
using System.Reflection;
using ChasmTracker.Utility;

namespace ChasmTracker;

public class Version
{
	public static readonly string TopBannerClassic = "Impulse Tracker v2.14 Copyright (C) 1995-1998 Jeffrey Lim";
	public static readonly string TopBannerNormal = "Chasm Tracker " + GetBannerVersionString();

	static string GetBannerVersionString()
	{
		if (!string.IsNullOrEmpty(ThisAssembly.Git.CommitDate)
		 && DateTime.TryParseExact(ThisAssembly.Git.CommitDate, "s", default, DateTimeStyles.AssumeUniversal, out var commitDate))
			return commitDate.ToString("yyyyMMdd");
		else
			return "built " + ExtractBuildDateTime();
	}

	static string ExtractBuildDateTime()
	{
		var assembly = typeof(Version).Assembly;

		var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

		if (attribute?.InformationalVersion is string version)
		{
			int revisionTagIndex = version.IndexOf("+build");

			if (revisionTagIndex >= 0)
			{
				version = version.Substring(revisionTagIndex + 6);

				if (DateTime.TryParseExact(version, "o", CultureInfo.InvariantCulture, default, out var buildDateTime))
					return buildDateTime.ToString("MMM d yyyy HH:mm:ss");
			}
		}

		return "in the future";
	}

	/* -------------------------------------------------------------- */

	const int EpochYear = 2009;
	const int EpochMonth = 9;
	const int EpochDay = 31;

	// only used by ver_mktime, do not use directly
	// see https://alcor.concordia.ca/~gpkatch/gdate-algorithm.html
	// for a description of the algorithm used here
	static long EncodeDate(int y, int m, int d)
	{
		long mm = (m + 9) % 12;
		long yy = y - (mm / 10);

		return (yy * 365) + (yy / 4) - (yy / 100) + (yy / 400) + ((mm * 306 + 5) / 10) + (d - 1);
	}

	public static uint MKTime(DateTime date)
		=> MKTime(date.Year, date.Month, date.Day);

	public static uint MKTime(int y, int m, int d)
	{
		long date = EncodeDate(y, m, d);

		long res = date - EncodeDate(EpochYear, EpochMonth, EpochDay);

		return (uint)res.Clamp(0, uint.MaxValue);
	}

	static DateTime DecodeDate(uint ver)
	{
		long date = ver + EncodeDate(EpochYear, EpochMonth, EpochDay);

		long y = ((date * 10000) + 14780) / 3652425;

		long ddd = date - ((365 * y) + (y / 4) - (y / 100) + (y - 400));

		if (ddd < 0)
		{
			y--;
			ddd = date - ((365L * y) + (y / 4) - (y / 100) + (y / 400));
		}

		long mi = ((100 * ddd) + 52) / 3060;

		return new DateTime(
			(int)(y + ((mi + 2) / 12)),
			(int)(((mi + 2) % 12) + 1),
			(int)(ddd - ((mi * 306 + 5) / 10) + 1));
	}

	/* ----------------------------------------------------------------- */

	/*
	Lower 12 bits of the CWTV field in IT and S3M files.

	"Proper" version numbers went the way of the dodo, but we can't really fit an eight-digit date stamp directly
	into a twelve-bit number. Since anything < 0x50 already carries meaning (even though most of them weren't
	used), we have 0xfff - 0x50 = 4015 possible values. By encoding the date as an offset from a rather arbitrarily
	chosen epoch, there can be plenty of room for the foreseeable future.

		< 0x020: a proper version (files saved by such versions are likely very rare)
		= 0x020: any version between the 0.2a release (2005-04-29?) and 2007-04-17
		= 0x050: anywhere from 2007-04-17 to 2009-10-31 (version was updated to 0x050 in hg changeset 2f6bd40c0b79)
		> 0x050: the number of days since 2009-10-31, for example:
		0x051 = (0x051 - 0x050) + 2009-10-31 = 2009-11-01
		0x052 = (0x052 - 0x050) + 2009-10-31 = 2009-11-02
		0x14f = (0x14f - 0x050) + 2009-10-31 = 2010-07-13
		0xffe = (0xfff - 0x050) + 2009-10-31 = 2020-10-27
		= 0xfff: a non-value indicating a date after 2020-10-27. in this case, the full version number is stored in a reserved header field.
				this field follows the same format, using the same epoch, but without adding 0x50. */
	public static short CreatedWithTrackerVersion;
	public static uint Reserved;

	/* these should be 50 characters or shorter, as they are used in the startup dialog */
	const string ShortCopyright =
		"Copyright (c) 2025 Jonathan Gilbert";
	const string ShortBasedOn =
		"Based on Schism Tracker by Storlek, Mrs. Brisby, ..";

	/* SEE ALSO: helptext/copyright (contains full copyright information, credits, and GPL boilerplate) */

	public static string GetBanner(bool classic)
		=> classic ? TopBannerClassic : TopBannerNormal;

	public static string DecodeCreatedWithTrackerVersion(ushort cwtv, uint reserved)
	{
		cwtv &= 0xfff;

		if (cwtv > 0x050)
		{
			var date = DecodeDate((cwtv < 0xFFF) ? ((uint)cwtv - 0x050) : reserved);

			// Classic Mac OS's snprintf is not C99 compliant (duh) so we need
			// to cast our integers to unsigned long first.
			// We should probably have a replacement snprintf in case we don't
			// actually have a standard one.
			return date.ToString("yyyy-MM-dd");
		}
		else
			return "0." + cwtv;
	}

	// Tries multiple methods to get a reasonable date to start with.
	static bool GetVersionDate(out DateTime date)
	{
		// by the time we reach the year 10000 nobody will care that this breaks
		return
			DateTime.TryParseExact(GetBannerVersionString(), "yyyyMMdd", default, DateTimeStyles.AssumeUniversal, out date) ||
			DateTime.TryParse(ThisAssembly.Git.CommitDate, out date);
	}

	public static void Initialize()
	{
		uint versionSec;

		if (GetVersionDate(out var date))
			versionSec = MKTime(date);
		else
		{
			Console.WriteLine("help, I am very confused about myself");
			versionSec = 0;
		}

		CreatedWithTrackerVersion = (short)(0x050 + versionSec);
		Reserved = (CreatedWithTrackerVersion < 0xFFF) ? 0 : versionSec;

		CreatedWithTrackerVersion = CreatedWithTrackerVersion.Clamp(0x050, 0xFFF);
	}
}
