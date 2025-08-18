using System;

namespace ChasmTracker.Songs;

public static class KeyJazz
{
	/* Sample/inst slots 1+ are used "normally"; the sample loader uses slot #0 for preview playback -- but reports
	KEYJAZZ_INST_FAKE to keydown/up, since zero conflicts with the standard "use previous sample for this channel"
	behavior which is normally internal, but is exposed on the pattern editor where it's possible to explicitly
	select sample #0. (note: this is a hack to work around another hack) */
	public const int CurrentChannel = 0;
	// For automatic channel allocation when playing chords in the instrument editor.
	public const int AutomaticChannel = -1;
	public const int NoInstrument = -1;
	public const int DefaultVolume = -1;
	public const int FakeInstrument = -2;

	/* Channel corresponding to each note played.
	That is, s_noteToChannel[66] will indicate in which channel F-5 was played most recently.
	This will break if the same note was keydown'd twice without a keyup, but I think that's a
	fairly unlikely scenario that you'd have to TRY to bring about. */
	static int[] s_noteToChannel = new int[SpecialNotes.Last + 1];
	static int[] s_channelToNote = new int[Constants.MaxChannels + 1];

	public static void ResetChannelNoteMappings()
	{
		Array.Clear(s_noteToChannel);
		Array.Clear(s_channelToNote);
	}

	public static int GetLastChannelForNote(int note)
	{
		return s_noteToChannel[note];
	}

	public static int GetLastNoteInChannel(int chan)
	{
		return s_channelToNote[chan];
	}

	public static void LinkNoteAndChannel(int note, int chan)
	{
		s_noteToChannel[note] = chan;
		s_channelToNote[chan] = note;
	}

	public static void UnlinkNoteAndChannel(int note, int chan)
	{
		s_noteToChannel[note] = 0;
		s_channelToNote[chan] = 0;
	}

	public static void UnlinkLastNoteForChannel(int chan)
	{
		s_noteToChannel[s_channelToNote[chan]] = 0;
	}
}
