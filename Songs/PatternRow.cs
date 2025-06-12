namespace ChasmTracker.Songs;

public class PatternRow
{
	public SongNote[] Notes = new SongNote[Constants.MaxChannels];

	public ref SongNote this[int channelNumber]
	{
		get => ref Notes[channelNumber - 1];
	}
}
