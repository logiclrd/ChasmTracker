
using System;
using System.Collections.Generic;

namespace ChasmTracker.Memory;

using System.Linq;
using ChasmTracker.Pages;
using ChasmTracker.Songs;

public static class MemoryUsage
{
	static List<(string Name, Func<int> Get)> s_pressures = new List<(string Name, Func<int> Get)>();

	[Flags]
	enum CacheType
	{
		Patterns = 1,
		Clipboard = 2,
		History = 4,
		Samples = 8,
		Instruments = 16,
		SongMessage = 32,
	}

	static CacheType s_cacheOK = 0;

	static int s_cachePatterns;
	static int s_cacheClipboard;
	static int s_cacheHistory;
	static int s_cacheSamples;
	static int s_cacheInstruments;
	static int s_cacheSongMessage;

	public static void RegisterMemoryPressure(string name, Func<int> get)
	{
		s_pressures.Add((name, get));
	}

	public static void NotifySongChanged()
	{
		s_cacheOK = 0;
	}

	/* packed patterns */
	public static int GetPatternUsage()
	{
		if (s_cacheOK.HasFlag(CacheType.Patterns)) return s_cachePatterns;
		s_cacheOK |= CacheType.Patterns;

		int q = 0;

		foreach (var pattern in Song.CurrentSong.Patterns.OfType<Pattern>())
		{
			if (pattern.IsEmpty) continue;

			q += (pattern.Rows.Count * 256);
		}

		s_cachePatterns = q;

		return q;
	}

	public static int GetClipboardUsage()
	{
		if (s_cacheOK.HasFlag(CacheType.Clipboard)) return s_cacheClipboard;
		s_cacheOK |= CacheType.Clipboard;

		s_cacheClipboard = AllPages.PatternEditor.ClipboardMemoryUsage() * 256;

		return s_cacheClipboard;
	}

	public static int GetHistoryUsage()
	{
		if (s_cacheOK.HasFlag(CacheType.History)) return s_cacheHistory;
		s_cacheOK |= CacheType.History;

		s_cacheHistory = AllPages.PatternEditor.HistoryMemoryUsage() * 256;

		return s_cacheHistory;
	}

	public static int GetSampleUsage()
	{
		if (s_cacheOK.HasFlag(CacheType.Samples)) return s_cacheSamples;
		s_cacheOK |= CacheType.Samples;

		int q = 0;

		foreach (var s in Song.CurrentSong.Samples)
		{
			if (s == null)
				continue;

			int qs = s.Length;

			if (s.Flags.HasFlag(SampleFlags.Stereo)) qs *= 2;
			if (s.Flags.HasFlag(SampleFlags._16Bit)) qs *= 2;

			q += qs;
		}

		s_cacheSamples = q;

		return q;
	}

	public static int GetInstrumentUsage()
	{
		if (s_cacheOK.HasFlag(CacheType.Instruments)) return s_cacheInstruments;
		s_cacheOK |= CacheType.Instruments;

		int q = 0;

		foreach (var i in Song.CurrentSong.Instruments)
			if ((i != null) && !i.IsEmpty)
				q += 512;

		s_cacheInstruments = q;

		return q;
	}

	public static int GetSongMessageUsage()
	{
		if (s_cacheOK.HasFlag(CacheType.SongMessage)) return s_cacheSongMessage;
		s_cacheOK |= CacheType.SongMessage;

		s_cacheSongMessage = Song.CurrentSong.Message.Length * 2;

		return s_cacheSongMessage;
	}

	/* this makes an effort to calculate about how much memory IT would report
	is being taken up by the current song.

	it's pure, unadulterated crack, but the routines are useful for schism mode :)
	*/
	static int Align4K(int q)
		=> (q + 0xFFF) & ~0xFFF;

	public static int EMS
		=> Align4K(GetSampleUsage() + GetHistoryUsage() + GetPatternUsage());

	public static int LowMemory
		=> GetSongMessageUsage() + GetInstrumentUsage() + GetClipboardUsage();
}
