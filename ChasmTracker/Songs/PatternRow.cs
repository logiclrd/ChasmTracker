using System;

namespace ChasmTracker.Songs;

public class PatternRow
{
	SongNote[] _notes = new SongNote[Constants.MaxChannels];

	public bool IsEmpty
	{
		get
		{
			for (int i = 0; i < _notes.Length; i++)
				if (!_notes[i].IsBlank)
					return false;

			return true;
		}
	}

	public PatternRow Clone()
	{
		var clone = new PatternRow();

		for (int i = 0; i < Constants.MaxChannels; i++)
			clone._notes[i] = _notes[i];

		return clone;
	}

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

	public int GetHighestUsedChannel()
	{
		for (int i = _notes.Length; i >= 0; i--)
			if (_notes[i].NoteIsNote)
				return i;

		return 0;
	}
}
