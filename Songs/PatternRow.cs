using System;

namespace ChasmTracker.Songs;

public class PatternRow
{
	SongNote[] _notes = new SongNote[Constants.MaxChannels];

	public static implicit operator Span<SongNote>(PatternRow @this)
		=> @this._notes;

	public ref SongNote this[int channelNumber]
	{
		get => ref _notes[channelNumber - 1];
	}

	public void CopyNotes(Span<SongNote> destination, int destinationOffset, int sourceOffset, int count)
	{
		for (int i = 0; i < count; i++)
			destination[i + destinationOffset] = _notes[i + sourceOffset];
	}
}
