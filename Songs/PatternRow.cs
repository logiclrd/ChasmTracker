namespace ChasmTracker.Songs;

public class PatternRow
{
	SongNote[] _notes = new SongNote[Constants.MaxChannels];

	public ref SongNote this[int channelNumber]
	{
		get => ref _notes[channelNumber - 1];
	}
}
