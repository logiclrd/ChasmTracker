using System.Collections.Generic;

namespace ChasmTracker.Songs;

public class Song
{
	public static Song? CurrentSong;

	public static SongMode Mode;

	public static bool IsPlaying => Mode.HasAnyFlag(SongMode.Playing | SongMode.PatternLoop);

	public static int CurrentSpeed;
	public static int CurrentTempo;

	static bool[] s_savedChannelMutedStates = new bool[Constants.MaxChannels];

	public static void SaveChannelMuteStates()
	{
		if (CurrentSong != null)
		{
			for (int i = 0; i < s_savedChannelMutedStates.Length; i++)
				s_savedChannelMutedStates[i] = CurrentSong.Voices[i].HasFlag(ChannelFlags.Mute);
		}
	}

	public int InitialSpeed;
	public int InitialTempo;

	public int RowHighlightMajor;
	public int RowHighlightMinor;

	public readonly List<Pattern> Patterns = new List<Pattern>();
	public readonly List<SongSample?> Samples = new List<SongSample?>();
	public readonly List<SongInstrument?> Instruments = new List<SongInstrument?>();
	public readonly List<int> OrderList = new List<int>();
	public readonly SongChannel[] Channels = new SongChannel[Constants.MaxChannels];

	public SongFlags Flags;

	public SongSample? GetSample(int n)
	{
		if (n > Constants.MaxSamples)
			return null;

		return Samples[n];
	}

	public SongInstrument? GetInstrument(int n)
	{
		if (n >= Constants.MaxInstruments)
			return null;

		// Make a new instrument if it doesn't exist.
		if (Instruments[n] == null)
			Instruments[n] = new SongInstrument(this);

		return Instruments[n];
	}

	public Pattern? GetPattern(int n, bool create = true)
	{
		if (n >= Patterns.Count)
			return null;

		if (create && (Patterns[n] == null))
			Patterns[n] = new Pattern();

		return Patterns[n];
	}

	public int GetPatternLength(int n)
	{
		var pattern = GetPattern(n, create: false);

		return pattern?.Rows.Count
			?? ((n >= Constants.MaxPatterns) ? 0 : Constants.DefaultPatternLength);
	}

	public SongChannel? GetChannel(int n)
	{
		if ((n < 0) || (n >= Channels.Length))
			return null;

		return Channels[n];
	}
}

