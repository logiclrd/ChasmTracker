namespace ChasmTracker.Songs;

public class SongNoteRef
{
	public Pattern Pattern;
	public int Row, Channel;

	public SongNoteRef(Pattern pattern, int row, int channel)
	{
		Pattern = pattern;
		Row = row;
		Channel = channel;
	}

	public ref SongNote Get()
	{
		return ref Pattern[Row][Channel];
	}
}
